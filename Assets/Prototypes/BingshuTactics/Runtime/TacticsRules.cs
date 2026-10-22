// PROTOTYPE - NOT FOR PRODUCTION
// Question: 空轨式 AT 行动条＋小范围格子战斗，配合“学派回路镶嵌兵书→策略”，是否好玩、是否看得懂？
// Date: 2026-10-22
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Prototype.BingshuTactics
{
    public enum Element { None, Metal, Wood, Water, Fire, Earth }
    public enum School { Ru, Bing, Dao, Mo, Fa }
    public enum Team { Player, Enemy }
    public enum Troop { Infantry, Cavalry, Archer, Rebel, Strategist }
    public enum Area { Single, Circle, Line, SelfCircle }
    public enum Effect { Damage, Heal, Guard, Buff, Debuff }

    public static class Wuxing
    {
        public static string Name(Element e) => e switch
        {
            Element.Metal => "金", Element.Wood => "木", Element.Water => "水",
            Element.Fire => "火", Element.Earth => "土", _ => "无"
        };

        public static Color Tint(Element e) => e switch
        {
            Element.Metal => new Color(0.86f, 0.84f, 0.70f), Element.Wood => new Color(0.36f, 0.78f, 0.36f),
            Element.Water => new Color(0.30f, 0.55f, 0.95f), Element.Fire => new Color(0.95f, 0.38f, 0.22f),
            Element.Earth => new Color(0.82f, 0.62f, 0.25f), _ => new Color(0.6f, 0.6f, 0.6f)
        };

        // 相生：木生火、火生土、土生金、金生水、水生木
        public static Element Generates(Element e) => e switch
        {
            Element.Wood => Element.Fire, Element.Fire => Element.Earth, Element.Earth => Element.Metal,
            Element.Metal => Element.Water, Element.Water => Element.Wood, _ => Element.None
        };

        // 相克：木克土、土克水、水克火、火克金、金克木
        public static Element Overcomes(Element e) => e switch
        {
            Element.Wood => Element.Earth, Element.Earth => Element.Water, Element.Water => Element.Fire,
            Element.Fire => Element.Metal, Element.Metal => Element.Wood, _ => Element.None
        };

        public static readonly Element[] All = { Element.Metal, Element.Wood, Element.Water, Element.Fire, Element.Earth };
    }

    public sealed class Book
    {
        public string Name; public Element Element; public int Grade;
        public Book(string name, Element element, int grade) { Name = name; Element = element; Grade = grade; }
        public string GradeName => Grade switch { 1 => "残卷", 2 => "抄本", _ => "善本" };
    }

    public sealed class SlotDef
    {
        public Element Locked;     // None = 任意五行
        public Vector2 Pos;        // 绘制位置，单位圆坐标
        public SlotDef(Element locked, float x, float y) { Locked = locked; Pos = new Vector2(x, y); }
    }

    public sealed class CircuitDef
    {
        public School School; public string Name; public Element Affinity; public string Inclusivity; public string Note;
        public SlotDef[] Slots; public int[][] Lines;
        public int FreeSlots => Slots.Count(s => s.Locked == Element.None);
    }

    public static class Circuits
    {
        static Vector2 P(float deg, float r = 1f) => new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad) * r, Mathf.Sin(deg * Mathf.Deg2Rad) * r);
        static SlotDef S(Element e, Vector2 p) => new SlotDef(e, p.x, p.y);
        static SlotDef S(Element e, float deg, float r = 1f) => S(e, P(deg, r));

        // 各学派回路：五行布局（锁定位）与包容性（自由位多少、线路长短）不同。中枢位在多条线上，计入每条线。
        public static readonly CircuitDef[] All =
        {
            new CircuitDef
            {
                School = School.Ru, Name = "儒家", Affinity = Element.Water, Inclusivity = "高",
                Note = "中枢锁水，其余六位不限五行；两条四位线，均衡易配。",
                Slots = new[] { S(Element.Water, Vector2.zero), S(Element.None, 90), S(Element.None, 30), S(Element.None, -30),
                                S(Element.None, 150), S(Element.None, 210), S(Element.None, 270) },
                Lines = new[] { new[] { 0, 1, 2, 3 }, new[] { 0, 4, 5, 6 } }
            },
            new CircuitDef
            {
                School = School.Bing, Name = "兵家", Affinity = Element.Fire, Inclusivity = "中",
                Note = "六位一线贯通，威力最易叠高；但三位锁火，配法偏狭。",
                Slots = new[] { S(Element.Fire, Vector2.zero), S(Element.None, 90), S(Element.Fire, 30), S(Element.None, -30),
                                S(Element.Fire, 270), S(Element.None, 210) },
                Lines = new[] { new[] { 0, 1, 2, 3, 4, 5 } }
            },
            new CircuitDef
            {
                School = School.Dao, Name = "道家", Affinity = Element.Wood, Inclusivity = "最高",
                Note = "七位全不限五行，但分成三条三位短线：策略种类多、高阶难成。",
                Slots = new[] { S(Element.None, Vector2.zero), S(Element.None, 90), S(Element.None, 50), S(Element.None, -30),
                                S(Element.None, -70), S(Element.None, 210), S(Element.None, 170) },
                Lines = new[] { new[] { 0, 1, 2 }, new[] { 0, 3, 4 }, new[] { 0, 5, 6 } }
            },
            new CircuitDef
            {
                School = School.Mo, Name = "墨家", Affinity = Element.Metal, Inclusivity = "中",
                Note = "中枢锁金、首位锁土（土生金）；一条四位线加一条三位线。",
                Slots = new[] { S(Element.Metal, Vector2.zero), S(Element.Earth, 90), S(Element.None, 30), S(Element.None, -30),
                                S(Element.Metal, 210), S(Element.None, 150) },
                Lines = new[] { new[] { 0, 1, 2, 3 }, new[] { 0, 4, 5 } }
            },
            new CircuitDef
            {
                School = School.Fa, Name = "法家", Affinity = Element.Earth, Inclusivity = "最低",
                Note = "三位锁土、一位锁金，只剩两位自由；主线五位，法度森严而强。",
                Slots = new[] { S(Element.Earth, Vector2.zero), S(Element.Earth, 90), S(Element.Earth, 30), S(Element.None, -30),
                                S(Element.None, 270), S(Element.Metal, 180) },
                Lines = new[] { new[] { 0, 1, 2, 3, 4 }, new[] { 0, 5 } }
            },
        };

        public static CircuitDef Of(School s) => All.First(c => c.School == s);
    }

    public sealed class SkillDef
    {
        public string Name; public string Desc; public bool IsStrategy; public Element Element = Element.None;
        public Effect Effect; public Area Area; public int Range; public int Radius; public int Length; public int Width = 1;
        public float Power; public bool UsesMight;        // true=物攻／物防；false=计攻／计防
        public int Cost; public int Cast; public int Recover = 80; public int AtDelay;
        public int Turns; public float Amount;            // 增减益幅度
        public Dictionary<Element, int> Req = new Dictionary<Element, int>();
        public Element Main => Req.Count == 0 ? Element : Req.OrderByDescending(kv => kv.Value).First().Key;
        public bool TargetsAllies => Effect == Effect.Heal || Effect == Effect.Guard || Effect == Effect.Buff;
    }

    public static class Strategies
    {
        static SkillDef D(string name, Element e, int main, Element sec, int secReq, string desc)
        {
            var s = new SkillDef { Name = name, Element = e, IsStrategy = true, Desc = desc };
            s.Req[e] = main; if (sec != Element.None) s.Req[sec] = secReq;
            return s;
        }

        // 策略：靠回路中“单条线”的五行合计点亮。阶位门槛 2 / 4+1 / 7+2。
        public static readonly SkillDef[] All =
        {
            Set(D("引火", Element.Fire, 2, Element.None, 0, "单体纵火"), Effect.Damage, Area.Single, 5, 0, 1.25f, 6, 40),
            Set(D("火烧营寨", Element.Fire, 4, Element.Wood, 1, "半径1纵火"), Effect.Damage, Area.Circle, 5, 1, 1.0f, 12, 70),
            Set(D("火海", Element.Fire, 7, Element.Wood, 2, "半径2大火"), Effect.Damage, Area.Circle, 6, 2, 1.1f, 22, 110),
            Set(D("截流", Element.Water, 2, Element.None, 0, "单体，推迟其行动"), Effect.Damage, Area.Single, 5, 0, 1.0f, 6, 40, atDelay: 30),
            Set(D("水攻", Element.Water, 4, Element.Metal, 1, "直线5格，推迟行动"), Effect.Damage, Area.Line, 1, 0, 0.9f, 12, 70, len: 5, atDelay: 25),
            Set(D("决堤", Element.Water, 7, Element.Metal, 2, "宽3长7洪水，大幅推迟"), Effect.Damage, Area.Line, 1, 0, 1.05f, 22, 110, len: 7, width: 3, atDelay: 45),
            Set(D("抚军", Element.Wood, 2, Element.None, 0, "单体回复30%"), Effect.Heal, Area.Single, 4, 0, 0.30f, 6, 30),
            Set(D("鼓舞", Element.Wood, 4, Element.Water, 1, "半径1回复20%并物攻+20%"), Effect.Heal, Area.Circle, 4, 1, 0.20f, 12, 60, turns: 2, amount: 0.2f),
            Set(D("收拢溃卒", Element.Wood, 7, Element.Water, 2, "全军回复40%"), Effect.Heal, Area.Circle, 0, 99, 0.40f, 22, 100),
            Set(D("坚壁", Element.Earth, 2, Element.None, 0, "单体受伤-25%，2回合"), Effect.Guard, Area.Single, 4, 0, 0f, 6, 30, turns: 2, amount: 0.25f),
            Set(D("八阵", Element.Earth, 6, Element.Fire, 2, "半径2友军受伤-35%，3回合"), Effect.Guard, Area.Circle, 3, 2, 0f, 20, 90, turns: 3, amount: 0.35f),
            Set(D("箭雨", Element.Metal, 3, Element.None, 0, "半径1箭雨（吃武力）"), Effect.Damage, Area.Circle, 6, 1, 0.85f, 10, 60, might: true),
            Set(D("万箭齐发", Element.Metal, 6, Element.Earth, 2, "半径2箭雨（吃武力）"), Effect.Damage, Area.Circle, 6, 2, 1.0f, 20, 100, might: true),
        };

        static SkillDef Set(SkillDef s, Effect fx, Area area, int range, int radius, float power, int cost, int cast,
            int len = 0, int width = 1, int atDelay = 0, int turns = 0, float amount = 0f, bool might = false)
        {
            s.Effect = fx; s.Area = area; s.Range = range; s.Radius = radius; s.Power = power; s.Cost = cost; s.Cast = cast;
            s.Length = len; s.Width = width; s.AtDelay = atDelay; s.Turns = turns; s.Amount = amount; s.UsesMight = might;
            return s;
        }
    }

    public readonly struct LitStrategy
    {
        public readonly SkillDef Def; public readonly int Line; public readonly int Overflow; public readonly float PowerMul;
        public LitStrategy(SkillDef def, int line, int overflow)
        {
            Def = def; Line = line; Overflow = overflow; PowerMul = CircuitMath.PowerMul(overflow);
        }
    }

    public static class CircuitMath
    {
        public const float OverflowStep = 0.08f; public const int OverflowCap = 5;

        public static bool Fits(CircuitDef c, int slot, Book b) =>
            b != null && slot >= 0 && slot < c.Slots.Length && (c.Slots[slot].Locked == Element.None || c.Slots[slot].Locked == b.Element);

        // 一本书在该学派回路中的五行值：品级，若与学派偏向五行相同再 +1。
        public static int Value(CircuitDef c, Book b) => b == null ? 0 : b.Grade + (b.Element == c.Affinity ? 1 : 0);

        public static Dictionary<Element, int> LineTotals(CircuitDef c, Book[] slots, int line)
        {
            var t = Wuxing.All.ToDictionary(e => e, e => 0);
            foreach (int i in c.Lines[line])
                if (i < slots.Length && slots[i] != null) t[slots[i].Element] += Value(c, slots[i]);
            return t;
        }

        public static float PowerMul(int overflow) => 1f + OverflowStep * Mathf.Min(overflow, OverflowCap);

        // 每个策略取能点亮它、且主五行溢出最多的那条线；溢出（超出门槛的点数）提高威力。
        public static List<LitStrategy> Lit(CircuitDef c, Book[] slots)
        {
            var result = new List<LitStrategy>();
            var totals = Enumerable.Range(0, c.Lines.Length).Select(l => LineTotals(c, slots, l)).ToArray();
            foreach (var s in Strategies.All)
            {
                int best = -1, bestOver = -1;
                for (int l = 0; l < totals.Length; l++)
                {
                    if (!s.Req.All(kv => totals[l][kv.Key] >= kv.Value)) continue;
                    int over = totals[l][s.Main] - s.Req[s.Main];
                    if (over > bestOver) { best = l; bestOver = over; }
                }
                if (best >= 0) result.Add(new LitStrategy(s, best, bestOver));
            }
            return result;
        }
    }

    public sealed class Status { public string Name; public float AtkMul = 1f, DefMul = 1f, Guard; public int Turns; }

    public sealed class Unit
    {
        public string Name, Key; public Team Team; public Troop Troop; public Element Element; public School School;
        public int MaxHp, Hp, Atk, Def, Mat, Mdef, Speed, Move, AtkRange, MaxEp, Ep, Cp;
        public Vector2Int Pos; public float At; public bool Defending;
        public List<Status> Statuses = new List<Status>();
        public List<SkillDef> Crafts = new List<SkillDef>();
        public Book[] Slots = new Book[7];
        public SkillDef PendingCast; public Unit PendingFollow; public float PendingMul; public Vector2Int PendingTarget; public Vector2Int PendingDir;
        public bool Alive => Hp > 0;
        public CircuitDef Circuit => Circuits.Of(School);

        // 增益取最强、减益取最强，两者相乘
        public float AtkMul => Statuses.Select(s => s.AtkMul).Where(m => m < 1f).DefaultIfEmpty(1f).Min()
                             * Statuses.Select(s => s.AtkMul).Where(m => m > 1f).DefaultIfEmpty(1f).Max();
        public float DefMul => Statuses.Where(s => s.DefMul < 1f).Select(s => s.DefMul).DefaultIfEmpty(1f).Min();
        public float Guard => Statuses.Select(s => s.Guard).DefaultIfEmpty(0f).Max();

        public void AddStatus(Status s)
        {
            // V1：同属性同方向不叠加，取较强并刷新回合
            var same = Statuses.FirstOrDefault(x => x.Name == s.Name);
            if (same != null) Statuses.Remove(same);
            Statuses.Add(s);
        }

        public void TickStatuses()
        {
            foreach (var s in Statuses) s.Turns--;
            Statuses.RemoveAll(s => s.Turns <= 0);
        }
    }

    public static class Combat
    {
        public const float TroopAdv = 0.4f, ElementAdv = 0.2f, AdvCap = 0.6f, MinTaken = 0.3f;

        public static bool TroopBeats(Troop a, Troop b) =>
            (a == Troop.Cavalry && (b == Troop.Infantry || b == Troop.Rebel)) ||
            (a == Troop.Infantry && b == Troop.Archer) || (a == Troop.Archer && b == Troop.Cavalry);

        public static float AdvMul(Unit a, Unit t, Element skillElement)
        {
            float adv = 0f;
            if (TroopBeats(a.Troop, t.Troop)) adv += TroopAdv;
            if (skillElement != Element.None && Wuxing.Overcomes(skillElement) == t.Element) adv += ElementAdv;
            return 1f + Mathf.Min(adv, AdvCap);
        }

        public static float TakenMul(Unit t)
        {
            float reduce = Mathf.Max(t.Defending ? 0.5f : 0f, t.Guard);   // 主动减伤只取最强
            return Mathf.Max(MinTaken, 1f - reduce);
        }

        public static int Damage(Unit a, Unit t, float coef, bool might, Element skillElement)
        {
            float atk = (might ? a.Atk : a.Mat) * a.AtkMul;
            float def = (might ? t.Def : t.Mdef) * t.DefMul;
            float baseDmg = atk * coef - def * 0.5f;
            return Mathf.Max(1, Mathf.FloorToInt(baseDmg * AdvMul(a, t, skillElement) * TakenMul(t)));
        }

        public static int Heal(Unit t, float rate) => Mathf.Min(t.MaxHp - t.Hp, Mathf.FloorToInt(t.MaxHp * rate));
    }

    public sealed class Grid
    {
        public readonly int W, H; public readonly bool[,] Blocked;
        public Grid(int w, int h) { W = w; H = h; Blocked = new bool[w, h]; }
        public bool In(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < W && p.y < H;
        public static int Dist(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        // 可达格（含起点）及来路；所有单位都挡路，不能穿过
        public Dictionary<Vector2Int, Vector2Int> Reachable(Vector2Int start, int move, Func<Vector2Int, bool> occupied)
        {
            var parent = new Dictionary<Vector2Int, Vector2Int> { [start] = start };
            var frontier = new Queue<(Vector2Int, int)>(); frontier.Enqueue((start, 0));
            while (frontier.Count > 0)
            {
                var (p, d) = frontier.Dequeue();
                if (d == move) continue;
                foreach (var dir in Dirs)
                {
                    var n = p + dir;
                    if (!In(n) || Blocked[n.x, n.y] || parent.ContainsKey(n) || occupied(n)) continue;
                    parent[n] = p; frontier.Enqueue((n, d + 1));
                }
            }
            return parent;
        }

        public static List<Vector2Int> Path(Dictionary<Vector2Int, Vector2Int> parent, Vector2Int to)
        {
            var path = new List<Vector2Int> { to };
            while (parent[to] != to) { to = parent[to]; path.Add(to); }
            path.Reverse();
            return path;
        }

        public IEnumerable<Vector2Int> Diamond(Vector2Int c, int r)
        {
            for (int x = -r; x <= r; x++)
                for (int y = -r + Mathf.Abs(x); y <= r - Mathf.Abs(x); y++)
                {
                    var p = new Vector2Int(c.x + x, c.y + y);
                    if (In(p)) yield return p;
                }
        }

        public static Vector2Int DirTo(Vector2Int from, Vector2Int to)
        {
            var d = to - from;
            if (d == Vector2Int.zero) return Vector2Int.right;
            return Mathf.Abs(d.x) >= Mathf.Abs(d.y) ? new Vector2Int(Math.Sign(d.x), 0) : new Vector2Int(0, Math.Sign(d.y));
        }

        public IEnumerable<Vector2Int> LineArea(Vector2Int origin, Vector2Int dir, int len, int width)
        {
            var side = new Vector2Int(-dir.y, dir.x);
            for (int k = 1; k <= len; k++)
                for (int w = -(width / 2); w <= width / 2; w++)
                {
                    var p = origin + dir * k + side * w;
                    if (In(p)) yield return p;
                }
        }

        public IEnumerable<Vector2Int> AreaOf(SkillDef s, Vector2Int caster, Vector2Int target)
        {
            switch (s.Area)
            {
                case Area.Single: return new[] { target };
                case Area.Circle: return Diamond(target, s.Radius);
                case Area.SelfCircle: return Diamond(caster, s.Radius);
                default: return LineArea(caster, DirTo(caster, target), s.Length, s.Width);
            }
        }
    }

    // AT 行动条：数值小者先动；动作越重，推后越多；速度越快，推后越少。
    public static class AtClock
    {
        public static float Delay(int cost, int speed) => cost * 8f / Mathf.Max(1, speed);

        public static Unit Next(IEnumerable<Unit> units) =>
            units.Where(u => u.Alive).OrderBy(u => u.At).ThenBy(u => u.Team == Team.Player ? 0 : 1).FirstOrDefault();
    }
}
