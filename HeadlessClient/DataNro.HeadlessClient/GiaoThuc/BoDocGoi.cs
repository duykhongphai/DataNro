using DataNro.Mang;

namespace DataNro.GiaoThuc;

/// <summary>
/// Bộ đọc gói tin. <b>Chỉ</b> hiểu những mã lệnh cần cho việc lấy dữ liệu; mọi mã lệnh khác
/// bị bỏ qua lặng lẽ.
///
/// <para>
/// Đây là chỗ khác hẳn một client chơi được: không có đánh nhau, không đi lại, không nhặt
/// đồ, không nhiệm vụ, không bang hội. Nó chỉ biết đăng nhập, hứng bốn bảng mẫu, xin ảnh,
/// rồi thôi.
/// </para>
/// </summary>
public sealed class BoDocGoi : IBoDoc
{
    private readonly KetNoi noi;
    private readonly Action<string> log;

    public BoDocGoi(KetNoi ketNoi, Action<string> ghiLog)
    {
        noi = ketNoi;
        log = ghiLog;
        noi.BoDoc = this;
    }

    public GameData Data { get; } = new();

    /// <summary>Tài khoản dùng cho gói đăng nhập, đặt trước khi nối.</summary>
    public string TaiKhoan { get; set; } = string.Empty;

    public string MatKhau { get; set; } = string.Empty;

    /// <summary>Chuỗi phiên bản gửi kèm gói đăng nhập, phải khớp bản client đang chạy.</summary>
    public string PhienBan { get; set; } = "2.5.0";

    public int LoaiClient { get; set; } = 3;

    /// <summary>
    /// Mức phóng của client (<c>mGraphics.zoomLevel</c> bên bản gốc), 1..4. Máy chủ dùng con
    /// số này để chọn bộ ảnh gửi xuống, nên xin <b>4</b> là được ảnh nét nhất.
    ///
    /// <para>
    /// Client thật đặt mức này theo cỡ màn hình (<c>MotherCanvas</c>). Nó chỉ ảnh hưởng ảnh
    /// và cách vẽ - toạ độ trong gói tin vẫn là đơn vị game, không nhân theo mức phóng - nên
    /// khai 4 không làm lệch thứ gì.
    /// </para>
    /// </summary>
    public int MucPhongAnh { get; set; } = 4;

    /// <summary>Máy chủ báo đăng nhập thành công (gói messageNotMap nhánh 4).</summary>
    public bool DaDangNhap { get; private set; }

    /// <summary>Máy chủ chưa cho vào, kèm lời nhắn - lần thử này coi như hỏng.</summary>
    public string LoiDangNhap { get; private set; }

    /// <summary>
    /// Đã báo máy chủ "client sẵn sàng" - tức là phiên này thật sự dùng được (xin ảnh được).
    /// Đây mới là mốc để bên ngoài biết đăng nhập xong, chứ không phải <see cref="GameData.DaDayDu"/>:
    /// bảng dữ liệu còn nguyên từ lần nối trước nên nó luôn đúng, kể cả khi phiên mới chưa vào.
    /// </summary>
    public bool DaSanSang { get; private set; }

    /// <summary>Gói ảnh (-67) đi thẳng ra ngoài cho bộ tải ảnh, ở đây không đụng vào.</summary>
    public event Action<Message> NhanAnh;

    // ==================== sự kiện kết nối ====================

    /// <summary>
    /// Dọn trạng thái của lần nối trước. <b>Bắt buộc</b> gọi trước mỗi lần nối mới: mấy cái
    /// cờ dưới đây đều tính theo một kết nối, để nguyên thì lần nối sau không gửi lại tài
    /// khoản, không báo sẵn sàng, mà bên ngoài lại tưởng đã vào xong.
    /// </summary>
    public void DatLai()
    {
        DaDangNhap = false;
        DaSanSang = false;
        LoiDangNhap = null;
        daGuiLai = false;
        daBaoSanSang = false;
        daVaoMap = false;
        soLanTaoNhanVat = 0;
    }

    public void KhiNoiXong()
    {
        // Client thật gửi CLIENT_INFO trước rồi mới gửi tài khoản; máy chủ trả lời CLIENT_INFO
        // xong ta gửi lại tài khoản lần nữa (xem nhánh -29 dưới).
        GuiLoaiClient();
        GuiDangNhap();
    }

    public void KhiDut()
    {
        DaDangNhap = false;
        log?.Invoke("Mất kết nối.");
    }

    // ==================== phân phối ====================

    /// <summary>Bật thì ghi lại mọi mã lệnh nhận được - chỉ dùng khi soi giao thức.</summary>
    public bool GhiMoiGoi { get; set; }

    public void KhiNhanGoi(Message msg)
    {
        if (GhiMoiGoi) log?.Invoke($"<- cmd={msg.command} len={msg.RawLength}");

        switch (msg.command)
        {
            case -29:
                GoiNotLogin(msg);
                break;
            case -28:
                GoiNotMap(msg);
                break;
            case -87:
                createData(msg.reader());
                Data.vcData = Data.vsData;
                KiemTraDuData();
                break;
            case 12:
            {
                // Bảng vật phẩm to hơn 64KB nên KHÔNG về được ở gói -28 (độ dài chỉ hai
                // byte). Máy chủ đẩy nó qua mã lệnh 12 - một trong mấy mã dùng độ dài ba
                // byte - với một byte 0 dẫn đầu.
                var r = msg.reader();
                if (r.readByte() == 0) loadItemNew(r);
                break;
            }
            case -67:
                NhanAnh?.Invoke(msg);
                break;
            case 42:
                // Máy chủ hỏi bảng "cập nhật thông tin". Không trả lời là nó ngừng đẩy dữ
                // liệu, nên cứ trả lời cho xong - nội dung câu hỏi không cần đọc.
                GuiThongTinXacMinh();
                break;
            case 0:
                if (ChoPhepVaoMap) ChonNhanVat(msg);
                break;
            case 2:
                if (ChoPhepVaoMap) TaoNhanVat();
                break;
            case -24:
                VaoMap();
                break;
            case -26:
            case -25:
            case 94:
                // Hộp thoại / lời nhắn của máy chủ. Bắt để biết vì sao bị từ chối mà thoát
                // sớm, thay vì đứng chờ cho hết giờ.
                TinMayChu(msg);
                break;
        }
    }

    private bool daVaoMap;
    private int soLanTaoNhanVat;

    /// <summary>
    /// Máy chủ trả danh sách nhân vật (gói <c>0</c>): chọn ngay nhân vật đầu để vào game.
    /// Chỉ cần đọc tên, mọi thứ khác trong gói bỏ qua.
    /// </summary>
    private void ChonNhanVat(Message msg)
    {
        try
        {
            var r = msg.reader();
            var n = r.readByte();
            if (n <= 0)
            {
                TaoNhanVat();
                return;
            }

            r.readInt();          // id nhân vật
            var ten = r.readUTF();

            var m = new Message((sbyte)-28);
            m.writer().writeByte(1);
            m.writer().writeUTF(ten);
            Gui(m);
            log?.Invoke($"Chọn nhân vật '{ten}' để vào game.");
        }
        catch (Exception e)
        {
            log?.Invoke("Đọc danh sách nhân vật hỏng: " + e.Message);
        }
    }

    /// <summary>
    /// Tài khoản chưa có nhân vật ở máy chủ này (gói <c>2</c>) thì phải tạo, không thì không
    /// vào được game - mà không vào game thì máy chủ không gửi bảng mảnh dựng hình.
    ///
    /// <para>
    /// Trùng tên thì máy chủ hỏi lại bằng chính gói <c>2</c>, nên cứ đổi tên rồi gửi lại;
    /// giới hạn số lần để không lỡ đẻ ra một đàn nhân vật rác.
    /// </para>
    /// </summary>
    private void TaoNhanVat()
    {
        if (++soLanTaoNhanVat > SoLanTaoNhanVatToiDa)
        {
            log?.Invoke("Tạo nhân vật hỏng quá nhiều lần, thôi.");
            return;
        }

        var ten = TenNhanVat + Random.Shared.Next(0, 100000).ToString("D5");
        var m = new Message((sbyte)-28);
        m.writer().writeByte(2);
        m.writer().writeUTF(ten);
        m.writer().writeByte(GioiTinh);
        m.writer().writeByte(MaToc[Math.Clamp(GioiTinh, 0, 2)]);
        Gui(m);
        log?.Invoke($"Máy chủ chưa có nhân vật, tạo '{ten}' (lần {soLanTaoNhanVat}).");
    }

    /// <summary>Tên gốc để tạo nhân vật; năm chữ số ngẫu nhiên được nối vào sau.</summary>
    public string TenNhanVat { get; set; } = "dnro";

    /// <summary>0 Trái Đất, 1 Namếc, 2 Xayda.</summary>
    public int GioiTinh { get; set; }

    public int SoLanTaoNhanVatToiDa { get; set; } = 8;

    /// <summary>
    /// Mã tóc thật của từng hành tinh, chép từ <c>CreateCharScr.hairID</c> của client gốc.
    /// Để 0 là một mã không có thật, có máy chủ nuốt luôn gói tạo mà không nói gì.
    /// </summary>
    private static readonly int[] MaToc = { 64, 9, 6 };

    /// <summary>
    /// Máy chủ đẩy thông tin map (<c>-24</c>) thì trả lời <c>-39</c> là chính thức vào game.
    ///
    /// <para>
    /// Đây là chỗ <b>bắt buộc</b> phải vào game thật, khác với phần còn lại của công cụ. Bảng
    /// mảnh dựng hình (gói <c>-87</c>) chỉ được máy chủ gửi khi nhân vật đã vào map - đo thực
    /// tế: đứng ở bước đăng nhập mà xin <c>-87</c> thì bốn mươi tư gói về không có lấy một
    /// cái. Không cần đọc dữ liệu ô của map, chỉ cần báo đã sẵn sàng.
    /// </para>
    /// </summary>
    /// <summary>Tắt thì dừng ở bước đăng nhập, không vào game và không có bảng part.</summary>
    public bool ChoPhepVaoMap { get; set; } = true;

    private void VaoMap()
    {
        if (daVaoMap || !ChoPhepVaoMap) return;
        daVaoMap = true;
        Gui(new Message((sbyte)-39));
        log?.Invoke("Đã vào map, xin lại nhóm data.");

        // Xin lại nhóm data: lần xin lúc đăng nhập chắc chắn bị bỏ qua.
        Gui(new Message((sbyte)-87));
    }

    private void TinMayChu(Message msg)
    {
        try
        {
            var chu = msg.reader().readUTF();
            if (string.IsNullOrWhiteSpace(chu)) return;
            log?.Invoke("Máy chủ nói: " + chu);

            // Lời nhắn đến TRƯỚC khi đăng nhập xong thì gần như chắc chắn là lời từ chối
            // (sai mật khẩu, tài khoản bị khoá, đang trong hàng chờ).
            if (!DaDangNhap) LoiDangNhap = chu;
        }
        catch (Exception)
        {
            // gói này mỗi máy chủ một kiểu, đọc không ra thì thôi
        }
    }

    private bool daGuiLai;

    private void GoiNotLogin(Message msg)
    {
        var r = msg.reader();
        if (r.readByte() != 2) return;

        // Máy chủ vừa xác nhận CLIENT_INFO. Client thật gửi lại tài khoản đúng MỘT lần nữa ở
        // đây - thiếu lần gửi lại này có máy chủ không đẩy dữ liệu xuống.
        if (DaDangNhap || daGuiLai) return;
        daGuiLai = true;
        GuiDangNhap();
    }

    private void GoiNotMap(Message msg)
    {
        var r = msg.reader();
        switch (r.readByte())
        {
            case 4:
                DangNhapXong(r);
                break;
            case 6:
                createMap(r);
                Data.vcMap = Data.vsMap;
                KiemTraDuData();
                break;
            case 7:
                createSkill(r);
                Data.vcSkill = Data.vsSkill;
                KiemTraDuData();
                break;
            case 8:
                loadItemNew(r);
                break;
        }
    }

    /// <summary>
    /// Đăng nhập xong: máy chủ báo phiên bản của bốn nhóm dữ liệu. Ta không có đệm nên nhóm
    /// nào cũng phải xin lại.
    /// </summary>
    private void DangNhapXong(myReader r)
    {
        Data.vsData = r.readByte();
        Data.vsMap = r.readByte();
        Data.vsSkill = r.readByte();
        Data.vsItem = r.readByte();
        r.readByte(); // cờ phụ, client gốc cũng không dùng

        DaDangNhap = true;
        log?.Invoke($"Đăng nhập xong, phiên bản dữ liệu data={Data.vsData} map={Data.vsMap} " +
                    $"skill={Data.vsSkill} item={Data.vsItem}");

        // Nối lại để xin tiếp ảnh thì bảng mẫu đã có sẵn từ lần trước - xin lại chỉ tổ kéo
        // thêm vài trăm KB mỗi lượt mà chẳng để làm gì.
        if (Data.DaDayDu)
        {
            BaoSanSang();
            return;
        }

        Gui(NotMap(6)); // xin bảng map + npc + quái
        Gui(NotMap(7)); // xin bảng kĩ năng
        Gui(NotMap(8)); // xin bảng vật phẩm
        Gui(new Message((sbyte)-87)); // xin nhóm data
    }

    private bool daBaoSanSang;

    /// <summary>
    /// Đủ bốn nhóm thì báo máy chủ là client đã sẵn sàng. Không gửi cặp gói này thì máy chủ
    /// ngừng đẩy tiếp - và quan trọng hơn với ta, nó cũng không trả lời gói xin ảnh.
    /// </summary>
    private void KiemTraDuData()
    {
        if (daBaoSanSang || !Data.AllLoaded) return;
        BaoSanSang();
    }

    private void BaoSanSang()
    {
        if (daBaoSanSang) return;
        daBaoSanSang = true;
        Gui(NotMap(13));
        Gui(NotMap(13));
        Gui(new Message((sbyte)-38));
        DaSanSang = true;
    }

    // ==================== gói gửi đi ====================

    private void Gui(Message m) => noi.Gui(m);

    private static Message NotLogin(int lenh)
    {
        var m = new Message((sbyte)-29);
        m.writer().writeByte(lenh);
        return m;
    }

    private static Message NotMap(int lenh)
    {
        var m = new Message((sbyte)-28);
        m.writer().writeByte(lenh);
        return m;
    }

    private void GuiLoaiClient()
    {
        var m = NotLogin(2);
        var w = m.writer();
        w.writeByte(LoaiClient);
        w.writeByte(MucPhongAnh);
        w.writeBoolean(false);
        w.writeInt(1280);
        w.writeInt(720);
        w.writeBoolean(true);
        w.writeBoolean(false);
        w.writeUTF("Windows|" + PhienBan);
        Gui(m);
    }

    private void GuiDangNhap()
    {
        if (string.IsNullOrEmpty(TaiKhoan)) return;

        var m = NotLogin(0);
        m.writer().writeUTF(TaiKhoan);
        m.writer().writeUTF(MatKhau);
        m.writer().writeUTF(PhienBan);
        m.writer().writeByte(0);
        Gui(m);
        log?.Invoke($"Gửi đăng nhập '{TaiKhoan}' (phiên bản {PhienBan})");
    }

    /// <summary>
    /// Trả lời bảng "cập nhật thông tin". Client gốc chỉ điền năm ô: ngày, tháng, năm, số
    /// điện thoại, tên - bốn ô còn lại luôn để trống.
    /// </summary>
    private void GuiThongTinXacMinh()
    {
        var m = new Message((sbyte)42);
        var w = m.writer();
        w.writeUTF("01");
        w.writeUTF("01");
        w.writeUTF("1990");
        w.writeUTF(string.Empty);
        w.writeUTF(string.Empty);
        w.writeUTF(string.Empty);
        w.writeUTF(string.Empty);
        w.writeUTF("0968" + Random.Shared.Next(0, 1000000).ToString("D6"));
        w.writeUTF("Nguyễn Văn A");
        Gui(m);
    }

    /// <summary>Xin một ảnh theo id. Máy chủ im lặng với id nó không có.</summary>
    public void XinAnh(int id)
    {
        var m = new Message((sbyte)-67);
        m.writer().writeInt(id);
        Gui(m);
    }

    // ==================== đọc bảng mẫu ====================

    /// <summary>
    /// Nguyên bản <c>Controller.createData</c>. Nhóm "data" gồm sáu mảng byte nối đuôi nhau,
    /// client gốc lưu thành sáu tệp trong máy:
    /// <c>dart, arrow, effect, image, part, skill</c>.
    ///
    /// <para>
    /// Ta chỉ cần <b>part</b> - bảng mảnh dựng hình NPC. Năm mảng còn lại vẫn phải đọc cho
    /// đúng thứ tự, không thì con trỏ lệch và mảng part ra rác.
    /// </para>
    /// </summary>
    private void createData(myReader d)
    {
        d.readByte(); // phiên bản; ta không đệm nên không dùng tới

        DocMangByte(d); // dart
        DocMangByte(d); // arrow
        DocMangByte(d); // effect
        DocMangByte(d); // image
        var part = DocMangByte(d);
        DocMangByte(d); // skill

        if (part != null) DocPart(part);
    }

    /// <summary>Nguyên bản <c>NinjaUtil.readByteArray</c>: một <c>int</c> độ dài rồi bấy nhiêu byte.</summary>
    private static sbyte[] DocMangByte(myReader d)
    {
        try
        {
            var n = d.readInt();
            if (n <= 0) return null;
            var ra = new sbyte[n];
            d.readFully(ref ra);
            return ra;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Bảng mảnh dựng hình: <c>short</c> số part, rồi mỗi part một <c>byte</c> loại và bấy
    /// nhiêu khung <c>(short id, byte dx, byte dy)</c>. Số khung suy từ loại.
    /// </summary>
    private void DocPart(sbyte[] raw)
    {
        try
        {
            var d = new myReader(raw);
            var n = d.readShort();
            var ds = new Part[n];

            for (var i = 0; i < n; i++)
            {
                var loai = d.readByte();
                var soKhung = Part.SoKhung(loai);
                if (soKhung == 0)
                {
                    // Loại lạ thì không biết đọc bao nhiêu khung, đọc bừa là lệch hết phần
                    // sau. Giữ những part đã đọc được rồi dừng, còn hơn ra một bảng rác.
                    log?.Invoke($"Part loại lạ ({loai}) ở vị trí {i}, dừng đọc bảng part.");
                    Array.Resize(ref ds, i);
                    break;
                }

                var p = new Part { type = loai, pi = new PartImage[soKhung] };
                for (var j = 0; j < soKhung; j++)
                    p.pi[j] = new PartImage { id = d.readShort(), dx = d.readByte(), dy = d.readByte() };
                ds[i] = p;
            }

            Data.parts = ds;
            log?.Invoke($"Bảng part: {ds.Length} mảnh");
        }
        catch (Exception e)
        {
            log?.Invoke("Đọc bảng part hỏng: " + e.Message);
        }
    }

    /// <summary>Nguyên bản <c>Controller.createMap</c>: tên map, mẫu NPC, mẫu quái.</summary>
    private void createMap(myReader d)
    {
        Data.vcMap = d.readByte();

        Data.mapNames = new string[d.readShort()];
        for (var i = 0; i < Data.mapNames.Length; i++) Data.mapNames[i] = d.readUTF();

        Data.arrNpcTemplate = new NpcTemplate[d.readByte()];
        for (var i = 0; i < Data.arrNpcTemplate.Length; i++)
        {
            var t = new NpcTemplate { npcTemplateId = i, name = d.readUTF() };
            t.headId = d.readShort();
            t.bodyId = d.readShort();
            t.legId = d.readShort();
            t.menu = new string[d.readByte()][];
            for (var j = 0; j < t.menu.Length; j++)
            {
                t.menu[j] = new string[d.readByte()];
                for (var k = 0; k < t.menu[j].Length; k++) t.menu[j][k] = d.readUTF();
            }

            Data.arrNpcTemplate[i] = t;
        }

        Data.arrMobTemplate = new MobTemplate[d.readShort()];
        for (var i = 0; i < Data.arrMobTemplate.Length; i++)
        {
            Data.arrMobTemplate[i] = new MobTemplate
            {
                mobTemplateId = i,
                type = d.readByte(),
                name = d.readUTF(),
                hp = d.readLong(),
                rangeMove = d.readByte(),
                speed = d.readByte(),
                dartType = d.readByte()
            };
        }
    }

    /// <summary>Nguyên bản <c>Controller.createSkill</c>: lớp nhân vật → chiêu → từng cấp.</summary>
    private void createSkill(myReader d)
    {
        Data.vcSkill = d.readByte();

        Data.sOptionTemplates = new SkillOptionTemplate[d.readByte()];
        for (var i = 0; i < Data.sOptionTemplates.Length; i++)
            Data.sOptionTemplates[i] = new SkillOptionTemplate { id = i, name = d.readUTF() };

        Data.nClasss = new NClass[d.readByte()];
        for (var i = 0; i < Data.nClasss.Length; i++)
        {
            var nc = new NClass { classId = i, name = d.readUTF() };
            nc.skillTemplates = new SkillTemplate[d.readByte()];
            for (var j = 0; j < nc.skillTemplates.Length; j++)
            {
                var st = new SkillTemplate
                {
                    id = d.readByte(),
                    name = d.readUTF(),
                    maxPoint = d.readByte(),
                    manaUseType = d.readByte(),
                    type = d.readByte(),
                    iconId = d.readShort()
                };
                st.damInfo = d.readUTF();
                st.description = d.readUTF();

                st.skills = new Skill[d.readByte()];
                for (var k = 0; k < st.skills.Length; k++)
                {
                    var sk = new Skill
                    {
                        skillId = d.readShort(),
                        template = st,
                        point = d.readByte(),
                        powRequire = d.readLong(),
                        manaUse = d.readShort(),
                        coolDown = d.readInt(),
                        dx = d.readShort(),
                        dy = d.readShort(),
                        maxFight = d.readByte(),
                        damage = d.readShort(),
                        price = d.readShort()
                    };
                    sk.moreInfo = d.readUTF();
                    st.skills[k] = sk;
                }

                nc.skillTemplates[j] = st;
            }

            Data.nClasss[i] = nc;
        }
    }

    /// <summary>
    /// Nguyên bản <c>Controller.loadItemNew</c>. Gói vật phẩm chia làm nhiều phần:
    /// 0 = bảng option, 1 = bảng vật phẩm, 100/101 = bảng ảnh đầu (ta không cần).
    /// </summary>
    private void loadItemNew(myReader d)
    {
        Data.vcItem = d.readByte();
        var loai = d.readByte();

        if (loai == 0)
        {
            Data.iOptionTemplates = new ItemOptionTemplate[d.readShort()];
            for (var i = 0; i < Data.iOptionTemplates.Length; i++)
                Data.iOptionTemplates[i] = new ItemOptionTemplate
                {
                    id = i, name = d.readUTF(), type = d.readByte()
                };

            try
            {
                var n = d.readShort();
                for (var i = 0; i < n; i++)
                {
                    var idx = d.readShort();
                    var mau = d.readUnsignedByte();
                    if (idx >= 0 && idx < Data.iOptionTemplates.Length)
                        Data.iOptionTemplates[idx].color = mau;
                }
            }
            catch (Exception)
            {
                // bảng màu không bắt buộc
            }
        }
        else if (loai == 1)
        {
            Data.itemTemplates.Clear();
            var n = d.readShort();
            for (short i = 0; i < n; i++)
            {
                var kieu = d.readByte();
                var gioi = d.readByte();
                var ten = d.readUTF();
                var mota = d.readUTF();
                var cap = d.readByte();
                var sm = d.readInt();
                var icon = d.readShort();
                var part = d.readShort();
                var chong = d.readBoolean();
                Data.AddItem(new ItemTemplate(i, kieu, gioi, ten, mota, cap, sm, icon, chong)
                {
                    part = part
                });
            }

            // Chỉ phần 1 mới là "xong bảng vật phẩm"; phần 0 và 100/101 đến rời rạc.
            Data.vcItem = Data.vsItem;
            KiemTraDuData();
        }
    }
}
