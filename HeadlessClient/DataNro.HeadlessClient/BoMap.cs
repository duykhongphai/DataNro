using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using DataNro.GiaoThuc;

namespace DataNro;

/// <summary>Bố cục ô của một map, dạng đem xuất ra JSON.</summary>
public class BoCucRa
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("w")] public int W { get; set; }
    [JsonPropertyName("h")] public int H { get; set; }

    /// <summary>Lưới <c>w * h</c> ô, mỗi số là chỉ số ảnh trong bộ tile; 0 là ô trống.</summary>
    [JsonPropertyName("tiles")] public int[] Tiles { get; set; }
}

/// <summary>
/// Xin bố cục ô của từng map (gói <c>-28</c> nhánh 10).
///
/// <para>
/// Khác hẳn ảnh với hình quái: gói này nhẹ và máy chủ trả lời ngay, không thấy hạn mức theo
/// phiên. Nhưng nó <b>không kèm id map</b> trong gói trả về, nên phải hỏi từng cái một và đợi
/// trả lời xong mới hỏi tiếp - bắn một loạt rồi ghép id theo thứ tự là sai ngay khi có một gói
/// rơi mất.
/// </para>
/// </summary>
public sealed class BoMap
{
    private readonly Phien phien;
    private readonly string thuMuc;
    private readonly object khoa = new();
    private readonly Dictionary<int, BoCucRa> bang = new();

    private TaskCompletionSource<MauMap> chờ;

    public BoMap(Phien phien, string thuMucRa)
    {
        this.phien = phien;
        thuMuc = thuMucRa;
    }

    public int SoDaCo
    {
        get
        {
            lock (khoa) return bang.Count;
        }
    }

    /// <summary>Hỏi lần lượt từng map, trả về số map lấy được.</summary>
    public async Task<int> LayAsync(IEnumerable<int> mapIds, int hetGioMoiMapMs, int nhipMs,
        CancellationToken ct)
    {
        DocBangCu();

        var duoc = 0;
        phien.Doc.NhanBoCucMap += Nhan;

        try
        {
            foreach (var id in mapIds)
            {
                if (ct.IsCancellationRequested || !phien.DaNoi) break;

                var tcs = new TaskCompletionSource<MauMap>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                Volatile.Write(ref chờ, tcs);

                phien.Doc.XinMauMap(id);

                var xong = await Task.WhenAny(tcs.Task, Task.Delay(hetGioMoiMapMs, ct))
                    .ConfigureAwait(false);
                if (xong != tcs.Task) continue;

                var mm = await tcs.Task.ConfigureAwait(false);
                if (mm.rong <= 0 || mm.cao <= 0) continue;

                lock (khoa)
                    bang[id] = new BoCucRa { Id = id, W = mm.rong, H = mm.cao, Tiles = mm.o };
                duoc++;

                try
                {
                    await Task.Delay(nhipMs, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        finally
        {
            phien.Doc.NhanBoCucMap -= Nhan;
        }

        return duoc;

        void Nhan(MauMap mm) => Volatile.Read(ref chờ)?.TrySetResult(mm);
    }

    /// <summary>Map nào đã có bố cục ngoài đĩa - lượt sau khỏi hỏi lại.</summary>
    public static HashSet<int> DaCoTrenDia(string thuMucRa)
    {
        var co = new HashSet<int>();
        var duong = Path.Combine(thuMucRa, "MapTiles.json");
        if (!File.Exists(duong)) return co;

        try
        {
            var ds = JsonSerializer.Deserialize<List<BoCucRa>>(File.ReadAllText(duong));
            if (ds != null)
                foreach (var x in ds) co.Add(x.Id);
        }
        catch (Exception)
        {
            // bảng hỏng thì coi như chưa có gì
        }

        return co;
    }

    private void DocBangCu()
    {
        var duong = Path.Combine(thuMuc, "MapTiles.json");
        if (!File.Exists(duong)) return;

        try
        {
            var ds = JsonSerializer.Deserialize<List<BoCucRa>>(File.ReadAllText(duong));
            if (ds == null) return;
            lock (khoa)
                foreach (var x in ds) bang[x.Id] = x;
        }
        catch (Exception)
        {
            // bảng hỏng thì ghi đè bằng bản mới
        }
    }

    /// <summary>Ghi cả bảng ra một tệp, gộp với phần đã có từ lượt trước.</summary>
    public void Ghi()
    {
        Directory.CreateDirectory(thuMuc);

        List<BoCucRa> ds;
        lock (khoa) ds = bang.Values.OrderBy(x => x.Id).ToList();

        File.WriteAllText(Path.Combine(thuMuc, "MapTiles.json"),
            JsonSerializer.Serialize(ds, new JsonSerializerOptions { WriteIndented = false }) + "\n");
    }
}
