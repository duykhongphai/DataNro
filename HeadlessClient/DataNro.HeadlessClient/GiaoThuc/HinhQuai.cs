namespace DataNro.GiaoThuc;

/// <summary>Một ô cắt trên tấm sprite của quái.</summary>
public class OAnh
{
    public int id;
    public int x0, y0, w, h;
}

/// <summary>Một khung hình: ghép từ vài mảnh, mỗi mảnh là một ô cắt đặt lệch đi (dx, dy).</summary>
public class KhungQuai
{
    public short[] dx = Array.Empty<short>();
    public short[] dy = Array.Empty<short>();
    public sbyte[] oAnh = Array.Empty<sbyte>();
}

/// <summary>
/// Hình của một mẫu quái: một tấm PNG kèm bảng cắt và bảng khung.
///
/// <para>
/// Quái vẽ khác hẳn NPC. NPC ghép từ ba mảnh trong bảng part dùng chung, còn mỗi mẫu quái có
/// <b>tấm sprite riêng</b> phải xin từng con bằng gói <c>11</c>. Chuyển thể từ
/// <c>EffectData.readData</c> / <c>readMobNew</c> của client gốc.
/// </para>
/// </summary>
public class HinhQuai
{
    public int mobTemplateId;

    /// <summary>Kiểu đọc máy chủ báo: 0 là bố cục thường, khác 0 là bố cục boss đời mới.</summary>
    public sbyte kieuDoc;

    public int typeData;

    /// <summary>Tấm PNG gốc, chưa cắt.</summary>
    public byte[] anh = Array.Empty<byte>();

    public OAnh[] oAnh = Array.Empty<OAnh>();
    public KhungQuai[] khung = Array.Empty<KhungQuai>();

    /// <summary>Chuỗi khung của hoạt ảnh, mỗi phần tử là chỉ số vào <see cref="khung"/>.</summary>
    public short[] chuoiKhung = Array.Empty<short>();

    /// <summary>Kích thước hình, tính từ khung đầu - đúng cách client tính.</summary>
    public int rong, cao;

    /// <summary>
    /// Bảng ô do <b>tự dò</b> chứ không phải máy chủ gửi (bố cục chữ chỉ có toạ độ góc trên
    /// trái). Kèm theo một điều quan trọng cho bên vẽ: tấm sprite của mấy con này là
    /// <b>ảnh gốc chưa phóng</b>, tức một đơn vị game bằng đúng một điểm ảnh, khác hẳn tấm
    /// thường vốn được máy chủ trả về ở mức phóng 4.
    /// </summary>
    public bool thieuKichThuocO;

    /// <summary>
    /// Đọc bảng cắt + bảng khung.
    ///
    /// <para>
    /// Bố cục boss đời mới (<paramref name="kieuDoc"/> khác 0 và khác 1) chỉ khác đúng một
    /// chỗ: toạ độ ô cắt là <c>short</c> thay vì <c>byte</c> không dấu - tấm sprite của boss
    /// to hơn 255 pixel nên một byte không đủ.
    /// </para>
    /// </summary>
    public void Doc(sbyte[] du, sbyte kieuDoc)
    {
        this.kieuDoc = kieuDoc;

        if (LaChu(du)) DocChu(du);
        else DocNhiPhan(du, kieuDoc);
    }

    /// <summary>Ô cắt theo id. Client tra theo <c>ID</c> chứ không theo vị trí trong mảng.</summary>
    public OAnh TimO(int id)
    {
        foreach (var o in oAnh)
            if (o.id == id) return o;
        return null;
    }

    // ==================== bố cục nhị phân ====================

    private void DocNhiPhan(sbyte[] du, sbyte kieuDoc)
    {
        var toaDoNgan = kieuDoc != 0 && kieuDoc != 1;
        var p = 0;

        int B() => 0xFF & du[p++];
        sbyte S() => du[p++];

        short N()
        {
            var v = (short)(((0xFF & du[p]) << 8) | (0xFF & du[p + 1]));
            p += 2;
            return v;
        }

        var soO = S();
        oAnh = new OAnh[soO];
        for (var i = 0; i < soO; i++)
        {
            oAnh[i] = new OAnh
            {
                id = S(),
                x0 = toaDoNgan ? N() : B(),
                y0 = toaDoNgan ? N() : B(),
                w = B(),
                h = B()
            };
        }

        int traiNhat = 0, trenNhat = 0, phaiNhat = 0, duoiNhat = 0;

        khung = new KhungQuai[N()];
        for (var i = 0; i < khung.Length; i++)
        {
            var soManh = S();
            var k = new KhungQuai
            {
                dx = new short[soManh],
                dy = new short[soManh],
                oAnh = new sbyte[soManh]
            };

            for (var j = 0; j < soManh; j++)
            {
                k.dx[j] = N();
                k.dy[j] = N();
                k.oAnh[j] = S();

                // Kích thước lấy theo KHUNG ĐẦU, y như client gốc.
                if (i != 0) continue;
                var o = TimO(k.oAnh[j]);
                if (o == null) continue;

                if (traiNhat > k.dx[j]) traiNhat = k.dx[j];
                if (trenNhat > k.dy[j]) trenNhat = k.dy[j];
                if (phaiNhat < k.dx[j] + o.w) phaiNhat = k.dx[j] + o.w;
                if (duoiNhat < k.dy[j] + o.h) duoiNhat = k.dy[j] + o.h;
                rong = phaiNhat - traiNhat;
                cao = duoiNhat - trenNhat;
            }

            khung[i] = k;
        }

        chuoiKhung = DocChuoiKhung(du, p);
    }

    /// <summary>
    /// Đuôi hoạt ảnh - phần dễ lệch nhất của gói.
    ///
    /// <para>
    /// Client đọc số phần tử bằng <c>readShort</c> rồi bọc cả hàm trong <c>catch</c> rỗng, nên
    /// con nào ghi khác kiểu số đếm là nó lặng lẽ bỏ luôn hoạt ảnh. Godzilla với Kong rơi đúng
    /// vào đó: số đếm của chúng là <b>một byte</b>, theo sau vừa khít chừng ấy short rồi hai
    /// byte 0 kết thúc. Ở đây ưu tiên cách đọc nào ăn khớp trọn vẹn phần byte còn lại, hết cách
    /// mới bỏ trống - dù sao bảng ô và bảng khung cũng đã đọc xong, y như client.
    /// </para>
    /// </summary>
    private static short[] DocChuoiKhung(sbyte[] du, int p)
    {
        var con = du.Length - p;
        if (con < 2) return Array.Empty<short>();

        var theoShort = ((0xFF & du[p]) << 8) | (0xFF & du[p + 1]);
        var theoByte = 0xFF & du[p];

        // Khớp trọn vẹn thì chắc chắn đúng, khỏi đoán thêm.
        if (2 + 2 * theoShort == con) return Lay(p + 2, theoShort);
        if (1 + 2 * theoByte == con || 1 + 2 * theoByte + 2 == con) return Lay(p + 1, theoByte);

        // Không khớp trọn thì đọc y như client: số đếm là short, thừa byte thì kệ.
        if (2 + 2 * theoShort <= con) return Lay(p + 2, theoShort);

        return Array.Empty<short>();

        short[] Lay(int tu, int soPhanTu)
        {
            var ra = new short[soPhanTu];
            for (var i = 0; i < soPhanTu; i++)
                ra[i] = (short)(((0xFF & du[tu + 2 * i]) << 8) | (0xFF & du[tu + 2 * i + 1]));
            return ra;
        }
    }

    // ==================== bố cục chữ ====================

    private static bool LaChu(sbyte[] du) =>
        du.Length > 4 && du[0] == 61 && du[1] == 61 && du[2] == 61 && du[3] == 61;

    /// <summary>
    /// Vài con boss máy chủ gửi thẳng tệp nguồn dạng chữ thay cho gói nhị phân:
    /// <c>==== SMALLIMAGES ====</c> / <c>==== FRAMES ===</c> / <c>==== SEQUENCE ====</c>.
    /// Client gặp cái này thì đọc byte đầu (dấu <c>=</c>, tức 61) thành "61 ô cắt", tràn mảng
    /// rồi bỏ trắng cả con quái - Hirudegarn không có hình động trong game là vì vậy.
    ///
    /// <para>
    /// Bảng ô ở đây <b>chỉ có toạ độ góc</b>, không kèm rộng/cao, nên phải tự dò lấy từ vùng
    /// đục của tấm PNG - xem <see cref="DoKichThuocO"/>.
    /// </para>
    /// </summary>
    private void DocChu(sbyte[] du)
    {
        var b = new byte[du.Length];
        for (var i = 0; i < du.Length; i++) b[i] = unchecked((byte)du[i]);
        var chu = System.Text.Encoding.ASCII.GetString(b);

        var so = So(Doan(chu, "SMALLIMAGES", "==== FRAMES"));
        var p = 0;
        var soO = so[p++];
        oAnh = new OAnh[soO];
        for (var i = 0; i < soO; i++)
            oAnh[i] = new OAnh { id = so[p++], x0 = so[p++], y0 = so[p++] };
        thieuKichThuocO = true;

        so = So(Doan(chu, "FRAMES", "==== SEQUENCE"));
        p = 0;
        khung = new KhungQuai[so[p++]];
        for (var i = 0; i < khung.Length; i++)
        {
            var soManh = so[p++];
            var k = new KhungQuai
            {
                dx = new short[soManh],
                dy = new short[soManh],
                oAnh = new sbyte[soManh]
            };

            for (var j = 0; j < soManh; j++)
            {
                k.dx[j] = (short)so[p++];
                k.dy[j] = (short)so[p++];
                k.oAnh[j] = (sbyte)so[p++];
            }

            khung[i] = k;
        }

        // Phần SEQUENCE gồm nhiều dòng - mỗi dòng một bộ hoạt ảnh, xen giữa là vài con số nhịp.
        // Lấy dòng danh sách đầu tiên làm hoạt ảnh chính, đúng vai trò arrFrame bên nhị phân.
        foreach (var dong in Doan(chu, "SEQUENCE", null).Split('\n'))
        {
            var t = dong.Trim();
            if (!t.Contains(',')) continue;
            chuoiKhung = t.Split(',')
                .Select(x => short.TryParse(x.Trim(), out var v) ? v : (short)0)
                .ToArray();
            break;
        }

        DoKichThuocO();
    }

    /// <summary>
    /// Dò rộng/cao của từng ô cắt từ chính tấm PNG.
    ///
    /// <para>
    /// Bảng ô chỉ cho toạ độ góc trên trái, mà các ô lại xếp khít nhau nên không tách được bằng
    /// cách khoanh vùng màu liền mạch - một mảng liền có thể vắt qua bốn ô. Cách chạy được: mỗi
    /// điểm ảnh đục thuộc về cái góc <b>gần nó nhất trong số các góc nằm trên-trái nó</b>, hoà
    /// nhau thì nhường cho góc thấp hơn (góc thấp mà hoà nghĩa là nó ở ngay bên trên điểm ảnh,
    /// còn cái kia thì tận đâu bên trái). Hộp bao của từng nhóm, neo lại vào đúng góc máy chủ
    /// gửi, chính là ô cắt.
    /// </para>
    ///
    /// <para>
    /// Đây là suy đoán chứ không phải dữ liệu máy chủ gửi, nên nó không hoàn hảo: đo trên
    /// Hirudegarn thì 45/47 ô ra hộp bao trùng khít góc đã cho, ghép lại nhìn đúng con quái,
    /// còn sót vài mảnh đuôi hơi lẹm. Đổi lại client gốc <b>không vẽ được gì cả</b> cho những
    /// con này.
    /// </para>
    /// </summary>
    private void DoKichThuocO()
    {
        if (oAnh.Length == 0) return;

        var duc = AnhPng.DocDuc(anh, out var rongAnh, out var caoAnh);
        if (duc == null) return;

        var phai = new int[oAnh.Length];
        var duoi = new int[oAnh.Length];
        for (var i = 0; i < oAnh.Length; i++) phai[i] = duoi[i] = -1;

        for (var y = 0; y < caoAnh; y++)
        for (var x = 0; x < rongAnh; x++)
        {
            if (!duc[y * rongAnh + x]) continue;

            var chu = -1;
            var gan = int.MaxValue;
            var thap = -1;
            for (var i = 0; i < oAnh.Length; i++)
            {
                var o = oAnh[i];
                if (o.x0 > x || o.y0 > y) continue;
                var xa = x - o.x0 + (y - o.y0);
                if (xa > gan || (xa == gan && o.y0 <= thap)) continue;
                gan = xa;
                thap = o.y0;
                chu = i;
            }

            if (chu < 0) continue;
            if (phai[chu] < x) phai[chu] = x;
            if (duoi[chu] < y) duoi[chu] = y;
        }

        for (var i = 0; i < oAnh.Length; i++)
        {
            if (phai[i] < 0) continue;
            oAnh[i].w = phai[i] - oAnh[i].x0 + 1;
            oAnh[i].h = duoi[i] - oAnh[i].y0 + 1;
        }

        CatTheoGocKhac();
        TinhKichThuoc();
    }

    /// <summary>
    /// Không ô nào được trùm lên góc của ô khác - bộ xếp hình đặt các ô cạnh nhau chứ không
    /// chồng nhau. Ô nào phạm thì xén bớt một chiều, chọn chiều nào giữ lại được nhiều hơn.
    /// </summary>
    private void CatTheoGocKhac()
    {
        for (var vong = 0; vong < 10; vong++)
        {
            var doi = false;

            foreach (var a in oAnh)
            foreach (var b in oAnh)
            {
                if (ReferenceEquals(a, b) || a.w <= 0 || b.w <= 0) continue;
                if (b.x0 < a.x0 || b.x0 >= a.x0 + a.w) continue;
                if (b.y0 < a.y0 || b.y0 >= a.y0 + a.h) continue;
                if (b.x0 == a.x0 && b.y0 == a.y0) continue;

                var hepLai = b.x0 - a.x0;
                var thapLai = b.y0 - a.y0;
                if (hepLai * a.h >= a.w * thapLai) a.w = hepLai;
                else a.h = thapLai;
                doi = true;
            }

            if (!doi) return;
        }
    }

    /// <summary>Rộng/cao của con quái, lấy theo hộp bao của khung đầu - y như client tính.</summary>
    private void TinhKichThuoc()
    {
        if (khung.Length == 0) return;

        int trai = 0, tren = 0, phai = 0, duoi = 0;
        var k = khung[0];
        for (var j = 0; j < k.oAnh.Length; j++)
        {
            var o = TimO(k.oAnh[j]);
            if (o == null) continue;
            if (trai > k.dx[j]) trai = k.dx[j];
            if (tren > k.dy[j]) tren = k.dy[j];
            if (phai < k.dx[j] + o.w) phai = k.dx[j] + o.w;
            if (duoi < k.dy[j] + o.h) duoi = k.dy[j] + o.h;
        }

        rong = phai - trai;
        cao = duoi - tren;
    }

    /// <summary>Cắt phần nội dung nằm sau tiêu đề <paramref name="tu"/> và trước <paramref name="den"/>.</summary>
    private static string Doan(string chu, string tu, string den)
    {
        var i = chu.IndexOf(tu, StringComparison.Ordinal);
        if (i < 0) return string.Empty;
        i = chu.IndexOf('\n', i);
        if (i < 0) return string.Empty;
        i++;

        if (den == null) return chu[i..];
        var j = chu.IndexOf(den, i, StringComparison.Ordinal);
        return j < 0 ? chu[i..] : chu[i..j];
    }

    private static int[] So(string chu) =>
        chu.Split(new[] { '\r', '\n', ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => int.TryParse(x, out var v) ? v : 0)
            .ToArray();
}
