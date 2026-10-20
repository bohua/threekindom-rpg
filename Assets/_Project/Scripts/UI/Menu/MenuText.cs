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
    }
}
