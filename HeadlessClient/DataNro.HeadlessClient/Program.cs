using System.IO;
using DataNro.GiaoThuc;
using DataNro.Mang;

namespace DataNro;

/// <summary>
/// Đăng nhập một lần rồi đổ toàn bộ bảng mẫu và ảnh icon của máy chủ ra JSON/PNG.
///
/// <para>
/// Cố ý <b>không</b> vào map: máy chủ gửi data / map / skill / item ngay trong lúc bắt tay
/// đăng nhập, xong bốn bảng đó là có đủ mọi thứ cần xuất. Dừng ở đấy rồi ngắt kết nối thì
/// nhân vật chưa hề vào game - vừa nhanh vừa đỡ để lại dấu vết.
/// </para>
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var c = CauHinh.Doc(args);
        var loi = c.LoiCauHinh();
        if (loi != null)
        {
            Console.Error.WriteLine("Cấu hình sai: " + loi);
            InHuongDan();
            return 2;
        }

        Console.WriteLine("== DataNro headless client ==");
        Console.WriteLine(c.TomTat());

        if (string.IsNullOrWhiteSpace(c.Host))
        {
            var timDuoc = await TimMayChuAsync(c.TenMayChu);
            if (timDuoc == null)
            {
                Console.Error.WriteLine($"Không thấy máy chủ tên '{c.TenMayChu}' trong danh sách.");
                return 3;
            }

            c.Host = timDuoc.Host;
            c.Port = timDuoc.Port;
            Console.WriteLine($"Chọn máy chủ: {timDuoc.Name} ({c.Host}:{c.Port})");
        }

        using var phien = new Phien(m => Console.WriteLine("  " + m));
        if (!DatProxy(phien, c)) return 2;

        using var het = new CancellationTokenSource(c.ChoDuLieuMs);
        var duLieu = await phien.DangNhapAsync(c.Host, c.Port, c.TaiKhoan, c.MatKhau,
            c.ChoDuLieuMs, het.Token);

        if (!duLieu)
        {
            phien.Ngat();
            Console.Error.WriteLine("Không lấy đủ dữ liệu. " + phien.Data);
            return 1;
        }

        // Ghi bảng dữ liệu trước, rồi mới tải ảnh: ảnh mất cả chục phút và có thể đứt giữa
        // chừng, mà mất ảnh thì trang vẫn xem được - mất bảng thì không.
        var thuMuc = BoXuat.Ghi(phien.Data, c, phien.Doc.LoaiClient);
        Console.WriteLine("Đã ghi: " + Path.GetFullPath(thuMuc));
        Console.WriteLine("  " + phien.Data);

        if (c.TaiAnh) await TaiAnhAsync(phien, c);

        phien.Ngat();
        return 0;
    }

    private static bool DatProxy(Phien phien, CauHinh c)
    {
        if (string.IsNullOrWhiteSpace(c.Proxy)) return true;

        var p = ProxyInfo.Parse(c.Proxy);
        if (p == null)
        {
            Console.Error.WriteLine("Chuỗi proxy không đọc được: " + c.Proxy);
            return false;
        }

        phien.Proxy = p;
        return true;
    }

    /// <summary>
    /// Hỏi máy chủ từng ảnh icon rồi ghi ra <c>&lt;ra&gt;/&lt;nhà phát hành&gt;/Icons/</c>.
    ///
    /// <para>
    /// Ảnh để chung một thư mục theo nhà phát hành chứ không theo từng máy chủ: id ảnh giống
    /// nhau trên mọi máy chủ cùng nhà phát hành, tách ra chỉ tổ nhân đôi vài nghìn tệp.
    /// </para>
    ///
    /// <para>
    /// Máy chủ chỉ trả khoảng hai trăm ảnh mỗi phiên rồi im, nên phải chia thành nhiều lượt:
    /// hỏi tới khi nó ngừng trả lời, đăng nhập lại, hỏi tiếp phần chưa rõ. Ảnh đã nằm ngoài
    /// đĩa thì bỏ qua, nên chạy lại lần sau là nối tiếp chứ không làm lại từ đầu.
    /// </para>
    /// </summary>
    private static async Task TaiAnhAsync(Phien phien, CauHinh c)
    {
        var thuMucAnh = Path.Combine(c.Ra, c.NhaPhatHanh, "Icons");
        var tatCa = BoAnh.GomId(phien.Data);
        var daCo = BoAnh.DaCoTrenDia(thuMucAnh);
        var conLai = tatCa.Where(id => !daCo.Contains(id)).ToList();

        Console.WriteLine($"Tải ảnh: {tatCa.Count} id, đã có sẵn {daCo.Count}, cần hỏi {conLai.Count}");
        if (conLai.Count == 0) return;

        using var hetAnh = new CancellationTokenSource(c.ChoAnhMs);
        var bo = new BoAnh(phien, thuMucAnh);
        var tongNhan = 0;
        var tongRong = 0;

        for (var luot = 1; luot <= c.SoLuotAnh && conLai.Count > 0; luot++)
        {
            if (hetAnh.IsCancellationRequested)
            {
                Console.WriteLine("  ảnh: hết giờ cho phép, dừng.");
                break;
            }

            if (!phien.DaNoi && !await NoiLaiAsync(phien, c, hetAnh.Token))
            {
                Console.WriteLine("  ảnh: nối lại không được, dừng.");
                break;
            }

            var kq = await bo.MotLuotAsync(conLai, c.NhipAnhMs, c.LangAnhMs, hetAnh.Token);
            tongNhan += kq.SoNhan;
            tongRong += kq.SoRong;

            Console.WriteLine($"  lượt {luot}: nhận {kq.SoNhan}, rỗng {kq.SoRong}, hỏng {kq.SoLoi}, " +
                              $"còn chưa rõ {kq.ChuaRo.Count}" +
                              (kq.BiNgatGiuaChung ? " (máy chủ ngừng trả lời)" : ""));

            // Lượt không xin thêm được ảnh nào thì lượt sau cũng vậy - đừng đăng nhập lại vô ích.
            if (kq.SoNhan == 0 && kq.SoRong == 0) break;

            conLai = kq.ChuaRo;
            if (conLai.Count == 0) break;

            // Ngắt hẳn rồi nghỉ một nhịp: bộ đếm của máy chủ tính theo phiên, giữ nguyên kết
            // nối mà hỏi tiếp thì vẫn im như cũ.
            phien.Ngat();
            try
            {
                await Task.Delay(c.NghiGiuaLuotMs, hetAnh.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        var coTrenDia = BoAnh.DaCoTrenDia(thuMucAnh).Count;
        Console.WriteLine($"  ảnh: tổng cộng {coTrenDia}/{tatCa.Count} tệp " +
                          $"(lần chạy này thêm {tongNhan}, máy chủ báo không có ảnh {tongRong}) " +
                          $"→ {Path.GetFullPath(thuMucAnh)}");
    }

    /// <summary>
    /// Đăng nhập lại để xin tiếp ảnh. Thử vài lần với quãng nghỉ dài dần: đăng nhập dồn dập
    /// thì máy chủ đẩy vào hàng chờ hoặc chặn thẳng.
    /// </summary>
    private static async Task<bool> NoiLaiAsync(Phien phien, CauHinh c, CancellationToken ct)
    {
        for (var lan = 1; lan <= 3 && !ct.IsCancellationRequested; lan++)
        {
            Console.WriteLine($"  ảnh: đăng nhập lại (lần {lan})...");
            if (await phien.DangNhapAsync(c.Host, c.Port, c.TaiKhoan, c.MatKhau,
                    c.ChoDuLieuMs, ct))
                return true;

            phien.Ngat();
            try
            {
                await Task.Delay(c.NghiGiuaLuotMs * lan, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        return phien.DaNoi;
    }

    private static async Task<ServerInfo> TimMayChuAsync(string ten)
    {
        var ds = await ServerList.LoadAsync().ConfigureAwait(false);
        Console.WriteLine($"  danh sách máy chủ: {ds.Count} mục ({(ds.FromNetwork ? "từ mạng" : "bản dự phòng")})");

        return ds.Servers.FirstOrDefault(x => string.Equals(x.Name, ten, StringComparison.OrdinalIgnoreCase))
               ?? ds.Servers.FirstOrDefault(x => x.Name.Contains(ten, StringComparison.OrdinalIgnoreCase));
    }

    private static void InHuongDan()
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine("Cách dùng:");
        Console.Error.WriteLine("  DataNro.HeadlessClient --thumuc Server1 --maychu \"Vũ trụ 1\" --tk taikhoan --mk matkhau --ra out");
        Console.Error.WriteLine("  DataNro.HeadlessClient --thumuc Server1 --host 112.213.94.23 --port 14445 --tk a --mk b");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Biến môi trường: NRO_THUMUC NRO_NPH NRO_HOST NRO_PORT NRO_MAYCHU NRO_TK NRO_MK NRO_PROXY NRO_RA");
        Console.Error.WriteLine("Hoặc một biến gộp DATA = thư mục|host|cổng|tài khoản|mật khẩu|proxy");
    }
}
