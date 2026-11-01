using System.Collections.Generic;
using System.Linq;
using ThreeKingdoms.Core.Story;

namespace ThreeKingdoms.Gameplay.Story
{
    // The keepsake shelf (信物栏, design/gdd/keepsake-shelf.md): chapter one's side-quest keepsakes after the NPC bible §6.
    // Each is bound to one person, has no use or value, and is held when its flag keepsake.<id> is set (SideQuests.Keepsake).
    // Only InGame entries are shown and counted; the rest wait for their quests (extension layer, Zhending).
    public static class KeepsakeCatalog
    {
        public sealed class Entry
        {
            public string Id, Name, Who, WhoName, Place, Year, Origin, Description;
            public bool InGame;
            public string Flag => SideQuests.KeepsakePrefix + Id;
            public string Icon => "keepsake_" + Id;
        }

        static Entry E(string id, string name, string who, string whoName, string place, string year, string origin, string description, bool inGame = true) =>
            new Entry { Id = id, Name = name, Who = who, WhoName = whoName, Place = place, Year = year, Origin = origin, Description = description, InGame = inGame };

        public static readonly Entry[] All =
        {
            E("hu_horse_tally", "胡市马契", "kumo", "库莫", "蓟城胡市", "初平元年（190）",
                "库莫换马时掰开的木契，另一半在他手里。", "半片刻木契，刻痕里还嵌着马厩的土。"),
            E("shijing_copy", "石经摹本", "lu_zhi", "卢植", "涿县卢宅", "初平三年（192）",
                "太学老博士在洛阳交到手里，没有给卢毓，留在了身边。", "影写的熹平石经，纸边被雨水泡起了毛。"),
            E("zang_hong_draft", "臧洪盟辞底稿", "zang_hong", "臧洪", "酸枣大营", "初平元年（190）",
                "坛上那份烧给天地了，这份留给听完的人。", "竹简三片，墨迹在“必无二志”四字上洇开了一点。"),
            E("bao_tao_arrow", "鲍韬的箭", "bao_xin", "鲍信", "汴水之后", "初平元年（190）",
                "鲍信从弟弟的箭囊里抽出一支，没说话，递了过来。", "白羽箭，箭杆近尾缠着一道红线。"),
            E("a_he_banner", "白马残旗", "a_he", "阿禾", "平原", "初平三年（192）",
                "界桥上没人扛的旗，阿禾扛了回来：“你收着。我没地方放。”", "白绢旗角，烧去一半，边上缝着一匹白马。"),
            E("lu_ditou_clappers", "夯歌竹板", "lu_ditou", "鲁堤头", "平原·千丈之堤", "初平二年（191）",
                "老鲁说：板子给你，歌留在堤上。", "两片竹板，领唱时敲拍子，竹面磨得发亮。"),
            E("gu_sou_drumsticks", "说书人的鼓槌", "gu_sou", "鼓叟", "下邳市口", "兴平元年（194）",
                "说了四十年书，头一回说到正主脸上，散场后把鼓槌塞了过来。", "一对枣木鼓槌，握手处被汗浸得发黑。"),
            E("zhang_shiping_ledger", "马商的旧账簿", "zhang_shiping", "张世平", "下邳糜家商号", "兴平元年（194）",
                "从涿郡那年起，他借给刘备的每一匹马都记在这里。", "旧账簿，第一页写着“中山张、苏，赠涿县刘君马”。"),
            E("pan_daqiao_tags", "竹名牌", "pan_daqiao", "潘大樵", "下邳营中", "兴平元年（194）",
                "刘备问过每个丹杨兵的名字，潘大樵把这一什的名字都刻了下来。", "十枚竹片穿一根绳，每片刻一个名字。"),
            E("chu_weng_mat", "旧蒲席", "chu_weng", "褚翁", "下邳城外", "兴平元年（194）",
                "浴佛日铺在路边的那一张，是褚翁自己编的。", "一领蒲席，边上破了，压边压得松。"),

            E("fengsu_copy", "《风俗通》草稿抄本", "ying_shao", "应劭", "", "", "", "", false),
            E("tian_chou_notes", "北山路记", "tian_chou", "田畴", "", "", "", "", false),
            E("liuwan_mat", "柳湾旧苇席", "ge_weng", "葛翁", "", "", "", "", false),
            E("zhen_grain_tally", "甄家谷筹", "zhen", "甄氏", "", "", "", "", false),
            E("heishan_tag", "黑山木腰牌", "wu_dou", "伍斗", "", "", "", "", false),
            E("talisman_paper", "包药的旧符纸", "ge_niang", "葛娘", "", "", "", "", false),
            E("hebo_notice", "河伯帖", "zhang_boatman", "漳水船家", "", "", "", "", false),
            E("yamen_ring", "牙门旗铜环", "dian_wei", "典韦", "", "", "", "", false),
            E("changle_tile", "长乐未央瓦当", "bu_shen", "卜慎", "", "", "", "", false),
            E("bao_xin_whip", "鲍信的马鞭", "fan_qi", "樊七", "", "", "", "", false),
            E("zhegou_roll", "柘沟名册副本", "gan_po", "甘婆", "", "", "", "", false),
            E("wuqin_scroll", "五禽图", "hua_tuo", "华佗", "", "", "", "", false),
        };

        public static IEnumerable<Entry> Shown => All.Where(e => e.InGame);

        public static Entry Find(string id) => All.FirstOrDefault(e => e.Id == id);

        // R1's two keepsakes came before the shelf: the horse tally is the hu_market flag, the rubbing is Lu Yu's memory.
        public static bool Held(ChapterState ch, Entry e)
        {
            if (ch == null || e == null) return false;
            if (ch.Has(e.Flag)) return true;
            if (e.Id == "hu_horse_tally") return ch.Has(FieldScriptsR1.HuMarket);
            if (e.Id == "shijing_copy") return ch.Recall("lu_yu", "rubbing") == "kept_rubbing";
            return false;
        }

        public static int HeldCount(ChapterState ch) => Shown.Count(e => Held(ch, e));

        // The flag that just went up, if it is a keepsake on the shelf.
        public static Entry FromFlag(string flag) =>
            flag != null && flag.StartsWith(SideQuests.KeepsakePrefix, System.StringComparison.Ordinal)
                ? Shown.FirstOrDefault(e => e.Flag == flag) : null;
    }
}
