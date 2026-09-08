using System.Diagnostics;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using DataNro.GiaoThuc;

namespace DataNro;

// ==================== mẫu xuất ====================

public class ORa
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("x")] public int X { get; set; }
    [JsonPropertyName("y")] public int Y { get; set; }
    [JsonPropertyName("w")] public int W { get; set; }
    [JsonPropertyName("h")] public int H { get; set; }
}

public class ManhRa
{
    [JsonPropertyName("dx")] public short Dx { get; set; }
    [JsonPropertyName("dy")] public short Dy { get; set; }
    [JsonPropertyName("o")] public sbyte O { get; set; }
}

public class QuaiRa
{
    [JsonPropertyName("mobTemplateId")] public int MobTemplateId { get; set; }
    [JsonPropertyName("width")] public int Width { get; set; }
    [JsonPropertyName("height")] public int Height { get; set; }
    [JsonPropertyName("typeData")] public int TypeData { get; set; }

    /// <summary>Bảng ô cắt trên tấm sprite.</summary>
    [JsonPropertyName("rects")] public ORa[] Rects { get; set; }

    /// <summary>Mỗi khung là một danh sách mảnh.</summary>
    [JsonPropertyName("frames")] public ManhRa[][] Frames { get; set; }

    /// <summary>Chuỗi hoạt ảnh, mỗi phần tử là chỉ số vào <c>frames</c>.</summary>
    [JsonPropertyName("anim")] public short[] Anim { get; set; }

    /// <summary>
    /// Một đơn vị game bằng mấy điểm ảnh trên tấm sprite - gần như luôn là 4, trừ vài con máy
    /// chủ gửi ảnh gốc chưa phóng. Bên vẽ phải phóng tấm ảnh lên cho khớp trước khi cắt.
    /// </summary>
    [JsonPropertyName("scale")] public int Scale { get; set; } = 4;

    /// <summary>
    /// Kích thước tấm sprite, tính bằng điểm ảnh thật. Bên vẽ cần con số này để phóng tấm ảnh
    /// lên đúng mức trước khi cắt - CSS không có cách nào hỏi kích thước gốc của ảnh nền.
    /// </summary>
    [JsonPropertyName("sheetW")] public int SheetW { get; set; }

    [JsonPropertyName("sheetH")] public int SheetH { get; set; }

    /// <summary>
    /// Rộng/cao của ô cắt là <b>tự dò</b> từ tấm PNG chứ không phải máy chủ gửi, nên có thể
    /// lệch vài điểm ảnh. Chỉ vài con boss dính, nên bỏ hẳn trường này khi không cần.
    /// </summary>
    [JsonPropertyName("autoSize")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool AutoSize { get; set; }
}

/// <summary>
/// Xin hình từng mẫu quái và ghi ra đĩa.
///
/// <para>
/// Mỗi mẫu quái có một tấm sprite riêng, xin bằng gói <c>11</c> - khác hẳn NPC vốn ghép từ
/// bảng part dùng chung. Cách hỏi thì giống hệt bộ tải ảnh: hỏi từng cái, im lặng nghĩa là
/// không có, nên cũng chia lô và hỏi lại phần chưa rõ.
/// </para>
/// </summary>
public sealed class BoQuai
{
    private readonly Phien phien;
    private readonly string thuMuc;
    private readonly object khoa = new();
    private readonly HashSet<int> daTraLoi = new();
    private readonly Dictionary<int, QuaiRa> bang = new();

    private long nhipCuoi;
    private int daGui;
    private int daGuiLucNhanCuoi;
    private int soLoi;

    private static readonly JsonSerializerOptions Gon = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public BoQuai(Phien phien, string thuMucRa)
    {
        this.phien = phien;
        thuMuc = thuMucRa;
    }

    public int SoLoi => soLoi;

    /// <summary>Mẫu quái đã có tấm sprite ngoài đĩa.</summary>
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

        lock (khoa) daTraLoi.Clear();
        daGui = 0;
        daGuiLucNhanCuoi = 0;

        var dongHo = Stopwatch.StartNew();
        nhipCuoi = dongHo.ElapsedMilliseconds;
        phien.Doc.NhanQuai += Nhan;

        try
        {
            for (var i = 0; i < ids.Count; i++)
            {
                if (ct.IsCancellationRequested || !phien.DaNoi) break;

                phien.Doc.XinHinhQuai(ids[i]);
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
            phien.Doc.NhanQuai -= Nhan;
        }

        // Cùng luật với bộ tải ảnh: chỉ id nào máy chủ đã trả lời mới coi là xong, và id hỏi
        // sau khi nó im thì không tính là đã thử.
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

    /// <summary>
    /// Tính lại kích thước và tỉ lệ tấm sprite cho <b>mọi</b> con trong bảng, kể cả những con của lần chạy
    /// trước. Chỉ cần đọc khối IHDR của tệp PNG ngoài đĩa nên rẻ, mà bù lại bảng cũ ghi trước
    /// khi có trường này cũng tự lành, khỏi phải xin lại hình.
    /// </summary>
    private void TinhLaiTiLe(Dictionary<int, QuaiRa> bangGop)
    {
        foreach (var q in bangGop.Values)
        {
            if (q.Rects == null || q.Rects.Length == 0) continue;

            var duong = Path.Combine(thuMuc, q.MobTemplateId + ".png");
            if (!File.Exists(duong)) continue;

            try
            {
                // Chỉ cần đúng cái đầu tệp: rộng/cao nằm ngay trong khối IHDR.
                var dau = new byte[64];
                using (var f = File.OpenRead(duong))
                {
                    if (f.Read(dau, 0, dau.Length) < dau.Length) continue;
                }

                if (!AnhPng.KichThuoc(dau, out var rongAnh, out var caoAnh)) continue;

                q.SheetW = rongAnh;
                q.SheetH = caoAnh;
                q.Scale = HinhQuai.TinhTiLe(rongAnh, caoAnh,
                    q.Rects.Select(r => new OAnh { x0 = r.X, y0 = r.Y, w = r.W, h = r.H }).ToArray());
            }
            catch (Exception)
            {
                Interlocked.Increment(ref soLoi);
            }
        }
    }

    private static QuaiRa Chuyen(HinhQuai q) => new()
    {
        MobTemplateId = q.mobTemplateId,
        Width = q.rong,
        Height = q.cao,
        TypeData = q.typeData,
        Rects = q.oAnh.Select(o => new ORa { Id = o.id, X = o.x0, Y = o.y0, W = o.w, H = o.h }).ToArray(),
        Frames = q.khung.Select(k => k.dx
            .Select((_, i) => new ManhRa { Dx = k.dx[i], Dy = k.dy[i], O = k.oAnh[i] })
            .ToArray()).ToArray(),
        Anim = q.chuoiKhung,
        Scale = q.tiLe,
        SheetW = q.rongAnh,
        SheetH = q.caoAnh,
        AutoSize = q.thieuKichThuocO
    };

    /// <summary>
    /// Ghi bảng khung ra một tệp chung. Gộp bản đã có sẵn ngoài đĩa để lượt chạy chỉ xin vài
    /// con mới cũng không xoá mất bảng của những con cũ.
    /// </summary>
    public void GhiBang()
    {
        var duong = Path.Combine(thuMuc, "MobFrames.json");
        var gop = new Dictionary<int, QuaiRa>();

        if (File.Exists(duong))
            try
            {
                var cu = JsonSerializer.Deserialize<List<QuaiRa>>(File.ReadAllText(duong));
                if (cu != null)
                    foreach (var x in cu) gop[x.MobTemplateId] = x;
            }
            catch (Exception)
            {
                // bảng cũ hỏng thì ghi đè bằng bản mới, còn hơn không có gì
            }

        lock (khoa)
            foreach (var kv in bang) gop[kv.Key] = kv.Value;

        TinhLaiTiLe(gop);

        var ds = gop.Values.OrderBy(x => x.MobTemplateId).ToList();
        File.WriteAllText(duong, JsonSerializer.Serialize(ds, Gon) + "\n");
    }
}
