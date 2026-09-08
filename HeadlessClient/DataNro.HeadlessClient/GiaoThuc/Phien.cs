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

        if (!await noi.NoiAsync(host, port, ct)) return false;
        BatNhip();

        var han = Environment.TickCount64 + hetGioMs;
        while (Environment.TickCount64 < han && !ct.IsCancellationRequested)
        {
            if (Data.DaDayDu) return true;

            if (!noi.DaNoi)
            {
                Log("Đứt kết nối trước khi lấy đủ dữ liệu.");
                return false;
            }

            if (Doc.LoiDangNhap != null)
            {
                Log("Máy chủ từ chối: " + Doc.LoiDangNhap);
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

        return Data.DaDayDu;
    }

    public void Ngat()
    {
        try { nhipCts?.Cancel(); } catch (Exception) { }
        noi.Dong();
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
