using System.Text.Json.Serialization;

namespace DataNro;

// Các lớp dưới đây chỉ để đổ ra JSON. Tên trường và THỨ TỰ khai báo cố ý chép đúng
// định dạng DataNRO đang phổ biến, để ai đang đọc dữ liệu của họ đổi sang nguồn này là
// chạy được ngay, không phải sửa bộ đọc.

public class MapRa
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
}

public class ItemTemplateRa
{
    [JsonPropertyName("isUpToUp")] public bool IsUpToUp { get; set; }
    [JsonPropertyName("id")] public short Id { get; set; }
    [JsonPropertyName("type")] public sbyte Type { get; set; }
    [JsonPropertyName("gender")] public sbyte Gender { get; set; }
    [JsonPropertyName("level")] public sbyte Level { get; set; }
    [JsonPropertyName("strRequire")] public int StrRequire { get; set; }
    [JsonPropertyName("iconID")] public short IconID { get; set; }
    [JsonPropertyName("part")] public short Part { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("description")] public string Description { get; set; }
}

public class ItemOptionTemplateRa
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("type")] public int Type { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
}

public class MobTemplateRa
{
    [JsonPropertyName("mobTemplateId")] public int MobTemplateId { get; set; }
    [JsonPropertyName("rangeMove")] public sbyte RangeMove { get; set; }
    [JsonPropertyName("speed")] public sbyte Speed { get; set; }
    [JsonPropertyName("type")] public sbyte Type { get; set; }
    [JsonPropertyName("dartType")] public sbyte DartType { get; set; }
    [JsonPropertyName("hp")] public long Hp { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
}

public class NpcTemplateRa
{
    [JsonPropertyName("npcTemplateId")] public int NpcTemplateId { get; set; }
    [JsonPropertyName("headId")] public short HeadId { get; set; }
    [JsonPropertyName("bodyId")] public short BodyId { get; set; }
    [JsonPropertyName("legId")] public short LegId { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("menu")] public string[][] Menu { get; set; }
}

public class SkillRa
{
    [JsonPropertyName("point")] public sbyte Point { get; set; }
    [JsonPropertyName("maxFight")] public sbyte MaxFight { get; set; }
    [JsonPropertyName("manaUse")] public long ManaUse { get; set; }
    [JsonPropertyName("skillId")] public short SkillId { get; set; }
    [JsonPropertyName("dx")] public int Dx { get; set; }
    [JsonPropertyName("dy")] public int Dy { get; set; }
    [JsonPropertyName("damage")] public short Damage { get; set; }
    [JsonPropertyName("price")] public short Price { get; set; }
    [JsonPropertyName("coolDown")] public long CoolDown { get; set; }
    [JsonPropertyName("powRequire")] public long PowRequire { get; set; }
    [JsonPropertyName("moreInfo")] public string MoreInfo { get; set; }
}

public class SkillTemplateRa
{
    [JsonPropertyName("id")] public sbyte Id { get; set; }
    [JsonPropertyName("maxPoint")] public int MaxPoint { get; set; }
    [JsonPropertyName("manaUseType")] public int ManaUseType { get; set; }
    [JsonPropertyName("type")] public int Type { get; set; }
    [JsonPropertyName("iconId")] public short IconId { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("description")] public string Description { get; set; }
    [JsonPropertyName("damInfo")] public string DamInfo { get; set; }
    [JsonPropertyName("skills")] public SkillRa[] Skills { get; set; }
}

public class NClassRa
{
    [JsonPropertyName("classId")] public int ClassId { get; set; }

    /// <summary>Tên hành tinh như client hiển thị. Xem <see cref="TenHanhTinh"/>.</summary>
    [JsonPropertyName("name")] public string Name { get; set; }

    /// <summary>
    /// Tên máy chủ thật sự gửi xuống. Trường riêng của ta, DataNRO không có.
    ///
    /// <para>
    /// Giữ lại vì nó là bằng chứng cho chuyện dưới đây, mà cũng là chỗ duy nhất còn tên
    /// của bốn lớp thừa 3..6.
    /// </para>
    /// </summary>
    [JsonPropertyName("serverName")] public string ServerName { get; set; }

    [JsonPropertyName("skillTemplates")] public SkillTemplateRa[] SkillTemplates { get; set; }

    /// <summary>
    /// Ngọc Rồng là bản rẽ nhánh của Ninja School và <b>bảng tên lớp chưa đổi bao giờ</b>:
    /// máy chủ gửi xuống nguyên "Chưa vào lớp / Ninja Kiếm / Ninja Phi Tiêu / Ninja Kunai /
    /// Ninja Cung / Ninja Đao / Ninja Quạt" (đã giải mã tay gói 7 để chắc, hết sạch byte
    /// không dư). Client không dùng tên đó mà lấy từ chuỗi trong máy -
    /// <c>mResources.MENUGENDER</c>, xem <c>T1.cs</c> của bản giải nén.
    /// </summary>
    private static readonly string[] MenuGender = { "Trái đất", "Namếc", "Xayda" };

    /// <summary>Bản của clientType 7, cũng lấy từ <c>T1.cs</c>.</summary>
    private static readonly string[] MenuGenderRong = { "Rồng đất", "Rồng xanh", "Rồng đỏ" };

    /// <summary>
    /// Tên hiển thị của lớp. Ba lớp thật lấy tên client, còn lại giữ tên máy chủ - bốn lớp
    /// 3..6 là rác Ninja School còn sót, client không có tên nào cho chúng cả.
    /// </summary>
    public static string TenHanhTinh(int classId, string tenMayChu, int clientType)
    {
        var bang = clientType == 7 ? MenuGenderRong : MenuGender;
        return classId >= 0 && classId < bang.Length ? bang[classId] : tenMayChu;
    }
}

/// <summary>Bảng tên option kĩ năng. DataNRO không xuất bảng này, ta có sẵn nên xuất luôn.</summary>
public class SkillOptionTemplateRa
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
}

/// <summary>Một khung của part. Chép đúng định dạng <c>Parts.json</c> của DataNRO.</summary>
public class PartImageRa
{
    [JsonPropertyName("id")] public short Id { get; set; }
    [JsonPropertyName("dx")] public sbyte Dx { get; set; }
    [JsonPropertyName("dy")] public sbyte Dy { get; set; }
}

public class PartRa
{
    [JsonPropertyName("type")] public int Type { get; set; }
    [JsonPropertyName("pi")] public PartImageRa[] Pi { get; set; }
}
