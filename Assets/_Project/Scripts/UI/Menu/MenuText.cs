using ThreeKingdoms.Core.Battle;

namespace ThreeKingdoms.UI.Menu
{
    // Town main menu strings (simplified Chinese). ADR-0007 replaces this with the localisation tables.
    public static class MenuText
    {
        public const string Title = "主菜单";
        public const string Generals = "武将", Items = "物品", Settings = "设置", Close = "关闭菜单";
        public const string GeneralsHint = "查看已入队武将的等级、属性、技能与装备。";
        public const string SettingsHint = "测试版暂无可调整的设置项，可在此返回标题或退出游戏。";
        public const string ToTitle = "返回标题", Quit = "退出游戏", Back2 = "返回";
        public const string ToTitleHint = "回到标题画面。未存档的进度会丢失，可在酒馆掌柜处存档。";
        public const string QuitHint = "退出游戏。未存档的进度会丢失。";
        public const string SkillsHeader = "技能", GearHeader = "装备";
        public const string BagEmpty = "背包里没有未装备的物品。";
        public const string Back = "[菜单／取消] 关闭";

        public static string Chapter(string chapter) => chapter;

        public static string Exp(int exp, int? next) => next is int n ? $"经验 {exp}／升级需 {n}" : $"经验 {exp}（已达等级上限）";

        public static string Attributes(UnitStats s) => $"武力 {s.Force}　统率 {s.Command}　智力 {s.Intellect}";

        public static string Skill(string name, int cost, string description, bool learned, int level) =>
            learned ? $"{name}（技力 {cost}）：{description}" : $"{name}（{level} 级习得）";
        public static string Reputation(int value) => $"声望 {value}";

        // The pause menu and the general's page (user 2026-10-22).
        public const string EmptySlot = "此栏位尚未装备。", Candidates = "可换装备";
        public static string TabName(MainMenuHud.Tab t) => t switch
        {
            MainMenuHud.Tab.Items => "物品", MainMenuHud.Tab.Equip => "装备", MainMenuHud.Tab.Skills => "技能",
            MainMenuHud.Tab.Generals => "武将", _ => "系统",
        };
        public static string InfoTabName(MainMenuHud.InfoTab t) => t switch
        {
            MainMenuHud.InfoTab.Status => "状态", MainMenuHud.InfoTab.Skills => "技能", MainMenuHud.InfoTab.Gear => "装备", _ => "列传",
        };
        public static string CourtesyName(string id) => id switch { "liu_bei" => "玄德", "guan_yu" => "云长", "zhang_fei" => "翼德", _ => "" };

        // Who this troop type beats and loses to (V1_SPEC §5: ×1.4 once, no reverse penalty).
        public static string Counter(TroopType t) => t switch
        {
            TroopType.Archer => "弓兵克骑兵，被步兵所克。占优时伤害 ×1.4。",
            TroopType.Cavalry => "骑兵克步兵与黄巾军，被弓兵所克。占优时伤害 ×1.4。",
            TroopType.Infantry => "步兵克弓兵，被骑兵所克。占优时伤害 ×1.4。",
            _ => "",
        };

        // 列传: short lives after 《三国演义》第一回 (paraphrased).
        public static string Biography(string id) => id switch
        {
            "liu_bei" => "字玄德，涿郡涿县人，自称汉中山靖王刘胜之后。少孤，与母贩屦织席为业。为人宽和，寡言语，喜怒不形于色，素有大志，专好结交天下豪杰。黄巾作乱，见榜慨然长叹，遂与关羽、张飞结义桃园，共举义兵。",
            "guan_yu" => "字云长，河东解良人。因乡里豪强倚势凌人，杀之，逃难江湖数年。闻涿郡招军破贼，特来应募，酒店中与刘备、张飞相遇，结为兄弟。所使青龙偃月刀，重八十二斤。",
            "zhang_fei" => "字翼德，涿郡人。颇有庄田，卖酒屠猪，专好结交天下豪杰。豹头环眼，燕颔虎须，声若巨雷，势如奔马。见刘备在榜前长叹，厉声相问，遂共举大事。使一条丈八蛇矛。",
            _ => "",
        };
    }
}
