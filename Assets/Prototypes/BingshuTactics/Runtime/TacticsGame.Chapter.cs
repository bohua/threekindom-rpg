// 0.2 黄巾篇的三场战斗改用本战棋（用户 2026-10-25：“这里的几场战斗，我想现在开始就改成我们之前做虎牢关的那种战斗模式。
// 不再是回合制的 RPG，而是地图的 SLG。”）。决策见游戏仓库 docs/architecture/adr-0010-chapter-tactics.md。
// 主线在载入战斗场景前填好 TacticsGame.Chapter：地图、双方武将（数值由主线成长公式算好）、战中事件和结束回调。
// 本文件不引用正式代码，奖励、剧情与存档都由回调交还主线处理。
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Prototype.BingshuTactics
{
    public sealed class TacticsUnitDef
    {
        public string Name, Key;
        public Team Team;
        public bool Ai, Escort, Leader;     // Ai：我方阵营但由电脑操作（友军）；Escort：倒下即败；Leader：头顶标名
        public string Focus;                // 优先攻击的我方单位名（张角亲卫盯董卓）
        public Troop Troop;
        public School School = School.Bing;
        public int Level, Hp, Atk, Def, Mat, Mdef, Speed, Move, Range = 1, Ep, Int, Wu;
        public Vector2Int Pos;
        public float Floor;                 // 0.3：兵力不低于 MaxHp×Floor（吕布）
        public string[] Crafts = new string[0];
        public Element Element = Element.None;
    }

    public enum TacticsActionKind { Banner, Line, Objective, Damage, Chaos, RetreatAll, Retreat, Fire, Win, Spawn, Choice, Guard, Cancel, OpenExit }

    public sealed class TacticsAction
    {
        public TacticsActionKind Kind;
        public string Speaker, Text, Unit;
        public Team Team = Team.Enemy;
        public float Value;
        public int Turns;
        public Vector2Int[] Tiles = new Vector2Int[0];
        public TacticsUnitDef[] Units = new TacticsUnitDef[0];   // Spawn：登场的单位（落在最近的空格）
        // 0.3（SYSTEMS_V03 §3）：Choice＝战中选择（Text 提问、Options 选项键文字、Branches 分支、Unit＝回传标记前缀）；
        // Guard＝Speaker 对 Unit 施“众望”；Cancel＝取消 Unit 指名的事件；Spawn 时 Near 指名则贴着该单位落位。
        public string[] Options = new string[0];
        public TacticsAction[][] Branches = new TacticsAction[0][];
        public string Near;
    }

    // Round > 0：到该回合时触发；HpBelow：该单位兵力低于 Ratio 时触发。每个事件只触发一次。
    public sealed class TacticsEvent
    {
        public string Id;
        public int Round;
        public string HpBelow; public float Ratio;
        public TacticsAction[] Actions = new TacticsAction[0];
        internal bool Fired;
    }

    public struct TacticsOutcome { public bool Victory; public int Rounds; public List<string> Flags; }

    // 对白立绘：Crop＝从顶部截取的高度比例，Axis＝水平中心
    public struct TacticsPortrait { public Texture2D Texture; public float Crop, Axis; public bool Flip; }

    public sealed class TacticsStageDef
    {
        public string Id, Title, Where, Objective;
        public string[] Rows;
        public RectInt Deploy;
        public bool Night;
        public List<TacticsUnitDef> Units = new List<TacticsUnitDef>();
        public List<TacticsEvent> Events = new List<TacticsEvent>();
        public System.Action<TacticsOutcome> Finished;
        public List<(string speaker, string text)> OpeningLines = new List<(string, string)>(), VictoryLines = new List<(string, string)>();
        public Dictionary<string, TacticsPortrait> Portraits = new Dictionary<string, TacticsPortrait>();   // 按说话人显示名
        public int Seed = 1;                // 混乱恢复的随机数种子（重开同一场结果一致）
        public bool AutoPlay;               // 测试与截图：我方也由电脑代打，台词自动翻页
        public bool NoRetreatOnDefeat;
        public int Reputation;
        public RectInt? ExitZone;
        public List<TacticsUnitDef> Reserves = new List<TacticsUnitDef>();   // 0.5：后备武将（不在地图上，可换人上阵）           // 0.4：撤离区（OpenExit 之后，我方进入即撤出战场）              // 0.3：声望（“众望”的物防加成随之增强）
    }

    public sealed partial class TacticsGame
    {
        // 主线填入；为 null 时是原型自己的两关
        public static TacticsStageDef Chapter;
        // 0.2 角色像素图（主线角色库），按材质名前缀匹配单位的 Key
        public List<Material> CharMats = new List<Material>();
        // 0.2 高清地块（Codex art/v02-tactics-ground）：名称为 hd_<地形>
        public Texture2D[] HdGround = new Texture2D[0];

        public bool ChapterFinished { get; private set; }
        public bool Won => mode == Mode.Victory;
        public bool Lost => mode == Mode.Defeat;
        public int Rounds => RoundNo;
        public IEnumerable<Unit> AllUnits => units;

        string objective, bannerText; float bannerT = 99f;
        (string speaker, string text)? line;
        readonly List<(Transform t, Light l, float phase)> fires = new List<(Transform, Light, float)>();
        Material fireMat;

        bool IsChapter => Chapter != null;

        public static (Terrain, int) ChapterLegend(char ch) => ch switch
        {
            't' => (Terrain.Woods, 0), 'T' => (Terrain.Woods, 1), 'm' => (Terrain.Ford, 0), 'R' => (Terrain.Rock, 1), 'B' => (Terrain.Rock, 2),
            'v' => (Terrain.Road, 1), 'V' => (Terrain.Road, 2), 's' => (Terrain.Sand, 1), 'C' => (Terrain.Camp, 2), 'p' => (Terrain.JuMa, 2),
            _ => Legend(ch)
        };

        void BuildChapterUnits()
        {
            foreach (var d in Chapter.Units) SpawnUnit(d, d.Pos);
            BuildReserves();
            objective = Chapter.Objective;
        }

        Unit SpawnUnit(TacticsUnitDef d, Vector2Int pos)
        {
            var crafts = new Dictionary<string, SkillDef>
            {
                ["整队"] = Craft("整队", "单体友军回复30%兵力", Effect.Heal, Area.Single, 3, 0, 0.30f, 20),
                ["火矢"] = Craft("火矢", "单体计策1.3倍（射程4）", Effect.Damage, Area.Single, 4, 0, 1.3f, 25),
                ["突击"] = Craft("突击", "近战单体物理1.5倍", Effect.Damage, Area.Single, -1, 0, 1.5f, 25, might: true),
                ["破阵"] = Craft("破阵", "近战单体1.1倍，物防-20% 2回合", Effect.Debuff, Area.Single, -1, 0, 1.1f, 30, might: true, turns: 2, amount: 0.2f),
                ["横扫"] = Craft("横扫", "自身周围2格内敌军物理0.65倍", Effect.Damage, Area.SelfCircle, 0, 2, 0.65f, 30, might: true),
                ["威慑"] = Craft("威慑", "自身周围3格内敌军物攻-20% 2回合", Effect.Debuff, Area.SelfCircle, 0, 3, 0f, 25, turns: 2, amount: 0.2f),
            };
            var u = NewUnit(d.Name, d.Key, d.Team, d.Troop, d.School, d.Hp, d.Atk, d.Def, d.Mat, d.Mdef, d.Speed, d.Move, d.Range, d.Ep, pos);
            u.Int = d.Int; u.Wu = d.Wu; u.Level = d.Level; u.Ai = d.Ai; u.Escort = d.Escort; u.Leader = d.Leader; u.Focus = d.Focus;
            u.Floor = d.Floor;
            AddChapter3Crafts(u, d, crafts);
            if (d.Element != Element.None) u.Element = d.Element;
            foreach (var c in d.Crafts) if (crafts.TryGetValue(c, out var s)) u.Crafts.Add(s);
            DefaultBooks(u);
            return u;
        }

        // 离 want 最近、能站人的空格
        Vector2Int NearestFree(Vector2Int want)
        {
            Vector2Int best = want; int bd = int.MaxValue;
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                {
                    var p = new Vector2Int(x, y);
                    if (Grid.Impassable(grid.TerrainAt(p)) || At(p) != null) continue;
                    int d = Grid.Dist(p, want);
                    if (d < bd) { bd = d; best = p; }
                }
            return best;
        }

        Material CharMat(string key) => CharMats.FirstOrDefault(m => m && (m.name.StartsWith(key + "_") || m.name.StartsWith("Npc" + key + "_")));

        Texture2D HdTop(Terrain t)
        {
            if (!IsChapter || HdGround == null) return null;
            string n = "hd_" + t.ToString().ToLowerInvariant();
            return HdGround.FirstOrDefault(x => x && x.name == n);
        }

        // 夜战：冷月光、暗蓝环境，火光另加点光源
        void ChapterLighting(Light sun)
        {
            if (!Chapter.Night) return;
            sun.color = new Color(0.70f, 0.78f, 1f); sun.intensity = 1.5f;
            sun.transform.rotation = Quaternion.Euler(55f, 200f, 0f);
            RenderSettings.ambientSkyColor = new Color(0.50f, 0.56f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.40f, 0.42f, 0.56f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.22f, 0.28f);
            RenderSettings.fogColor = new Color(0.10f, 0.12f, 0.20f);
            cam.backgroundColor = new Color(0.04f, 0.05f, 0.09f);
        }

        int CurrentRound => Mathf.Max(1, RoundNo);

        // 每次行动前：到期的回合事件依次发生
        IEnumerator ChapterRoundEvents()
        {
            foreach (var e in Chapter.Events.Where(e => !e.Fired && e.Round > 0 && e.Round <= CurrentRound).OrderBy(e => e.Round).ToList())
            {
                if (e.Fired) continue;   // cancelled by an earlier event this pass (0.3)
            {
                e.Fired = true;
                yield return RunActions(e.Actions);
                if (mode == Mode.Victory || mode == Mode.Defeat) yield break;
            }
            }
        }

        // 每次行动后：兵力条件
        IEnumerator ChapterHpEvents()
        {
            foreach (var e in Chapter.Events.Where(e => !e.Fired && e.HpBelow != null).ToList())
            {
                var u = units.FirstOrDefault(x => x.Name == e.HpBelow);
                if (u == null || u.Hp >= u.MaxHp * e.Ratio) continue;
                e.Fired = true;
                yield return RunActions(e.Actions);
                if (mode == Mode.Victory || mode == Mode.Defeat) yield break;
            }
        }

        IEnumerator RunActions(IEnumerable<TacticsAction> actions)
        {
            foreach (var a in actions)
            {
                switch (a.Kind)
                {
                    case TacticsActionKind.Banner:
                        bannerText = a.Text; bannerT = 0f;
                        yield return new WaitForSeconds(1.2f);
                        break;
                    case TacticsActionKind.Line:
                        yield return ShowLine(a.Speaker, a.Text);
                        break;
                    case TacticsActionKind.Objective:
                        objective = a.Text;
                        break;
                    case TacticsActionKind.Damage:
                        foreach (var t in units.Where(x => x.Alive && x.Team == a.Team && !x.Escort))
                        {
                            int d = Mathf.Min(t.Hp - Mathf.Max(1, FloorHp(t)), Mathf.Max(1, Mathf.FloorToInt(t.MaxHp * a.Value)));
                            if (d <= 0) continue;
                            t.Hp -= d;
                            popups.Add((views[t].position + Vector3.up * (1.8f * HeadK), d.ToString(), new Color(1f, 0.6f, 0.3f), 0f));
                        }
                        yield return new WaitForSeconds(0.4f);
                        break;
                    case TacticsActionKind.Chaos:
                        foreach (var t in units.Where(x => x.Alive && x.Team == a.Team && !x.Escort))
                        {
                            Confuse(t, Mathf.Max(1, a.Turns));
                            popups.Add((views[t].position + Vector3.up * (2.2f * HeadK), "混乱", new Color(1f, 0.55f, 0.5f), 0f));
                        }
                        yield return new WaitForSeconds(0.4f);
                        break;
                    case TacticsActionKind.Fire:
                        foreach (var p in a.Tiles) Ignite(p);
                        yield return new WaitForSeconds(0.6f);
                        break;
                    case TacticsActionKind.Retreat:
                        foreach (var t in units.Where(x => x.Alive && x.Name == a.Unit)) Withdraw(t);
                        yield return new WaitForSeconds(0.4f);
                        break;
                    case TacticsActionKind.RetreatAll:
                        foreach (var t in units.Where(x => x.Alive && x.Team == a.Team)) Withdraw(t);
                        yield return new WaitForSeconds(0.6f);
                        CheckChapterEnd(forced: a.Team == Team.Enemy);
                        if (mode == Mode.Victory) yield break;
                        break;
                    case TacticsActionKind.Spawn:   // 援军登场（用户 2026-10-25：援军加入成为友军，不让敌军撤退）
                        for (int i = 0; i < a.Units.Length; i++)
                        {
                            var u = SpawnUnit(a.Units[i], NearestFree(SpawnSpot(a, a.Units[i])));
                            bool foe = u.Team == Team.Enemy;   // 0.5（REVIEW_V05 R12）：敌方波次红色“增援”，登场后延迟到下一轮才行动
                            u.At = now + (foe ? 100f : 5f) * (i + 1);
                            popups.Add((views[u].position + Vector3.up * (2.2f * HeadK), foe ? "增援" : "援军", foe ? new Color(1f, 0.5f, 0.45f) : new Color(0.6f, 1f, 0.75f), 0f));
                            flashes.Add((new List<Vector2Int> { u.Pos }, foe ? new Color(1f, 0.4f, 0.35f) : new Color(0.5f, 1f, 0.7f), 0f));
                        }
                        yield return new WaitForSeconds(0.6f);
                        break;
                    case TacticsActionKind.Win:
                        CheckChapterEnd(forced: true);
                        yield break;
                    case TacticsActionKind.Choice:
                    case TacticsActionKind.Guard:
                    case TacticsActionKind.Cancel:
                    case TacticsActionKind.OpenExit:
                        yield return RunAction3(a);
                        if (mode == Mode.Victory || mode == Mode.Defeat) yield break;
                        break;
                }
            }
        }

        // 退走（不是阵亡）：从战场撤下
        void Withdraw(Unit t)
        {
            t.Hp = 0; t.PendingCast = null; t.PendingFollow = null;
            popups.Add((views[t].position + Vector3.up * (2.3f * HeadK), "退走", new Color(1f, 0.8f, 0.5f), 0f));
        }

        // ---------------- 混乱（用户 2026-10-25） ----------------
        // 剧情强制混乱：原地不动挨打，持续 forcedTurns 次行动；之后转为普通混乱：再原地不动 1 次，
        // 此后每次轮到时按智力掷骰恢复，恢复率随混乱的次数上升，直到恢复。混乱期间物攻、计攻−30%，物防−20%。
        System.Random chaosRng;

        void Confuse(Unit t, int forcedTurns)
        {
            t.Chaos = true; t.ChaosForced = forcedTurns; t.ChaosTurns = 0;
            t.AddStatus(new Status { Name = "混乱", AtkMul = 0.7f, DefMul = 0.8f, Turns = 9999 });
            t.PendingCast = null; t.PendingFollow = null;
        }

        // 轮到混乱单位时调用：返回 true 表示本次原地不动
        bool ChaosHoldsTurn(Unit u)
        {
            if (!u.Chaos) return false;
            if (u.ChaosForced > 0) { u.ChaosForced--; Debug.Log($"TACTICS_CHAOS_FORCED {u.Name} left {u.ChaosForced}"); return true; }
            u.ChaosTurns++;
            if (u.ChaosTurns > 1)
            {
                chaosRng ??= new System.Random(Chapter?.Seed ?? 1);
                float p = Mathf.Clamp01(0.15f + u.Int / 250f + 0.15f * (u.ChaosTurns - 2));
                if (chaosRng.NextDouble() < p)
                {
                    u.Chaos = false;
                    u.Statuses.RemoveAll(s => s.Name == "混乱");
                    popups.Add((views[u].position + Vector3.up * (2.2f * HeadK), "恢复", new Color(0.6f, 1f, 0.7f), 0f));
                    Log($"{u.Name} 从混乱中恢复");
                    Debug.Log($"TACTICS_CHAOS_RECOVER {u.Name} after {u.ChaosTurns}");
                    return false;
                }
            }
            return true;
        }

        IEnumerator ShowLine(string speaker, string text)
        {
            line = (speaker, text);
            if (Chapter.AutoPlay || autoPlay) yield return new WaitForSeconds(0.5f);
            else
            {
                yield return null;
                while (!(ConfirmPressed || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame))) yield return null;
                yield return null;
            }
            line = null;
        }

        // 胜负：敌方全退（或事件宣布胜利）为胜；我方可操作武将全退或护送对象倒下为败
        bool CheckChapterEnd(bool forced = false)
        {
            if (mode == Mode.Victory || mode == Mode.Defeat) return true;
            if (ExitCheck()) return true;   // 0.4 阈值撤离
            bool escortDown = units.Any(x => x.Escort && !x.Alive);
            bool partyDown = !units.Any(x => x.Alive && x.Team == Team.Player && !x.Ai) && exited.Count == 0;
            if (escortDown || partyDown)
            {
                mode = Mode.Defeat;
                Log(escortDown ? "护送的友军已倒下……按确认重来。" : "我军败退……按确认重来。");
                return true;
            }
            if (forced || !units.Any(x => x.Alive && x.Team == Team.Enemy))
            {
                foreach (var t in units.Where(x => x.Alive && x.Team == Team.Enemy)) Withdraw(t);
                mode = Mode.Victory;
                Log("胜利！");
                StartCoroutine(VictorySequence());
                return true;
            }
            return false;
        }

        bool victoryReady;

        IEnumerator VictorySequence()
        {
            yield return new WaitForSeconds(0.8f);
            foreach (var (who, text) in Chapter.VictoryLines) yield return ShowLine(who, text);
            victoryReady = true;
            Log("胜利！按确认继续。");
            if (Chapter.AutoPlay) { yield return new WaitForSeconds(0.3f); FinishChapter(); }
        }

        IEnumerator OpeningLines()
        {
            foreach (var (who, text) in Chapter.OpeningLines) yield return ShowLine(who, text);
        }

        // 胜：交还主线；败：重开本场（战前什么也没写入，等于还原战前状态）
        void FinishChapter()
        {
            if (ChapterFinished) return;
            if (mode == Mode.Victory)
            {
                if (!victoryReady) return;
                ChapterFinished = true;
                var done = Chapter.Finished;
                done?.Invoke(new TacticsOutcome { Victory = true, Rounds = RoundNo, Flags = new List<string>(chapterFlags) });
            }
            else if (mode == Mode.Defeat)
            {
                foreach (var e in Chapter.Events) e.Fired = false;
                UnityEngine.SceneManagement.SceneManager.LoadScene(gameObject.scene.buildIndex);
            }
        }

        // ---------------- 火 ----------------
        void Ignite(Vector2Int p)
        {
            if (!grid.In(p) || grid.TerrainAt(p) == Terrain.Fire) return;
            var who = At(p);
            if (who != null) return;   // 有人站着的格子不点
            grid.Ter[p.x, p.y] = Terrain.Fire;
            if (!fireMat)
            {
                var tex = new Texture2D(32, 48, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                for (int x = 0; x < 32; x++)
                    for (int y = 0; y < 48; y++)
                    {
                        float cx = (x - 15.5f) / 16f, cy = y / 48f;
                        float wdt = (1f - cy) * (0.85f - 0.25f * Mathf.Sin(cy * 9f + x * 0.3f));
                        float a = Mathf.Clamp01((wdt - Mathf.Abs(cx)) * 4f);
                        var c = Color.Lerp(new Color(1f, 0.92f, 0.55f), new Color(0.95f, 0.30f, 0.08f), Mathf.Clamp01(Mathf.Abs(cx) * 1.4f + cy * 0.6f));
                        c.a = a;
                        tex.SetPixel(x, y, c);
                    }
                tex.Apply();
                fireMat = new Material(OverlayMat) { mainTexture = tex };
            }
            var root = new GameObject("Fire " + p).transform;
            root.position = Tile(p);
            for (int k = 0; k < 3; k++)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(q.GetComponent<Collider>());
                q.transform.SetParent(root, false);
                q.transform.localPosition = new Vector3((k - 1) * 0.28f, 0.25f, (k % 2) * 0.2f - 0.1f);
                q.transform.localScale = new Vector3(0.32f, 0.5f, 1f);
                var r = q.GetComponent<MeshRenderer>(); r.sharedMaterial = fireMat; r.shadowCastingMode = ShadowCastingMode.Off;
                billboards.Add(q.transform);
            }
            var l = new GameObject("Fire light").AddComponent<Light>();
            l.transform.SetParent(root, false); l.transform.localPosition = Vector3.up * 0.8f;
            l.type = LightType.Point; l.color = new Color(1f, 0.55f, 0.2f); l.range = 3f; l.intensity = 1f;
            fires.Add((root, l, Random.value * 10f));
            flashes.Add((new List<Vector2Int> { p }, new Color(1f, 0.5f, 0.1f), 0f));
        }

        void UpdateChapter()
        {
            if (!IsChapter) return;
            bannerT += Time.deltaTime;
            foreach (var (t, l, ph) in fires)
            {
                float f = Mathf.PerlinNoise(Time.time * 3f, ph);
                l.intensity = 0.7f + 0.6f * f;
                foreach (Transform q in t) if (q != l.transform) q.localScale = new Vector3(0.32f, 0.42f + 0.18f * f, 1f);
            }
        }

        // ---------------- 界面 ----------------
        static readonly Color Ink = new Color(0.063f, 0.11f, 0.19f), Silver = new Color(0.55f, 0.65f, 0.73f), Ivory = new Color(0.96f, 0.95f, 0.91f), Teal = new Color(0.38f, 0.84f, 0.91f);

        void DrawChapterLabels()
        {
            foreach (var u in units.Where(u => u.Alive && (u.Leader || u.Escort)))
            {
                var g = ToGui(views[u].position + Vector3.up * (2.25f * HeadK));
                string col = u.Team == Team.Player ? "#7fe0a0" : "#ff8a70";
                GUI.Label(new Rect(g.x - 50, g.y - 10, 100, 20), $"<color={col}><b>{(u.Escort ? "护·" : "")}{u.Name}</b></color>", new GUIStyle(center) { fontSize = 15 });
            }
        }

        void DrawChapterOverlay(float sw)
        {
            if (!string.IsNullOrEmpty(objective) && mode != Mode.Circuit)
            {
                var r = new Rect(sw - 330, 272, 316, 30);   // 小地图下方
                Rect(r, new Color(Ink.r, Ink.g, Ink.b, 0.88f));
                Rect(new Rect(r.x, r.y, 3, r.height), Teal);
                GUI.Label(new Rect(r.x + 12, r.y, r.width - 16, r.height), "目标：" + objective, St(15, Ivory, TextAnchor.MiddleLeft));
            }
            if (bannerT < 1.8f && !string.IsNullOrEmpty(bannerText))
            {
                float a = Mathf.Clamp01(Mathf.Min(bannerT * 5f, (1.8f - bannerT) * 3f));
                var r = new Rect(0, 230, sw, 74);
                Rect(r, new Color(Ink.r, Ink.g, Ink.b, 0.78f * a));
                Rect(new Rect(0, r.y, sw, 1.5f), new Color(Silver.r, Silver.g, Silver.b, a));
                Rect(new Rect(0, r.yMax - 1.5f, sw, 1.5f), new Color(Silver.r, Silver.g, Silver.b, a));
                GUI.Label(r, bannerText, St(38, new Color(Ivory.r, Ivory.g, Ivory.b, a), TextAnchor.MiddleCenter, true));
            }
            DrawChoice(sw);
            if (line.HasValue)
            {
                var r = new Rect(sw / 2 - 420, RefH - 300, 840, 120);
                bool hasPortrait = Chapter.Portraits.TryGetValue(line.Value.speaker ?? "", out var pt) && pt.Texture;
                if (hasPortrait)   // 立绘：头部到上胸，站在对话框左侧、下半压在框后（用户 2026-10-25）
                {
                    var pr = new Rect(r.x + 4, r.y - 190, 240, 300);
                    float crop = pt.Crop > 0 ? pt.Crop : 0.6f, axis = pt.Axis > 0 ? pt.Axis : 0.5f;
                    float uh = crop, uw = Mathf.Min(1f, crop * pt.Texture.height / (float)pt.Texture.width * pr.width / pr.height);
                    float ux = Mathf.Clamp(axis - uw / 2f, 0f, 1f - uw);
                    var uv = new Rect(ux, 1f - uh, uw, uh);
                    if (pt.Flip) uv = new Rect(ux + uw, 1f - uh, -uw, uh);
                    GUI.DrawTextureWithTexCoords(pr, pt.Texture, uv);
                }
                Rect(r, new Color(Ink.r, Ink.g, Ink.b, 0.94f));
                Rect(new Rect(r.x, r.y, r.width, 1.5f), Silver); Rect(new Rect(r.x, r.yMax - 1.5f, r.width, 1.5f), Silver);
                Rect(new Rect(r.x, r.y, 1.5f, r.height), Silver); Rect(new Rect(r.xMax - 1.5f, r.y, 1.5f, r.height), Silver);
                float tx = hasPortrait ? 252f : 22f;
                GUI.Label(new Rect(r.x + tx, r.y + 10, 300, 28), line.Value.speaker, St(20, Teal, TextAnchor.MiddleLeft, true));
                GUI.Label(new Rect(r.x + tx, r.y + 42, r.width - tx - 22, 70), line.Value.text, new GUIStyle(St(19, Ivory, TextAnchor.UpperLeft)) { wordWrap = true });
                GUI.Label(new Rect(r.xMax - 40, r.yMax - 30, 30, 24), "▼", St(16, Teal, TextAnchor.MiddleCenter));
            }
            if ((mode == Mode.Victory && victoryReady && !line.HasValue) || mode == Mode.Defeat)
                GUI.Label(new Rect(0, 340, sw, 30), mode == Mode.Victory ? "按确认继续" : "按确认重来本场", St(18, Ivory, TextAnchor.MiddleCenter));
        }
    }
}
