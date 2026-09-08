namespace DataNro;

/// <summary>
/// Tham số một lần dump. Đọc được từ ba nguồn, ưu tiên giảm dần:
/// tham số dòng lệnh → biến môi trường rời → biến môi trường gộp <c>DATA</c>.
///
/// <para>
/// Biến gộp <c>DATA</c> là để nhét vừa một secret của GitHub Actions, bố cục
/// <c>thư mục|host|cổng|tài khoản|mật khẩu|proxy</c>, hai trường cuối bỏ trống được:
/// <c>Server1|112.213.94.23|14445|abc|123|socks5://user:pass@1.2.3.4:1080</c>
/// </para>
/// </summary>
public class CauHinh
{
    /// <summary>Tên thư mục con trong repo dữ liệu, ví dụ <c>Server1</c>.</summary>
    public string TenThuMuc { get; set; } = "Server1";

    /// <summary>Nhà phát hành - thư mục cha, để sau này thêm server lậu vẫn không đụng nhau.</summary>
    public string NhaPhatHanh { get; set; } = "TeaMobi";

    public string Host { get; set; }
    public int Port { get; set; }

    /// <summary>
    /// Không cho host thì lấy máy chủ theo tên trong danh sách máy chủ sống của game.
    /// Khớp không phân biệt hoa thường, chấp nhận khớp một phần ("Vũ trụ 1").
    /// </summary>
    public string TenMayChu { get; set; }

    public string TaiKhoan { get; set; }
    public string MatKhau { get; set; }

    /// <summary>Chuỗi proxy dạng <c>socks5://user:pass@host:port</c>; bỏ trống là đi thẳng.</summary>
    public string Proxy { get; set; }

    /// <summary>
    /// Danh sách tài khoản phụ để chạy song song, mỗi dòng <c>tài khoản|mật khẩu</c>.
    ///
    /// <para>
    /// Hạn mức ảnh của máy chủ tính theo <b>phiên</b>: mỗi phiên trả lời chừng trăm gói rồi im
    /// tới hết phiên. Một tài khoản thì chỉ còn cách ngắt ra đăng nhập lại, mà mỗi vòng như vậy
    /// mất cả phút. Nhiều tài khoản cùng rút chung một hàng chờ thì thời gian chia đều cho số
    /// tài khoản.
    /// </para>
    /// </summary>
    public List<(string tk, string mk)> DsTaiKhoan { get; } = new();

    /// <summary>Thư mục gốc để ghi dữ liệu ra; tệp nằm ở <c>&lt;Ra&gt;/&lt;NhàPhátHành&gt;/&lt;TênThưMục&gt;/</c>.</summary>
    public string Ra { get; set; } = "out";

    /// <summary>Cũng ghi <c>map.json</c> ở gốc thư mục ra, giữ tương thích với các tool cũ.</summary>
    public bool GhiMapJsonGoc { get; set; } = true;

    /// <summary>Hết giờ chờ đủ bảng dữ liệu.</summary>
    /// Tính cả các lần thử lại, nên rộng hơn <see cref="ChoDangNhapMs"/> nhiều lần.
    public int ChoDuLieuMs { get; set; } = 300000;

    /// <summary>
    /// Chờ bảng mảnh dựng hình bao lâu. Nó chỉ về sau khi nhân vật đã vào map, mà trước đó
    /// còn phải chọn hoặc tạo nhân vật nên lâu hơn mấy bảng kia nhiều.
    /// </summary>
    public int ChoPartMs { get; set; } = 60000;

    /// <summary>
    /// Vào hẳn trong game (chọn nhân vật, hoặc tạo nếu máy chủ chưa có) để lấy bảng mảnh
    /// dựng hình. Tắt thì dừng ở bước đăng nhập như trước và không có <c>Parts.json</c>.
    /// </summary>
    public bool VaoMap { get; set; } = true;

    /// <summary>Tải luôn hình sprite của từng mẫu quái.</summary>
    public bool TaiQuai { get; set; } = true;

    /// <summary>Xin cả kho tài nguyên của client (gói -74) - trong đó có ảnh nền map.</summary>
    public bool TaiTaiNguyen { get; set; }

    /// <summary>Hết giờ cho việc tải kho tài nguyên.</summary>
    public int ChoTaiNguyenMs { get; set; } = 600000;

    /// <summary>Xin luôn bố cục ô của từng map (gói -28 nhánh 10).</summary>
    public bool TaiMap { get; set; } = true;

    /// <summary>Chờ bố cục một map bao lâu rồi bỏ qua sang map kế.</summary>
    public int ChoMotMapMs { get; set; } = 4000;

    /// <summary>Cách nhau bao lâu giữa hai lần xin bố cục map.</summary>
    public int NhipMapMs { get; set; } = 120;

    /// <summary>Mỗi lượt hỏi tối đa bao nhiêu mẫu quái.</summary>
    public int SoQuaiMoiLuot { get; set; } = 40;

    /// <summary>
    /// Cách nhau bao lâu giữa hai lần hỏi quái. Chậm hơn hỏi ảnh vì mỗi gói trả về kèm cả
    /// một tấm sprite, nặng hơn hẳn một cái icon.
    /// </summary>
    public int NhipQuaiMs { get; set; } = 150;

    /// <summary>Hết giờ cho cả việc tải hình quái.</summary>
    public int ChoQuaiMs { get; set; } = 900000;

    /// <summary>Tải luôn ảnh icon sau khi có bảng dữ liệu.</summary>
    public bool TaiAnh { get; set; } = true;

    /// <summary>
    /// Quét mù id ảnh từ 0 tới số này thay vì chỉ hỏi những id có bảng dữ liệu trỏ tới. 0 là
    /// tắt. Máy chủ không có gói nào liệt kê kho ảnh, nên muốn lấy sạch thì chỉ còn cách hỏi
    /// hết - id nào không có thì nó im lặng, ghi lại vào <c>Icons/KhongCo.json</c> để lần sau
    /// khỏi hỏi lại.
    /// </summary>
    public int IdAnhToiDa { get; set; }

    /// <summary>
    /// Số phiên chạy song song, mỗi phiên một tài khoản. <b>0 nghĩa là dùng hết</b> số tài
    /// khoản đã khai - khai bao nhiêu chạy bấy nhiêu. Đặt số cụ thể để hãm lại nếu máy chủ
    /// khó chịu với chuyện nhiều phiên cùng một địa chỉ.
    /// </summary>
    public int SoPhienSongSong { get; set; }

    /// <summary>
    /// Cách nhau bao lâu giữa hai lần hỏi ảnh. Giao diện các tool đang để 70ms; ở đây nhanh
    /// hơn một chút vì chỉ có mỗi việc này, nhưng đừng hạ sâu quá kẻo máy chủ coi là spam.
    /// </summary>
    public int NhipAnhMs { get; set; } = 40;

    /// <summary>Máy chủ im lặng bao lâu thì coi như hết ảnh để trả.</summary>
    public int LangAnhMs { get; set; } = 8000;

    /// <summary>
    /// Mỗi lượt hỏi tối đa bao nhiêu id. Đo thực tế máy chủ trả khoảng một trăm gói mỗi phiên
    /// rồi im, nên lô lớn hơn ngần này chỉ tổ hỏi ra gió mà vẫn tốn 40ms mỗi cái.
    /// </summary>
    /// <para>
    /// Đo thực tế máy chủ trả khoảng một trăm gói mỗi phiên. Để lô <b>90</b> - dưới hạn mức
    /// đó - thì mọi id trong lô đều được trả lời, không còn khúc đuôi chết nào. Nhờ vậy "hỏi
    /// mà không thấy trả lời" nghĩa đúng là "không có ảnh", khỏi phải suy đoán gì thêm.
    /// Người dùng chốt (2026-09-08): thà chạy thêm vài lượt còn hơn sót ảnh.
    /// </para>
    public int SoAnhMoiLuot { get; set; } = 90;

    /// <summary>
    /// Hỏi một id mấy lần (ở mấy phiên khác nhau) mà vẫn không thấy trả lời thì mới kết luận
    /// là máy chủ không có ảnh đó. Im lặng có hai nghĩa - "không có ảnh" và "đã hết hạn mức
    /// phiên" - nên phải hỏi lại vài lần mới phân biệt được.
    /// </summary>
    public int SoLanHoiLaiAnh { get; set; } = 3;

    /// <summary>Hạn cho MỘT lần thử đăng nhập. Ngắn thôi: lag thì cắt ra vào lại nhanh hơn chờ.</summary>
    public int ChoDangNhapMs { get; set; } = 45000;

    /// <summary>Thử đăng nhập tối đa mấy lần trước khi chịu thua.</summary>
    public int SoLanDangNhap { get; set; } = 4;

    /// <summary>
    /// Hai lần đăng nhập <b>bất kì</b> phải cách nhau ít nhất ngần này, tính chung cho mọi
    /// phiên. Máy chủ bắt chờ theo địa chỉ chứ không theo tài khoản. 0 là tắt.
    /// </summary>
    public int CachDangNhapMs { get; set; } = 30000;

    /// <summary>Hết giờ cho cả việc tải ảnh, tính riêng với <see cref="ChoDuLieuMs"/>.</summary>
    public int ChoAnhMs { get; set; } = 2400000;

    /// <summary>Tối đa bao nhiêu lượt đăng nhập lại để xin nốt ảnh.</summary>
    public int SoLuotAnh { get; set; } = 60;

    /// <summary>
    /// Nghỉ bao lâu giữa hai lượt. Đo thực tế: nghỉ 5 giây thì tới lần đăng nhập thứ ba là
    /// máy chủ không cho vào nữa, nên để rộng tay.
    /// </summary>
    public int NghiGiuaLuotMs { get; set; } = 20000;

    public static CauHinh Doc(string[] args)
    {
        var c = new CauHinh();

        // 1. biến gộp DATA
        var gop = Environment.GetEnvironmentVariable("DATA");
        if (!string.IsNullOrWhiteSpace(gop))
        {
            var f = gop.Split('|');
            if (f.Length > 0 && f[0].Length > 0) c.TenThuMuc = f[0].Trim();
            if (f.Length > 1 && f[1].Length > 0) c.Host = f[1].Trim();
            if (f.Length > 2 && int.TryParse(f[2].Trim(), out var p)) c.Port = p;
            if (f.Length > 3) c.TaiKhoan = f[3].Trim();
            if (f.Length > 4) c.MatKhau = f[4].Trim();
            if (f.Length > 5 && f[5].Length > 0) c.Proxy = f[5].Trim();
        }

        // 2. biến môi trường rời
        Moi("NRO_THUMUC", v => c.TenThuMuc = v);
        Moi("NRO_NPH", v => c.NhaPhatHanh = v);
        Moi("NRO_HOST", v => c.Host = v);
        Moi("NRO_PORT", v => { if (int.TryParse(v, out var p)) c.Port = p; });
        Moi("NRO_MAYCHU", v => c.TenMayChu = v);
        Moi("NRO_TK", v => c.TaiKhoan = v);
        Moi("NRO_MK", v => c.MatKhau = v);
        Moi("NRO_PROXY", v => c.Proxy = v);
        Moi("NRO_RA", v => c.Ra = v);
        Moi("NRO_KHONG_ANH", _ => c.TaiAnh = false);
        Moi("NRO_ID_ANH_TOI_DA", v => { if (int.TryParse(v, out var m)) c.IdAnhToiDa = m; });
        Moi("NRO_SONG_SONG", v => { if (int.TryParse(v, out var ss)) c.SoPhienSongSong = ss; });
        Moi("NRO_CACH_DANG_NHAP", v => { if (int.TryParse(v, out var cdn2)) c.CachDangNhapMs = cdn2; });

        // Danh sách tài khoản phụ: mỗi dòng "tài khoản|mật khẩu". Xuống dòng trong một secret
        // của GitHub Actions vẫn giữ nguyên nên nhét cả danh sách vào một secret là được.
        var ds = Environment.GetEnvironmentVariable("NRO_TK_DS");
        if (!string.IsNullOrWhiteSpace(ds))
            foreach (var dong in ds.Split('\n'))
            {
                var t = dong.Trim();
                if (t.Length == 0) continue;
                var f = t.Split('|');
                if (f.Length < 2) continue;
                var tk = f[0].Trim();
                var mk = f[1].Trim();
                if (tk.Length > 0 && mk.Length > 0) c.DsTaiKhoan.Add((tk, mk));
            }

        // 3. dòng lệnh
        for (var i = 0; i < args.Length; i++)
        {
            string KeTiep() => i + 1 < args.Length ? args[++i] : null;
            switch (args[i])
            {
                case "--thumuc": c.TenThuMuc = KeTiep(); break;
                case "--nph": c.NhaPhatHanh = KeTiep(); break;
                case "--host": c.Host = KeTiep(); break;
                case "--port": if (int.TryParse(KeTiep(), out var p)) c.Port = p; break;
                case "--maychu": c.TenMayChu = KeTiep(); break;
                case "--tk": c.TaiKhoan = KeTiep(); break;
                case "--mk": c.MatKhau = KeTiep(); break;
                case "--proxy": c.Proxy = KeTiep(); break;
                case "--ra": c.Ra = KeTiep(); break;
                case "--khong-map-goc": c.GhiMapJsonGoc = false; break;
                case "--cho": if (int.TryParse(KeTiep(), out var t)) c.ChoDuLieuMs = t; break;
                case "--cho-part": if (int.TryParse(KeTiep(), out var cp)) c.ChoPartMs = cp; break;
                case "--khong-vao-map": c.VaoMap = false; break;
                case "--khong-anh": c.TaiAnh = false; break;
                case "--khong-quai": c.TaiQuai = false; break;
                case "--khong-map": c.TaiMap = false; break;
                case "--tai-res": c.TaiTaiNguyen = true; break;
                case "--nhip-map": if (int.TryParse(KeTiep(), out var nm)) c.NhipMapMs = nm; break;
                case "--lo-quai": if (int.TryParse(KeTiep(), out var lq)) c.SoQuaiMoiLuot = lq; break;
                case "--nhip-quai": if (int.TryParse(KeTiep(), out var nq)) c.NhipQuaiMs = nq; break;
                case "--nhip-anh": if (int.TryParse(KeTiep(), out var na)) c.NhipAnhMs = na; break;
                case "--lang-anh": if (int.TryParse(KeTiep(), out var la)) c.LangAnhMs = la; break;
                case "--cho-anh": if (int.TryParse(KeTiep(), out var ca)) c.ChoAnhMs = ca; break;
                case "--luot-anh": if (int.TryParse(KeTiep(), out var lu)) c.SoLuotAnh = lu; break;
                case "--lo-anh": if (int.TryParse(KeTiep(), out var lo)) c.SoAnhMoiLuot = lo; break;
                case "--hoi-lai-anh": if (int.TryParse(KeTiep(), out var hl)) c.SoLanHoiLaiAnh = hl; break;
                case "--cho-dang-nhap": if (int.TryParse(KeTiep(), out var cdn)) c.ChoDangNhapMs = cdn; break;
                case "--lan-dang-nhap": if (int.TryParse(KeTiep(), out var ldn)) c.SoLanDangNhap = ldn; break;
                case "--nghi-luot": if (int.TryParse(KeTiep(), out var ng)) c.NghiGiuaLuotMs = ng; break;
                case "--id-anh-toi-da": if (int.TryParse(KeTiep(), out var im)) c.IdAnhToiDa = im; break;
                case "--song-song": if (int.TryParse(KeTiep(), out var ss2)) c.SoPhienSongSong = ss2; break;
                case "--cach-dang-nhap": if (int.TryParse(KeTiep(), out var cd)) c.CachDangNhapMs = cd; break;
            }
        }

        if (c.Port <= 0) c.Port = 14445;

        // Khai hết vào danh sách, không đặt riêng tài khoản chính, cũng chạy: lấy dòng đầu làm
        // tài khoản chính. Phiên đầu tiên - phiên đi lấy bảng dữ liệu - dùng tài khoản này.
        if (string.IsNullOrWhiteSpace(c.TaiKhoan) && c.DsTaiKhoan.Count > 0)
        {
            c.TaiKhoan = c.DsTaiKhoan[0].tk;
            c.MatKhau = c.DsTaiKhoan[0].mk;
        }

        // Tài khoản chính luôn là thợ đầu tiên; danh sách chỉ bổ sung, và bỏ trùng để không có
        // hai phiên cùng đăng nhập một tài khoản (máy chủ đá phiên cũ ra).
        if (!string.IsNullOrWhiteSpace(c.TaiKhoan) && !string.IsNullOrWhiteSpace(c.MatKhau))
        {
            c.DsTaiKhoan.RemoveAll(x => string.Equals(x.tk, c.TaiKhoan, StringComparison.OrdinalIgnoreCase));
            c.DsTaiKhoan.Insert(0, (c.TaiKhoan, c.MatKhau));
        }

        return c;

        static void Moi(string ten, Action<string> dat)
        {
            var v = Environment.GetEnvironmentVariable(ten);
            if (!string.IsNullOrWhiteSpace(v)) dat(v.Trim());
        }
    }

    /// <summary>Lỗi cấu hình dễ thấy, trả về null là hợp lệ.</summary>
    public string LoiCauHinh()
    {
        if (string.IsNullOrWhiteSpace(TaiKhoan))
            return "Chưa có tài khoản: cho --tk, hoặc NRO_TK, hoặc một dòng trong NRO_TK_DS";
        if (string.IsNullOrWhiteSpace(MatKhau))
            return "Chưa có mật khẩu: cho --mk, hoặc NRO_MK, hoặc phần sau dấu | trong NRO_TK_DS";
        if (string.IsNullOrWhiteSpace(Host) && string.IsNullOrWhiteSpace(TenMayChu))
            return "Chưa biết nối vào đâu: cho --host hoặc --maychu";
        if (string.IsNullOrWhiteSpace(TenThuMuc)) return "Chưa có tên thư mục dữ liệu (--thumuc)";
        return null;
    }

    /// <summary>Dòng tóm tắt để in ra log mà không lộ mật khẩu.</summary>
    public string TomTat()
    {
        var dich = string.IsNullOrWhiteSpace(Host) ? TenMayChu : $"{Host}:{Port}";
        var qua = string.IsNullOrWhiteSpace(Proxy) ? "đi thẳng" : "qua proxy";
        return $"{NhaPhatHanh}/{TenThuMuc} ← {dich} ({qua}), tài khoản {TaiKhoan}";
    }
}
