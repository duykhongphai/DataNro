namespace DataNro.GiaoThuc;

/// <summary>Một cấp độ của kĩ năng (createSkill).</summary>
public class Skill
{
    public short skillId;
    public SkillTemplate template;
    public sbyte point;
    public long powRequire;
    public long manaUse;
    public long coolDown;
    public int dx, dy;
    public short damage;

    /// <summary>Số mục tiêu tối đa một lần đánh (client dùng để vẽ).</summary>
    public sbyte maxFight;

    /// <summary>Giá học cấp này.</summary>
    public short price;

    /// <summary>Ghi chú "học ở đâu" máy chủ gửi kèm.</summary>
    public string moreInfo;

    public override string ToString() =>
        $"[{skillId}] {template?.name} lv{point} mana={manaUse} cd={coolDown}ms";
}
