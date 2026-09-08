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

    public void KhiNhanGoi(Message msg)
    {
        switch (msg.command)
        {
            case -29:
                GoiNotLogin(msg);
                break;
            case -28:
                GoiNotMap(msg);
                break;
            case -87:
                // Nhóm "data" chỉ có mỗi số phiên bản, không có bảng nào để đọc.
                msg.reader().readByte(); // phiên bản, ta không đệm nên không dùng tới
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
            case -26:
            case -25:
            case 94:
                // Hộp thoại / lời nhắn của máy chủ. Bắt để biết vì sao bị từ chối mà thoát
                // sớm, thay vì đứng chờ cho hết giờ.
                TinMayChu(msg);
                break;
        }
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
