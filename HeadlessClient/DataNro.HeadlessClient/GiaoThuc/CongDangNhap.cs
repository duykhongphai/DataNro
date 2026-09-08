namespace DataNro.GiaoThuc;

/// <summary>
/// Cổng giãn cách đăng nhập, dùng chung cho mọi phiên trong tiến trình.
///
/// <para>
/// Máy chủ bắt chờ sau khi <b>đăng xuất</b>: thoát ra rồi thì phải đợi chừng nửa phút mới được
/// vào lại, và nó tính theo <b>địa chỉ</b> chứ không theo tài khoản - acc 1 vừa thoát thì acc 2
/// cũng phải đợi. Mỗi thợ tự nghỉ không cứu được vì chúng nghỉ song song rồi ùa vào cùng lúc:
/// đo thực tế bốn tài khoản khác nhau cùng vào lại thì cả bốn đều "hết giờ chờ máy chủ nhận
/// đăng nhập", phải thử lại lần hai mới lọt.
/// </para>
/// </summary>
public static class CongDangNhap
{
    /// <summary>Mốc lần đăng xuất gần nhất của bất kì phiên nào.</summary>
    private static long lanNgat;

    /// <summary>Phải đợi ngần này kể từ lần đăng xuất gần nhất mới được đăng nhập. 0 là tắt.</summary>
    public static int CachMs { get; set; } = 30000;

    /// <summary>Đánh dấu vừa có một phiên rời máy chủ.</summary>
    public static void GhiNgat() => Volatile.Write(ref lanNgat, Environment.TickCount64);

    /// <summary>
    /// Chờ cho đủ giãn cách rồi mới cho đăng nhập.
    ///
    /// <para>
    /// Lặp lại chứ không tính một lần: trong lúc thợ này đang đợi thì thợ khác có thể vừa
    /// thoát ra, đẩy mốc lùi lại - phải đo lại từ mốc mới.
    /// </para>
    /// </summary>
    public static async Task ChoLuotAsync(Action<string> log, CancellationToken ct)
    {
        if (CachMs <= 0) return;

        var daBao = false;
        while (!ct.IsCancellationRequested)
        {
            var moc = Volatile.Read(ref lanNgat);
            if (moc == 0) return;

            var con = CachMs - (int)(Environment.TickCount64 - moc);
            if (con <= 0) return;

            if (!daBao)
            {
                log?.Invoke($"Đợi {con / 1000.0:0.#}s kể từ lần đăng xuất gần nhất.");
                daBao = true;
            }

            await Task.Delay(con, ct).ConfigureAwait(false);
        }
    }
}
