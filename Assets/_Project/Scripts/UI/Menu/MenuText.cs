using System;
using System.Globalization;
using ThreeKingdoms.Core.Battle;
using ThreeKingdoms.Core.Story;
using ThreeKingdoms.Gameplay.Story;
using ThreeKingdoms.Save;
using ThreeKingdoms.UI.Story;

namespace ThreeKingdoms.UI.Menu
{
    // Travel menu strings (simplified Chinese). ADR-0007 replaces this with the localisation tables.
    public static class MenuText
    {
        public const string TroopsFull = "该角色兵力已满。";
        public const string TroopsFullLong = "当前兵力已满，使用后不会产生效果。";
        public const string TroopsFullSkill = "当前兵力已满，无法使用。";
        public const string BattleOnly = "仅能在战斗中使用。";

        // 行旅 / 涿郡 · 街市 (the tavern scene is 酒肆).
        public static string Place(IFieldHost c) =>
            c is UnityEngine.MonoBehaviour m && m.gameObject.scene.name == GameRun.TavernScene ? "涿郡 · 酒肆"
            : c?.SaveLocation == SaveData.CampLocation || c?.SaveLocation == SaveData.AnxiLocation
              || c?.SaveLocation == SaveData.GongsunLocation || c?.SaveLocation == SaveData.SuanzaoLocation ? SavePlace(c.SaveLocation) : "涿郡 · 街市";

        // "目标：从南门出城迎敌（第二战 · 张梁）" → "从南门出城迎敌 · 第二战 张梁", as on the mock-ups.
        public static string Quest(StoryState s, ChapterState c = null)
        {
            string t = StoryText.Objective(s, c);
            if (t.StartsWith("目标：")) t = t.Substring(3);
            int open = t.IndexOf('（');
            if (open > 0) t = t.Substring(0, open) + " · " + t.Substring(open + 1).TrimEnd('）').Replace(" · ", " ");
            return t;
        }

        public static string CourtesyName(string id) => id switch { "liu_bei" => "玄德", "guan_yu" => "云长", "zhang_fei" => "翼德", _ => "" };

        // One line under the skill name (技能 list).
        public static string SkillBrief(SkillSpec s) => s.Id switch
        {
            "regroup" => "回复友军兵力。", "fire_arrows" => "单体计策攻击。", "charge" => "单体物理强攻。",
            "break_formation" => "单体攻击并削弱物防。", "sweep" => "攻击全部敌军。", "intimidate" => "全体敌军物攻下降。",
            _ => s.TargetsAllies ? "支援友军。" : "攻击敌军。",
        };

        public static string SkillDetail(SkillSpec s) => s.Id switch
        {
            "regroup" => "回复一名存活友军 <color=#D6BA78>30%</color> 的最大兵力。\n不能用于已撤退单位。",
            _ => (s.Description ?? "").Trim(),
        };

        static readonly string[] Ordinals = { "一", "二", "三" };

        public static string SavePlace(string location)
        {
            if (location == SaveData.TownLocation) return "涿郡 · 酒肆";
            if (location == SaveData.CampLocation) return "广宗 · 卢植大营";
            if (location == SaveData.AnxiLocation) return "中山 · 安喜县";
            if (location == SaveData.GongsunLocation) return "幽州 · 公孙瓒营";
            if (location == SaveData.SuanzaoLocation) return "陈留 · 酸枣大营";
            if (location == "battle_7") return "公孙瓒营 · 校场";
            if (location == "battle_8") return "虎牢关 · 战前整备";
            if (location == "battle_4") return "颍川 · 战前整备";
            if (location == "battle_5") return "广宗 · 战前整备";
            if (location == "battle_6") return "安喜 · 战前整备";
            if (location != null && location.StartsWith("battle_") && int.TryParse(location.Substring(7), out int n) && n >= 1 && n <= 3)
                return $"南门整备 · 第{Ordinals[n - 1]}战";
            return location;
        }

        public static string SavedAt(string iso) =>
            DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t.ToString("MM-dd HH:mm") : iso;

        // 列传 (kept for later pages): short lives after 《三国演义》第一回 (paraphrased).
        public static string Biography(string id) => id switch
        {
            "liu_bei" => "字玄德，涿郡涿县人，自称汉中山靖王刘胜之后。少孤，与母贩屦织席为业。为人宽和，寡言语，喜怒不形于色，素有大志，专好结交天下豪杰。黄巾作乱，见榜慨然长叹，遂与关羽、张飞结义桃园，共举义兵。",
            "guan_yu" => "字云长，河东解良人。因乡里豪强倚势凌人，杀之，逃难江湖数年。闻涿郡招军破贼，特来应募，酒店中与刘备、张飞相遇，结为兄弟。所使青龙偃月刀，重八十二斤。",
            "zhang_fei" => "字翼德，涿郡人。颇有庄田，卖酒屠猪，专好结交天下豪杰。豹头环眼，燕颔虎须，声若巨雷，势如奔马。见刘备在榜前长叹，厉声相问，遂共举大事。使一条丈八蛇矛。",
            _ => "",
        };
    }
}
