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
