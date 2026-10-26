using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Prototype.BingshuTactics;
using ThreeKingdoms.Gameplay.Story;

namespace ThreeKingdoms.UI.Menu
{
    // The play-through's books: the shared library (策略书 and 功法书) and what each general has socketed in his
    // school's circuit. One copy for the travel menu's 兵书 page and the tactics battles (user 2026-10-26: the battle
    // gets the same Book objects and the same slot arrays, so changes on either side show on the other). Rules, the
    // library and the opening sockets are Bookcase / TacticsRules (Prototypes/BingshuTactics). Saved as one line of
    // text in SaveData.bingshu through GameRun; an old save without it starts from the opening sockets.
    public sealed class BingshuShelf
    {
        static readonly ConditionalWeakTable<GameRun, BingshuShelf> PerRun = new ConditionalWeakTable<GameRun, BingshuShelf>();

        public static BingshuShelf For(GameRun run) => PerRun.GetValue(run, r =>
        {
            var shelf = new BingshuShelf(r);
            shelf.Restore(r.BingshuState);
            // Every general gets his sockets up front, so books another general holds never show as free.
            foreach (var g in r.Data.Party) shelf.Slots(g.Id);
            r.BingshuSnapshot = shelf.Serialize;
            return shelf;
        });

        public readonly List<Book> Library = Bookcase.NewLibrary();
        readonly Dictionary<string, School> schools = new Dictionary<string, School>();
        readonly Dictionary<string, Book[]> slots = new Dictionary<string, Book[]>();
        readonly GameRun run;

        BingshuShelf(GameRun run) { this.run = run; }

        public BingshuShelf() { }   // a free-standing shelf (tests)

        string NameOf(string id) => run?.Data.Party.FirstOrDefault(g => g.Id == id)?.DisplayName ?? id switch
        {
            "liu_bei" => "刘备", "guan_yu" => "关羽", "zhang_fei" => "张飞", _ => id,
        };

        public School SchoolOf(string id) => schools.TryGetValue(id, out var s) ? s : Bookcase.SchoolOf(NameOf(id));

        public CircuitDef Circuit(string id) => Circuits.Of(SchoolOf(id));

        public Book[] Slots(string id)
        {
            if (slots.TryGetValue(id, out var s)) return s;
            s = new Book[7];
            schools[id] = Bookcase.SchoolOf(NameOf(id));
            // Opening sockets, but never a book another general already holds.
            var free = Library.Where(b => Owner(b) == null).ToList();
            Bookcase.Default(NameOf(id), s, Library);
            for (int i = 0; i < s.Length; i++) if (s[i] != null && !free.Contains(s[i])) s[i] = null;
            slots[id] = s;
            return s;
        }

        public string Owner(Book b) => slots.FirstOrDefault(kv => kv.Value.Contains(b)).Key;

        // 藏书阁: this general's books first (with a pearl), then the free ones by kind, element and grade; books another
        // general has socketed are not shown (UI_STYLE_GUIDE 命盘 rule 3).
        public List<Book> ShelfFor(string id)
        {
            Slots(id);
            return Library.Where(b => { var o = Owner(b); return o == null || o == id; })
                .OrderBy(b => Owner(b) == id ? 0 : 1).ThenBy(b => b.Kind).ThenBy(b => b.Element).ThenByDescending(b => b.Grade).ToList();
        }

        public List<LitStrategy> Strategies(string id, int intel) => CircuitMath.Lit(Circuit(id), Slots(id), intel);
        public List<LitTechnique> Techniques(string id, int intel) => CircuitMath.Techniques(Circuit(id), Slots(id), intel);

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

        // "liu_bei=Ru:0.8,1.4;guan_yu=Bing:…" — slot.libraryIndex pairs; library order is fixed by Bookcase.
        public string Serialize() => string.Join(";", slots.Select(kv =>
            $"{kv.Key}={SchoolOf(kv.Key)}:" + string.Join(",", kv.Value.Select((b, i) => (b, i)).Where(x => x.b != null).Select(x => $"{x.i}.{Library.IndexOf(x.b)}"))));

        public void Restore(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            foreach (var entry in text.Split(';'))
            {
                int eq = entry.IndexOf('='), colon = entry.IndexOf(':');
                if (eq <= 0 || colon < eq) continue;
                string id = entry.Substring(0, eq);
                if (!System.Enum.TryParse(entry.Substring(eq + 1, colon - eq - 1), out School school)) continue;
                var s = new Book[7];
                foreach (var pair in entry.Substring(colon + 1).Split(',', System.StringSplitOptions.RemoveEmptyEntries))
                {
                    var p = pair.Split('.');
                    if (p.Length == 2 && int.TryParse(p[0], out int slot) && int.TryParse(p[1], out int book)
                        && slot >= 0 && slot < s.Length && book >= 0 && book < Library.Count && Owner(Library[book]) == null && !s.Contains(Library[book]))
                        s[slot] = Library[book];
                }
                schools[id] = school;
                slots[id] = s;
            }
        }
    }
}
