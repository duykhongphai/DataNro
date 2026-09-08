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
        GiaoThuc.CongDangNhap.CachMs = c.CachDangNhapMs;
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
        phien.Doc.GhiMoiGoi = Environment.GetEnvironmentVariable("NRO_SOI") == "1";
        phien.Doc.ChoPhepVaoMap = c.VaoMap;
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

        // Bảng mảnh dựng hình về muộn hơn hẳn: máy chủ chỉ gửi sau khi nhân vật đã vào map,
        // mà vào map thì còn phải chọn (hoặc tạo) nhân vật xong đã. Chờ có hạn - máy chủ nào
        // không gửi thì vẫn xuất những bảng còn lại chứ không hỏng cả lượt.
        if (c.VaoMap && phien.Data.parts.Length == 0)
        {
            var hanPart = Environment.TickCount64 + c.ChoPartMs;
            while (phien.Data.parts.Length == 0 && Environment.TickCount64 < hanPart
                   && phien.DaNoi && !het.IsCancellationRequested)
                await Task.Delay(250).ConfigureAwait(false);

            if (phien.Data.parts.Length == 0)
                Console.WriteLine("  không nhận được bảng part, bỏ qua Parts.json");
        }

        // Ghi bảng dữ liệu trước, rồi mới tải ảnh: ảnh mất cả chục phút và có thể đứt giữa
        // chừng, mà mất ảnh thì trang vẫn xem được - mất bảng thì không.
        var thuMuc = BoXuat.Ghi(phien.Data, c, phien.Doc.LoaiClient);
        Console.WriteLine("Đã ghi: " + Path.GetFullPath(thuMuc));
        Console.WriteLine("  " + phien.Data);

        if (c.TaiMap) await TaiMapAsync(phien, c);
        if (c.TaiAnh) await TaiAnhAsync(phien, c);
        if (c.TaiQuai) await TaiQuaiAsync(phien, c);

        // Bảng kích thước ảnh: quét cả thư mục chứ không chỉ phần vừa tải, và chạy cả khi lượt
        // này bỏ ảnh - lần chạy trước có thể đã thêm ảnh mà chưa kịp ghi bảng.
        var thuMucNph = Path.Combine(c.Ra, c.NhaPhatHanh);
        BoAnh.GhiKichThuoc(Path.Combine(thuMucNph, "Icons"));

        // Ghép sẵn hình NPC với hình quái: để trang tự ghép thì một trang NPC phải tải hơn hai
        // trăm sáu mươi ảnh rời, còn mỗi con quái kéo về nguyên tấm sprite chỉ để lấy một khung.
        try
        {
            var soNpc = BoGhepHinh.GhepNpc(phien.Data, thuMucNph);
            var soQuai = BoGhepHinh.GhepQuai(thuMucNph);
            Console.WriteLine($"Ghép sẵn hình: {soNpc} NPC, {soQuai} quái");
        }
        catch (Exception e)
        {
            Console.WriteLine("Ghép hình hỏng: " + e.Message);
        }

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
    /// <summary>
    /// Xin bố cục ô của từng map. Nhẹ hơn hẳn ảnh với hình quái nên làm trước, và làm ngay
    /// trên phiên vừa đăng nhập chứ không cần chia tài khoản.
    /// </summary>
    private static async Task TaiMapAsync(Phien phien, CauHinh c)
    {
        var thuMucMap = Path.Combine(c.Ra, c.NhaPhatHanh, "Maps");
        var tatCa = Enumerable.Range(0, phien.Data.mapNames.Length).ToList();
        if (tatCa.Count == 0) return;

        var daCo = BoMap.DaCoTrenDia(thuMucMap);
        var can = tatCa.Where(id => !daCo.Contains(id)).ToList();

        Console.WriteLine($"Bố cục map: {tatCa.Count} map, đã có sẵn {daCo.Count}, cần hỏi {can.Count}");
        if (can.Count == 0) return;

        var bo = new BoMap(phien, thuMucMap);
        using var het = new CancellationTokenSource(c.ChoQuaiMs);
        var duoc = await bo.LayAsync(can, c.ChoMotMapMs, c.NhipMapMs, het.Token);
        bo.Ghi();

        Console.WriteLine($"  map: lấy thêm {duoc}, tổng cộng {bo.SoDaCo}/{tatCa.Count} " +
                          $"→ {Path.GetFullPath(thuMucMap)}");
    }

    private static async Task TaiAnhAsync(Phien phienDau, CauHinh c)
    {
        var thuMucAnh = Path.Combine(c.Ra, c.NhaPhatHanh, "Icons");

        var daDon = BoAnh.DonVaoThuMucCon(thuMucAnh);
        if (daDon > 0) Console.WriteLine($"  dọn {daDon} ảnh cũ vào thư mục con");

        var tatCa = c.IdAnhToiDa > 0
            ? Enumerable.Range(0, c.IdAnhToiDa + 1).ToList()
            : BoAnh.GomId(phienDau.Data);

        var daCo = BoAnh.DaCoTrenDia(thuMucAnh);
        var khongCo = BoAnh.DocKhongCo(thuMucAnh);

        var canHoi = tatCa.Where(id => !daCo.Contains(id) && !khongCo.Contains(id)).ToList();
        var hang = new HangAnh(canHoi);

        Console.WriteLine($"Tải ảnh: {tatCa.Count} id" +
                          (c.IdAnhToiDa > 0 ? " (quét mù 0.." + c.IdAnhToiDa + ")" : "") +
                          $", đã có sẵn {daCo.Count}, biết là không có {khongCo.Count}, " +
                          $"cần hỏi {canHoi.Count}");
        if (canHoi.Count == 0) return;

        var tho = c.SoPhienSongSong > 0
            ? c.DsTaiKhoan.Take(c.SoPhienSongSong).ToList()
            : c.DsTaiKhoan.ToList();
        Console.WriteLine($"  chạy {tho.Count} phiên song song: " +
                          string.Join(", ", tho.Select(x => x.tk)));

        using var hetAnh = new CancellationTokenSource(c.ChoAnhMs);
        var moiPhat = new ThongKeAnh();

        // Thợ đầu dùng luôn phiên đang mở - nó vừa lấy xong bảng dữ liệu nên còn nguyên hạn
        // mức chưa đụng tới. Mấy thợ sau mở phiên riêng.
        var viec = tho.Select((tk, i) => MotThoAnhAsync(
            i, tk, i == 0 ? phienDau : null, hang, moiPhat, c, thuMucAnh, hetAnh.Token)).ToList();
        await Task.WhenAll(viec).ConfigureAwait(false);

        BoAnh.GhiKhongCo(thuMucAnh, moiPhat.KhongCo);

        // Đếm theo danh sách CẦN chứ không theo số tệp ngoài đĩa: đĩa còn giữ cả ảnh của
        // những lần chạy trước không còn ai tham chiếu, lấy hiệu hai số ra âm ngay.
        var coTrenDia = BoAnh.DaCoTrenDia(thuMucAnh);
        var thieu = tatCa.Count(id => !coTrenDia.Contains(id));
        Console.WriteLine($"  ảnh: {tatCa.Count - thieu}/{tatCa.Count} id có ảnh " +
                          $"(lần chạy này thêm {moiPhat.Nhan}, trên đĩa tổng cộng {coTrenDia.Count} tệp). " +
                          $"Thiếu {thieu}: {moiPhat.KhongCo.Count} id hỏi {c.SoLanHoiLaiAnh} lần không thấy " +
                          $"trả lời, {hang.Con} id còn trong hàng → {Path.GetFullPath(thuMucAnh)}");

        if (hang.Con > 0)
            Console.WriteLine("  ảnh: CHƯA hỏi hết - chạy lại lần nữa sẽ xin tiếp phần còn thiếu.");
    }

    /// <summary>Số liệu gộp của mọi thợ. Mọi thao tác đều phải khoá vì nhiều luồng cùng ghi.</summary>
    private sealed class ThongKeAnh
    {
        private readonly object khoa = new();
        public int Nhan, Rong;

        /// <summary>Id đã hỏi đủ số lần mà máy chủ vẫn im - coi như kho không có.</summary>
        public readonly HashSet<int> KhongCo = new();

        public void Them(int nhan, int rong)
        {
            lock (khoa)
            {
                Nhan += nhan;
                Rong += rong;
            }
        }

        public void ThemKhongCo(int id)
        {
            lock (khoa) KhongCo.Add(id);
        }
    }

    /// <summary>
    /// Một thợ: đăng nhập bằng tài khoản của mình, rút lô từ hàng chung, hỏi, ngắt, nghỉ, lặp.
    ///
    /// <para>
    /// Ngắt hẳn rồi mới nghỉ chứ không giữ kết nối: bộ đếm của máy chủ tính theo <b>phiên</b>,
    /// giữ nguyên kết nối mà hỏi tiếp thì nó vẫn im như cũ.
    /// </para>
    /// </summary>
    private static async Task MotThoAnhAsync(int soTho, (string tk, string mk) tk, Phien coSan,
        HangAnh hang, ThongKeAnh thongKe, CauHinh c, string thuMucAnh, CancellationToken ct)
    {
        var ten = $"#{soTho + 1} {tk.tk}";
        var phien = coSan ?? new Phien(d => Console.WriteLine($"  [{ten}] {d}"));
        var bo = new BoAnh(phien, thuMucAnh);

        try
        {
            for (var luot = 1; luot <= c.SoLuotAnh; luot++)
            {
                if (ct.IsCancellationRequested || hang.Con == 0) break;

                if (!phien.DaNoi && !await phien.DangNhapCoThuLaiAsync(c.Host, c.Port, tk.tk,
                        tk.mk, c.ChoDangNhapMs, c.SoLanDangNhap, c.NghiGiuaLuotMs, ct))
                {
                    Console.WriteLine($"  [{ten}] nối lại không được, dừng.");
                    break;
                }

                // Lô vừa đúng hạn mức máy chủ. Ném cả nghìn id vào một lượt thì nó chỉ trả lời
                // hơn trăm cái đầu rồi im, phần sau hỏi ra gió mà vẫn tốn 40ms mỗi cái.
                var lo = hang.Lay(c.SoAnhMoiLuot);
                if (lo.Count == 0) break;

                var kq = await bo.MotLuotAsync(lo.Select(x => x.id).ToList(),
                    c.NhipAnhMs, c.LangAnhMs, ct);
                thongKe.Them(kq.SoNhan, kq.SoRong);

                // Id máy chủ trả lời thì xong hẳn. Phần im lặng quay lại cuối hàng; chỉ id nào
                // hỏi lúc máy chủ CÒN ĐANG trả lời mới bị tính một lần thử hỏng - id hỏi sau
                // khi nó đã im thì coi như chưa thử, không thì cả khúc đuôi lô bị loại oan.
                var daThu = new HashSet<int>(kq.ChuaRoDaThu);
                var chuaThu = new HashSet<int>(kq.ChuaRoChuaThu);
                var boLuotNay = 0;

                foreach (var (id, soLanHoi) in lo)
                {
                    if (chuaThu.Contains(id))
                    {
                        hang.Tra(id, soLanHoi);
                    }
                    else if (daThu.Contains(id))
                    {
                        if (soLanHoi + 1 >= c.SoLanHoiLaiAnh)
                        {
                            boLuotNay++;
                            thongKe.ThemKhongCo(id);
                        }
                        else
                        {
                            hang.Tra(id, soLanHoi + 1);
                        }
                    }
                }

                var traLoi = lo.Count - daThu.Count - chuaThu.Count;
                Console.WriteLine($"  [{ten}] lượt {luot}: hỏi {lo.Count}, trả lời {traLoi} " +
                                  $"(ảnh {kq.SoNhan}, rỗng {kq.SoRong}, hỏng {kq.SoLoi}), " +
                                  $"hỏi lại {daThu.Count - boLuotNay + chuaThu.Count}, " +
                                  $"bỏ {boLuotNay}, còn trong hàng {hang.Con}" +
                                  (kq.BiNgatGiuaChung ? " - máy chủ ngừng trả lời" : ""));

                if (hang.Con == 0) break;

                phien.Ngat();
                try
                {
                    await Task.Delay(c.NghiGiuaLuotMs, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"  [{ten}] hỏng: {e.Message}");
        }
        finally
        {
            // Phiên của thợ đầu là của hàm gọi, để nó tự dọn.
            if (coSan == null) phien.Dispose();
        }
    }

    /// <summary>
    /// Xin hình từng mẫu quái (gói <c>11</c>) rồi ghi tấm sprite ra
    /// <c>&lt;ra&gt;/&lt;nhà phát hành&gt;/Mobs/&lt;id&gt;.png</c> kèm bảng khung
    /// <c>MobFrames.json</c>.
    /// </summary>
    private static async Task TaiQuaiAsync(Phien phien, CauHinh c)
    {
        var thuMucQuai = Path.Combine(c.Ra, c.NhaPhatHanh, "Mobs");
        var tatCa = Enumerable.Range(0, phien.Data.arrMobTemplate.Length).ToList();
        if (tatCa.Count == 0) return;

        var daCo = BoQuai.DaCoTrenDia(thuMucQuai);
        var hang = new Queue<(int id, int soLan)>(
            tatCa.Where(id => !daCo.Contains(id)).Select(id => (id, 0)));

        Console.WriteLine($"Tải hình quái: {tatCa.Count} mẫu, đã có sẵn {daCo.Count}, cần hỏi {hang.Count}");
        if (hang.Count == 0) return;

        using var het = new CancellationTokenSource(c.ChoQuaiMs);
        var bo = new BoQuai(phien, thuMucQuai);
        var boCuoc = 0;

        for (var luot = 1; luot <= c.SoLuotAnh && hang.Count > 0; luot++)
        {
            if (het.IsCancellationRequested) break;

            if (!phien.DaNoi && !await phien.DangNhapCoThuLaiAsync(c.Host, c.Port, c.TaiKhoan,
                    c.MatKhau, c.ChoDangNhapMs, c.SoLanDangNhap, c.NghiGiuaLuotMs, het.Token))
            {
                Console.WriteLine("  quái: nối lại không được, dừng.");
                break;
            }

            // Đăng nhập xong CHƯA phải là đã vào map. Đo thực tế: lượt đầu (còn nguyên phiên
            // đã vào map từ lúc lấy dữ liệu) trả lời 37/40, lượt sau vừa nối lại đã hỏi ngay
            // thì trả lời 0/40 - máy chủ chỉ phục vụ gói này khi nhân vật đứng trong map.
            if (!await phien.ChoVaoMapAsync(c.ChoPartMs, het.Token))
            {
                Console.WriteLine("  quái: chưa vào được map, dừng.");
                break;
            }

            var lo = new List<(int id, int soLan)>();
            while (lo.Count < c.SoQuaiMoiLuot && hang.Count > 0) lo.Add(hang.Dequeue());

            var chuaRo = new HashSet<int>(
                await bo.MotLuotAsync(lo.Select(x => x.id).ToList(), c.NhipQuaiMs, c.LangAnhMs, het.Token));

            var boLuotNay = 0;
            foreach (var (id, soLan) in lo)
            {
                if (!chuaRo.Contains(id)) continue;
                if (soLan + 1 >= c.SoLanHoiLaiAnh)
                {
                    boLuotNay++;
                    boCuoc++;
                }
                else
                {
                    hang.Enqueue((id, soLan + 1));
                }
            }

            bo.GhiBang();
            Console.WriteLine($"  lượt {luot}: hỏi {lo.Count}, trả lời {lo.Count - chuaRo.Count}, " +
                              $"hỏi lại {chuaRo.Count - boLuotNay}, bỏ {boLuotNay}, còn {hang.Count}");

            if (hang.Count == 0) break;

            phien.Ngat();
            try
            {
                await Task.Delay(c.NghiGiuaLuotMs, het.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        var co = BoQuai.DaCoTrenDia(thuMucQuai).Count;
        Console.WriteLine($"  quái: {co}/{tatCa.Count} tấm sprite (bỏ {boCuoc}, còn trong hàng " +
                          $"{hang.Count}) → {Path.GetFullPath(thuMucQuai)}");
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
