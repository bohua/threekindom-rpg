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
            // 主稿 v1.3 §3.6／§3.12（2026-10-27）：补 1 品功法残卷（张飞默认《盾法》残卷、安喜书肆三本残卷）与 0.3 赠书两本 2 品策略。
            // 只往末尾追加：存档按书库下标记录镶嵌。
            T("《盾法》残卷", Element.Earth, 1); T("《盾法》残卷", Element.Earth, 1); T("《越女剑》残卷", Element.Water, 1); T("《导引》残卷", Element.Wood, 1);
            B("《孙子·虚实》", Element.Water, 2); B("《孙子·九地》", Element.Fire, 2);
            return lib;
        }

        // 书从哪里来（主稿 §3.12 供给表）：不在表里的书开局就有；其余按剧情进度赠送、缴获或在书肆购买。
        public enum Source { Opening, AnxiShop, GongsunGift, PingyuanShop, PanheLoot, XiapiShop }

        public readonly struct Supply
        {
            public readonly string Name; public readonly int Copy; public readonly Source From; public readonly int Price;
            public Supply(string name, int copy, Source from, int price = 0) { Name = name; Copy = copy; From = from; Price = price; }
            public bool ForSale => Price > 0;
        }

        // 3 品撤出开局（E4），走缴获与下邳书肆
        public static readonly Supply[] Supplies =
        {
            new Supply("《盾法》残卷", 1, Source.AnxiShop, 30), new Supply("《越女剑》残卷", 0, Source.AnxiShop, 30), new Supply("《导引》残卷", 0, Source.AnxiShop, 30),
            new Supply("《孙子·虚实》", 0, Source.GongsunGift), new Supply("《孙子·九地》", 0, Source.GongsunGift),
            new Supply("《越女剑》", 0, Source.PingyuanShop, 80), new Supply("《导引》", 0, Source.PingyuanShop, 80),
            new Supply("《六韬·虎韬》", 0, Source.PingyuanShop, 30), new Supply("《司马法》残卷", 0, Source.PingyuanShop, 30),
            new Supply("《吴子》", 0, Source.PanheLoot),
            new Supply("《孙子·火攻》", 0, Source.XiapiShop, 250), new Supply("《管子·度地》", 0, Source.XiapiShop, 250),
            new Supply("《孙膑兵法·八阵》", 0, Source.XiapiShop, 250),
        };

        // 该书的供给条目（同名书按出现次序区分第几本）；开局就有的书返回 null
        public static Supply? SupplyOf(IList<Book> lib, Book b)
        {
            int copy = 0;
            foreach (var x in lib) { if (x == b) break; if (x.Name == b.Name) copy++; }
            foreach (var s in Supplies) if (s.Name == b.Name && s.Copy == copy) return s;
            return null;
        }

        public static string SourceName(Source s) => s switch
        {
            Source.AnxiShop => "安喜书肆", Source.GongsunGift => "公孙瓒营赠书", Source.PingyuanShop => "平原书肆",
            Source.PanheLoot => "磐河缴获", Source.XiapiShop => "下邳书肆", _ => "开局",
        };

        public static School SchoolOf(string name) => name switch { "刘备" => School.Ru, "张飞" => School.Fa, _ => School.Bing };

        // Opening sockets by general name (both the menu and the battle know generals by display name).
        public static void Default(string name, Book[] slots, IList<Book> lib)
        {
            Book L(string n, int skip = 0) => lib.Where(b => b.Name == n).Skip(skip).First();
            // 主稿 v1.3 §3.6 默认镶嵌（E4 降一品，3 品撤出开局）
            switch (name)
            {
                // 儒家（智 76，每段 5 槽）：中枢—坎乾兑一段 水 5 木 4 → 截流、抚军、鼓舞；震位《李将军射法》→ 刺击
                case "刘备": slots[0] = L("《墨子·备水》"); slots[1] = L("《孙子·行军》"); slots[2] = L("《三略》"); slots[3] = L("《司马法》"); slots[6] = L("《李将军射法》"); break;
                // 兵家（智 70，每段 4 槽）：中枢—艮震巽 火 5 木 1 金 1 → 引火、火烧营寨；离位残卷留待升档，坤位《剑道》→ 刺击
                case "关羽": slots[0] = L("《六韬·火战》"); slots[1] = L("《尉缭子》残卷"); slots[2] = L("《管子·牧民》"); slots[3] = L("《火攻》残卷"); slots[4] = L("《火攻》残卷", 1); slots[5] = L("《剑道》"); break;
                // 法家（智 40，每段 3 槽）：中枢—坤兑 土 5 金 2 → 坚壁；乾位《手搏》→ 猛击；艮坎《盾法》与残卷 土 5 → 中流砥柱
                case "张飞": slots[0] = L("《墨子·备城门》"); slots[1] = L("《六韬·龙韬》"); slots[2] = L("《尉缭子》"); slots[3] = L("《手搏》"); slots[5] = L("《盾法》"); slots[4] = L("《盾法》残卷"); break;
            }
        }
    }
}
