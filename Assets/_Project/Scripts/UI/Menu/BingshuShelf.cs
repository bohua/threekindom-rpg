using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Prototype.BingshuTactics;
using ThreeKingdoms.Gameplay.Story;

namespace ThreeKingdoms.UI.Menu
{
    // The 兵书 page's state: the shared library and what each general has socketed in his school's circuit. Rules and
    // books are the military-book prototype's (Prototypes/BingshuTactics, TacticsRules.cs, PR #14); schools and the
    // opening sockets are the prototype's too (刘备 儒家, 关羽 兵家, 张飞 法家). The prototype is not V1: this state is
    // kept for the play-through in memory only and is not written to save files.
    public sealed class BingshuShelf
    {
        static readonly ConditionalWeakTable<GameRun, BingshuShelf> PerRun = new ConditionalWeakTable<GameRun, BingshuShelf>();

        public static BingshuShelf For(GameRun run) => PerRun.GetValue(run, _ => new BingshuShelf());

        public readonly List<Book> Library = new List<Book>();
        readonly Dictionary<string, School> schools = new Dictionary<string, School>();
        readonly Dictionary<string, Book[]> slots = new Dictionary<string, Book[]>();

        public BingshuShelf()
        {
            void B(string n, Element e, int g) => Library.Add(new Book(n, e, g));
            B("《尉缭子》", Element.Metal, 2); B("《吴子》", Element.Metal, 3); B("《六韬·虎韬》", Element.Metal, 1); B("《尉缭子》残卷", Element.Metal, 1);
            B("《三略》", Element.Wood, 2); B("《司马法》", Element.Wood, 2); B("《管子·牧民》", Element.Wood, 1); B("《司马法》残卷", Element.Wood, 1);
            B("《管子·度地》", Element.Water, 3); B("《墨子·备水》", Element.Water, 2); B("《孙子·行军》", Element.Water, 1);
            B("《孙子·火攻》", Element.Fire, 3); B("《六韬·火战》", Element.Fire, 2); B("《火攻》残卷", Element.Fire, 1); B("《火攻》残卷", Element.Fire, 1);
            B("《孙膑兵法·八阵》", Element.Earth, 3); B("《墨子·备城门》", Element.Earth, 2); B("《六韬·龙韬》", Element.Earth, 1);

            Book L(string name) => Library.First(b => b.Name == name);
            Set("liu_bei", School.Ru, (0, L("《管子·度地》")), (1, L("《三略》")), (2, L("《司马法》")), (3, L("《墨子·备水》")));
            Set("guan_yu", School.Bing, (0, L("《孙子·火攻》")), (3, L("《六韬·火战》")), (4, L("《火攻》残卷")), (1, L("《尉缭子》")), (2, L("《管子·牧民》")));
            Set("zhang_fei", School.Fa, (0, L("《孙膑兵法·八阵》")), (1, L("《墨子·备城门》")), (5, L("《六韬·龙韬》")), (2, L("《吴子》")));
        }

        void Set(string id, School school, params (int slot, Book book)[] books)
        {
            schools[id] = school;
            var s = new Book[7];
            foreach (var (i, b) in books) s[i] = b;
            slots[id] = s;
        }

        public CircuitDef Circuit(string id) => Circuits.Of(schools.TryGetValue(id, out var s) ? s : School.Ru);

        public Book[] Slots(string id)
        {
            if (!slots.TryGetValue(id, out var s)) { s = new Book[7]; slots[id] = s; schools[id] = School.Ru; }
            return s;
        }

        public string Owner(Book b) => slots.FirstOrDefault(kv => kv.Value.Contains(b)).Key;

        // 藏书阁: this general's books first (with a pearl), then the free ones by element and grade; books another
        // general has socketed are not shown (UI_STYLE_GUIDE 命盘 rule 3).
        public List<Book> ShelfFor(string id) => Library.Where(b => { var o = Owner(b); return o == null || o == id; })
            .OrderBy(b => Owner(b) == id ? 0 : 1).ThenBy(b => b.Element).ThenByDescending(b => b.Grade).ToList();

        // Socket a book (into the chosen slot when it fits, else the first free slot that fits); a book already in
        // this circuit comes out instead. Returns false when there is nowhere to put it.
        public bool Toggle(string id, Book b, ref int chosenSlot)
        {
            var c = Circuit(id); var s = Slots(id);
            int at = System.Array.IndexOf(s, b);
            if (at >= 0) { s[at] = null; return true; }
            int slot = chosenSlot >= 0 && chosenSlot < c.Slots.Length && CircuitMath.Fits(c, chosenSlot, b) ? chosenSlot
                : Enumerable.Range(0, c.Slots.Length).Where(i => s[i] == null && CircuitMath.Fits(c, i, b)).DefaultIfEmpty(-1).First();
            if (slot < 0) return false;
            s[slot] = b; chosenSlot = slot;
            return true;
        }
    }
}
