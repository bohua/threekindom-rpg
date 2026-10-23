using System;
using System.Globalization;
using ThreeKingdoms.Save;

namespace ThreeKingdoms.UI.Save
{
    // Save, load and title strings (simplified Chinese). ADR-0007 replaces this with the localisation tables.
    public static class SaveText
    {
        public const string SaveTitle = "存档";
        public const string Greeting = "客官要歇歇脚？行囊交给小店，记下这一路的进度吧。";
        public const string Auto = "战前自动存档";
        public const string Empty = "（空）";
        public const string Damaged = "存档损坏，无法读取。原文件已保留，未作改动。";
        public const string Unsupported = "存档版本不支持，无法读取。原文件已保留，未作改动。";
        public const string Overwrite = "覆盖", Cancel = "取消";
        public const string Back = "[取消] 返回";

        public const string GameTitle = "涿郡起兵";
        public const string Subtitle = "三国 HD-2D（暂名）　Alpha 0.1 测试版";
        public const string NewGame = "新的游戏", Load = "读取存档", Quit = "退出游戏";
        public const string LoadTitle = "读取存档";
        public const string NoSaves = "还没有任何存档。";

        public static string Slot(int index) => $"存档 {index}";

        public static string Place(string location) => location == SaveData.TownLocation ? "涿郡・酒馆"
            : location != null && location.StartsWith("battle_") ? $"第{location.Substring(7)}战　战前准备" : location;

        // Slot summary (save-system answer 4): chapter, place, Liu Bei's level, copper, time saved.
        public static string Summary(SaveData d)
        {
            string time = DateTime.TryParse(d.savedAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t.ToString("yyyy-MM-dd HH:mm") : d.savedAt;
            return $"{d.chapter}　{Place(d.location)}\n刘备 {d.leaderLevel} 级　铜钱 {d.copper}　{time}";
        }

        public static string Status(SaveReadStatus s) => s switch
        {
            SaveReadStatus.Damaged => Damaged,
            SaveReadStatus.Unsupported => Unsupported,
            _ => Empty,
        };

        public static string ConfirmOverwrite(int index) => $"覆盖{Slot(index)}？原有进度将被替换。";
        public static string Saved(int index) => $"已保存到{Slot(index)}。";
        public static string SaveFailed(string reason) => $"保存失败：{reason}";
        public static string LoadFailed(string reason) => $"无法读取：存档内容与当前游戏不符（{reason}）。原文件已保留。";
    }
}
