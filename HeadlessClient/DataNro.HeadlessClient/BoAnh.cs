using System.Diagnostics;
using System.IO;
using DataNro.GiaoThuc;
using DataNro.Mang;

namespace DataNro;

/// <summary>Kết quả một lượt hỏi ảnh.</summary>
public class KetQuaLuot
{
    /// <summary>Số ảnh ghi được trong lượt này.</summary>
    public int SoNhan;

    /// <summary>Máy chủ trả về độ dài 0 - "có id nhưng không có ảnh".</summary>
    public int SoRong;

    /// <summary>Gói đọc không xuôi.</summary>
    public int SoLoi;

    /// <summary>
    /// Id đã hỏi nhưng <b>chưa biết kết quả</b>: hỏi sau thời điểm máy chủ trả lời lần cuối,
    /// nên không phân biệt được "không có ảnh" với "máy chủ đã ngừng trả lời". Phải hỏi lại
    /// ở lượt sau.
    /// </summary>
    public List<int> ChuaRo = new();

    /// <summary>Máy chủ ngừng trả lời giữa chừng (im lặng quá lâu trong lúc vẫn đang hỏi).</summary>
    public bool BiNgatGiuaChung;
}

/// <summary>
/// Tải ảnh icon từ máy chủ và ghi ra tệp.
///
/// <para>
/// Đường lấy ảnh là gói <c>-67</c>: gửi lên một <c>int</c> id, máy chủ trả về
/// <c>int</c> id + <c>int</c> độ dài + bấy nhiêu byte ảnh. Không có gói nào hỏi được
/// "cho tôi tất cả", nên phải hỏi từng cái một - và máy chủ <b>im lặng</b> với id nó không
/// có, chứ không nói "không tìm thấy".
/// </para>
///
/// <para>
/// Đo thực tế trên Vũ trụ 1: máy chủ trả lời ngoan khoảng hai trăm gói đầu rồi <b>im hẳn</b>
/// cho tới hết phiên, dù kết nối vẫn sống. Là hạn theo số lần hỏi mỗi phiên chứ không phải
/// theo nhịp, nên hạ nhịp không cứu được - phải ngắt ra rồi đăng nhập lại. Lớp này vì thế
/// chỉ lo <i>một lượt</i>: hỏi tới khi máy chủ im quá lâu thì dừng và trả về phần chưa rõ,
/// việc nối lại để <c>Program</c> lo.
/// </para>
/// </summary>
public sealed class BoAnh
{
    /// <summary>Ảnh lớn hơn ngần này chắc chắn là đọc sai gói, bỏ qua.</summary>
    private const int ToiDaByte = 2 * 1024 * 1024;

    private readonly Phien phien;
    private readonly string thuMuc;
    private readonly object khoa = new();
    private readonly HashSet<int> daNhan = new();

    private int soRong;
    private int soLoi;

    /// <summary>Mốc lần cuối nhận được gói ảnh, để đo khoảng lặng.</summary>
    private long nhipCuoi;

    /// <summary>Đã hỏi tới id thứ mấy tại lần nhận gói gần nhất.</summary>
    private int thuTuLucNhan;

    /// <summary>Số id đã hỏi trong lượt này; hàm bắt gói đọc để ghi <see cref="thuTuLucNhan"/>.</summary>
    private int daHoi;

    public BoAnh(Phien phien, string thuMucRa)
    {
        this.phien = phien;
        thuMuc = thuMucRa;
    }

    /// <summary>Id đã có sẵn ngoài đĩa, để lượt sau không hỏi lại.</summary>
    public static HashSet<int> DaCoTrenDia(string thuMucRa)
    {
        var co = new HashSet<int>();
        if (!Directory.Exists(thuMucRa)) return co;

        foreach (var f in Directory.EnumerateFiles(thuMucRa))
            if (int.TryParse(Path.GetFileNameWithoutExtension(f), out var id))
                co.Add(id);

        return co;
    }

    /// <summary>
    /// Mọi id ảnh đáng hỏi, đã lọc trùng và xếp tăng dần.
    ///
    /// <para>
    /// Gộp cả ba nguồn vào một kho vì gói <c>-67</c> chỉ nhận đúng một con số, không phân
    /// biệt "ảnh vật phẩm" hay "ảnh part": phía máy chủ tất cả nằm chung một bảng ảnh nhỏ.
    /// Nhờ vậy hỏi part của NPC là ra luôn hình NPC, không phải dựng lại sprite.
    /// </para>
    /// </summary>
    public static List<int> GomId(GameData d)
    {
        var set = new SortedSet<int>();

        foreach (var t in d.itemTemplates.Values)
            if (t.iconID >= 0) set.Add(t.iconID);

        foreach (var nc in d.nClasss)
        foreach (var st in nc.skillTemplates)
            if (st.iconId > 0) set.Add(st.iconId);

        foreach (var npc in d.arrNpcTemplate)
        {
            if (npc == null) continue;
            if (npc.headId > 0) set.Add(npc.headId);
            if (npc.bodyId > 0) set.Add(npc.bodyId);
            if (npc.legId > 0) set.Add(npc.legId);
        }

        return set.ToList();
    }

    /// <summary>
    /// Hỏi lần lượt từng id trong <paramref name="ids"/> cho tới khi hết, hoặc tới khi máy
    /// chủ im lặng quá <paramref name="langMs"/>.
    /// </summary>
    public async Task<KetQuaLuot> MotLuotAsync(IReadOnlyList<int> ids, int nhipMs, int langMs,
        CancellationToken ct)
    {
        Directory.CreateDirectory(thuMuc);

        lock (khoa) daNhan.Clear();
        soRong = 0;
        soLoi = 0;
        daHoi = 0;
        thuTuLucNhan = 0;

        // Đồng hồ phải chạy trước khi gắn tay bắt gói: hàm Nhan dùng nó ngay khi gói đầu về.
        var dongHo = Stopwatch.StartNew();
        nhipCuoi = dongHo.ElapsedMilliseconds;

        var kq = new KetQuaLuot();
        phien.Doc.NhanAnh += Nhan;

        try
        {
            for (var i = 0; i < ids.Count; i++)
            {
                if (ct.IsCancellationRequested) break;
                if (!phien.DaNoi)
                {
                    kq.BiNgatGiuaChung = true;
                    break;
                }

                phien.Doc.XinAnh(ids[i]);
                Interlocked.Exchange(ref daHoi, i + 1);

                // Máy chủ đã ngừng trả lời hẳn thì hỏi tiếp chỉ tốn thời gian. Chỉ tính từ
                // lúc đã nhận được ít nhất một ảnh, không thì lượt đầu chưa kịp về đã bỏ.
                if (SoDaNhan > 0 && dongHo.ElapsedMilliseconds - Interlocked.Read(ref nhipCuoi) > langMs)
                {
                    kq.BiNgatGiuaChung = true;
                    break;
                }

                try
                {
                    await Task.Delay(nhipMs, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            // Hỏi xong rồi vẫn còn ảnh đang trên đường về: chờ tới khi im lặng đủ lâu.
            while (!ct.IsCancellationRequested && phien.DaNoi)
            {
                if (dongHo.ElapsedMilliseconds - Interlocked.Read(ref nhipCuoi) > langMs) break;

                try
                {
                    await Task.Delay(200, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        finally
        {
            phien.Doc.NhanAnh -= Nhan;
        }

        // Id hỏi TRƯỚC lần trả lời cuối cùng thì coi như đã có kết luận: hoặc ảnh đã về, hoặc
        // máy chủ thật sự không có ảnh đó. Id hỏi sau đó thì không biết được, phải hỏi lại.
        var moc = Volatile.Read(ref thuTuLucNhan);
        for (var i = moc; i < ids.Count; i++) kq.ChuaRo.Add(ids[i]);

        kq.SoNhan = SoDaNhan;
        kq.SoRong = soRong;
        kq.SoLoi = soLoi;
        return kq;

        void Nhan(Message msg)
        {
            Interlocked.Exchange(ref nhipCuoi, dongHo.ElapsedMilliseconds);
            Volatile.Write(ref thuTuLucNhan, Volatile.Read(ref daHoi));

            try
            {
                var r = msg.freshReader();
                if (r == null) return;

                var id = r.readInt();
                var n = r.readInt();

                // Độ dài 0/1 là cách máy chủ nói "có id này nhưng ảnh rỗng".
                if (n <= 1)
                {
                    Interlocked.Increment(ref soRong);
                    return;
                }

                if (n > ToiDaByte || n > r.available())
                {
                    Interlocked.Increment(ref soLoi);
                    return;
                }

                var raw = new sbyte[n];
                r.readFully(ref raw);

                var bytes = new byte[n];
                for (var k = 0; k < n; k++) bytes[k] = unchecked((byte)raw[k]);

                File.WriteAllBytes(Path.Combine(thuMuc, id + DuoiTep(bytes)), bytes);
                lock (khoa) daNhan.Add(id);
            }
            catch (Exception)
            {
                Interlocked.Increment(ref soLoi);
            }
        }
    }

    private int SoDaNhan
    {
        get
        {
            lock (khoa) return daNhan.Count;
        }
    }

    /// <summary>
    /// Đuôi tệp theo mấy byte đầu. Máy chủ trả PNG là chính, nhưng có ảnh cũ còn ở dạng
    /// GIF, đặt sai đuôi thì trình duyệt không chịu hiện.
    /// </summary>
    private static string DuoiTep(byte[] b)
    {
        if (b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return ".png";
        if (b.Length > 3 && b[0] == 0x47 && b[1] == 0x49 && b[2] == 0x46) return ".gif";
        if (b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8) return ".jpg";
        return ".png";
    }
}
