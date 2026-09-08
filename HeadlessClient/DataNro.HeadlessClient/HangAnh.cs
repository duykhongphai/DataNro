namespace DataNro;

/// <summary>
/// Hàng chờ id ảnh dùng chung cho nhiều thợ.
///
/// <para>
/// Xoay vòng chứ không nằm lại đầu hàng: máy chủ im lặng với cả id không có ảnh lẫn id bị cắt
/// vì quá hạn mức, mà đám không có ảnh thì im mãi mãi - để chúng ở đầu là mỗi lượt lại hỏi
/// đúng chúng, dồn dần cho tới khi chiếm hết cả lô và không id mới nào được hỏi nữa.
/// </para>
/// </summary>
public sealed class HangAnh
{
    private readonly object khoa = new();
    private readonly Queue<(int id, int soLanHoi)> hang;

    public HangAnh(IEnumerable<int> ids)
    {
        hang = new Queue<(int, int)>(ids.Select(id => (id, 0)));
    }

    public int Con
    {
        get
        {
            lock (khoa) return hang.Count;
        }
    }

    /// <summary>Rút tối đa <paramref name="soLuong"/> mục ra khỏi hàng.</summary>
    public List<(int id, int soLanHoi)> Lay(int soLuong)
    {
        var lo = new List<(int, int)>(soLuong);
        lock (khoa)
            while (lo.Count < soLuong && hang.Count > 0)
                lo.Add(hang.Dequeue());
        return lo;
    }

    /// <summary>Trả một id về cuối hàng để thợ khác hỏi lại.</summary>
    public void Tra(int id, int soLanHoi)
    {
        lock (khoa) hang.Enqueue((id, soLanHoi));
    }
}
