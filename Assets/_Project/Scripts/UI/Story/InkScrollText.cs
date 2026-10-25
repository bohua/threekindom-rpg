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
        public const string Continue = "▼ 確認";
        public const string SkipOpening = "［取消］跳過片頭";
    }
}
