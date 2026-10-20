using System.Collections.Generic;
using ThreeKingdoms.Core.Story;

namespace ThreeKingdoms.UI.Story
{
    // All main-line text (simplified Chinese). Dialogue is 暂定: story-events GDD marks the lines 【待编写】, so these are
    // short placeholders in the spirit of Romance ch. 1 (meeting at the recruitment notice and the inn; the oath in the
    // peach garden), to be replaced by the written script. ADR-0007 moves this into localisation tables.
    public static class StoryText
    {
        static readonly Dictionary<string, string> Speakers = new Dictionary<string, string>
        {
            ["liu_bei"] = "刘备", ["guan_yu"] = "关羽", ["zhang_fei"] = "张飞", ["narrator"] = "", ["system"] = "",
        };

        static readonly Dictionary<string, string> Lines = new Dictionary<string, string>
        {
            ["guan.1"] = "此地黄巾将至，官府张榜募兵。阁下也是来投军的吗？",
            ["guan.2"] = "在下刘备，字玄德。愿聚义兵，保境安民。",
            ["guan.3"] = "某姓关名羽，字云长。若蒙不弃，愿随兄长同行。",
            ["guan.joined"] = "关羽加入了队伍。",
            ["zhang.1"] = "大丈夫不为国家出力，在榜前叹什么气？",
            ["zhang.2"] = "我有心破贼安民，只恨力量不足。",
            ["zhang.3"] = "俺张飞，字翼德，颇有些家财。愿与你共举大事！",
            ["zhang.joined"] = "张飞加入了队伍。",
            ["oath.1"] = "城西桃园，桃花正盛。三人备下香案。",
            ["oath.2"] = "今日在此结为兄弟，同心协力！",
            ["oath.3"] = "上报国家，下安黎庶。",
            ["oath.4"] = "不求同年同月同日生，但愿同年同月同日死。",
            ["oath.5"] = "桃园结义已成。三人整备兵马，准备出城迎敌。",
            ["gate:locked"] = "黄巾军就在城外。先在城里找到可以共事的豪杰，再出城迎敌。",
            ["gate:done"] = "城外的黄巾军已经退去。",
        };

        public static string Speaker(string key) => key != null && Speakers.TryGetValue(key, out var s) ? s : "";
        public static string Line(string key) => key != null && Lines.TryGetValue(key, out var s) ? s : key;

        static readonly Dictionary<string, string> BattleNames = new Dictionary<string, string>
        {
            ["battle_1"] = "第一战 · 程远志来犯", ["battle_2"] = "第二战 · 张梁", ["battle_3"] = "第三战 · 张角三兄弟",
        };

        public static string Prompt(string key)
        {
            if (key == null) return "";
            if (key.StartsWith("talk:")) return $"[确认] 与{Speaker(key.Substring(5))}交谈";
            if (key.StartsWith("gate:battle_")) return $"[确认] 出城迎敌：{BattleNames[key.Substring(5)]}";
            if (key == "gate:locked") return "[确认] 南门";
            return "[确认] 南门";
        }

        public static string Objective(StoryState s) => s.Phase switch
        {
            StoryPhase.FindCompanions => $"目标：在城中寻找志同道合的豪杰（{(s.GuanYuJoined ? 1 : 0) + (s.ZhangFeiJoined ? 1 : 0)}/2）",
            StoryPhase.Oath => "目标：桃园结义",
            StoryPhase.LeaveTown => $"目标：从南门出城迎敌（{BattleNames[s.NextBattleId]}）",
            _ => "目标：黄巾已退",
        };

        public const string Advance = "▼ 确认";
    }
}
