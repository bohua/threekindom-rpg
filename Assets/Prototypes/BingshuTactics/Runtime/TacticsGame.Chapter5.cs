// 0.5 徐州篇的战棋扩展（design/v0.5-xuzhou/SYSTEMS_V05.md §2）：后备换人。
// 上阵最多 3 人，其余在后备；轮到我方武将时可用“换人”（耗本次行动）与后备第一人互换，各自兵力与气势保持。
// 后备武将不在地图上（视同不在场），不计入“全军撤退”；上阵者全部撤退即败，后备不自动顶上。
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Prototype.BingshuTactics
{
    public sealed partial class TacticsGame
    {
        readonly List<(Unit unit, int hp)> bench = new List<(Unit, int)>();
        public IEnumerable<Unit> BenchUnits => bench.Select(b => b.unit);

        // Called after the stage's units are spawned: the reserves come onto the board and go straight to the bench.
        void BuildReserves()
        {
            if (Chapter?.Reserves == null) return;
            foreach (var d in Chapter.Reserves)
            {
                var u = SpawnUnit(d, NearestFree(d.Pos));
                Bench(u);
            }
            foreach (var u in units.Where(x => x.Team == Team.Player && !x.Ai && !bench.Any(b => b.unit == x)))
                if (bench.Count > 0 && !u.Crafts.Any(c => c.Name == "换人"))
                    u.Crafts.Add(Craft("换人", "与后备第一名武将互换（耗本次行动）", Effect.Buff, Area.Single, 0, 0, 0f, 0));
        }

        void Bench(Unit u)
        {
            bench.Add((u, u.Hp));
            u.Hp = 0; u.PendingCast = null; u.PendingFollow = null;
            u.Pos = new Vector2Int(-100 - bench.Count, -100);
            if (rigs.TryGetValue(u, out var rig)) { rig.DeadT = 9f; rig.R.enabled = false; rig.Shadow.gameObject.SetActive(false); }
        }

        bool BenchReady(Unit u) => bench.Count > 0 && u.Team == Team.Player && !u.Ai;

        IEnumerator SwapOut(Unit u)
        {
            if (bench.Count == 0) yield break;
            var (inc, hp) = bench[0];
            bench.RemoveAt(0);
            var at = u.Pos;
            Log($"{u.Name} 退下，{inc.Name} 上阵");
            popups.Add((views[u].position + Vector3.up * (2.3f * HeadK), "换人", new Color(0.6f, 0.9f, 1f), 0f));
            yield return new WaitForSeconds(0.3f);
            Bench(u);
            inc.Hp = Mathf.Max(1, hp); inc.Pos = at; inc.At = now + 10f;
            if (!inc.Crafts.Any(c => c.Name == "换人")) inc.Crafts.Add(Craft("换人", "与后备第一名武将互换（耗本次行动）", Effect.Buff, Area.Single, 0, 0, 0f, 0));
            views[inc].position = Tile(at);
            if (rigs.TryGetValue(inc, out var rig)) { rig.DeadT = 0f; rig.R.enabled = true; rig.Shadow.gameObject.SetActive(true); }
            popups.Add((views[inc].position + Vector3.up * (2.3f * HeadK), "上阵", new Color(0.6f, 1f, 0.75f), 0f));
            yield return new WaitForSeconds(0.4f);
        }
    }
}
