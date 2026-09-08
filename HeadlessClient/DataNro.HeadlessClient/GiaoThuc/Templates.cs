namespace DataNro.GiaoThuc;

/// <summary>
/// Bảng mẫu vật phẩm. Đọc từ gói UPDATE_ITEM (messageNotMap sub 8 / cmd 12 sub 0),
/// tương ứng <c>Controller.loadItemNew</c> với type = 1 bên bản gốc.
/// </summary>
public class ItemTemplate
{
    public short id;
    public sbyte type;
    public sbyte gender;
    public string name;
    public string description;
    public sbyte level;
    public int strRequire;
    public short iconID;
    public bool isUpToUp;

    /// <summary>Part ngoại hình khi mặc (-1 là không đổi hình). Chỉ dùng để xuất dữ liệu.</summary>
    public short part = -1;

    public ItemTemplate(short id, sbyte type, sbyte gender, string name, string description, sbyte level,
        int strRequire, short iconID, bool isUpToUp)
    {
        this.id = id;
        this.type = type;
        this.gender = gender;
        this.name = name;
        this.description = description;
        this.level = level;
        this.strRequire = strRequire;
        this.iconID = iconID;
        this.isUpToUp = isUpToUp;
    }

    public override string ToString() => $"[{id}] {name}";
}

/// <summary>Mẫu option của vật phẩm (loadItemNew type = 0).</summary>
public class ItemOptionTemplate
{
    public int id;
    public string name;
    public int type;
    public int color;
}

/// <summary>Mẫu quái (createMap - phần cuối gói UPDATE_MAP).</summary>
public class MobTemplate
{
    public int mobTemplateId;
    public sbyte type;
    public string name;
    public long hp;

    /// <summary>Phạm vi đi lại của AI phía client.</summary>
    public sbyte rangeMove;

    /// <summary>Tốc độ đi của AI phía client.</summary>
    public sbyte speed;

    /// <summary>Loại phi tiêu hiển thị khi quái đánh xa.</summary>
    public sbyte dartType;

    public override string ToString() => $"[{mobTemplateId}] {name} hp={hp}";
}

/// <summary>Mẫu NPC (createMap).</summary>
public class NpcTemplate
{
    public int npcTemplateId;
    public string name;

    /// <summary>Ba part dựng hình NPC.</summary>
    public short headId, bodyId, legId;

    public string[][] menu = Array.Empty<string[]>();

    public override string ToString() => $"[{npcTemplateId}] {name}";
}

/// <summary>Mẫu kĩ năng (createSkill).</summary>
public class SkillTemplate
{
    public sbyte id;
    public string name;
    public int maxPoint;
    public int manaUseType;
    public int type;

    /// <summary>Ảnh của kĩ năng trong bảng icon.</summary>
    public short iconId;

    public string damInfo;
    public string description;
    public Skill[] skills = Array.Empty<Skill>();

    public bool isAttackSkill() => type == 1;
    public bool isBuffToPlayer() => type == 2;
    public bool isUseAlone() => type == 3;
    public bool isSkillSpec() => type == 4;

    public override string ToString() => $"[{id}] {name}";
}

/// <summary>Mẫu option kĩ năng (createSkill).</summary>
public class SkillOptionTemplate
{
    public int id;
    public string name;
}

/// <summary>Lớp nhân vật (Trái Đất / Namek / Xayda).</summary>
public class NClass
{
    public int classId;
    public string name;
    public SkillTemplate[] skillTemplates = Array.Empty<SkillTemplate>();

    public override string ToString() => $"[{classId}] {name}";
}
