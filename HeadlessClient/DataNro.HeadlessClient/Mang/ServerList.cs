using System.Net.Http;
using System.Text;

namespace DataNro.Mang;

/// <summary>
/// Một máy chủ trong danh sách, đúng bố cục bản gốc gửi về:
/// <c>Tên:địa chỉ:cổng:ngôn ngữ:loại:mới</c>.
/// </summary>
public class ServerInfo
{
    /// <summary>Thứ tự trong danh sách, chính là chỉ số <c>ipSelect</c> bản gốc.</summary>
    public int Index { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }

    /// <summary>Ngôn ngữ của máy chủ (trường thứ tư).</summary>
    public sbyte Language { get; set; }

    /// <summary>Loại máy chủ (trường thứ năm) - bản gốc dùng để lọc máy chủ đặc biệt.</summary>
    public sbyte TypeSv { get; set; }

    /// <summary>Máy chủ mới mở (trường thứ sáu).</summary>
    public sbyte IsNew { get; set; }

    public string Display => $"{Name}  ({Host}:{Port})";

    public override string ToString() => Name;
}

/// <summary>Kết quả một lần đọc danh sách máy chủ.</summary>
public class ServerListResult
{
    public List<ServerInfo> Servers { get; } = new();

    /// <summary>Ngôn ngữ đọc từ phần tử áp chót của chuỗi.</summary>
    public sbyte Language { get; set; }

    /// <summary>Máy chủ ưu tiên đọc từ phần tử cuối.</summary>
    public sbyte Priority { get; set; }

    /// <summary>Danh sách lấy được từ mạng hay phải rơi về bản dự phòng.</summary>
    public bool FromNetwork { get; set; }

    public int Count => Servers.Count;
}

/// <summary>
/// Tải và phân tích danh sách máy chủ.
/// Làm y hệt <c>ServerListScreen.getServerList</c> của client gốc: chuỗi cắt theo dấu phẩy,
/// hai phần tử cuối là ngôn ngữ và máy chủ ưu tiên, mỗi phần tử còn lại cắt tiếp theo
/// dấu hai chấm thành <c>tên:địa chỉ:cổng:ngôn ngữ:loại:mới</c>.
/// <para>
/// Khác bản gốc ở chỗ không giữ trạng thái tĩnh: mỗi lần đọc trả về một
/// <see cref="ServerListResult"/> riêng, nhiều công cụ / nhiều phiên gọi song song vẫn đúng.
/// </para>
/// </summary>
public static class ServerList
{
    /// <summary>Đường lấy danh sách, chính là hằng <c>linkGetHost</c> bên client.</summary>
    public const string LinkGetHost = "http://112.213.94.23/mod/server_extra.php";

    /// <summary>Danh sách dự phòng khi không tải được, chính là hằng <c>javaVN</c> bên client.</summary>
    public const string LinkDefault =
        "Vũ trụ 1:112.213.94.23:14445:0:0:0,Vũ trụ 2:210.211.109.199:14445:0:0:0,Vũ trụ 3:112.213.85.88:14445:0:0:0," +
        "Vũ trụ 4:27.0.12.164:14445:0:0:0,Vũ trụ 5:27.0.12.16:14445:0:0:0,Vũ trụ 6:27.0.12.173:14445:0:0:0," +
        "Vũ trụ 7:112.213.94.223:14445:0:0:0,Vũ trụ 8:27.0.14.66:14446:0:0:0,Vũ trụ 9:27.0.14.66:14447:0:0:0," +
        "Vũ trụ 10:27.0.14.66:14445:0:0:0,Vũ trụ 11:112.213.85.35:14445:0:0:0,Võ đài liên vũ trụ:27.0.12.173:20000:0:0:0," +
        "Universe 1:52.74.230.22:14445:1:0:0,Naga:52.74.230.22:14446:2:0:0,0,0";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    /// <summary>
    /// Tải danh sách từ mạng. Hỏng mạng hoặc chuỗi trả về không đọc được thì rơi về
    /// <see cref="LinkDefault"/> - đúng cách client gốc xử lý, nên hàm này không ném lỗi.
    /// </summary>
    /// <param name="url">Đường lấy danh sách, bỏ trống thì dùng <see cref="LinkGetHost"/>.</param>
    public static async Task<ServerListResult> LoadAsync(string url = null, CancellationToken ct = default)
    {
        try
        {
            // Trang trả về chữ có dấu nhưng thường không khai báo charset, nên tự giải mã UTF-8.
            byte[] raw = await Http.GetByteArrayAsync(url ?? LinkGetHost, ct).ConfigureAwait(false);
            var result = Parse(Encoding.UTF8.GetString(raw));
            if (result.Count > 0)
            {
                result.FromNetwork = true;
                return result;
            }
        }
        catch (Exception)
        {
            // im lặng rơi về danh sách dự phòng
        }

        return Parse(LinkDefault);
    }

    /// <summary>Phân tích chuỗi danh sách máy chủ. Chuỗi hỏng thì trả về kết quả rỗng.</summary>
    public static ServerListResult Parse(string str)
    {
        var result = new ServerListResult();
        if (string.IsNullOrWhiteSpace(str)) return result;

        string[] parts = str.Trim().Split(',');
        if (parts.Length < 3) return result;

        // hai phần tử cuối không phải máy chủ
        if (sbyte.TryParse(parts[parts.Length - 2].Trim(), out sbyte lang)) result.Language = lang;
        if (sbyte.TryParse(parts[parts.Length - 1].Trim(), out sbyte priority)) result.Priority = priority;

        for (int i = 0; i < parts.Length - 2; i++)
        {
            string[] f = parts[i].Trim().Split(':');
            if (f.Length < 3) continue;
            if (!short.TryParse(f[2].Trim(), out short port)) continue;

            var sv = new ServerInfo
            {
                Index = result.Servers.Count,
                Name = f[0].Trim(),
                Host = f[1].Trim(),
                Port = port
            };
            if (f.Length > 3 && sbyte.TryParse(f[3].Trim(), out sbyte l)) sv.Language = l;
            if (f.Length > 4 && sbyte.TryParse(f[4].Trim(), out sbyte t)) sv.TypeSv = t;
            if (f.Length > 5 && sbyte.TryParse(f[5].Trim(), out sbyte n)) sv.IsNew = n;
            result.Servers.Add(sv);
        }

        return result;
    }
}
