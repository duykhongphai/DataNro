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

        // Hạn tổng phải bao được hết các lần thử lại, không thì nó cắt ngang giữa chừng và
        // ta mất một lần thử vô cớ.
        var hanTong = Math.Max(c.ChoDuLieuMs,
            c.SoLanDangNhap * c.ChoDangNhapMs + c.NghiGiuaLuotMs * c.SoLanDangNhap * c.SoLanDangNhap + 30000);
        using var het = new CancellationTokenSource(hanTong);
        var duLieu = await phien.DangNhapCoThuLaiAsync(c.Host, c.Port, c.TaiKhoan, c.MatKhau,
            c.ChoDangNhapMs, c.SoLanDangNhap, c.NghiGiuaLuotMs, het.Token);

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
    /// Máy chủ chỉ trả khoảng một trăm ảnh mỗi phiên rồi im, nên phải chia nhiều lượt, mỗi
    /// lượt một lô nhỏ rồi ngắt ra đăng nhập lại. <b>Chỉ id nào máy chủ đã trả lời mới bị gạch
    /// khỏi danh sách</b>; phần im lặng quay lại hàng chờ nguyên vẹn. Nhờ vậy không thể xảy
    /// ra chuyện bỏ sót: vòng lặp chỉ dừng khi hỏi lại mà vẫn không ra thêm cái nào.
    /// </para>
    /// </summary>
    private static async Task TaiAnhAsync(Phien phien, CauHinh c)
    {
        var thuMucAnh = Path.Combine(c.Ra, c.NhaPhatHanh, "Icons");
        var tatCa = BoAnh.GomId(phien.Data);
        var daCo = BoAnh.DaCoTrenDia(thuMucAnh);

        // Hàng chờ xoay vòng: id nào máy chủ không trả lời thì xuống CUỐI hàng chứ không nằm
        // lại đầu. Máy chủ im lặng với cả id không có ảnh lẫn id bị cắt vì quá hạn mức, mà
        // đám không có ảnh thì im mãi mãi - để chúng ở đầu là mỗi lượt lại hỏi đúng chúng,
        // dồn dần cho tới khi chiếm hết cả lô và không id mới nào được hỏi nữa.
        var hang = new Queue<(int id, int soLanHoi)>(
            tatCa.Where(id => !daCo.Contains(id)).Select(id => (id, 0)));

        Console.WriteLine($"Tải ảnh: {tatCa.Count} id, đã có sẵn {daCo.Count}, cần hỏi {hang.Count}");
        if (hang.Count == 0) return;

        using var hetAnh = new CancellationTokenSource(c.ChoAnhMs);
        var bo = new BoAnh(phien, thuMucAnh);
        var tongNhan = 0;
        var tongRong = 0;
        var boCuoc = 0;

        for (var luot = 1; luot <= c.SoLuotAnh && hang.Count > 0; luot++)
        {
            if (hetAnh.IsCancellationRequested)
            {
                Console.WriteLine("  ảnh: hết giờ cho phép, dừng.");
                break;
            }

            if (!phien.DaNoi && !await phien.DangNhapCoThuLaiAsync(c.Host, c.Port, c.TaiKhoan,
                    c.MatKhau, c.ChoDangNhapMs, c.SoLanDangNhap, c.NghiGiuaLuotMs, hetAnh.Token))
            {
                Console.WriteLine("  ảnh: nối lại không được, dừng.");
                break;
            }

            // Lô vừa đúng hạn mức máy chủ. Ném cả nghìn id vào một lượt thì nó chỉ trả lời
            // hơn trăm cái đầu rồi im, phần sau hỏi ra gió mà vẫn tốn 40ms mỗi cái.
            var lo = new List<(int id, int soLanHoi)>();
            while (lo.Count < c.SoAnhMoiLuot && hang.Count > 0) lo.Add(hang.Dequeue());

            var kq = await bo.MotLuotAsync(lo.Select(x => x.id).ToList(),
                c.NhipAnhMs, c.LangAnhMs, hetAnh.Token);
            tongNhan += kq.SoNhan;
            tongRong += kq.SoRong;

            // Id nào máy chủ trả lời thì xong hẳn. Id im lặng quay lại cuối hàng, cộng một
            // lần hỏi; hỏi đủ số lần mà vẫn im thì mới kết luận là nó không có ảnh.
            var chuaRo = new HashSet<int>(kq.ChuaRo);
            var boLuotNay = 0;
            foreach (var (id, soLanHoi) in lo)
            {
                if (!chuaRo.Contains(id)) continue;
                if (soLanHoi + 1 >= c.SoLanHoiLaiAnh)
                {
                    boLuotNay++;
                    boCuoc++;
                }
                else
                {
                    hang.Enqueue((id, soLanHoi + 1));
                }
            }

            Console.WriteLine($"  lượt {luot}: hỏi {lo.Count}, trả lời {lo.Count - chuaRo.Count} " +
                              $"(ảnh {kq.SoNhan}, rỗng {kq.SoRong}, hỏng {kq.SoLoi}), " +
                              $"hỏi lại sau {chuaRo.Count - boLuotNay}, bỏ {boLuotNay}, " +
                              $"còn trong hàng {hang.Count}" +
                              (kq.BiNgatGiuaChung ? " - máy chủ ngừng trả lời" : ""));

            if (hang.Count == 0) break;

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
        var thieu = tatCa.Count - coTrenDia;
        Console.WriteLine($"  ảnh: {coTrenDia}/{tatCa.Count} tệp (lần chạy này thêm {tongNhan}). " +
                          $"Thiếu {thieu}: {boCuoc} id hỏi {c.SoLanHoiLaiAnh} lần không thấy trả lời, " +
                          $"{hang.Count} id còn trong hàng → {Path.GetFullPath(thuMucAnh)}");

        // Còn id trong hàng nghĩa là vòng lặp dừng vì hết giờ / hết lượt / mất kết nối chứ
        // không phải vì đã hỏi xong. Nói rõ ra để lần chạy sau biết mà xin nốt.
        if (hang.Count > 0)
            Console.WriteLine("  ảnh: CHƯA hỏi hết - chạy lại lần nữa sẽ xin tiếp phần còn thiếu.");
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
