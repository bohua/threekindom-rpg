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
            ["npc.N01"] = "南门守卫", ["npc.N02"] = "巡城兵", ["npc.N03"] = "城主府掌士", ["npc.N04"] = "铁匠", ["npc.N05"] = "酒馆掌柜",
            ["npc.N06"] = "医者", ["npc.N07"] = "驿卒", ["npc.N08"] = "织席妇人", ["npc.N09"] = "街坊老者", ["npc.N10"] = "孩童",
            ["npc.N11"] = "粮贩", ["npc.N12"] = "运粮脚夫",
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
            // Residents (ZHUOJUN_TOWN_DESIGN.md §4). 暂定 wording; ".after" after the oath, ".won" after the first victory.
            ["npc.N01"] = "这里是涿郡南门。往北直走是市集，再往北便是郡署。黄巾贼就在城外，出入多加小心。",
            ["npc.N01.after"] = "诸位整备妥当，便可从这道门出城迎敌。",
            ["npc.N01.won"] = "又打了胜仗！城里的人都在说你们兄弟三人呢。",
            ["npc.N02"] = "巡城呢，借过。……城里人心惶惶，官府正张榜募兵。",
            ["npc.N02.after"] = "听说你们要出城破贼？城里的弟兄都盼着呢。",
            ["npc.N02.won"] = "贼兵退了一阵，弟兄们士气都上来了。",
            ["npc.N03"] = "此乃郡署。太守已张榜募集义兵，以御黄巾。有志之士，可往市集看榜。",
            ["npc.N03.after"] = "诸位义士愿出城破贼，郡署上下感念。",
            ["npc.N03.won"] = "捷报已呈太守。黄巾未灭，还望诸位再接再厉。",
            ["npc.N04"] = "要买兵器、换甲胄，尽管来找我。旧货我也收。",
            ["npc.N05"] = "客官歇歇脚？要记下这一路的进度，跟我说一声便是。",
            ["npc.N06"] = "部队折了兵力不要紧，战后整顿自会复原；阵中吃紧时，可靠整队或军粮救急。",
            ["npc.N07"] = "驿道都断了，公文只能走小路。黄巾在城南扎营，听说领头的叫程远志。",
            ["npc.N07.won"] = "程远志败了，可贼势未衰。听说张角三兄弟还在附近聚众。",
            ["npc.N08"] = "玄德，又来看我织席了？外头不太平，你也要当心。",
            ["npc.N08.after"] = "听说你和两位壮士结了义？好，好，往后有人照应了。",
            ["npc.N09"] = "老朽在这井边坐了几十年喽。出西门有一片桃园，这时节花开得正好。",
            ["npc.N09.won"] = "听说你们打退了黄巾贼，好啊，好啊！",
            ["npc.N10"] = "大哥哥，桃园的花开了！就在西门外，我刚去看过。",
            ["npc.N10.won"] = "大哥哥好厉害！我长大也要当兵！",
            ["npc.N11"] = "粮价又涨了，贼兵一来，谁还敢运粮进城。粮仓在北边，脚夫们正往这儿搬。",
            ["npc.N11.won"] = "贼兵一退，粮价总算稳了些。",
            ["npc.N12"] = "嘿哟……这一袋袋粮食要送到市集去。兵荒马乱的，有口饭吃就不错了。",
            ["gate:locked"] = "黄巾军就在城外。先在城里找到可以共事的豪杰，再出城迎敌。",
            ["gate:done"] = "城外的黄巾军已经退去。",
        };

        public static string Speaker(string key) => key != null && Speakers.TryGetValue(key, out var s) ? s : "";
        public static string Line(string key)
        {
            if (key != null && key.StartsWith("warn:no_weapon:")) return $"{key.Substring(15)}没有装备武器，仍可出战。再次在南门确认即出城。";
            if (key == null) return null;
            // A resident with nothing new to say repeats the earlier line (.won → .after → base).
            for (string k = key; k != null; k = k.EndsWith(".won") ? k.Substring(0, k.Length - 4) + ".after" : k.EndsWith(".after") ? k.Substring(0, k.Length - 6) : null)
                if (Lines.TryGetValue(k, out var s)) return s;
            return key;
        }

        static readonly Dictionary<string, string> BattleNames = new Dictionary<string, string>
        {
            ["battle_1"] = "第一战 · 程远志来犯", ["battle_2"] = "第二战 · 张梁", ["battle_3"] = "第三战 · 张角三兄弟",
        };

        public static string Prompt(string key)
        {
            if (key == null) return "";
            if (key.StartsWith("talk:")) return $"[确认] 与{Speaker(key.Substring(5))}交谈";
            if (key.StartsWith("gate:battle_")) return $"[确认] 出城迎敌：{BattleNames[key.Substring(5)]}";
            if (key.StartsWith("npc:")) return $"[确认] 与{Speaker("npc." + key.Substring(4))}交谈";
            if (key == "shop") return "[确认] 与铁匠交谈（武器装备店）";
            if (key == "save") return "[确认] 与酒馆掌柜交谈（存档）";
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
