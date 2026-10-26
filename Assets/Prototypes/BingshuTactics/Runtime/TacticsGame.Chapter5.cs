// 0.5 徐州篇的战棋扩展（design/v0.5-xuzhou/SYSTEMS_V05.md §2，修订见 REVIEW_V05 R9–R12）：后备换人。
// 上阵最多 3 人，其余在后备（Benched：不在地图上，兵力与气势保持）。轮到我方武将时，每名后备有一个“换上·某某”武技
// （耗本次行动）；换下者清除增减益。后备不计入“全军撤退”，上阵者全部撤退即败。电脑代打不换人。
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Prototype.BingshuTactics
{
    public sealed partial class TacticsGame
    {
        readonly List<Unit> reserveBench = new List<Unit>();
        public IEnumerable<Unit> BenchUnits => reserveBench;

        // After the stage's units are spawned: the reserves are made and sent straight to the bench.
        void BuildReserves()
        {
            if (Chapter?.Reserves == null || Chapter.Reserves.Count == 0) return;
            foreach (var d in Chapter.Reserves) Bench(SpawnUnit(d, NearestFree(d.Pos)));
            RefreshSwapCrafts();
        }

        void Bench(Unit u)
        {
            reserveBench.Add(u);
            u.Benched = true; u.PendingCast = null; u.PendingFollow = null; u.Statuses.Clear(); u.Guardian = null;
            u.Pos = new Vector2Int(-100 - reserveBench.Count, -100);
            if (rigs.TryGetValue(u, out var rig)) { rig.DeadT = 9f; rig.R.enabled = false; rig.Shadow.gameObject.SetActive(false); }
        }

        // Every active player general gets one “换上·<name>” per reserve.
        void RefreshSwapCrafts()
        {
            foreach (var u in units.Where(x => x.Team == Team.Player && !x.Ai))
            {
                u.Crafts.RemoveAll(c => c.Name.StartsWith("换上·"));
                if (u.Benched) continue;
                foreach (var b in reserveBench)
                    u.Crafts.Add(Craft("换上·" + b.Name, $"{b.Name}上阵，{u.Name}退到后备（耗本次行动）", Effect.Buff, Area.Single, 0, 0, 0f, 0));
            }
        }

        bool BenchReady(Unit u, string name) => u.Team == Team.Player && !u.Ai && reserveBench.Any(b => b.Name == name);

        IEnumerator SwapOut(Unit u, string name)
        {
            var inc = reserveBench.FirstOrDefault(b => b.Name == name);
            if (inc == null) yield break;
            reserveBench.Remove(inc);
            var at = u.Pos;
            Log($"{u.Name} 退下，{inc.Name} 上阵");
            popups.Add((views[u].position + Vector3.up * (2.3f * HeadK), "换人", new Color(0.6f, 0.9f, 1f), 0f));
            yield return new WaitForSeconds(0.3f);
            Bench(u);
            inc.Benched = false; inc.Pos = at; inc.At = now + 10f;
            views[inc].position = Tile(at);
            if (rigs.TryGetValue(inc, out var rig)) { rig.DeadT = 0f; rig.R.enabled = true; rig.Shadow.gameObject.SetActive(true); }
            popups.Add((views[inc].position + Vector3.up * (2.3f * HeadK), "上阵", new Color(0.6f, 1f, 0.75f), 0f));
            RefreshSwapCrafts();
            yield return new WaitForSeconds(0.4f);
        }
    }
}
