using System.IO;
using System.IO.Compression;

namespace DataNro.GiaoThuc;

/// <summary>
/// Bộ đọc PNG tí hon, <b>chỉ lấy kênh trong suốt</b>.
///
/// <para>
/// Dùng đúng một việc: dò rộng/cao của ô cắt cho mấy con boss máy chủ gửi bảng ô thiếu kích
/// thước. Có chừng ấy nhu cầu nên không kéo về thư viện ảnh nào cả - <c>System.Drawing</c> thì
/// chỉ chạy trên Windows mà máy dựng là Linux, còn thư viện ngoài thì thêm một mắt xích phụ
/// thuộc cho một tính năng dùng cho ba con quái.
/// </para>
/// </summary>
public static class AnhPng
{
    /// <summary>Rộng/cao ghi trong khối IHDR. Rẻ hơn hẳn <see cref="DocDuc"/> vì khỏi giải nén.</summary>
    public static bool KichThuoc(byte[] png, out int rong, out int cao)
    {
        rong = cao = 0;
        if (png == null || png.Length < 33 || png[0] != 0x89 || png[1] != 'P') return false;
        if (System.Text.Encoding.ASCII.GetString(png, 12, 4) != "IHDR") return false;

        rong = DocInt(png, 16);
        cao = DocInt(png, 20);
        return rong > 0 && cao > 0;
    }

    /// <summary>
    /// Giải một tấm PNG ra mảng RGBA, mỗi điểm bốn byte. Trả <c>null</c> nếu tấm ảnh dùng
    /// kiểu mã hoá không đỡ (ảnh xen kẽ, độ sâu khác 8 bit).
    /// </summary>
    public static byte[] Doc(byte[] png, out int rong, out int cao)
    {
        rong = cao = 0;
        if (png == null || png.Length < 8 || png[0] != 0x89 || png[1] != 'P') return null;

        int sauBit = 0, kieuMau = 0, xenKe = 0;
        byte[] trns = null, plte = null;
        var idat = new MemoryStream();

        var p = 8;
        while (p + 8 <= png.Length)
        {
            var dai = DocInt(png, p);
            var loai = System.Text.Encoding.ASCII.GetString(png, p + 4, 4);
            var noiDung = p + 8;
            if (dai < 0 || noiDung + dai > png.Length) return null;

            switch (loai)
            {
                case "IHDR":
                    rong = DocInt(png, noiDung);
                    cao = DocInt(png, noiDung + 4);
                    sauBit = png[noiDung + 8];
                    kieuMau = png[noiDung + 9];
                    xenKe = png[noiDung + 12];
                    break;
                case "PLTE":
                    plte = new byte[dai];
                    Array.Copy(png, noiDung, plte, 0, dai);
                    break;
                case "tRNS":
                    trns = new byte[dai];
                    Array.Copy(png, noiDung, trns, 0, dai);
                    break;
                case "IDAT":
                    idat.Write(png, noiDung, dai);
                    break;
                case "IEND":
                    p = png.Length;
                    break;
            }

            p = noiDung + dai + 4;
        }

        if (rong <= 0 || cao <= 0 || sauBit != 8 || xenKe != 0) return null;

        var soKenh = kieuMau switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, 6 => 4, _ => 0 };
        if (soKenh == 0) return null;
        if (kieuMau == 3 && plte == null) return null;

        var tho = GiaiNen(idat.ToArray());
        if (tho == null) return null;

        var moiDong = rong * soKenh;
        if (tho.Length < (moiDong + 1) * (long)cao) return null;

        var ra = new byte[rong * cao * 4];
        var dong = new byte[moiDong];
        var truoc = new byte[moiDong];
        var q = 0;

        for (var y = 0; y < cao; y++)
        {
            var loc = tho[q++];
            Array.Copy(tho, q, dong, 0, moiDong);
            q += moiDong;
            BoLoc(loc, dong, truoc, soKenh);

            for (var x = 0; x < rong; x++)
            {
                var o = (y * rong + x) * 4;
                switch (kieuMau)
                {
                    case 6:
                        ra[o] = dong[x * 4];
                        ra[o + 1] = dong[x * 4 + 1];
                        ra[o + 2] = dong[x * 4 + 2];
                        ra[o + 3] = dong[x * 4 + 3];
                        break;
                    case 2:
                        ra[o] = dong[x * 3];
                        ra[o + 1] = dong[x * 3 + 1];
                        ra[o + 2] = dong[x * 3 + 2];
                        ra[o + 3] = 255;
                        break;
                    case 4:
                        ra[o] = ra[o + 1] = ra[o + 2] = dong[x * 2];
                        ra[o + 3] = dong[x * 2 + 1];
                        break;
                    case 3:
                    {
                        var i = dong[x];
                        var b = i * 3;
                        if (b + 2 < plte.Length)
                        {
                            ra[o] = plte[b];
                            ra[o + 1] = plte[b + 1];
                            ra[o + 2] = plte[b + 2];
                        }

                        ra[o + 3] = AlphaBang(trns, i);
                        break;
                    }
                    default:
                        ra[o] = ra[o + 1] = ra[o + 2] = dong[x];
                        ra[o + 3] = 255;
                        break;
                }
            }

            (truoc, dong) = (dong, truoc);
        }

        return ra;
    }

    /// <summary>Mặt nạ điểm ảnh đục (<c>true</c> là không trong suốt).</summary>
    public static bool[] DocDuc(byte[] png, out int rong, out int cao)
    {
        var rgba = Doc(png, out rong, out cao);
        if (rgba == null) return null;

        var duc = new bool[rong * cao];
        for (var i = 0; i < duc.Length; i++) duc[i] = rgba[i * 4 + 3] != 0;
        return duc;
    }

    /// <summary>
    /// Đóng một mảng RGBA thành tệp PNG. Lọc dòng để 0 hết - dữ liệu đã qua deflate rồi, bày
    /// đặt chọn bộ lọc chỉ để bớt vài phần trăm thì không bõ.
    /// </summary>
    public static byte[] Ghi(byte[] rgba, int rong, int cao)
    {
        var tho = new byte[(rong * 4 + 1) * cao];
        for (var y = 0; y < cao; y++)
        {
            tho[y * (rong * 4 + 1)] = 0;
            Array.Copy(rgba, y * rong * 4, tho, y * (rong * 4 + 1) + 1, rong * 4);
        }

        byte[] nen;
        using (var ra = new MemoryStream())
        {
            using (var ep = new ZLibStream(ra, CompressionLevel.Optimal, true))
                ep.Write(tho, 0, tho.Length);
            nen = ra.ToArray();
        }

        using var tep = new MemoryStream();
        tep.Write(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 13, 10, 26, 10 }, 0, 8);

        var ihdr = new byte[13];
        GhiInt(ihdr, 0, rong);
        GhiInt(ihdr, 4, cao);
        ihdr[8] = 8;   // 8 bit mỗi kênh
        ihdr[9] = 6;   // RGBA
        Khoi(tep, "IHDR", ihdr);
        Khoi(tep, "IDAT", nen);
        Khoi(tep, "IEND", Array.Empty<byte>());
        return tep.ToArray();
    }

    private static void Khoi(Stream ra, string loai, byte[] noiDung)
    {
        var dai = new byte[4];
        GhiInt(dai, 0, noiDung.Length);
        ra.Write(dai, 0, 4);

        var than = new byte[4 + noiDung.Length];
        for (var i = 0; i < 4; i++) than[i] = (byte)loai[i];
        Array.Copy(noiDung, 0, than, 4, noiDung.Length);
        ra.Write(than, 0, than.Length);

        var crc = new byte[4];
        GhiInt(crc, 0, unchecked((int)Crc(than)));
        ra.Write(crc, 0, 4);
    }

    private static void GhiInt(byte[] b, int i, int v)
    {
        b[i] = (byte)(v >> 24);
        b[i + 1] = (byte)(v >> 16);
        b[i + 2] = (byte)(v >> 8);
        b[i + 3] = (byte)v;
    }

    private static readonly uint[] BangCrc = TaoBangCrc();

    private static uint[] TaoBangCrc()
    {
        var b = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            var c = i;
            for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            b[i] = c;
        }

        return b;
    }

    private static uint Crc(byte[] d)
    {
        var c = 0xFFFFFFFFu;
        foreach (var x in d) c = BangCrc[(c ^ x) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }

    private static byte AlphaBang(byte[] trns, byte chiSo) =>
        trns == null || chiSo >= trns.Length ? (byte)255 : trns[chiSo];

    private static int DocInt(byte[] b, int i) =>
        (b[i] << 24) | (b[i + 1] << 16) | (b[i + 2] << 8) | b[i + 3];

    private static byte[] GiaiNen(byte[] zlib)
    {
        try
        {
            using var vao = new MemoryStream(zlib);
            using var xa = new ZLibStream(vao, CompressionMode.Decompress);
            using var ra = new MemoryStream();
            xa.CopyTo(ra);
            return ra.ToArray();
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Gỡ bộ lọc từng dòng của PNG - năm kiểu, y như đặc tả.</summary>
    private static void BoLoc(byte loc, byte[] dong, byte[] truoc, int bpp)
    {
        switch (loc)
        {
            case 1:
                for (var i = bpp; i < dong.Length; i++)
                    dong[i] = (byte)(dong[i] + dong[i - bpp]);
                break;
            case 2:
                for (var i = 0; i < dong.Length; i++)
                    dong[i] = (byte)(dong[i] + truoc[i]);
                break;
            case 3:
                for (var i = 0; i < dong.Length; i++)
                {
                    var trai = i >= bpp ? dong[i - bpp] : 0;
                    dong[i] = (byte)(dong[i] + (trai + truoc[i]) / 2);
                }

                break;
            case 4:
                for (var i = 0; i < dong.Length; i++)
                {
                    int trai = i >= bpp ? dong[i - bpp] : 0;
                    int tren = truoc[i];
                    int cheo = i >= bpp ? truoc[i - bpp] : 0;
                    dong[i] = (byte)(dong[i] + Paeth(trai, tren, cheo));
                }

                break;
        }
    }

    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        if (pa <= pb && pa <= pc) return a;
        return pb <= pc ? b : c;
    }
}
