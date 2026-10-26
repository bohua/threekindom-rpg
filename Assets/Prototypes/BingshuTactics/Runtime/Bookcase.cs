using System.Collections.Generic;
using System.Linq;

namespace Prototype.BingshuTactics
{
    // The book library and each general's opening sockets, shared by the travel menu's 兵书 page and the tactics
    // battles (user 2026-10-26: one set of book data for both). Two kinds of book (策略书 / 功法书, design §15.9).
    // Socket indices follow Circuits: slot 0 is the hub, which links to one direction only.
    public static class Bookcase
    {
        public static List<Book> NewLibrary()
        {
            var lib = new List<Book>();
            void B(string n, Element e, int g) => lib.Add(new Book(n, e, g));
            void T(string n, Element e, int g) => lib.Add(new Book(n, e, g, BookKind.Technique));
            B("《尉缭子》", Element.Metal, 2); B("《吴子》", Element.Metal, 3); B("《六韬·虎韬》", Element.Metal, 1); B("《尉缭子》残卷", Element.Metal, 1);
            B("《三略》", Element.Wood, 2); B("《司马法》", Element.Wood, 2); B("《管子·牧民》", Element.Wood, 1); B("《司马法》残卷", Element.Wood, 1);
            B("《管子·度地》", Element.Water, 3); B("《墨子·备水》", Element.Water, 2); B("《孙子·行军》", Element.Water, 1);
            B("《孙子·火攻》", Element.Fire, 3); B("《六韬·火战》", Element.Fire, 2); B("《火攻》残卷", Element.Fire, 1); B("《火攻》残卷", Element.Fire, 1);
            B("《孙膑兵法·八阵》", Element.Earth, 3); B("《墨子·备城门》", Element.Earth, 2); B("《六韬·龙韬》", Element.Earth, 1);
            // 功法书：《剑道》《手搏》《李将军射法》见《汉书·艺文志》兵技巧；《越女剑》取吴越剑术传说；《导引》《盾法》为原创书名
            T("《剑道》", Element.Metal, 2); T("《剑道》残卷", Element.Metal, 1); T("《李将军射法》", Element.Metal, 1);
            T("《手搏》", Element.Fire, 2); T("《手搏》残卷", Element.Fire, 1);
            T("《越女剑》", Element.Water, 2);
            T("《导引》", Element.Wood, 2);
            T("《盾法》", Element.Earth, 2);
            return lib;
        }

        public static School SchoolOf(string name) => name switch { "刘备" => School.Ru, "张飞" => School.Fa, _ => School.Bing };

        // Opening sockets by general name (both the menu and the battle know generals by display name).
        public static void Default(string name, Book[] slots, IList<Book> lib)
        {
            Book L(string n) => lib.First(b => b.Name == n);
            switch (name)
            {
                // 儒家：中枢—坎乾兑一线装策略书，离巽震另一段装一本功法书
                case "刘备": slots[0] = L("《管子·度地》"); slots[1] = L("《三略》"); slots[2] = L("《司马法》"); slots[3] = L("《墨子·备水》"); slots[6] = L("《李将军射法》"); break;
                // 兵家：一线贯通，智力切成两段；末段坤位放《剑道》
                case "关羽": slots[0] = L("《孙子·火攻》"); slots[3] = L("《六韬·火战》"); slots[4] = L("《火攻》残卷"); slots[1] = L("《尉缭子》"); slots[2] = L("《管子·牧民》"); slots[5] = L("《剑道》"); break;
                // 法家：中枢—坤兑乾一线装土金策略书，艮坎短段装两本功法书
                case "张飞": slots[0] = L("《孙膑兵法·八阵》"); slots[1] = L("《墨子·备城门》"); slots[2] = L("《吴子》"); slots[5] = L("《盾法》"); slots[4] = L("《手搏》"); break;
            }
        }
    }
}
