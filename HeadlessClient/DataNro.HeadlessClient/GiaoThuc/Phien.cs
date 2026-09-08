using DataNro.Mang;

namespace DataNro.GiaoThuc;

/// <summary>
/// Một phiên làm việc với máy chủ: nối, đăng nhập, chờ đủ bảng mẫu.
///
/// <para>
/// Cố ý dừng ở đó, <b>không vào map</b>. Máy chủ gửi cả bốn bảng data / map / skill / item
/// ngay trong lúc bắt tay đăng nhập, trước cả bước chọn nhân vật - nên tới đây là đã có mọi
/// thứ cần xuất, mà nhân vật thì chưa từng bước chân vào game.
/// </para>
/// </summary>
public sealed class Phien : IDisposable
{
    private readonly KetNoi noi = new();
    private CancellationTokenSource nhipCts;
    private Task nhipTask;

    public Phien(Action<string> ghiLog = null)
    {
        Log = ghiLog ?? (_ => { });
        noi.Log = m => Log(m);
        Doc = new BoDocGoi(noi, Log);
    }

    public Action<string> Log { get; }
    public BoDocGoi Doc { get; }
    public GameData Data => Doc.Data;
    public bool DaNoi => noi.DaNoi;

    public ProxyInfo Proxy
    {
        get => noi.Proxy;
        set => noi.Proxy = value;
    }

    /// <summary>Nối rồi đăng nhập. Trả về true khi bảng mẫu đã về đủ.</summary>
    public async Task<bool> DangNhapAsync(string host, int port, string taiKhoan, string matKhau,
        int hetGioMs, CancellationToken ct)
    {
        Doc.TaiKhoan = taiKhoan;
        Doc.MatKhau = matKhau;
        Doc.DatLai();

        // Xếp hàng trước khi nối: máy chủ tính nhịp đăng nhập theo địa chỉ, mà mấy thợ chạy
        // song song thì nghỉ xong là ùa vào cùng lúc.
        await CongDangNhap.ChoLuotAsync(Log, ct).ConfigureAwait(false);

        if (!await noi.NoiAsync(host, port, ct)) return false;
        BatNhip();

        var han = Environment.TickCount64 + hetGioMs;
        while (Environment.TickCount64 < han && !ct.IsCancellationRequested)
        {
            // Mốc là DaSanSang của PHIÊN NÀY, không phải Data.DaDayDu: bảng dữ liệu còn
            // nguyên từ lần nối trước nên DaDayDu luôn đúng, dùng nó thì lần nối lại nào
            // cũng trả về "xong" ngay tức khắc dù máy chủ chưa hề nhận.
            if (Doc.DaSanSang && Data.DaDayDu) return true;

            if (!noi.DaNoi)
            {
                Log("Đứt kết nối trước khi lấy đủ dữ liệu.");
                return false;
            }

            if (Doc.LoiDangNhap != null)
            {
                // Không phân biệt "sai mật khẩu" với "vui lòng chờ một lát nữa" bằng cách dò
                // chữ - mỗi máy chủ một kiểu. Cứ coi là lần thử này hỏng rồi để bên ngoài thử
                // lại: hàng chờ thì lần sau vào được, sai mật khẩu thì hỏng nốt mấy lần rồi
                // dừng, mất chừng một phút chứ không sai kết quả.
                Log("Máy chủ chưa cho vào: " + Doc.LoiDangNhap);
                return false;
            }

            try
            {
                await Task.Delay(100, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        if (!Doc.DaSanSang) Log("Hết giờ chờ máy chủ nhận đăng nhập.");
        return Doc.DaSanSang && Data.DaDayDu;
    }

    /// <summary>
    /// Đăng nhập, hỏng thì ngắt hẳn rồi thử lại. Mỗi lần thử có hạn giờ riêng và <b>ngắn</b>:
    /// máy chủ lag hoặc đẩy ta vào hàng chờ thì ngồi đợi ba phút cũng vô ích, cắt ra vào lại
    /// nhanh hơn nhiều. Chờ giữa các lần dài dần để khỏi bị coi là đăng nhập dồn dập.
    /// </summary>
    public async Task<bool> DangNhapCoThuLaiAsync(string host, int port, string taiKhoan,
        string matKhau, int hetGioMoiLanMs, int soLan, int nghiMs, CancellationToken ct)
    {
        for (var lan = 1; lan <= soLan && !ct.IsCancellationRequested; lan++)
        {
            if (lan > 1) Log($"Đăng nhập lại (lần {lan}/{soLan})...");
            if (await DangNhapAsync(host, port, taiKhoan, matKhau, hetGioMoiLanMs, ct)) return true;

            Ngat();
            if (lan == soLan) break;

            try
            {
                await Task.Delay(nghiMs * lan, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Chờ tới khi nhân vật thật sự đứng trong map. Đăng nhập xong chưa có nghĩa là đã vào:
    /// còn phải chọn (hoặc tạo) nhân vật, đợi máy chủ đẩy thông tin map rồi mới báo sẵn sàng.
    /// Gói xin hình quái mà gửi trước lúc đó thì máy chủ lặng thinh.
    /// </summary>
    public async Task<bool> ChoVaoMapAsync(int hetGioMs, CancellationToken ct)
    {
        var han = Environment.TickCount64 + hetGioMs;
        while (!Doc.DaVaoMap && Environment.TickCount64 < han && DaNoi && !ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(200, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        return Doc.DaVaoMap;
    }

    public void Ngat()
    {
        try { nhipCts?.Cancel(); } catch (Exception) { }

        // Chỉ tính là một lần đăng xuất khi đang thật sự nối - gọi Ngat() trên phiên đã đóng
        // (Dispose gọi lại chẳng hạn) mà cũng đẩy mốc thì mọi thợ đợi oan thêm một vòng.
        var dangNoi = noi.DaNoi;
        noi.Dong();
        if (dangNoi) CongDangNhap.GhiNgat();
    }

    public void Dispose()
    {
        Ngat();
        noi.Dispose();
    }

    /// <summary>
    /// Vòng nhịp bơm gói từ hàng đợi của tầng mạng lên bộ đọc. Tách ra một luồng riêng để
    /// tầng mạng không phải gọi ngược vào logic ngay trên luồng đọc socket.
    /// </summary>
    private void BatNhip()
    {
        // Đăng nhập lại nhiều lượt: phải dừng vòng nhịp cũ, không thì mỗi lượt đẻ thêm một
        // vòng nữa cùng bơm một hàng đợi.
        try { nhipCts?.Cancel(); } catch (Exception) { }

        nhipCts = new CancellationTokenSource();
        var ct = nhipCts.Token;
        nhipTask = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                noi.Bom();
                try
                {
                    await Task.Delay(20, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }, ct);
    }
}
