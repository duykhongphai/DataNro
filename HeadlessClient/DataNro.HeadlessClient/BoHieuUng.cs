using System.Diagnostics;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using DataNro.GiaoThuc;

namespace DataNro;

/// <summary>Bảng khung của một hiệu ứng, dạng đem xuất ra JSON.</summary>
public class HieuUngRa
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("width")] public int Width { get; set; }
    [JsonPropertyName("height")] public int Height { get; set; }
    [JsonPropertyName("scale")] public int Scale { get; set; } = 4;
    [JsonPropertyName("sheetW")] public int SheetW { get; set; }
    [JsonPropertyName("sheetH")] public int SheetH { get; set; }
    [JsonPropertyName("rects")] public ORa[] Rects { get; set; }
    [JsonPropertyName("frames")] public ManhRa[][] Frames { get; set; }
    [JsonPropertyName("anim")] public short[] Anim { get; set; }
}

/// <summary>
/// Xin hình từng hiệu ứng (gói <c>-66</c>) rồi ghi tấm sprite ra đĩa.
///
/// <para>
/// Nội dung y hệt hình quái - cũng là tấm sprite kèm bảng ô cắt và bảng khung - nên dùng chung
/// bộ đọc. Khác ở chỗ <b>không có bảng nào liệt kê id hiệu ứng</b>: bảng dữ liệu máy chủ gửi
/// lúc đăng nhập không nhắc tới chúng, client thì hỏi từng cái khi cần vẽ. Nên chỉ còn cách
/// quét mù, im lặng nghĩa là không có.
/// </para>
/// </summary>
public sealed class BoHieuUng
{
    private readonly Phien phien;
    private readonly string thuMuc;
    private readonly object khoa = new();
    private readonly HashSet<int> daTraLoi = new();
    private readonly Dictionary<int, HieuUngRa> bang = new();

    private long nhipCuoi;
    private int daGui;
    private int daGuiLucNhanCuoi;
    private int soLoi;

    private static readonly JsonSerializerOptions Gon = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public BoHieuUng(Phien phien, string thuMucRa)
    {
        this.phien = phien;
        thuMuc = thuMucRa;
    }

    public int SoLoi => soLoi;

    /// <summary>Hiệu ứng đã có tấm sprite ngoài đĩa.</summary>
    public static HashSet<int> DaCoTrenDia(string thuMucRa)
    {
        var co = new HashSet<int>();
        if (!Directory.Exists(thuMucRa)) return co;

        foreach (var f in Directory.EnumerateFiles(thuMucRa, "*.png"))
            if (int.TryParse(Path.GetFileNameWithoutExtension(f), out var id))
                co.Add(id);

        return co;
    }

    /// <summary>Hỏi một lô, trả về những id máy chủ không hề trả lời.</summary>
    public async Task<List<int>> MotLuotAsync(IReadOnlyList<int> ids, int nhipMs, int langMs,
        CancellationToken ct)
    {
        Directory.CreateDirectory(thuMuc);
        DocBangCu();

        lock (khoa) daTraLoi.Clear();
        daGui = 0;
        daGuiLucNhanCuoi = 0;

        var dongHo = Stopwatch.StartNew();
        nhipCuoi = dongHo.ElapsedMilliseconds;
        phien.Doc.NhanHieuUng += Nhan;

        try
        {
            for (var i = 0; i < ids.Count; i++)
            {
                if (ct.IsCancellationRequested || !phien.DaNoi) break;

                phien.Doc.XinHieuUng(ids[i]);
                Interlocked.Exchange(ref daGui, i + 1);

                if (SoDaTraLoi > 0 && dongHo.ElapsedMilliseconds - Interlocked.Read(ref nhipCuoi) > langMs)
                    break;

                try
                {
                    await Task.Delay(nhipMs, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

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
            phien.Doc.NhanHieuUng -= Nhan;
        }

        var moc = SoDaTraLoi == 0 ? ids.Count : Volatile.Read(ref daGuiLucNhanCuoi);
        var chuaRo = new List<int>();
        lock (khoa)
            for (var i = 0; i < ids.Count; i++)
                if (!daTraLoi.Contains(ids[i]) && i < moc)
                    chuaRo.Add(ids[i]);

        return chuaRo;

        void Nhan(HinhQuai q)
        {
            Interlocked.Exchange(ref nhipCuoi, dongHo.ElapsedMilliseconds);
            Volatile.Write(ref daGuiLucNhanCuoi, Volatile.Read(ref daGui));

            try
            {
                lock (khoa) daTraLoi.Add(q.mobTemplateId);
                if (q.anh.Length > 0)
                    File.WriteAllBytes(Path.Combine(thuMuc, q.mobTemplateId + ".png"), q.anh);

                lock (khoa) bang[q.mobTemplateId] = Chuyen(q);
            }
            catch (Exception)
            {
                Interlocked.Increment(ref soLoi);
            }
        }
    }

    private int SoDaTraLoi
    {
        get
        {
            lock (khoa) return daTraLoi.Count;
        }
    }

    private static HieuUngRa Chuyen(HinhQuai q) => new()
    {
        Id = q.mobTemplateId,
        Width = q.rong,
        Height = q.cao,
        Scale = q.tiLe,
        SheetW = q.rongAnh,
        SheetH = q.caoAnh,
        Rects = q.oAnh.Select(o => new ORa { Id = o.id, X = o.x0, Y = o.y0, W = o.w, H = o.h }).ToArray(),
        Frames = q.khung.Select(k => k.dx
            .Select((_, i) => new ManhRa { Dx = k.dx[i], Dy = k.dy[i], O = k.oAnh[i] })
            .ToArray()).ToArray(),
        Anim = q.chuoiKhung
    };

    private void DocBangCu()
    {
        var duong = Path.Combine(thuMuc, "EffectFrames.json");
        if (!File.Exists(duong)) return;

        try
        {
            var cu = JsonSerializer.Deserialize<List<HieuUngRa>>(File.ReadAllText(duong));
            if (cu == null) return;
            lock (khoa)
                foreach (var x in cu) bang.TryAdd(x.Id, x);
        }
        catch (Exception)
        {
            // bảng cũ hỏng thì ghi đè bằng bản mới
        }
    }

    /// <summary>Ghi bảng khung ra một tệp chung, gộp với phần đã có từ lượt trước.</summary>
    public void Ghi()
    {
        DocBangCu();
        Directory.CreateDirectory(thuMuc);

        List<HieuUngRa> ds;
        lock (khoa) ds = bang.Values.OrderBy(x => x.Id).ToList();

        File.WriteAllText(Path.Combine(thuMuc, "EffectFrames.json"),
            JsonSerializer.Serialize(ds, Gon) + "\n");
    }
}
