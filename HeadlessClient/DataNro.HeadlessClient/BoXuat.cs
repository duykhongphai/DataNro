using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using DataNro.GiaoThuc;

namespace DataNro;

/// <summary>Đổ <see cref="GameData"/> của một phiên ra bộ tệp JSON.</summary>
public static class BoXuat
{
    /// <summary>
    /// Chữ Việt phải để nguyên, không cho bộ mã hoá mặc định đổi thành \uXXXX - dữ liệu
    /// người khác đọc bằng mắt, mà escape xong tệp còn phình gấp rưỡi.
    /// </summary>
    private static readonly JsonSerializerOptions Dep = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly JsonSerializerOptions Gon = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>Ghi ra <c>&lt;ra&gt;/&lt;nhà phát hành&gt;/&lt;thư mục&gt;/</c>. Trả về đường dẫn thư mục đó.</summary>
    public static string Ghi(GameData d, CauHinh c, int clientType)
    {
        var thuMuc = Path.Combine(c.Ra, c.NhaPhatHanh, c.TenThuMuc);
        Directory.CreateDirectory(thuMuc);

        var maps = Maps(d);

        GhiTep(thuMuc, "Maps.json", maps);
        GhiTep(thuMuc, "ItemTemplates.json", Items(d));
        GhiTep(thuMuc, "ItemOptionTemplates.json", ItemOptions(d));
        GhiTep(thuMuc, "MobTemplates.json", Mobs(d));
        GhiTep(thuMuc, "NpcTemplates.json", Npcs(d));
        GhiTep(thuMuc, "NClasses.json", Classes(d, clientType));
        GhiTep(thuMuc, "SkillOptionTemplates.json", SkillOptions(d));
        if (d.parts.Length > 0) GhiTep(thuMuc, "Parts.json", Parts(d));

        // Mốc thời gian không có xuống dòng cuối, đúng như bản của DataNRO.
        File.WriteAllText(Path.Combine(thuMuc, "LastUpdated"),
            DateTime.UtcNow.ToString("o"));

        // map.json ở gốc: sáu tool và XmapData đang trỏ thẳng vào đây, giữ nguyên định dạng
        // một dòng như tệp cũ để bản cập nhật đầu tiên không đẻ ra một diff toàn tệp.
        if (c.GhiMapJsonGoc)
        {
            Directory.CreateDirectory(c.Ra);
            File.WriteAllText(Path.Combine(c.Ra, "map.json"), JsonSerializer.Serialize(maps, Gon) + "\n");
        }

        return thuMuc;
    }

    private static void GhiTep<T>(string thuMuc, string ten, T noiDung)
    {
        var duong = Path.Combine(thuMuc, ten);
        File.WriteAllText(duong, JsonSerializer.Serialize(noiDung, Dep) + "\n");
    }

    // ==================== chuyển đổi ====================

    private static List<MapRa> Maps(GameData d)
    {
        var ra = new List<MapRa>(d.mapNames.Length);
        for (var i = 0; i < d.mapNames.Length; i++)
            ra.Add(new MapRa { Id = i, Name = d.mapNames[i] });
        return ra;
    }

    private static List<ItemTemplateRa> Items(GameData d) =>
        d.itemTemplates.Values
            .OrderBy(t => t.id)
            .Select(t => new ItemTemplateRa
            {
                IsUpToUp = t.isUpToUp,
                Id = t.id,
                Type = t.type,
                Gender = t.gender,
                Level = t.level,
                StrRequire = t.strRequire,
                IconID = t.iconID,
                Part = t.part,
                Name = t.name,
                Description = t.description
            })
            .ToList();

    private static List<ItemOptionTemplateRa> ItemOptions(GameData d) =>
        d.iOptionTemplates
            .Select(t => new ItemOptionTemplateRa { Id = t.id, Type = t.type, Name = t.name })
            .ToList();

    private static List<MobTemplateRa> Mobs(GameData d) =>
        d.arrMobTemplate
            .Where(t => t != null)
            .Select(t => new MobTemplateRa
            {
                MobTemplateId = t.mobTemplateId,
                RangeMove = t.rangeMove,
                Speed = t.speed,
                Type = t.type,
                DartType = t.dartType,
                Hp = t.hp,
                Name = t.name
            })
            .ToList();

    private static List<NpcTemplateRa> Npcs(GameData d) =>
        d.arrNpcTemplate
            .Where(t => t != null)
            .Select(t => new NpcTemplateRa
            {
                NpcTemplateId = t.npcTemplateId,
                HeadId = t.headId,
                BodyId = t.bodyId,
                LegId = t.legId,
                Name = t.name,
                Menu = t.menu
            })
            .ToList();

    private static List<NClassRa> Classes(GameData d, int clientType) =>
        d.nClasss
            .Where(c => c != null)
            .Select(c => new NClassRa
            {
                ClassId = c.classId,
                Name = NClassRa.TenHanhTinh(c.classId, c.name, clientType),
                ServerName = c.name,
                SkillTemplates = c.skillTemplates.Select(st => new SkillTemplateRa
                {
                    Id = st.id,
                    MaxPoint = st.maxPoint,
                    ManaUseType = st.manaUseType,
                    Type = st.type,
                    IconId = st.iconId,
                    Name = st.name,
                    Description = st.description,
                    DamInfo = st.damInfo,
                    Skills = st.skills.Select(sk => new SkillRa
                    {
                        Point = sk.point,
                        MaxFight = sk.maxFight,
                        ManaUse = sk.manaUse,
                        SkillId = sk.skillId,
                        Dx = sk.dx,
                        Dy = sk.dy,
                        Damage = sk.damage,
                        Price = sk.price,
                        CoolDown = sk.coolDown,
                        PowRequire = sk.powRequire,
                        MoreInfo = sk.moreInfo
                    }).ToArray()
                }).ToArray()
            })
            .ToList();

    private static List<PartRa> Parts(GameData d) =>
        d.parts
            .Where(p => p != null)
            .Select(p => new PartRa
            {
                Type = p.type,
                Pi = p.pi.Select(x => new PartImageRa { Id = x.id, Dx = x.dx, Dy = x.dy }).ToArray()
            })
            .ToList();

    private static List<SkillOptionTemplateRa> SkillOptions(GameData d) =>
        d.sOptionTemplates
            .Select(t => new SkillOptionTemplateRa { Id = t.id, Name = t.name })
            .ToList();
}
