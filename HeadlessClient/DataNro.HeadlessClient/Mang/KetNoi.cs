using System.Net.Sockets;
using System.Threading.Channels;

namespace DataNro.Mang;

/// <summary>Nơi nhận gói tin đã giải mã.</summary>
public interface IBoDoc
{
    void KhiNhanGoi(Message msg);
    void KhiNoiXong();
    void KhiDut();
}

/// <summary>
/// Tầng vận chuyển: mở socket (thẳng hoặc qua proxy), đóng gói theo khung của máy chủ Ngọc
/// Rồng, mã hoá bằng khoá máy chủ phát lúc bắt tay.
///
/// <para>
/// Bản này cố tình <b>chỉ lo một kết nối một lần</b>: không tự nối lại, không theo dõi socket
/// nửa sống, không tạm dừng hàng đợi. Công cụ đổ dữ liệu chạy một lượt rồi thoát, nên những
/// thứ đó chỉ làm mã dài thêm mà chẳng dùng tới - cần nối lại thì gọi lại từ ngoài.
/// </para>
/// </summary>
public sealed class KetNoi : IDisposable
{
    /// <summary>
    /// Mấy mã lệnh này gửi độ dài bằng <b>ba</b> byte little-endian thay vì hai byte
    /// big-endian như phần còn lại - gói ảnh và bảng vật phẩm to hơn 64KB.
    /// </summary>
    private static readonly HashSet<sbyte> BaByteDoDai =
        new() { -32, -66, 11, -67, -74, -87, 66, 12 };

    private readonly SemaphoreSlim khoaGhi = new(1, 1);
    private readonly List<Message> hangNhan = new();

    private TcpClient may;
    private NetworkStream luong;
    private Channel<Message> hangGui;
    private CancellationTokenSource huy;

    private sbyte[] khoa;
    private sbyte viTriDoc, viTriGhi;
    private bool daCoKhoa;

    public ProxyInfo Proxy { get; set; }
    public int HetGioNoiMs { get; set; } = 15000;
    public Action<string> Log { get; set; }
    public IBoDoc BoDoc { get; set; }

    public bool DaNoi { get; private set; }

    // ==================== mở / đóng ====================

    public async Task<bool> NoiAsync(string host, int port, CancellationToken ct)
    {
        Dong();

        // Tuổi thọ socket KHÔNG buộc vào ct: ct là hạn cho việc "nối và đăng nhập", còn kết
        // nối thì phải sống tiếp cho tới khi gọi Dong(). Buộc chung thì hạn chờ dữ liệu hết
        // giờ là socket chết theo, ngay giữa lượt tải ảnh sau đó.
        huy = new CancellationTokenSource();
        var token = huy.Token;
        may = new TcpClient();

        // Riêng thao tác nối thì có quyền bỏ cuộc theo ct.
        using var hetGio = CancellationTokenSource.CreateLinkedTokenSource(token, ct);
        hetGio.CancelAfter(HetGioNoiMs);

        try
        {
            if (Proxy is { Type: not ProxyType.None })
            {
                Log?.Invoke("Nối qua proxy " + Proxy);
                await ProxyHandshake.ConnectThroughAsync(may, Proxy, host, port, hetGio.Token);
            }
            else
            {
                await may.ConnectAsync(host, port, hetGio.Token);
            }
        }
        catch (Exception e)
        {
            Log?.Invoke("Không nối được tới máy chủ: " + e.Message);
            Dong();
            return false;
        }

        luong = may.GetStream();
        hangGui = Channel.CreateUnbounded<Message>(new UnboundedChannelOptions { SingleReader = true });
        khoa = null;
        daCoKhoa = false;
        viTriDoc = viTriGhi = 0;
        DaNoi = true;

        _ = Task.Run(() => VongGuiAsync(token), token);
        _ = Task.Run(() => VongNhanAsync(token), token);

        // Gói đầu tiên xin khoá mã hoá; gửi khi chưa có khoá nên đi ở dạng thô.
        await GuiRaAsync(new Message((sbyte)-27), token);
        BoDoc?.KhiNoiXong();
        return true;
    }

    public void Dong()
    {
        DaNoi = false;
        try { huy?.Cancel(); } catch (Exception) { }
        try { hangGui?.Writer.TryComplete(); } catch (Exception) { }
        try { luong?.Close(); } catch (Exception) { }
        try { may?.Close(); } catch (Exception) { }
        luong = null;
        may = null;
        khoa = null;
        daCoKhoa = false;
        lock (hangNhan) hangNhan.Clear();
    }

    public void Dispose() => Dong();

    public void Gui(Message msg) => hangGui?.Writer.TryWrite(msg);

    /// <summary>
    /// Đẩy các gói đã nhận lên tầng trên. Gọi đều đặn từ luồng chính - cố ý không gọi thẳng
    /// từ luồng đọc socket để bộ đọc không phải lo chuyện đồng bộ.
    /// </summary>
    public void Bom()
    {
        if (BoDoc == null) return;
        while (true)
        {
            Message m;
            lock (hangNhan)
            {
                if (hangNhan.Count == 0) return;
                m = hangNhan[0];
                hangNhan.RemoveAt(0);
            }

            try
            {
                BoDoc.KhiNhanGoi(m);
            }
            catch (Exception e)
            {
                Log?.Invoke($"Lỗi xử lý gói cmd={m.command}: {e.Message}");
            }
        }
    }

    // ==================== gửi ====================

    private async Task VongGuiAsync(CancellationToken ct)
    {
        try
        {
            while (DaNoi && !ct.IsCancellationRequested)
            {
                // Chưa có khoá thì chưa gửi được gì ngoài gói xin khoá.
                if (!daCoKhoa)
                {
                    await Task.Delay(5, ct);
                    continue;
                }

                var m = await hangGui.Reader.ReadAsync(ct);
                await GuiRaAsync(m, ct);
            }
        }
        catch (Exception)
        {
            // huỷ hoặc đứt - vòng nhận lo báo lên trên
        }
    }

    private async Task GuiRaAsync(Message m, CancellationToken ct)
    {
        var st = luong;
        if (st == null) return;

        var data = m.getData();
        var len = data?.Length ?? 0;

        await khoaGhi.WaitAsync(ct);
        try
        {
            var dau = new byte[3];
            dau[0] = daCoKhoa ? (byte)MaGhi(m.command) : (byte)m.command;
            dau[1] = daCoKhoa ? (byte)MaGhi((sbyte)(len >> 8)) : (byte)(len >> 8);
            dau[2] = daCoKhoa ? (byte)MaGhi((sbyte)(len & 0xFF)) : (byte)(len & 0xFF);
            await st.WriteAsync(dau, 0, 3, ct);

            if (len > 0)
            {
                var than = new byte[len];
                for (var i = 0; i < len; i++)
                    than[i] = daCoKhoa ? (byte)MaGhi(data[i]) : (byte)data[i];
                await st.WriteAsync(than, 0, len, ct);
            }

            await st.FlushAsync(ct);
        }
        catch (Exception e)
        {
            Log?.Invoke($"Gửi gói cmd={m.command} hỏng: {e.Message}");
            Dong();
        }
        finally
        {
            khoaGhi.Release();
        }
    }

    // ==================== nhận ====================

    private async Task VongNhanAsync(CancellationToken ct)
    {
        try
        {
            while (DaNoi && !ct.IsCancellationRequested)
            {
                var msg = await DocGoiAsync(ct);
                if (msg == null) break;

                // Gói khoá do tầng này nuốt, tầng trên không cần biết.
                if (msg.command == -27) LayKhoa(msg);
                else lock (hangNhan) hangNhan.Add(msg);
            }
        }
        catch (Exception)
        {
            // đứt hoặc bị huỷ
        }

        if (!DaNoi) return;
        Dong();
        BoDoc?.KhiDut();
    }

    private async Task<Message> DocGoiAsync(CancellationToken ct)
    {
        var st = luong;
        if (st == null) return null;

        try
        {
            var thoCmd = (sbyte)await DocByteAsync(st, ct);
            var cmd = daCoKhoa ? MaDoc(thoCmd) : thoCmd;

            int coLon;
            if (BaByteDoDai.Contains(cmd))
            {
                var b0 = await DocByteGiaiMaAsync(st, ct) + 128;
                var b1 = await DocByteGiaiMaAsync(st, ct) + 128;
                var b2 = await DocByteGiaiMaAsync(st, ct) + 128;
                coLon = (b2 * 256 + b1) * 256 + b0;
            }
            else
            {
                var h = (sbyte)await DocByteAsync(st, ct);
                var l = (sbyte)await DocByteAsync(st, ct);
                if (daCoKhoa) coLon = ((MaDoc(h) & 0xFF) << 8) | (MaDoc(l) & 0xFF);
                else coLon = ((h & 0xFF) << 8) | (l & 0xFF);
            }

            var dem = new byte[coLon];
            var da = 0;
            while (da < coLon)
            {
                var n = await st.ReadAsync(dem.AsMemory(da, coLon - da), ct);
                if (n <= 0) return null;
                da += n;
            }

            var than = new sbyte[coLon];
            for (var i = 0; i < coLon; i++) than[i] = (sbyte)dem[i];
            if (daCoKhoa)
                for (var i = 0; i < coLon; i++) than[i] = MaDoc(than[i]);

            return new Message(cmd, than);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private async Task<sbyte> DocByteGiaiMaAsync(NetworkStream st, CancellationToken ct)
    {
        var b = (sbyte)await DocByteAsync(st, ct);
        return daCoKhoa ? MaDoc(b) : b;
    }

    private static async Task<int> DocByteAsync(NetworkStream st, CancellationToken ct)
    {
        var mot = new byte[1];
        var n = await st.ReadAsync(mot.AsMemory(0, 1), ct);
        if (n <= 0) throw new IOException("socket đóng");
        return mot[0];
    }

    // ==================== khoá ====================

    /// <summary>
    /// Máy chủ gửi độ dài khoá rồi bấy nhiêu byte; mỗi byte sau XOR với byte trước để ra
    /// khoá thật. Đọc sai một byte ở đây là mọi gói sau đều thành rác.
    /// </summary>
    private void LayKhoa(Message msg)
    {
        try
        {
            var r = msg.reader();
            var n = r.readSByte();
            khoa = new sbyte[n];
            for (var i = 0; i < n; i++) khoa[i] = r.readSByte();
            for (var i = 0; i < khoa.Length - 1; i++) khoa[i + 1] ^= khoa[i];
            daCoKhoa = true;
        }
        catch (Exception e)
        {
            Log?.Invoke("Đọc khoá hỏng: " + e.Message);
        }
    }

    /// <summary>Hai chiều đọc và ghi có con trỏ khoá <b>riêng</b>, không dùng chung.</summary>
    private sbyte MaDoc(sbyte b)
    {
        var i = viTriDoc++;
        var ra = (sbyte)((khoa[i] & 0xFF) ^ (b & 0xFF));
        if (viTriDoc >= khoa.Length) viTriDoc %= (sbyte)khoa.Length;
        return ra;
    }

    private sbyte MaGhi(sbyte b)
    {
        var i = viTriGhi++;
        var ra = (sbyte)((khoa[i] & 0xFF) ^ (b & 0xFF));
        if (viTriGhi >= khoa.Length) viTriGhi %= (sbyte)khoa.Length;
        return ra;
    }
}
