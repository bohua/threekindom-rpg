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
            // More townsfolk (user 2026-10-22).
            ["npc.N13"] = "操练的兵卒", ["npc.N14"] = "练射的弓手", ["npc.N15"] = "教头", ["npc.N16"] = "看榜的汉子", ["npc.N17"] = "看榜的妇人",
            ["npc.N18"] = "杂耍艺人", ["npc.N19"] = "卖艺汉子", ["npc.N20"] = "卖饼小贩", ["npc.N21"] = "洗衣妇人", ["npc.N22"] = "顽童",
            ["npc.N23"] = "小姑娘", ["npc.N24"] = "读书人", ["npc.N25"] = "闲聊的汉子", ["npc.N26"] = "闲聊的妇人", ["npc.N27"] = "农夫",
            ["npc.N28"] = "征兵小吏", ["npc.N29"] = "应募青年", ["npc.N30"] = "乞丐", ["npc.N31"] = "地痞", ["npc.N32"] = "赌坊庄家",
            ["guard"] = "守城卫兵", ["waiter"] = "酒保", ["cheng_yuanzhi"] = "程远志", ["zhang_liang"] = "张梁", ["zhang_jiao"] = "张角", ["zhang_bao"] = "张宝",
        };

        static readonly Dictionary<string, string> Lines = new Dictionary<string, string>
        {
            // The notice board and Zhang Fei (user 2026-10-22, after 演义 第1回; lines are this game's adaptation).
            ["notice.1"] = "榜文：黄巾作乱，幽州太守刘焉出榜招募义兵，以保乡里。",
            ["notice.2"] = "黄巾猖獗，百姓流离……我空有此心，却无力破贼。唉——",
            ["zhang.1"] = "大丈夫不与国家出力，何故在榜前长叹？",
            ["zhang.2"] = "我本汉室宗亲，姓刘名备，字玄德。今闻黄巾倡乱，有志破贼安民，只恨力不能及。",
            ["zhang.3"] = "俺姓张名飞，字翼德，颇有些庄田。你既有此心，可愿与俺同招乡勇、报效朝廷？",
            ["choice.prompt"] = "（如何回答张飞？）",
            ["choice.serve"] = "愿报效朝廷，共举义兵",
            ["choice.wait"] = "时机尚未成熟……",
            ["zhang.serve.1"] = "正合我意！愿与翼德同招乡勇，共破黄巾。",
            ["zhang.serve.2"] = "痛快！俺这就散些家财，招募乡勇！",
            ["zhang.wait.1"] = "黄巾势大，我等兵微将寡，只怕时机尚未成熟。",
            ["zhang.wait.2"] = "哎呀，还等什么时机！贼兵都快到城下了。俺出钱，你出力，还怕大事不成？",
            ["zhang.wait.3"] = "……翼德说得是。好，便依你！",
            ["zhang.joined"] = "张飞加入了队伍。",
            ["guan.joined"] = "关羽加入了队伍。",
            ["zhang.tavern"] = "走！前面酒店喝上几碗，细细商议！",
            // In the tavern: Guan Yu arrives (演义 第1回).
            ["tavern.1"] = "二位客官，酒来了！慢用。",
            ["tavern.2"] = "哥哥，招募乡勇的事，俺明日便去张罗。",
            ["tavern.3"] = "有劳翼德。只是兵马易募，良将难求啊。",
            ["tavern.4"] = "店家，快斟酒来吃！我待赶入城去投军。",
            ["tavern.5"] = "好嘞，客官稍候！",
            ["tavern.6"] = "此人身长九尺，相貌堂堂，威风凛凛……必非常人。",
            ["tavern.7"] = "那汉子！俺哥哥请你过来同坐！",
            ["tavern.8"] = "壮士请了。方才听闻壮士要去投军，可否告知姓名？",
            ["tavern.9"] = "某姓关名羽，字云长，河东解良人。闻此处招军破贼，特来应募。",
            ["tavern.10"] = "在下刘备，与这位张翼德正要聚义破贼。壮士何不与我等同举大事？",
            ["tavern.11"] = "若蒙不弃，关某愿随兄长同行！",
            ["tavern.12"] = "好！俺庄后有一桃园，花开正盛。明日便在园中祭告天地，结为兄弟！",
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
            // Townsfolk bubbles (user 2026-10-22).
            ["npc.N13"] = "喝！哈！——教头说了，枪要扎得又快又稳。",
            ["npc.N14"] = "三十步外的靶子还好，再远就难说喽。",
            ["npc.N15"] = "都给我站直了！黄巾贼可不会等你们练好！",
            ["npc.N16"] = "太守出榜招募义兵……这年头，不去也不成啊。",
            ["npc.N17"] = "黄巾贼要打过来了？哎哟，可怎么好。",
            ["npc.N18"] = "走过路过不要错过！顶碗抛球，看好了——",
            ["npc.N19"] = "一套枪法献给诸位！有钱的捧个钱场，没钱的捧个人场！",
            ["npc.N20"] = "胡饼！刚出炉的胡饼！两文钱一个！",
            ["npc.N21"] = "趁着日头好，把这些衣裳洗了晾上。",
            ["npc.N22"] = "看我的毽子！一、二、三……",
            ["npc.N23"] = "哥哥，等等我！",
            ["npc.N24"] = "「天下大势，分久必合，合久必分」……妙哉。",
            ["npc.N25"] = "听说城外黄巾来了好几万人？",
            ["npc.N26"] = "我家那口子也说要去投军呢。",
            ["npc.N27"] = "这季的菜再不收，就要被兵荒耽误了。",
            ["npc.N28"] = "应募的报上姓名籍贯，一个一个来，莫要挤。",
            ["npc.N29"] = "俺也要去当兵，好歹能吃上一口饱饭。",
            ["npc.N30"] = "行行好吧，给口饭吃……",
            ["npc.N31"] = "哟，这位爷，进来玩两把？",
            ["npc.N32"] = "买定离手！",
            ["npc.N13.won"] = "听说城外打了胜仗！咱们练得更起劲了。",
            ["npc.N16.won"] = "榜上说黄巾贼被打退了一阵，好！",
            ["npc.N25.won"] = "那刘玄德三兄弟可真了不得！",
            ["npc.N29.won"] = "俺也要像刘玄德那样杀贼立功！",
            // The beggar.
            ["beggar.ask"] = "这位大爷，行行好，赏几个钱吧……",
            ["alms.5"] = "施舍五文",
            ["alms.20"] = "施舍二十文",
            ["alms.50"] = "施舍五十文",
            ["alms.none"] = "（不给）",
            ["beggar.thanks.0"] = "多谢大爷！够买个饼了。",
            ["beggar.thanks.1"] = "大爷心善！小的给您磕头了！",
            ["beggar.thanks.2"] = "这、这么多……恩公大名，小的一辈子记着！",
            ["beggar.poor"] = "（囊中羞涩，一时拿不出这许多。）",
            ["beggar.declined"] = "唉……",
            // The scholar.
            ["scholar.ask"] = "这位兄台气度不凡。在下近来读史有得，出三道题考考兄台，可愿一试？",
            ["scholar.yes"] = "愿闻其详",
            ["scholar.no"] = "改日再说",
            ["scholar.bye"] = "也罢，改日再请教。",
            ["scholar.later"] = "今日已讨教过了。他日兄台再来，在下另备新题。",
            ["scholar.right"] = "正是！兄台好见识。",
            ["scholar.result.0"] = "三题皆错……兄台还需多读些书啊。",
            ["scholar.result.1"] = "答对一题，尚可。",
            ["scholar.result.2"] = "答对两题，兄台胸中颇有丘壑。",
            ["scholar.result.3"] = "三题全对！兄台博古通今，在下佩服！",
            // The gambling house.
            ["casino.greet"] = "嘿，赌坊里头三颗骰子，押大押小，一赔一。豹子通杀，敢不敢来？",
            ["casino.ask"] = "押大还是押小？",
            ["casino.big"] = "押大（11～17）",
            ["casino.small"] = "押小（4～10）",
            ["casino.leave"] = "不赌了",
            ["casino.stake"] = "押多少？",
            ["casino.back"] = "再想想",
            ["casino.broke"] = "钱不够还想下注？去去去！",
            ["casino.bye"] = "下回再来啊，爷！",
            // The archery range.
            ["archery.ask"] = "这位壮士，可要试试弓箭？三支箭，看你能射中几个靶子。",
            ["archery.try"] = "试试身手",
            ["archery.no"] = "改日吧",
            ["archery.bye"] = "随时恭候。",
            ["archery.quit"] = "怎么，不射了？",
            ["archery.no_reward"] = "好箭法！不过今日的彩头已经给过了。",
            ["guard.stop"] = "站住！城外黄巾贼横行，太守有令：只许进城，不许出城。",
            ["guard.stop.south"] = "西门不开。要出城迎敌，请走南门。",
            // Before and after each battle (user 2026-10-22). The leaders' titles follow 演义 第1回; the meetings are
            // this game's adaptation, and the leaders only withdraw (V1_SPEC §2.2).
            ["foe.battle_1.1"] = "俺乃大贤良师麾下程远志！涿郡的人听着，速速开城投降，饶你们不死！",
            ["foe.battle_1.2"] = "黄巾作乱，劫掠乡里。我兄弟三人在此，休想踏进城门一步！",
            ["foe.battle_1.3"] = "少啰嗦！吃俺一矛！",
            ["foe.battle_2.1"] = "吾乃人公将军张梁！程远志无能，今日由我亲取涿郡！",
            ["foe.battle_2.2"] = "败军之后又来送死，某便成全你。",
            ["foe.battle_2.3"] = "此人带了部将同来，不可轻敌。大家同心破敌！",
            ["foe.battle_3.1"] = "苍天已死，黄天当立！吾乃天公将军张角，何人敢挡我大军？",
            ["foe.battle_3.2"] = "地公将军张宝在此！兄长，让我先拿下这三人！",
            ["foe.battle_3.3"] = "张角三兄弟齐至……二弟、三弟，此战关乎涿郡存亡！",
            ["foe.battle_3.4"] = "大哥放心，俺正要会会他们！",
            ["won.battle_1.1"] = "可恶，这三人好生了得……撤，快撤！",
            ["won.battle_1.2"] = "贼首已退。二弟、三弟，先回城整备，贼众必定还会再来。",
            ["won.battle_2.1"] = "哼，今日暂且退兵！待我兄长到来，定叫你们好看！",
            ["won.battle_2.2"] = "张梁虽退，张角、张宝必来报复。",
            ["won.battle_2.3"] = "回城整军，备好军粮，严阵以待。",
            ["won.battle_3.1"] = "天时未至……众军，随我撤！",
            ["won.battle_3.2"] = "大哥快走，我来断后！",
            ["won.battle_3.3"] = "黄巾主力已溃，涿郡总算保住了。",
        };

        public static string Speaker(string key) => key != null && Speakers.TryGetValue(key, out var s) ? s : "";
        public static string Line(string key)
        {
            if (key != null && key.StartsWith("warn:no_weapon:")) return $"{key.Substring(15)}没有装备武器，仍可出战。可先回城整备，再走近黄巾军便开战。";
            if (key == null) return null;
            if (key.StartsWith("raw:")) return key.Substring(4);
            var part = key.Split(':');
            switch (part[0])
            {
                case "rep.up": return $"声望提高了{part[1]}点（现在{part[2]}）。";
                case "rep.down": return $"声望降低了{part[1]}点（现在{part[2]}）。";
                case "stake": return $"押{part[1]}文";
                case "scholar.wrong": return $"错了，应是「{part[1]}」。";
                case "casino.result":
                    string what = part[1] == "triple" ? "豹子！庄家通杀" : part[1] == "big" ? "大" : "小";
                    return $"开——{part[2]}，{what}！";
                case "casino.won": return $"赢了{part[1]}文（现有铜钱{part[2]}）。";
                case "casino.lost": return $"输了{part[1]}文（现有铜钱{part[2]}）。";
                case "archery.score": return $"一共射中{part[1]}个靶子！" + (int.Parse(part[1]) >= 5 ? "好箭法！" : "");
                case "archery.reward": return $"得到彩头{part[1]}文（现有铜钱{part[2]}）。";
            }
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
            if (key == "notice") return "[确认] 查看告示";
            if (key == "beggar") return "[确认] 与乞丐交谈";
            if (key == "scholar") return "[确认] 与读书人交谈";
            if (key == "casino") return "[确认] 与地痞交谈（赌大小）";
            if (key == "archery") return "[确认] 与弓手交谈（射箭）";
            if (key == "tavern") return "[确认] 进入酒馆";
            if (key.StartsWith("gate:battle_")) return $"[确认] 出城迎敌：{BattleNames[key.Substring(5)]}";
            if (key.StartsWith("npc:")) return $"[确认] 与{Speaker("npc." + key.Substring(4))}交谈";
            if (key == "shop") return "[确认] 与铁匠交谈（武器装备店）";
            if (key == "save") return "[确认] 与酒馆掌柜交谈（存档）";
            if (key == "gate:locked") return "[确认] 南门";
            return "[确认] 南门";
        }

        public static string Objective(StoryState s) => s.Phase switch
        {
            StoryPhase.FindCompanions => s.ZhangFeiJoined ? "目标：与张飞到酒馆商议" : "目标：到市集广场查看告示",
            StoryPhase.Oath => "目标：桃园结义",
            StoryPhase.LeaveTown => $"目标：从南门出城迎敌（{BattleNames[s.NextBattleId]}）",
            _ => "目标：黄巾已退",
        };

        public const string Advance = "▼ 确认";
    }
}
