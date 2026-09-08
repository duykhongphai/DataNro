namespace DataNro.GiaoThuc;

/// <summary>
/// Bố cục ô của một map: lưới <see cref="rong"/> x <see cref="cao"/>, mỗi ô là chỉ số ảnh
/// trong bộ tile của map (0 là trống).
///
/// <para>
/// Đây đúng là thứ client nạp vào <c>TileMap.maps</c> để vẽ nền. Muốn ra hình thật thì còn
/// cần <c>tileID</c> - số hiệu bộ tile - mà máy chủ chỉ gửi kèm gói vào map, không có trong
/// gói này.
/// </para>
/// </summary>
public class MauMap
{
    public int mapId;
    public int rong, cao;
    public int[] o = Array.Empty<int>();
}
