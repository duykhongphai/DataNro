using System.IO;
using System.Text.Json;
using DataNro.GiaoThuc;

namespace DataNro;

/// <summary>
/// Ghép sẵn hình NPC và hình quái thành một tấm PNG cho mỗi con.
///
/// <para>
/// Trước đây trang web tự ghép: mỗi NPC là ba tấm ảnh rời chồng lên nhau, mỗi con quái là cả
/// một tấm sprite (có tấm 940x628) cắt ra mấy mảnh. Một trang tám mươi tám NPC thành hơn hai
/// trăm sáu mươi lượt tải, còn quái thì tải nguyên tấm sprite chỉ để lấy một khung. Ghép sẵn ở
/// đây thì người xem chỉ tải đúng một ảnh nhỏ cho mỗi con.
/// </para>
/// </summary>
public static class BoGhepHinh
{
    /// <summary>Ảnh máy chủ trả về ở mức phóng 4, mà mọi toạ độ trong dữ liệu là đơn vị game.</summary>
    private const int Phong = 4;

    /// <summary>
    /// Tư thế đứng yên (tư thế 0 trong bảng hằng của client), ba mục theo thứ tự
    /// <b>đầu / chân / thân</b>, mỗi mục là <c>{khung, dx, dy}</c>.
    /// </summary>
    private static readonly int[][] TuTheDung =
    {
        new[] { 0, -13, 34 },
        new[] { 1, -8, 10 },
        new[] { 1, -9, 16 }
    };

    /// <summary>Vài NPC client gọi thẳng một ảnh theo số thay vì ghép part - xem <c>Npc.paint</c>.</summary>
    private static readonly Dictionary<int, int> AnhRieng = new() { [3] = 265, [6] = 545 };

    /// <summary>NPC client không vẽ từ dữ liệu tĩnh: cây đậu thần, quả trứng, dưa hấu.</summary>
    private static readonly HashSet<int> KhongVe = new() { 4, 50, 51 };

    private sealed class Manh
    {
        public byte[] Rgba;
        public int Rong, Cao, X, Y;
    }

    // ==================== NPC ====================

    /// <summary>Ghép hình từng NPC ra <c>&lt;nhà phát hành&gt;/NpcHinh/&lt;id&gt;.png</c>.</summary>
    public static int GhepNpc(GameData d, string goc)
    {
        var thuMucAnh = Path.Combine(goc, "Icons");
        var thuMucRa = Path.Combine(goc, "NpcHinh");
        Directory.CreateDirectory(thuMucRa);

        var n = 0;
        foreach (var npc in d.arrNpcTemplate)
        {
            if (npc == null || KhongVe.Contains(npc.npcTemplateId)) continue;

            var manh = new List<Manh>();
            if (AnhRieng.TryGetValue(npc.npcTemplateId, out var idRieng))
            {
                var m = DocAnh(thuMucAnh, idRieng, 0, 0);
                if (m != null) manh.Add(m);
            }
            else
            {
                // Thứ tự vẽ là đầu, chân, thân - thân sau cùng nên đè lên trên. Ba id của NPC
                // lại theo thứ tự đầu / thân / chân, lệch với bảng tư thế nên rất dễ ghép nhầm.
                ThemManhPart(d, npc.headId, TuTheDung[0], thuMucAnh, manh);
                ThemManhPart(d, npc.legId, TuTheDung[1], thuMucAnh, manh);
                ThemManhPart(d, npc.bodyId, TuTheDung[2], thuMucAnh, manh);
            }

            if (Ghep(manh, out var rgba, out var rong, out var cao))
            {
                File.WriteAllBytes(Path.Combine(thuMucRa, npc.npcTemplateId + ".png"),
                    AnhPng.Ghi(rgba, rong, cao));
                n++;
            }
        }

        return n;
    }

    private static void ThemManhPart(GameData d, int chiSo, int[] tuThe, string thuMucAnh,
        List<Manh> ra)
    {
        if (chiSo < 0 || chiSo >= d.parts.Length) return;

        var p = d.parts[chiSo];
        if (p?.pi == null || tuThe[0] >= p.pi.Length) return;

        var k = p.pi[tuThe[0]];
        if (k.id < 0) return;

        // Công thức của client: x = dx tư thế + dx khung, y = TRỪ dy tư thế + dy khung.
        var m = DocAnh(thuMucAnh, k.id, (tuThe[1] + k.dx) * Phong, (-tuThe[2] + k.dy) * Phong);
        if (m != null) ra.Add(m);
    }

    private static Manh DocAnh(string thuMucAnh, int id, int x, int y)
    {
        var duong = BoAnh.DuongDanAnh(thuMucAnh, id, ".png");
        if (!File.Exists(duong)) return null;

        try
        {
            var rgba = AnhPng.Doc(File.ReadAllBytes(duong), out var rong, out var cao);
            return rgba == null ? null : new Manh { Rgba = rgba, Rong = rong, Cao = cao, X = x, Y = y };
        }
        catch (Exception)
        {
            return null;
        }
    }

    // ==================== quái ====================

    /// <summary>
    /// Cắt khung đầu của từng mẫu quái ra <c>&lt;nhà phát hành&gt;/MobHinh/&lt;id&gt;.png</c>.
    /// </summary>
    public static int GhepQuai(string goc)
    {
        var thuMucQuai = Path.Combine(goc, "Mobs");
        var bang = Path.Combine(thuMucQuai, "MobFrames.json");
        if (!File.Exists(bang)) return 0;

        List<QuaiRa> ds;
        try
        {
            ds = JsonSerializer.Deserialize<List<QuaiRa>>(File.ReadAllText(bang));
        }
        catch (Exception)
        {
            return 0;
        }

        if (ds == null) return 0;

        var thuMucRa = Path.Combine(goc, "MobHinh");
        Directory.CreateDirectory(thuMucRa);

        var n = 0;
        foreach (var q in ds)
        {
            var duong = Path.Combine(thuMucQuai, q.MobTemplateId + ".png");
            if (!File.Exists(duong) || q.Frames == null || q.Frames.Length == 0) continue;

            byte[] tam;
            int rongTam, caoTam;
            try
            {
                tam = AnhPng.Doc(File.ReadAllBytes(duong), out rongTam, out caoTam);
            }
            catch (Exception)
            {
                continue;
            }

            if (tam == null) continue;

            // Tấm sprite không phải con nào cũng ở mức phóng 4 - vài con máy chủ gửi ảnh gốc.
            var ti = q.Scale <= 0 ? Phong : q.Scale;
            var o = q.Rects.ToDictionary(r => r.Id);
            var manh = new List<Manh>();

            foreach (var m in q.Frames[0])
            {
                if (!o.TryGetValue(m.O, out var r)) continue;

                var cat = Cat(tam, rongTam, caoTam, r.X * ti, r.Y * ti, r.W * ti, r.H * ti);
                if (cat == null) continue;

                manh.Add(new Manh
                {
                    Rgba = cat,
                    Rong = r.W * ti,
                    Cao = r.H * ti,
                    // Quy về mức phóng 4 để mọi hình quái cùng một cỡ trên trang.
                    X = m.Dx * Phong,
                    Y = m.Dy * Phong
                });
            }

            // Con nào tấm sprite chưa phóng thì phóng nốt mảnh cho khớp mức 4.
            if (ti != Phong)
                foreach (var m in manh)
                {
                    var lan = Phong / ti;
                    m.Rgba = PhongTo(m.Rgba, m.Rong, m.Cao, lan);
                    m.Rong *= lan;
                    m.Cao *= lan;
                }

            if (!Ghep(manh, out var rgba, out var rong, out var cao)) continue;

            File.WriteAllBytes(Path.Combine(thuMucRa, q.MobTemplateId + ".png"),
                AnhPng.Ghi(rgba, rong, cao));
            n++;
        }

        return n;
    }

    // ==================== hiệu ứng ====================

    /// <summary>Một hiệu ứng đã ghép: dải khung nằm ngang, mỗi ô một khung.</summary>
    public class DaiRa
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")] public int Id { get; set; }

        /// <summary>Rộng/cao của MỘT ô, tính bằng điểm ảnh.</summary>
        [System.Text.Json.Serialization.JsonPropertyName("w")] public int W { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("h")] public int H { get; set; }

        /// <summary>Số ô trong dải, cũng là số khung của hoạt ảnh.</summary>
        [System.Text.Json.Serialization.JsonPropertyName("n")] public int N { get; set; }
    }

    /// <summary>Dải dài quá thì trình duyệt phải giữ một tấm ảnh khổng lồ chỉ để chạy một hiệu ứng.</summary>
    private const int ToiDaKhung = 120;

    /// <summary>
    /// Trần chiều ngang của dải. Trình duyệt có giới hạn cứng quanh 65535 điểm ảnh mỗi chiều,
    /// mà hiệu ứng to nhất ghép đủ 120 khung đã ra 63840 - sát mép, và tấm ảnh ngần ấy thì mở
    /// một trang là ngốn cả trăm MB bộ nhớ. Cắt bớt số khung cho vừa: hiệu ứng vốn lặp, chạy
    /// ba chục khung đầu vẫn ra đúng nhịp.
    /// </summary>
    private const int ToiDaRongDai = 16000;

    /// <summary>Trần tổng số điểm ảnh của cả dải - giữ mỗi tệp trong tầm vài MB.</summary>
    private const long ToiDaDiem = 12_000_000;

    /// <summary>
    /// Ghép mỗi hiệu ứng thành <b>một dải khung nằm ngang</b> ra
    /// <c>&lt;nhà phát hành&gt;/EffectHinh/&lt;id&gt;.png</c>, kèm bảng <c>Anim.json</c>.
    ///
    /// <para>
    /// Dải chứ không phải một khung: hiệu ứng phải chạy mới ra hồn, mà dải ngang thì trang web
    /// chạy được bằng đúng một câu CSS <c>steps()</c> - không cần canvas, không cần JS đếm nhịp.
    /// Mọi ô cùng cỡ, lấy theo hộp bao chung của tất cả khung, không thì hình nhảy loạn khi
    /// đổi khung.
    /// </para>
    ///
    /// <para>
    /// Thứ tự ô theo <c>anim</c> chứ không theo <c>frames</c>: <c>anim</c> mới là trình tự
    /// chiếu, và nó lặp lại khung - có hiệu ứng 69 khung mà chuỗi chiếu dài 155.
    /// </para>
    /// </summary>
    public static int GhepHieuUng(string goc)
    {
        var thuMuc = Path.Combine(goc, "Effects");
        var bang = Path.Combine(thuMuc, "EffectFrames.json");
        if (!File.Exists(bang)) return 0;

        List<HieuUngRa> ds;
        try
        {
            ds = JsonSerializer.Deserialize<List<HieuUngRa>>(File.ReadAllText(bang));
        }
        catch (Exception)
        {
            return 0;
        }

        if (ds == null) return 0;

        var thuMucRa = Path.Combine(goc, "EffectHinh");
        Directory.CreateDirectory(thuMucRa);

        var moTa = new List<DaiRa>();
        foreach (var e in ds)
        {
            var duong = Path.Combine(thuMuc, e.Id + ".png");
            if (!File.Exists(duong) || e.Frames == null || e.Frames.Length == 0) continue;

            byte[] tam;
            int rongTam, caoTam;
            try
            {
                tam = AnhPng.Doc(File.ReadAllBytes(duong), out rongTam, out caoTam);
            }
            catch (Exception)
            {
                continue;
            }

            if (tam == null) continue;

            var ti = e.Scale <= 0 ? Phong : e.Scale;
            var o = e.Rects.ToDictionary(r => r.Id);

            // Chuỗi chiếu; hiệu ứng nào không có thì cứ chạy lần lượt từng khung.
            var chuoi = e.Anim != null && e.Anim.Length > 0
                ? e.Anim.Where(x => x >= 0 && x < e.Frames.Length).Select(x => (int)x).ToList()
                : Enumerable.Range(0, e.Frames.Length).ToList();
            if (chuoi.Count == 0) continue;
            if (chuoi.Count > ToiDaKhung) chuoi = chuoi.Take(ToiDaKhung).ToList();

            // Hộp bao chung của mọi khung sẽ dùng, tính bằng đơn vị game.
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
            foreach (var k in chuoi.Distinct())
            foreach (var m in e.Frames[k])
            {
                if (!o.TryGetValue(m.O, out var r)) continue;
                if (x0 > m.Dx) x0 = m.Dx;
                if (y0 > m.Dy) y0 = m.Dy;
                if (x1 < m.Dx + r.W) x1 = m.Dx + r.W;
                if (y1 < m.Dy + r.H) y1 = m.Dy + r.H;
            }

            if (x0 == int.MaxValue) continue;

            var rongO = (x1 - x0) * ti;
            var caoO = (y1 - y0) * ti;
            if (rongO <= 0 || caoO <= 0) continue;

            // Cắt cho vừa hai cái trần - chiều ngang và tổng số điểm ảnh - chứ không bỏ hẳn:
            // hiệu ứng nào hộp bao to mà chuỗi dài thì chạy ít khung hơn, vẫn còn hơn không có.
            var vua = Math.Min(ToiDaRongDai / rongO, (int)(ToiDaDiem / ((long)rongO * caoO)));
            vua = Math.Max(1, vua);
            if (chuoi.Count > vua) chuoi = chuoi.Take(vua).ToList();

            // Một khung thôi mà đã quá khổ thì đành chịu.
            if ((long)rongO * caoO > ToiDaDiem) continue;

            var rongDai = rongO * chuoi.Count;
            var dai = new byte[rongDai * caoO * 4];

            for (var i = 0; i < chuoi.Count; i++)
            foreach (var m in e.Frames[chuoi[i]])
            {
                if (!o.TryGetValue(m.O, out var r)) continue;

                var cat = Cat(tam, rongTam, caoTam, r.X * ti, r.Y * ti, r.W * ti, r.H * ti);
                if (cat == null) continue;

                Dan(dai, rongDai, caoO,
                    new Manh { Rgba = cat, Rong = r.W * ti, Cao = r.H * ti },
                    i * rongO + (m.Dx - x0) * ti, (m.Dy - y0) * ti);
            }

            File.WriteAllBytes(Path.Combine(thuMucRa, e.Id + ".png"), AnhPng.Ghi(dai, rongDai, caoO));
            moTa.Add(new DaiRa { Id = e.Id, W = rongO, H = caoO, N = chuoi.Count });
        }

        File.WriteAllText(Path.Combine(thuMucRa, "Anim.json"),
            JsonSerializer.Serialize(moTa.OrderBy(x => x.Id),
                new JsonSerializerOptions { WriteIndented = false }) + "\n");

        return moTa.Count;
    }

    // ==================== dùng chung ====================

    /// <summary>Xếp các mảnh lên một tấm vừa khít hộp bao của chúng.</summary>
    private static bool Ghep(List<Manh> manh, out byte[] rgba, out int rong, out int cao)
    {
        rgba = null;
        rong = cao = 0;
        if (manh.Count == 0) return false;

        int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
        foreach (var m in manh)
        {
            if (x0 > m.X) x0 = m.X;
            if (y0 > m.Y) y0 = m.Y;
            if (x1 < m.X + m.Rong) x1 = m.X + m.Rong;
            if (y1 < m.Y + m.Cao) y1 = m.Y + m.Cao;
        }

        rong = x1 - x0;
        cao = y1 - y0;
        if (rong <= 0 || cao <= 0 || (long)rong * cao > 4_000_000) return false;

        rgba = new byte[rong * cao * 4];
        foreach (var m in manh) Dan(rgba, rong, cao, m, m.X - x0, m.Y - y0);
        return true;
    }

    /// <summary>Chồng một mảnh lên tấm đích, trộn theo alpha đúng kiểu "source over".</summary>
    private static void Dan(byte[] dich, int rongDich, int caoDich, Manh m, int dx, int dy)
    {
        for (var y = 0; y < m.Cao; y++)
        {
            var yy = dy + y;
            if (yy < 0 || yy >= caoDich) continue;

            for (var x = 0; x < m.Rong; x++)
            {
                var xx = dx + x;
                if (xx < 0 || xx >= rongDich) continue;

                var s = (y * m.Rong + x) * 4;
                var a = m.Rgba[s + 3];
                if (a == 0) continue;

                var d = (yy * rongDich + xx) * 4;
                if (a == 255)
                {
                    dich[d] = m.Rgba[s];
                    dich[d + 1] = m.Rgba[s + 1];
                    dich[d + 2] = m.Rgba[s + 2];
                    dich[d + 3] = 255;
                    continue;
                }

                var nghich = 255 - a;
                dich[d] = (byte)((m.Rgba[s] * a + dich[d] * nghich) / 255);
                dich[d + 1] = (byte)((m.Rgba[s + 1] * a + dich[d + 1] * nghich) / 255);
                dich[d + 2] = (byte)((m.Rgba[s + 2] * a + dich[d + 2] * nghich) / 255);
                dich[d + 3] = (byte)(a + dich[d + 3] * nghich / 255);
            }
        }
    }

    private static byte[] Cat(byte[] nguon, int rongNguon, int caoNguon, int x, int y, int w, int h)
    {
        if (w <= 0 || h <= 0) return null;

        var ra = new byte[w * h * 4];
        for (var j = 0; j < h; j++)
        {
            var yy = y + j;
            if (yy < 0 || yy >= caoNguon) continue;

            for (var i = 0; i < w; i++)
            {
                var xx = x + i;
                if (xx < 0 || xx >= rongNguon) continue;
                Array.Copy(nguon, (yy * rongNguon + xx) * 4, ra, (j * w + i) * 4, 4);
            }
        }

        return ra;
    }

    /// <summary>Phóng to nguyên khối, mỗi điểm thành một ô vuông - giữ nét pixel.</summary>
    private static byte[] PhongTo(byte[] nguon, int rong, int cao, int lan)
    {
        var rongMoi = rong * lan;
        var ra = new byte[rongMoi * cao * lan * 4];

        for (var y = 0; y < cao; y++)
        for (var x = 0; x < rong; x++)
        {
            var s = (y * rong + x) * 4;
            for (var j = 0; j < lan; j++)
            for (var i = 0; i < lan; i++)
                Array.Copy(nguon, s, ra, ((y * lan + j) * rongMoi + x * lan + i) * 4, 4);
        }

        return ra;
    }
}
