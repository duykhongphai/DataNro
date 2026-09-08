using System.Diagnostics;
using System.IO;
using System.Text.Json;
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
    /// Id đã hỏi trong lúc máy chủ <b>vẫn còn đang trả lời</b> mà nó không trả lời - lần này
    /// coi như đã thử thật. Hỏi đủ mấy lần thế này mà vẫn im thì mới kết luận không có ảnh.
    /// </summary>
    public List<int> ChuaRoDaThu = new();

    /// <summary>
    /// Id hỏi <b>sau</b> khi máy chủ đã im - coi như chưa thử lần nào, hỏi lại mà không tính
    /// một lần thất bại.
    ///
    /// <para>
    /// Thiếu chỗ phân biệt này là hỏng: lô 150 mà máy chủ chỉ trả khoảng 100 thì vị trí
    /// 100-150 của lô nào cũng chết. Id bị trả về dồn vào cuối hàng, gặp nhau, rồi lại rơi
    /// đúng khúc đuôi chết ấy - đủ ba lần là bị loại oan. Đo thực tế: 93 id có ảnh vẫn bị
    /// loại, và chúng nằm thành dải liền nhau (224-227, 265-272, 389-400) đúng như một khúc
    /// đuôi lô bị bỏ.
    /// </para>
    /// </summary>
    public List<int> ChuaRoChuaThu = new();

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

    /// <summary>Id đã ghi được ảnh trong lượt này.</summary>
    private readonly HashSet<int> daNhan = new();

    /// <summary>
    /// Id máy chủ đã <b>trả lời</b>, kể cả trả lời "không có ảnh".
    ///
    /// <para>
    /// Đây mới là căn cứ để nói một id đã xong. Trước đây suy theo mốc thời gian - "id nào
    /// hỏi trước lần trả lời cuối cùng thì coi như đã có kết luận" - và nó SAI nặng: ta hỏi
    /// mỗi 40ms còn trả lời thì về trễ, nên tới lúc gói cuối rơi xuống ta đã hỏi thêm hai ba
    /// trăm id nữa. Cả đám đó bị đánh dấu xong dù máy chủ chưa hề trả lời. Đo trên Vũ trụ 1:
    /// sáu lượt ra 597 ảnh rồi dừng, trong khi 1298 id bị vứt oan.
    /// </para>
    /// </summary>
    private readonly HashSet<int> daTraLoi = new();

    private int soRong;
    private int soLoi;

    /// <summary>Mốc lần cuối nhận được gói ảnh, để đo khoảng lặng.</summary>
    private long nhipCuoi;

    /// <summary>Đã gửi bao nhiêu lời hỏi tại thời điểm nhận gói gần nhất.</summary>
    private int daGuiLucNhanCuoi;

    /// <summary>Số lời hỏi đã gửi trong lượt này.</summary>
    private int daGui;

    public BoAnh(Phien phien, string thuMucRa)
    {
        this.phien = phien;
        thuMuc = thuMucRa;
    }

    /// <summary>
    /// Ảnh chia vào thư mục con theo <c>id / 1000</c>: <c>Icons/0/3.png</c>,
    /// <c>Icons/17/17529.png</c>.
    ///
    /// <para>
    /// Để phẳng một thư mục thì GitHub cắt danh sách ở 1000 tệp - tệp vẫn còn đủ và raw URL
    /// vẫn chạy, chỉ là mở trên web thì thấy thiếu. Chia theo nghìn ra mười tám thư mục, đông
    /// nhất hơn ba trăm tệp, dư chỗ kể cả khi game tăng gấp ba.
    /// </para>
    /// </summary>
    public static string DuongDanAnh(string thuMucRa, int id, string duoi) =>
        Path.Combine(thuMucRa, (id / 1000).ToString(), id + duoi);

    /// <summary>
    /// Dồn ảnh đang nằm phẳng ngay trong <c>Icons/</c> vào đúng thư mục con của nó. Chạy một
    /// lần lúc đổi bố cục; sau đó không còn gì để dọn nên gọi bao nhiêu lần cũng vô hại.
    /// </summary>
    public static int DonVaoThuMucCon(string thuMucRa)
    {
        if (!Directory.Exists(thuMucRa)) return 0;

        var n = 0;
        foreach (var f in Directory.EnumerateFiles(thuMucRa))
        {
            if (!int.TryParse(Path.GetFileNameWithoutExtension(f), out var id)) continue;

            var dich = DuongDanAnh(thuMucRa, id, Path.GetExtension(f));
            Directory.CreateDirectory(Path.GetDirectoryName(dich)!);
            File.Move(f, dich, true);
            n++;
        }

        return n;
    }

    /// <summary>Id đã có sẵn ngoài đĩa, để lượt sau không hỏi lại.</summary>
    public static HashSet<int> DaCoTrenDia(string thuMucRa)
    {
        var co = new HashSet<int>();
        if (!Directory.Exists(thuMucRa)) return co;

        // Quét cả cây: bản cũ để phẳng, bản mới chia thư mục con - đọc được cả hai thì đổi
        // bố cục không làm mất công tải lại từ đầu.
        foreach (var f in Directory.EnumerateFiles(thuMucRa, "*", SearchOption.AllDirectories))
            if (int.TryParse(Path.GetFileNameWithoutExtension(f), out var id))
                co.Add(id);

        return co;
    }

    /// <summary>
    /// Mọi id ảnh đáng hỏi, đã lọc trùng và xếp tăng dần. Gói <c>-67</c> chỉ nhận đúng một
    /// con số - phía máy chủ ảnh vật phẩm, ảnh kĩ năng và ảnh mảnh dựng hình nằm chung một
    /// bảng, nên gộp cả ba nguồn vào một kho.
    ///
    /// <para>
    /// Chỗ dễ sai nhất: <c>headId/bodyId/legId</c> của NPC <b>không phải id ảnh</b> mà là chỉ
    /// số vào bảng part; id ảnh nằm trong từng khung của part đó. Trước đây lấy thẳng ba số
    /// ấy làm id ảnh nên tải về toàn ảnh của người khác, còn ảnh thật của NPC thì không có -
    /// Ông Gôhan cần ảnh 250/251/252 mà ta lại đi tải 18/19/20.
    /// </para>
    ///
    /// <para>
    /// Chỉ gom part mà NPC dùng tới, không gom cả bảng: cả bảng có 13969 ảnh (đủ mọi bộ đồ,
    /// mọi kiểu tóc của người chơi) - hơn một trăm lượt đăng nhập lại, mà trang không dùng.
    /// Riêng part của NPC chỉ thêm bảy trăm ảnh.
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
            ThemAnhCuaPart(d, npc.headId, set);
            ThemAnhCuaPart(d, npc.bodyId, set);
            ThemAnhCuaPart(d, npc.legId, set);
        }

        // Hai ảnh client gọi thẳng bằng số, không qua bảng part nào: rương đồ (NPC 3) và biển
        // báo khu (NPC 6). Xem Npc.paint - hai id này nằm cứng trong mã client.
        set.Add(265);
        set.Add(545);

        return set.ToList();
    }

    /// <summary>
    /// Danh sách id máy chủ đã xác nhận là không có ảnh, để lượt sau khỏi hỏi lại.
    ///
    /// <para>
    /// Quét mù cả chục nghìn id thì phần lớn là id trống; không nhớ lại thì lượt sau vẫn phải
    /// hỏi hết từ đầu, mà mỗi lượt chỉ hỏi được chừng trăm cái.
    /// </para>
    /// </summary>
    public static HashSet<int> DocKhongCo(string thuMucRa)
    {
        var duong = Path.Combine(thuMucRa, "KhongCo.json");
        if (!File.Exists(duong)) return new HashSet<int>();

        try
        {
            return JsonSerializer.Deserialize<HashSet<int>>(File.ReadAllText(duong))
                   ?? new HashSet<int>();
        }
        catch (Exception)
        {
            return new HashSet<int>();
        }
    }

    /// <summary>Gộp thêm id mới vào danh sách "không có" rồi ghi lại.</summary>
    public static void GhiKhongCo(string thuMucRa, IEnumerable<int> them)
    {
        var gop = DocKhongCo(thuMucRa);
        var truoc = gop.Count;
        foreach (var id in them) gop.Add(id);
        if (gop.Count == truoc && truoc > 0) return;

        Directory.CreateDirectory(thuMucRa);
        File.WriteAllText(Path.Combine(thuMucRa, "KhongCo.json"),
            JsonSerializer.Serialize(gop.OrderBy(x => x)) + "\n");
    }

    /// <summary>
    /// Ghi bảng kích thước của mọi ảnh đang có ngoài đĩa: <c>{"3":[80,28], ...}</c>.
    ///
    /// <para>
    /// Trang web cần con số này để tính hộp bao của hình NPC <b>trước khi</b> ảnh tải xong -
    /// CSS không có cách nào hỏi kích thước gốc của một tấm ảnh, mà đợi ba mảnh tải xong rồi
    /// mới bố trí thì cả lưới nhấp nháy. Chỉ đọc 64 byte đầu mỗi tệp nên rẻ.
    /// </para>
    /// </summary>
    public static void GhiKichThuoc(string thuMucRa)
    {
        if (!Directory.Exists(thuMucRa)) return;

        var bang = new SortedDictionary<int, int[]>();
        var dau = new byte[64];

        foreach (var f in Directory.EnumerateFiles(thuMucRa, "*.png", SearchOption.AllDirectories))
        {
            if (!int.TryParse(Path.GetFileNameWithoutExtension(f), out var id)) continue;

            try
            {
                using var s = File.OpenRead(f);
                if (s.Read(dau, 0, dau.Length) < dau.Length) continue;
                if (AnhPng.KichThuoc(dau, out var rong, out var cao)) bang[id] = new[] { rong, cao };
            }
            catch (Exception)
            {
                // tệp hỏng thì bỏ qua, thiếu một dòng trong bảng không chết ai
            }
        }

        File.WriteAllText(Path.Combine(thuMucRa, "Sizes.json"),
            JsonSerializer.Serialize(bang, new JsonSerializerOptions { WriteIndented = false }) + "\n");
    }

    /// <summary>Mọi id ảnh trong một part. Chưa có bảng part thì không thêm gì.</summary>
    private static void ThemAnhCuaPart(GameData d, int chiSo, SortedSet<int> set)
    {
        if (chiSo < 0 || chiSo >= d.parts.Length) return;
        var p = d.parts[chiSo];
        if (p?.pi == null) return;

        foreach (var k in p.pi)
            if (k.id > 0) set.Add(k.id);
    }

    /// <summary>
    /// Hỏi lần lượt từng id trong <paramref name="ids"/> cho tới khi hết, hoặc tới khi máy
    /// chủ im lặng quá <paramref name="langMs"/>.
    /// </summary>
    public async Task<KetQuaLuot> MotLuotAsync(IReadOnlyList<int> ids, int nhipMs, int langMs,
        CancellationToken ct)
    {
        Directory.CreateDirectory(thuMuc);

        lock (khoa)
        {
            daNhan.Clear();
            daTraLoi.Clear();
        }

        soRong = 0;
        soLoi = 0;
        daGui = 0;
        daGuiLucNhanCuoi = 0;

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
                Interlocked.Exchange(ref daGui, i + 1);

                // Máy chủ đã ngừng trả lời hẳn thì hỏi tiếp chỉ tốn thời gian. Chỉ tính từ
                // lúc đã nhận được ít nhất một trả lời, không thì lượt đầu chưa kịp về đã bỏ.
                if (SoDaTraLoi > 0 && dongHo.ElapsedMilliseconds - Interlocked.Read(ref nhipCuoi) > langMs)
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

        // Chỉ id nào máy chủ ĐÃ TRẢ LỜI mới coi là xong. Phần còn lại chia làm hai: hỏi lúc
        // máy chủ còn đang trả lời thì tính là đã thử thật, hỏi sau khi nó im thì không.
        //
        // Trường hợp cả lượt KHÔNG có lấy một trả lời: mốc vẫn là 0 nên mọi id sẽ rơi vào
        // "chưa thử" và không id nào bị loại - vòng lặp quay mãi không dứt. Phiên vừa đăng
        // nhập tươi mà hỏi cả lô vẫn im thì đó là câu trả lời rồi: tính cả lô là đã thử.
        var moc = SoDaTraLoi == 0 ? ids.Count : Volatile.Read(ref daGuiLucNhanCuoi);
        lock (khoa)
            for (var i = 0; i < ids.Count; i++)
            {
                if (daTraLoi.Contains(ids[i])) continue;
                (i < moc ? kq.ChuaRoDaThu : kq.ChuaRoChuaThu).Add(ids[i]);
            }

        kq.SoNhan = SoDaNhan;
        kq.SoRong = soRong;
        kq.SoLoi = soLoi;
        return kq;

        void Nhan(Message msg)
        {
            Interlocked.Exchange(ref nhipCuoi, dongHo.ElapsedMilliseconds);
            Volatile.Write(ref daGuiLucNhanCuoi, Volatile.Read(ref daGui));

            try
            {
                var r = msg.freshReader();
                if (r == null) return;

                var id = r.readInt();
                var n = r.readInt();
                lock (khoa) daTraLoi.Add(id);

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

                var duong = DuongDanAnh(thuMuc, id, DuoiTep(bytes));
                Directory.CreateDirectory(Path.GetDirectoryName(duong)!);
                File.WriteAllBytes(duong, bytes);
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

    private int SoDaTraLoi
    {
        get
        {
            lock (khoa) return daTraLoi.Count;
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
