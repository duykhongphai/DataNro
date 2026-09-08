namespace DataNro.GiaoThuc;

/// <summary>
/// Kho bảng mẫu của một phiên: vật phẩm, quái, NPC, kĩ năng, tên map.
///
/// <para>
/// Cố ý <b>không có bộ đệm ra đĩa</b>. Client thật lưu bảng mẫu vào máy rồi so phiên bản với
/// máy chủ để khỏi tải lại; công cụ này thì ngược lại - lần nào cũng muốn bản mới nhất. Bỏ
/// đệm đi thì phiên bản phía ta luôn là -1, khác phiên bản máy chủ, nên máy chủ luôn gửi lại
/// đầy đủ. Vừa gọn mã, vừa tránh hẳn cái bẫy dump nhầm dữ liệu của máy chủ khác còn sót
/// trong đệm.
/// </para>
/// </summary>
public class GameData
{
    // Phiên bản phía ta (vc*) luôn -1, phía máy chủ (vs*) đọc từ gói đăng nhập.
    public sbyte vcData = -1, vcMap = -1, vcSkill = -1, vcItem = -1;
    public sbyte vsData = -1, vsMap = -1, vsSkill = -1, vsItem = -1;

    /// <summary>Bảng mẫu vật phẩm, khoá là id mẫu.</summary>
    public readonly Dictionary<int, ItemTemplate> itemTemplates = new();

    public ItemOptionTemplate[] iOptionTemplates = Array.Empty<ItemOptionTemplate>();
    public SkillOptionTemplate[] sOptionTemplates = Array.Empty<SkillOptionTemplate>();

    public MobTemplate[] arrMobTemplate = Array.Empty<MobTemplate>();
    public NpcTemplate[] arrNpcTemplate = Array.Empty<NpcTemplate>();
    public NClass[] nClasss = Array.Empty<NClass>();

    public string[] mapNames = Array.Empty<string>();

    public void AddItem(ItemTemplate t) => itemTemplates[t.id] = t;

    /// <summary>Đã nhận đủ bốn nhóm dữ liệu chưa - điều kiện gửi CLIENT_OK.</summary>
    public bool AllLoaded =>
        vsData == vcData && vsMap == vcMap && vsSkill == vcSkill && vsItem == vcItem;

    /// <summary>Đủ mọi bảng cần cho việc xuất dữ liệu chưa.</summary>
    public bool DaDayDu =>
        mapNames.Length > 0 &&
        itemTemplates.Count > 0 &&
        arrMobTemplate.Length > 0 &&
        arrNpcTemplate.Length > 0 &&
        nClasss.Length > 0;

    public override string ToString() =>
        $"item={itemTemplates.Count} mob={arrMobTemplate.Length} npc={arrNpcTemplate.Length} " +
        $"class={nClasss.Length} map={mapNames.Length}";
}
