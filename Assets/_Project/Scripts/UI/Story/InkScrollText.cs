using System.Collections.Generic;
using System.Text;

namespace ThreeKingdoms.UI.Story
{
    // The ink scroll that closes each main story beat (user 2026-10-24): a title and the story with its source, in
    // traditional characters, at most 100 characters. Written for this game; the oath words follow 演义 第1回, and the
    // three battles against the Yellow Turbans are this game's adaptation (V1_SPEC §2.3), so the text says so.
    public static class InkScrollText
    {
        public readonly struct Entry
        {
            public readonly string Title, Body;
            public Entry(string title, string body) { Title = title; Body = body; }
        }

        static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>
        {
            ["taoyuan"] = new Entry("桃園結義",
                "東漢末年，黃巾蜂起。涿郡劉備、關羽、張飛相遇於市，志同道合，於桃園焚香再拜，誓曰：不求同年同月同日生，但願同年同月同日死。自此同心協力，共圖大業。——典出《三國演義》第一回"),
            ["huangjin"] = new Entry("大破黃巾",
                "中平元年，張角以太平道聚眾，頭裹黃巾，攻掠州郡。劉備率關、張起義兵於涿郡，三戰皆捷，程遠志、張梁、張角兄弟相繼敗走。——事本《三國志·先主傳》與《三國演義》第一、二回，戰事為本作改編"),
            // 0.2 黄巾篇 (V02_HUANGJIN_PLAN §2): ② closes section two, ③ closes the version and alone carries the 完成 seal.
            ["zhuanzhan"] = new Entry("黃巾轉戰",
                "中平元年，劉備從盧植於廣宗，南下潁川，火攻草澤，於火光中初見曹操。歸途恩師檻車被徵，又救董卓於廣宗城下，反遭輕慢。是冬黃巾平定。——事本《三國志》《後漢書》與《三國演義》第一回"),
            ["anxi"] = new Entry("安喜掛印",
                "中平二年，劉備以功為安喜尉，施貧斷訟，修橋剿賊。督郵至縣，求謁不通，乃縛之於馬枊，杖二百，解綬繫其頸，棄官而去。——事本《三國志·先主傳》裴注引《典略》與《三國演義》第二回"),
            // 0.3 讨董篇 (V03_TAODONG_PLAN §2): ④ ⑤ the 演义 tales, ⑥ closes the version.
            ["wenjiu"] = new Entry("溫酒斬華雄",
                "初平元年，關東諸侯會盟酸棗，董卓將華雄連斬聯軍數將。關羽請戰，曹操斟熱酒與之，羽曰：酒且斟下，某去便來。須臾提華雄首級擲於地，其酒尚溫。——事本《三國演義》第五回；史載華雄為孫堅所斬"),
            ["sanying"] = new Entry("三英戰呂布",
                "呂布出虎牢關搦戰，公孫瓚不敵而走。張飛挺矛接戰，關羽、劉備相繼來助，三人圍住呂布轉燈般廝殺，布遮攔不住，撥馬退入關中。——事本《三國演義》第五回，非史實"),
            ["fen_luoyang"] = new Entry("焚洛陽",
                "董卓焚洛陽宮室，劫天子西遷長安。孫堅入洛，掃除宗廟；諸侯各懷異心，聯軍遂散。劉備隨公孫瓚北還，名聲始聞於天下。——事本《三國志·武帝紀》《孫破虜傳》與《三國演義》第六回"),
            // 0.4 河北篇 (V04_HEBEI_PLAN §2): ⑦ Panhe, ⑧ Jieqiao closes the version.
            ["panhe"] = new Entry("磐河·白馬銀槍",
                "初平三年，公孫瓚與袁紹戰於磐河，為文醜所逐，墜馬坡下。忽一少年將軍白馬銀槍，挺身救之，乃常山趙雲。雲曰：天下洶洶，從仁政所在。——事本《三國演義》第七回，語出裴注引《雲別傳》"),
            ["jieqiao"] = new Entry("界橋",
                "袁紹遣麴義以八百先登、千張強弩伏於界橋，白馬義從大敗，嚴綱陣斬。劉備與關、張、趙雲斷後，護公孫瓚南還。——事本《三國志·公孫瓚傳》裴注引《英雄記》；劉備斷後為本作改編"),
            ["beihai"] = new Entry("都昌解圍",
                "黃巾管亥圍北海孔融於都昌，太史慈突圍至平原求救。劉備斂容答曰：「孔北海知世間有劉備邪！」即遣精兵三千隨慈往，賊聞兵至，解圍散走。——事本《三國志·太史慈傳》；關羽斬管亥本《三國演義》第十一回，趙雲在場為本作改編"),
            ["sanrang"] = new Entry("三讓徐州",
                "陶謙病篤，謂別駕麋竺曰：「非劉備不能安此州也。」謙死，竺率州人迎先主，先主未敢當。陳登、孔融皆勸之，先主遂領徐州。——事本《三國志·先主傳》；三讓之數本《三國演義》第十二回"),
        };

        public static bool TryGet(string id, out Entry entry) => Entries.TryGetValue(id ?? "", out entry);

        public static IEnumerable<string> Ids => Entries.Keys;

        // Characters counted toward the 100 limit: everything but spaces.
        public static int Length(string body)
        {
            int n = 0;
            foreach (char c in body) if (!char.IsWhiteSpace(c)) n++;
            return n;
        }

        // Horizontal punctuation turned to its vertical form for top-to-bottom columns.
        static readonly Dictionary<char, char> Vertical = new Dictionary<char, char>
        {
            ['—'] = '︱', ['《'] = '︽', ['》'] = '︾', ['（'] = '︵', ['）'] = '︶', ['「'] = '﹁', ['」'] = '﹂',
            ['：'] = '︰', ['·'] = '・', ['、'] = '︑', ['，'] = '︐', ['。'] = '︒',
        };

        public static string ToVertical(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (char c in text) sb.Append(Vertical.TryGetValue(c, out var v) ? v : c);
            return sb.ToString();
        }

        // Splits the story into columns of at most perColumn characters, read right to left, top to bottom; the
        // source after "——" starts a column of its own.
        public static List<string> Columns(string body, int perColumn)
        {
            var columns = new List<string>();
            int dash = body.IndexOf("——", System.StringComparison.Ordinal);
            void Add(string part)
            {
                for (int i = 0; i < part.Length; i += perColumn)
                    columns.Add(part.Substring(i, System.Math.Min(perColumn, part.Length - i)));
            }
            if (dash > 0) { Add(body.Substring(0, dash)); Add(body.Substring(dash)); }
            else Add(body);
            return columns;
        }

        public const string Seal = "完成";
        public static string AchievementUnlocked(string title) => "成就解鎖　" + title;
        public const string Continue = "▼ 確認";
        public const string SkipOpening = "［取消］跳過片頭";
    }
}
