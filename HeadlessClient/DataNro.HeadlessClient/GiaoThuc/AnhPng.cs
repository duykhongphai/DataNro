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
    /// Trả về mặt nạ điểm ảnh đục (<c>true</c> là không trong suốt), hoặc <c>null</c> nếu tấm
    /// ảnh dùng kiểu mã hoá không đỡ (ảnh xen kẽ, độ sâu khác 8 bit).
    /// </summary>
    public static bool[] DocDuc(byte[] png, out int rong, out int cao)
    {
        rong = cao = 0;
        if (png == null || png.Length < 8 || png[0] != 0x89 || png[1] != 'P') return null;

        int sauBit = 0, kieuMau = 0, xenKe = 0;
        byte[] trns = null;
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

        var tho = GiaiNen(idat.ToArray());
        if (tho == null) return null;

        var moiDong = rong * soKenh;
        if (tho.Length < (moiDong + 1) * (long)cao) return null;

        var duc = new bool[rong * cao];
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
                var a = kieuMau switch
                {
                    6 => dong[x * 4 + 3],
                    4 => dong[x * 2 + 1],
                    3 => AlphaBang(trns, dong[x]),
                    _ => (byte)255
                };
                duc[y * rong + x] = a != 0;
            }

            (truoc, dong) = (dong, truoc);
        }

        return duc;
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
