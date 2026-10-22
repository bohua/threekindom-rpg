// PROTOTYPE - NOT FOR PRODUCTION
// Question: 空轨式 AT 行动条＋小范围格子战斗，配合“学派回路镶嵌兵书→策略”，是否好玩、是否看得懂？
// Date: 2026-10-22
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Prototype.BingshuTactics
{
    public sealed class TacticsGame : MonoBehaviour
    {
        // 由编辑器构建脚本填入（只引用美术资源，不引用正式代码）
        public Material LiuBeiMat, GuanYuMat, ZhangFeiMat, RebelMat, ZhangJiaoMat;
        public Texture2D LiuBeiBust, GuanYuBust, ZhangFeiBust;
        public Material GroundMat, OverlayMat, RockMat;

        const float T = 1.2f;                       // 每格边长（米）
        const int W = 12, H = 10, DeployCols = 3;
        const float RefH = 720f;

        enum Mode { Deploy, Circuit, Idle, Menu, SkillList, TargetMove, TargetAttack, TargetSkill, Busy, Victory, Defeat }
        Mode mode = Mode.Deploy;

        Grid grid;
        readonly List<Unit> units = new List<Unit>();
        readonly Dictionary<Unit, Transform> views = new Dictionary<Unit, Transform>();
        readonly Dictionary<Unit, Renderer> quads = new Dictionary<Unit, Renderer>();
        readonly Dictionary<Unit, int> facing = new Dictionary<Unit, int>();
        readonly List<Book> library = new List<Book>();
        readonly List<(Vector3 pos, string text, Color color, float t)> popups = new List<(Vector3, string, Color, float)>();
        readonly List<(List<Vector2Int> tiles, Color color, float t)> flashes = new List<(List<Vector2Int>, Color, float)>();
        string log = "";

        Camera cam; float yaw = 45f, yawTarget = 45f; Vector3 lookAt;
        Mesh overlayMesh; MaterialPropertyBlock block;
        Font font; GUIStyle text, small, big, center; Texture2D white, disc;

        Unit active, deploySel; int ringSel; float ringAngle; bool showCraftList;
        SkillDef pendingSkill; float pendingMul = 1f;
        Dictionary<Vector2Int, Vector2Int> reach;
        Vector2Int hover = new Vector2Int(-1, -1);
        bool actionDone; float now;
        int circuitUnit, circuitSlot = -1;

        static readonly string[] RingItems = { "攻击", "策略", "移动", "待机", "防御", "武技" };

        // ---------------- 初始化 ----------------
        void Start()
        {
            Application.targetFrameRate = 60;
            block = new MaterialPropertyBlock();
            grid = new Grid(W, H);
            foreach (var p in new[] { new Vector2Int(5, 2), new Vector2Int(5, 3), new Vector2Int(6, 7), new Vector2Int(4, 7), new Vector2Int(8, 4), new Vector2Int(3, 4) })
                grid.Blocked[p.x, p.y] = true;
            BuildWorld();
            BuildLibrary();
            BuildUnits();
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Microsoft YaHei UI", "SimHei", "SimSun", "DengXian" }, 18);
            white = Texture2D.whiteTexture;
            disc = MakeDisc(64);
            Log("布阵：点选我方武将，再点蓝色格子调整站位。可先打开「兵书回路」配书。");
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-poc-shots");
            if (i >= 0 && i + 1 < args.Length) StartCoroutine(AutoShots(args[i + 1]));
        }

        // 自检：命令行 -poc-shots <目录>，自动截图各界面，再由 AI 代打到分出胜负后退出
        string shotDir; bool autoPlay; int turns;
        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0); tex.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(shotDir, name + ".png"), tex.EncodeToPNG());
            Destroy(tex);
            yield return new WaitForSeconds(0.3f);
        }

        IEnumerator AutoShots(string dir)
        {
            shotDir = dir; System.IO.Directory.CreateDirectory(dir);
            yield return new WaitForSeconds(1.5f);
            yield return Shot("1-deploy");
            mode = Mode.Circuit; circuitUnit = 1; circuitSlot = 3;
            yield return new WaitForSeconds(0.5f); yield return Shot("2-circuit-guanyu");
            circuitUnit = 0; circuitSlot = -1;
            yield return new WaitForSeconds(0.3f); yield return Shot("3-circuit-liubei");
            mode = Mode.Deploy; StartCoroutine(BattleLoop());
            while (mode != Mode.Menu) yield return null;
            yield return new WaitForSeconds(0.6f); yield return Shot("4-ring-menu");
            showCraftList = false; mode = Mode.SkillList;
            yield return new WaitForSeconds(0.3f); yield return Shot("5-strategy-list");
            var lit = StrategiesOf(active).FirstOrDefault(l => l.Def.Area == Area.Circle && l.Def.Effect == Effect.Damage);
            if (lit.Def != null)
            {
                pendingSkill = lit.Def; pendingMul = lit.PowerMul; mode = Mode.TargetSkill;
                var target = Foes(active).OrderBy(f => Grid.Dist(f.Pos, active.Pos)).First().Pos;
                forcedHover = SkillRange(active, pendingSkill).OrderBy(p => Grid.Dist(p, target)).First();
                yield return new WaitForSeconds(0.4f); yield return Shot("6-strategy-target");
                forcedHover = null;
            }
            autoPlay = true; mode = Mode.Menu; ChooseRing(4);
            float t0 = Time.time;
            while (mode != Mode.Victory && mode != Mode.Defeat && Time.time - t0 < 240f)
            {
                if (units.Any(u => u.Alive && u.PendingCast != null && u.Team == Team.Enemy) && !shotCast) { shotCast = true; yield return Shot("7-enemy-casting"); }
                yield return null;
            }
            yield return new WaitForSeconds(0.5f); yield return Shot("8-end");
            Debug.Log($"POC_AUTOPLAY_RESULT {mode} turns={turns} time={Time.time - t0:0}s");
            Application.Quit();
        }
        bool shotCast; Vector2Int? forcedHover;

        void BuildWorld()
        {
            cam = Camera.main;
            if (!cam) cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.fieldOfView = 24f; cam.nearClipPlane = 0.3f; cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.10f, 0.12f, 0.14f);
            lookAt = new Vector3(W * T / 2f, 0, H * T / 2f);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.25f; sun.shadows = LightShadows.Soft;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.transform.rotation = Quaternion.Euler(52f, 140f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.52f, 0.55f, 0.62f);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.position = new Vector3(W * T / 2f, -0.15f, H * T / 2f);
            ground.transform.localScale = new Vector3(W * T + 2f, 0.3f, H * T + 2f);
            var gm = new Material(GroundMat) { mainTexture = GroundTexture() };
            gm.SetTexture("_BaseMap", gm.mainTexture);
            gm.SetTextureScale("_BaseMap", Vector2.one);
            ground.GetComponent<Renderer>().sharedMaterial = gm;

            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                    if (grid.Blocked[x, y])
                    {
                        var rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        rock.name = "Rock";
                        rock.transform.position = Tile(new Vector2Int(x, y)) + Vector3.up * 0.35f;
                        rock.transform.localScale = new Vector3(T * 0.85f, 0.7f, T * 0.85f);
                        rock.transform.rotation = Quaternion.Euler(0, (x * 37 + y * 11) % 30 - 15, 0);
                        rock.GetComponent<Renderer>().sharedMaterial = RockMat;
                    }

            var ov = new GameObject("Overlay");
            overlayMesh = new Mesh();
            ov.AddComponent<MeshFilter>().sharedMesh = overlayMesh;
            var mr = ov.AddComponent<MeshRenderer>();
            mr.sharedMaterial = OverlayMat; mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
        }

        Texture2D GroundTexture()
        {
            const int px = 16;
            var tex = new Texture2D((W + 2) * px, (H + 2) * px) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var rnd = new System.Random(7);
            for (int x = 0; x < tex.width; x++)
                for (int y = 0; y < tex.height; y++)
                {
                    int gx = x / px - 1, gy = y / px - 1;
                    bool inside = gx >= 0 && gy >= 0 && gx < W && gy < H;
                    float n = (float)rnd.NextDouble() * 0.05f;
                    Color c = inside
                        ? ((gx + gy) % 2 == 0 ? new Color(0.42f, 0.55f, 0.30f) : new Color(0.46f, 0.58f, 0.32f))
                        : new Color(0.36f, 0.32f, 0.24f);
                    if (inside && (x % px == 0 || y % px == 0)) c *= 0.82f;
                    tex.SetPixel(x, y, c + new Color(n, n, n * 0.5f));
                }
            tex.Apply();
            return tex;
        }

        void BuildLibrary()
        {
            void B(string n, Element e, int g) => library.Add(new Book(n, e, g));
            B("《尉缭子》", Element.Metal, 2); B("《吴子》", Element.Metal, 3); B("《六韬·虎韬》", Element.Metal, 1); B("《尉缭子》残卷", Element.Metal, 1);
            B("《三略》", Element.Wood, 3); B("《司马法》", Element.Wood, 2); B("《管子·牧民》", Element.Wood, 1); B("《司马法》残卷", Element.Wood, 1);
            B("《管子·度地》", Element.Water, 3); B("《墨子·备水》", Element.Water, 2); B("《孙子·行军》", Element.Water, 1);
            B("《孙子·火攻》", Element.Fire, 3); B("《六韬·火战》", Element.Fire, 2); B("《火攻》残卷", Element.Fire, 1); B("《火攻》残卷", Element.Fire, 1);
            B("《孙膑兵法·八阵》", Element.Earth, 3); B("《墨子·备城门》", Element.Earth, 2); B("《六韬·龙韬》", Element.Earth, 1);
        }

        Book L(string name, int skip = 0) => library.Where(b => b.Name == name).Skip(skip).First();

        void BuildUnits()
        {
            var liu = NewUnit("刘备", "LiuBei", Team.Player, Troop.Archer, School.Ru, 160, 50, 35, 56, 35, 8, 3, 3, 60, new Vector2Int(1, 5));
            liu.Crafts.Add(Craft("整队", "单体友军回复30%兵力", Effect.Heal, Area.Single, 3, 0, 0.30f, 20));
            liu.Crafts.Add(Craft("火矢", "单体计策1.3倍（射程4）", Effect.Damage, Area.Single, 4, 0, 1.3f, 25));
            liu.Slots[0] = L("《管子·度地》"); liu.Slots[1] = L("《三略》"); liu.Slots[2] = L("《司马法》"); liu.Slots[3] = L("《墨子·备水》");

            var guan = NewUnit("关羽", "GuanYu", Team.Player, Troop.Cavalry, School.Bing, 170, 64, 36, 47, 29, 10, 5, 1, 40, new Vector2Int(2, 7));
            guan.Crafts.Add(Craft("突击", "近战单体物理1.5倍", Effect.Damage, Area.Single, -1, 0, 1.5f, 25, might: true));
            guan.Crafts.Add(Craft("破阵", "近战单体1.1倍，物防-20% 2回合", Effect.Debuff, Area.Single, -1, 0, 1.1f, 30, might: true, turns: 2, amount: 0.2f));
            guan.Slots[0] = L("《孙子·火攻》"); guan.Slots[2] = L("《六韬·火战》"); guan.Slots[4] = L("《火攻》残卷");
            guan.Slots[1] = L("《尉缭子》"); guan.Slots[3] = L("《管子·牧民》");

            var zhang = NewUnit("张飞", "ZhangFei", Team.Player, Troop.Infantry, School.Fa, 190, 66, 40, 40, 24, 6, 3, 1, 30, new Vector2Int(2, 3));
            zhang.Crafts.Add(Craft("横扫", "自身周围2格内敌军物理0.65倍", Effect.Damage, Area.SelfCircle, 0, 2, 0.65f, 30, might: true));
            zhang.Crafts.Add(Craft("威慑", "自身周围3格内敌军物攻-20% 2回合", Effect.Debuff, Area.SelfCircle, 0, 3, 0f, 25, turns: 2, amount: 0.2f));
            zhang.Slots[0] = L("《孙膑兵法·八阵》"); zhang.Slots[1] = L("《墨子·备城门》"); zhang.Slots[2] = L("《六韬·龙韬》"); zhang.Slots[5] = L("《吴子》");

            var rebels = new[] { new Vector2Int(9, 2), new Vector2Int(10, 4), new Vector2Int(9, 6), new Vector2Int(10, 8) };
            for (int i = 0; i < rebels.Length; i++)
                NewUnit("黄巾兵" + "甲乙丙丁"[i], "Rebel", Team.Enemy, Troop.Rebel, School.Fa, 130, 45, 28, 30, 22, 6, 3, 1, 0, rebels[i]).Element = Element.Earth;
            var jiao = NewUnit("张角", "ZhangJiao", Team.Enemy, Troop.Strategist, School.Dao, 200, 35, 26, 60, 35, 7, 2, 2, 40, new Vector2Int(11, 5));
            jiao.Element = Element.Earth;
        }

        Unit NewUnit(string name, string key, Team team, Troop troop, School school, int hp, int atk, int def, int mat, int mdef,
            int speed, int move, int range, int ep, Vector2Int pos)
        {
            var u = new Unit
            {
                Name = name, Key = key, Team = team, Troop = troop, School = school, Element = Circuits.Of(school).Affinity,
                MaxHp = hp, Hp = hp, Atk = atk, Def = def, Mat = mat, Mdef = mdef, Speed = speed, Move = move, AtkRange = range,
                MaxEp = ep, Ep = ep, Cp = 30, Pos = pos, At = 800f / speed
            };
            units.Add(u);
            var material = key switch { "LiuBei" => LiuBeiMat, "GuanYu" => GuanYuMat, "ZhangFei" => ZhangFeiMat, "ZhangJiao" => ZhangJiaoMat, _ => RebelMat };
            var root = new GameObject(name).transform;
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(root, false);
            float size = 48f / 30f;
            quad.transform.localScale = new Vector3(size, size, 1f);
            var r = quad.GetComponent<MeshRenderer>();
            r.sharedMaterial = material; r.shadowCastingMode = ShadowCastingMode.On;
            views[u] = root; quads[u] = r; facing[u] = team == Team.Player ? 2 : 1;
            root.position = Tile(pos);
            SetFrame(u, 0);
            return u;
        }

        static SkillDef Craft(string name, string desc, Effect fx, Area area, int range, int radius, float power, int cost,
            bool might = false, int turns = 0, float amount = 0f) =>
            new SkillDef { Name = name, Desc = desc, Effect = fx, Area = area, Range = range, Radius = radius, Power = power, Cost = cost, UsesMight = might, Turns = turns, Amount = amount, Recover = 110 };

        // ---------------- 坐标与绘制工具 ----------------
        Vector3 Tile(Vector2Int p) => new Vector3((p.x + 0.5f) * T, 0f, (p.y + 0.5f) * T);
        Unit At(Vector2Int p) => units.FirstOrDefault(u => u.Alive && u.Pos == p);
        float S => Screen.height / RefH;

        Vector2 ToGui(Vector3 world)
        {
            var sp = cam.WorldToScreenPoint(world);
            return new Vector2(sp.x / S, (Screen.height - sp.y) / S);
        }

        void SetFrame(Unit u, int step)
        {
            var r = quads[u];
            var tex = r.sharedMaterial.GetTexture("_BaseMap");
            int cols = tex ? Mathf.Max(1, tex.width / tex.height) : 9;
            int frame = cols >= 25 ? 9 + facing[u] * 4 + (step % 4) : 0;
            r.GetPropertyBlock(block);
            block.SetVector("_BaseMap_ST", new Vector4(1f / cols, 1f, frame / (float)cols, 0f));
            r.SetPropertyBlock(block);
        }

        void Face(Unit u, Vector3 worldDir)
        {
            var right = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized;
            var fwd = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            float a = Vector3.Dot(worldDir, right), b = Vector3.Dot(worldDir, fwd);
            facing[u] = Mathf.Abs(a) >= Mathf.Abs(b) ? (a > 0 ? 2 : 1) : (b > 0 ? 3 : 0);
        }

        static Texture2D MakeDisc(int n)
        {
            var t = new Texture2D(n, n) { filterMode = FilterMode.Bilinear };
            for (int x = 0; x < n; x++)
                for (int y = 0; y < n; y++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                    t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01((1f - d) * n / 4f)));
                }
            t.Apply();
            return t;
        }

        void Log(string s) => log = s;

        // ---------------- 每帧 ----------------
        void Update()
        {
            yaw = Mathf.MoveTowardsAngle(yaw, yawTarget, 240f * Time.deltaTime);
            var rot = Quaternion.Euler(45f, yaw, 0f);
            cam.transform.SetPositionAndRotation(lookAt - rot * Vector3.forward * 38f, rot);

            foreach (var kv in quads)
            {
                var u = kv.Key; var q = kv.Value.transform;
                q.rotation = cam.transform.rotation;
                q.position = views[u].position + cam.transform.up * (48f / 30f / 2f) * 0.98f;
                kv.Value.enabled = u.Alive;
            }

            for (int i = popups.Count - 1; i >= 0; i--)
            {
                var p = popups[i]; p.t += Time.deltaTime; popups[i] = p;
                if (p.t > 1.2f) popups.RemoveAt(i);
            }
            for (int i = flashes.Count - 1; i >= 0; i--)
            {
                var f = flashes[i]; f.t += Time.deltaTime; flashes[i] = f;
                if (f.t > 0.6f) flashes.RemoveAt(i);
            }

            var ray = cam.ScreenPointToRay(mouseScreen);
            hover = new Vector2Int(-1, -1);
            if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float dist))
            {
                var hit = ray.GetPoint(dist);
                var p = new Vector2Int(Mathf.FloorToInt(hit.x / T), Mathf.FloorToInt(hit.z / T));
                if (grid.In(p)) hover = p;
            }
            if (forcedHover.HasValue) hover = forcedHover.Value;
            RebuildOverlay();
        }

        Vector2 mouseScreen;

        void RebuildOverlay()
        {
            var tiles = new List<(Vector2Int p, Color c, float inset)>();
            void Add(IEnumerable<Vector2Int> ps, Color c, float inset = 0.06f) { foreach (var p in ps) tiles.Add((p, c, inset)); }

            foreach (var u in units.Where(u => u.Alive))
                Add(new[] { u.Pos }, u.Team == Team.Player ? new Color(0.3f, 0.6f, 1f, 0.35f) : new Color(1f, 0.3f, 0.25f, 0.35f), 0.18f);

            if (mode == Mode.Deploy)
                for (int x = 0; x < DeployCols; x++) for (int y = 0; y < H; y++)
                        if (!grid.Blocked[x, y]) Add(new[] { new Vector2Int(x, y) }, new Color(0.35f, 0.65f, 1f, 0.22f));

            foreach (var u in units.Where(u => u.Alive && u.PendingCast != null))
                Add(CastArea(u), u.Team == Team.Player ? new Color(0.7f, 0.4f, 1f, 0.30f) : new Color(1f, 0.15f, 0.55f, 0.35f));

            if (mode == Mode.TargetMove && reach != null) Add(reach.Keys, new Color(0.3f, 0.65f, 1f, 0.38f));
            if (mode == Mode.TargetAttack && reach != null)
            {
                Add(reach.Keys, new Color(0.3f, 0.65f, 1f, 0.22f));
                Add(AttackableTargets(active, active.AtkRange).Select(t => t.Pos), new Color(1f, 0.55f, 0.15f, 0.55f));
            }
            if (mode == Mode.TargetSkill && pendingSkill != null)
            {
                if (pendingSkill.Range < 0)
                {
                    Add(reach.Keys, new Color(0.3f, 0.65f, 1f, 0.22f));
                    Add(AttackableTargets(active, 1).Select(t => t.Pos), new Color(1f, 0.55f, 0.15f, 0.55f));
                }
                else
                {
                    Add(SkillRange(active, pendingSkill), new Color(0.3f, 0.65f, 1f, 0.30f));
                    if (ValidSkillTarget(active, pendingSkill, hover))
                        Add(grid.AreaOf(pendingSkill, active.Pos, hover), new Color(1f, 0.6f, 0.15f, 0.55f), 0.02f);
                }
            }
            if (active != null && mode != Mode.Deploy && mode != Mode.Circuit) Add(new[] { active.Pos }, new Color(1f, 0.9f, 0.3f, 0.25f + 0.2f * Mathf.Sin(Time.time * 6f)), 0.02f);
            foreach (var f in flashes) Add(f.tiles, new Color(f.color.r, f.color.g, f.color.b, 0.7f * (1f - f.t / 0.6f)), 0f);
            if (grid.In(hover) && mode != Mode.Circuit) Add(new[] { hover }, new Color(1f, 1f, 1f, 0.25f), 0.0f);

            var v = new List<Vector3>(); var c = new List<Color>(); var idx = new List<int>();
            foreach (var (p, col, inset) in tiles)
            {
                float x0 = (p.x + inset) * T, x1 = (p.x + 1 - inset) * T, z0 = (p.y + inset) * T, z1 = (p.y + 1 - inset) * T, y = 0.02f + 0.001f * (v.Count / 4 % 50);
                int b = v.Count;
                v.Add(new Vector3(x0, y, z0)); v.Add(new Vector3(x0, y, z1)); v.Add(new Vector3(x1, y, z1)); v.Add(new Vector3(x1, y, z0));
                c.Add(col); c.Add(col); c.Add(col); c.Add(col);
                idx.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
            }
            overlayMesh.Clear();
            overlayMesh.SetVertices(v); overlayMesh.SetColors(c); overlayMesh.SetTriangles(idx, 0);
            overlayMesh.RecalculateBounds();
        }

        // ---------------- 规则辅助 ----------------
        Dictionary<Vector2Int, Vector2Int> ReachOf(Unit u) => grid.Reachable(u.Pos, u.Move, p => At(p) != null && At(p) != u);

        IEnumerable<Unit> Foes(Unit u) => units.Where(x => x.Alive && x.Team != u.Team);
        IEnumerable<Unit> Friends(Unit u) => units.Where(x => x.Alive && x.Team == u.Team);

        List<Unit> AttackableTargets(Unit u, int range)
        {
            var r = ReachOf(u);
            return Foes(u).Where(f => r.Keys.Any(p => Grid.Dist(p, f.Pos) <= range)).ToList();
        }

        Vector2Int ApproachTile(Unit u, Unit target, int range, Dictionary<Vector2Int, Vector2Int> r) =>
            r.Keys.Where(p => Grid.Dist(p, target.Pos) <= range)
                  .OrderBy(p => Grid.Path(r, p).Count).ThenByDescending(p => Grid.Dist(p, target.Pos)).First();

        IEnumerable<Vector2Int> SkillRange(Unit u, SkillDef s) =>
            s.Area == Area.SelfCircle ? new[] { u.Pos } :
            s.Area == Area.Line ? grid.Diamond(u.Pos, 1).Where(p => p != u.Pos) :
            s.Range >= 99 || s.Radius >= 99 ? new[] { u.Pos } : grid.Diamond(u.Pos, s.Range);

        bool ValidSkillTarget(Unit u, SkillDef s, Vector2Int p)
        {
            if (!grid.In(p) || !SkillRange(u, s).Contains(p)) return false;
            if (s.Area != Area.Single) return true;
            var t = At(p);
            return t != null && (s.TargetsAllies ? t.Team == u.Team : t.Team != u.Team);
        }

        List<Vector2Int> CastArea(Unit u)
        {
            var s = u.PendingCast;
            if (s.Radius >= 99) return Friends(u).Select(f => f.Pos).ToList();
            var center = u.PendingFollow != null && u.PendingFollow.Alive ? u.PendingFollow.Pos : u.PendingTarget;
            if (s.Area == Area.Line) return grid.LineArea(u.Pos, u.PendingDir, s.Length, s.Width).ToList();
            return grid.AreaOf(s, u.Pos, center).ToList();
        }

        List<LitStrategy> StrategiesOf(Unit u)
        {
            if (u.Name == "张角")
                return Strategies.All.Where(s => s.Name == "引火" || s.Name == "火烧营寨").Select(s => new LitStrategy(s, 0, 0)).ToList();
            return CircuitMath.Lit(u.Circuit, u.Slots);
        }

        // ---------------- 战斗主循环 ----------------
        IEnumerator BattleLoop()
        {
            Log("开战！左侧行动条从上到下为行动顺序；策略需要咏唱，读条期间会被行动条挤后。");
            while (true)
            {
                var u = AtClock.Next(units);
                now = u.At; active = u; u.Defending = false;
                yield return new WaitForSeconds(0.25f);

                if (u.PendingCast != null)
                {
                    yield return ExecuteCast(u);
                    u.At = now + AtClock.Delay(u.PendingCast?.Recover ?? 80, u.Speed);
                    u.PendingCast = null; u.PendingFollow = null;
                }
                else if (u.Team == Team.Player && autoPlay)
                {
                    mode = Mode.Busy;
                    yield return EnemyTurn(u);
                }
                else if (u.Team == Team.Player)
                {
                    actionDone = false; ringSel = 0; mode = Mode.Menu;
                    Log($"{u.Name} 的回合");
                    while (!actionDone) yield return null;
                }
                else
                {
                    mode = Mode.Busy;
                    yield return EnemyTurn(u);
                }
                u.TickStatuses();
                mode = Mode.Busy; turns++;

                if (!Foes(units.First(x => x.Team == Team.Player)).Any()) { mode = Mode.Victory; Log("胜利！黄巾溃退。按 R 重来。"); yield break; }
                if (!units.Any(x => x.Alive && x.Team == Team.Player)) { mode = Mode.Defeat; Log("我军败退……按 R 重来。"); yield break; }
            }
        }

        void EndTurn(int cost)
        {
            active.At = now + AtClock.Delay(cost, active.Speed);
            actionDone = true; mode = Mode.Busy; pendingSkill = null; reach = null;
        }

        IEnumerator Walk(Unit u, List<Vector2Int> path)
        {
            for (int i = 1; i < path.Count; i++)
            {
                var a = Tile(path[i - 1]); var b = Tile(path[i]);
                Face(u, b - a);
                for (float t = 0; t < 1f; t += Time.deltaTime / 0.16f)
                {
                    views[u].position = Vector3.Lerp(a, b, t);
                    SetFrame(u, (i * 2 + (t > 0.5f ? 1 : 0)));
                    yield return null;
                }
                views[u].position = b;
            }
            u.Pos = path[path.Count - 1];
            SetFrame(u, 1);
        }

        IEnumerator Lunge(Unit u, Vector2Int target)
        {
            var a = Tile(u.Pos); var b = Tile(target);
            Face(u, b - a); SetFrame(u, 0);
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.25f)
            {
                views[u].position = a + (b - a).normalized * Mathf.Sin(t * Mathf.PI) * 0.45f;
                yield return null;
            }
            views[u].position = a; SetFrame(u, 1);
        }

        void Hit(Unit a, Unit t, int dmg)
        {
            t.Hp = Mathf.Max(0, t.Hp - dmg);
            a.Cp = Mathf.Min(100, a.Cp + 8); t.Cp = Mathf.Min(100, t.Cp + 5);
            popups.Add((views[t].position + Vector3.up * 1.8f, dmg.ToString(), new Color(1f, 0.95f, 0.8f), 0f));
            if (!t.Alive)
            {
                t.PendingCast = null; t.PendingFollow = null;
                popups.Add((views[t].position + Vector3.up * 2.3f, "撤退", new Color(1f, 0.5f, 0.4f), 0f));
            }
        }

        IEnumerator DoAttack(Unit u, Unit target)
        {
            var r = ReachOf(u);
            yield return Walk(u, Grid.Path(r, ApproachTile(u, target, u.AtkRange, r)));
            yield return Lunge(u, target.Pos);
            Hit(u, target, Combat.Damage(u, target, 1f, true, Element.None));
        }

        IEnumerator DoCraft(Unit u, SkillDef s, Vector2Int targetTile)
        {
            u.Cp -= s.Cost;
            popups.Add((views[u].position + Vector3.up * 2.4f, "武技·" + s.Name, new Color(1f, 0.85f, 0.3f), 0f));
            if (s.Range < 0)
            {
                var target = At(targetTile);
                var r = ReachOf(u);
                yield return Walk(u, Grid.Path(r, ApproachTile(u, target, 1, r)));
                yield return Lunge(u, target.Pos);
            }
            else yield return Lunge(u, targetTile == u.Pos ? u.Pos + Vector2Int.right : targetTile);
            var area = grid.AreaOf(s, u.Pos, targetTile).ToList();
            yield return Apply(u, s, area, 1f);
        }

        IEnumerator Apply(Unit u, SkillDef s, List<Vector2Int> area, float mul)
        {
            var color = s.IsStrategy ? Wuxing.Tint(s.Element) : new Color(1f, 0.85f, 0.4f);
            flashes.Add((area, color, 0f));
            var targets = units.Where(x => x.Alive && area.Contains(x.Pos) && (s.TargetsAllies ? x.Team == u.Team : x.Team != u.Team)).ToList();
            foreach (var t in targets)
            {
                switch (s.Effect)
                {
                    case Effect.Damage:
                    case Effect.Debuff when s.Power > 0:
                        Hit(u, t, Combat.Damage(u, t, s.Power * mul, s.UsesMight, s.Element));
                        if (s.AtDelay > 0 && t.Alive)
                        {
                            t.At += s.AtDelay * mul;
                            popups.Add((views[t].position + Vector3.up * 2.6f, "迟滞", new Color(0.5f, 0.75f, 1f), 0f));
                        }
                        if (s.Effect == Effect.Debuff && t.Alive) t.AddStatus(new Status { Name = "破阵", DefMul = 1f - s.Amount, Turns = s.Turns });
                        break;
                    case Effect.Debuff:
                        t.AddStatus(new Status { Name = "威慑", AtkMul = 1f - s.Amount, Turns = s.Turns });
                        popups.Add((views[t].position + Vector3.up * 2f, "物攻↓", new Color(0.8f, 0.6f, 1f), 0f));
                        break;
                    case Effect.Heal:
                        int h = Combat.Heal(t, Mathf.Min(0.6f, s.Power * mul));
                        t.Hp += h;
                        popups.Add((views[t].position + Vector3.up * 1.8f, "+" + h, new Color(0.5f, 1f, 0.5f), 0f));
                        if (s.Amount > 0) t.AddStatus(new Status { Name = "鼓舞", AtkMul = 1f + s.Amount, Turns = s.Turns });
                        break;
                    case Effect.Guard:
                        t.AddStatus(new Status { Name = "阵", Guard = Mathf.Min(0.45f, s.Amount * mul), Turns = s.Turns });
                        popups.Add((views[t].position + Vector3.up * 2f, "结阵", new Color(0.9f, 0.75f, 0.4f), 0f));
                        break;
                }
            }
            yield return new WaitForSeconds(0.55f);
        }

        IEnumerator ExecuteCast(Unit u)
        {
            var s = u.PendingCast;
            if (u.Ep < s.Cost) { Log($"{u.Name}：策力不足，{s.Name}未能发动"); yield break; }
            u.Ep -= s.Cost;
            Log($"{u.Name} 发动策略「{s.Name}」{(u.PendingMul > 1f ? $"（威力×{u.PendingMul:0.00}）" : "")}");
            popups.Add((views[u].position + Vector3.up * 2.4f, "策略·" + s.Name, Wuxing.Tint(s.Element), 0f));
            yield return Lunge(u, u.Pos + Grid.DirTo(u.Pos, u.PendingTarget));
            yield return Apply(u, s, CastArea(u), u.PendingMul);
        }

        void BeginCast(Unit u, SkillDef s, float mul, Vector2Int target)
        {
            u.PendingCast = s; u.PendingMul = mul; u.PendingTarget = target;
            u.PendingDir = Grid.DirTo(u.Pos, target);
            var t = At(target);
            u.PendingFollow = s.Area == Area.Line || s.Radius >= 99 ? null : t;
            u.At = now + AtClock.Delay(s.Cast, u.Speed);
            Log($"{u.Name} 开始咏唱「{s.Name}」，将在行动条上的位置发动");
        }

        // ---------------- 敌方 AI（固定规则） ----------------
        IEnumerator EnemyTurn(Unit u)
        {
            yield return new WaitForSeconds(0.2f);
            var strats = StrategiesOf(u);
            var big = strats.Select(l => l.Def).FirstOrDefault(s => s.Area == Area.Circle && s.Effect == Effect.Damage && u.Ep >= s.Cost);
            if (big != null)
            {
                var best = grid.Diamond(u.Pos, big.Range)
                    .Select(p => (p, n: grid.Diamond(p, big.Radius).Count(q => At(q) != null && At(q).Team != u.Team)))
                    .OrderByDescending(x => x.n).First();
                if (best.n >= 2) { BeginCast(u, big, 1f, best.p); yield break; }
            }
            var single = strats.Select(l => l.Def).FirstOrDefault(s => s.Area == Area.Single && s.Effect == Effect.Damage && u.Ep >= s.Cost);
            if (single != null)
            {
                var t = units.Where(x => x.Alive && x.Team != u.Team && Grid.Dist(x.Pos, u.Pos) <= single.Range).OrderBy(x => x.Hp).FirstOrDefault();
                if (t != null && Random.value < 0.6f) { BeginCast(u, single, 1f, t.Pos); yield break; }
            }

            var targets = AttackableTargets(u, u.AtkRange);
            if (targets.Count > 0)
            {
                var t = targets.OrderBy(x => x.Hp).First();
                Log($"{u.Name} 攻击 {t.Name}");
                yield return DoAttack(u, t);
                u.At = now + AtClock.Delay(100, u.Speed);
                yield break;
            }
            var r = ReachOf(u);
            var dest = r.Keys.OrderBy(p => Foes(u).Min(f => Grid.Dist(p, f.Pos))).First();
            yield return Walk(u, Grid.Path(r, dest));
            u.At = now + AtClock.Delay(70, u.Speed);
        }

        // ---------------- 玩家输入 ----------------
        void ChooseRing(int i)
        {
            switch (RingItems[i])
            {
                case "移动": reach = ReachOf(active); mode = Mode.TargetMove; Log("选择移动目的地（右键返回）"); break;
                case "攻击": reach = ReachOf(active); mode = Mode.TargetAttack; Log("选择攻击目标：会先走到射程内再攻击"); break;
                case "武技": showCraftList = true; mode = Mode.SkillList; break;
                case "策略": showCraftList = false; mode = Mode.SkillList; break;
                case "防御": active.Defending = true; popups.Add((views[active].position + Vector3.up * 2f, "防御", Color.white, 0f)); Log($"{active.Name} 防御"); EndTurn(50); break;
                case "待机": Log($"{active.Name} 待机"); EndTurn(40); break;
            }
        }

        void ClickTile(Vector2Int p)
        {
            switch (mode)
            {
                case Mode.Deploy:
                    var u = At(p);
                    if (u != null && u.Team == Team.Player) { deploySel = u; return; }
                    if (deploySel != null && p.x < DeployCols && !grid.Blocked[p.x, p.y] && u == null)
                    {
                        deploySel.Pos = p; views[deploySel].position = Tile(p);
                    }
                    return;
                case Mode.TargetMove:
                    if (reach.ContainsKey(p) && p != active.Pos) StartCoroutine(Run(Walk(active, Grid.Path(reach, p)), 70, $"{active.Name} 移动"));
                    return;
                case Mode.TargetAttack:
                    var t = At(p);
                    if (t != null && AttackableTargets(active, active.AtkRange).Contains(t)) StartCoroutine(Run(DoAttack(active, t), 100, $"{active.Name} 攻击 {t.Name}"));
                    return;
                case Mode.TargetSkill:
                    if (pendingSkill.Range < 0)
                    {
                        var m = At(p);
                        if (m != null && AttackableTargets(active, 1).Contains(m)) StartCoroutine(Run(DoCraft(active, pendingSkill, p), pendingSkill.Recover, $"{active.Name} 武技「{pendingSkill.Name}」"));
                        return;
                    }
                    if (!ValidSkillTarget(active, pendingSkill, p)) return;
                    if (pendingSkill.IsStrategy) { BeginCast(active, pendingSkill, pendingMul, p); actionDone = true; mode = Mode.Busy; pendingSkill = null; }
                    else StartCoroutine(Run(DoCraft(active, pendingSkill, p), pendingSkill.Recover, $"{active.Name} 武技「{pendingSkill.Name}」"));
                    return;
            }
        }

        IEnumerator Run(IEnumerator action, int cost, string msg)
        {
            mode = Mode.Busy; Log(msg);
            yield return action;
            EndTurn(cost);
        }

        void Back()
        {
            if (mode == Mode.TargetMove || mode == Mode.TargetAttack || mode == Mode.SkillList) { mode = Mode.Menu; reach = null; }
            else if (mode == Mode.TargetSkill) { mode = Mode.SkillList; pendingSkill = null; reach = null; }
            else if (mode == Mode.Circuit) mode = Mode.Deploy;
        }

        // ---------------- 界面（IMGUI，原型用） ----------------
        void Styles()
        {
            if (text != null) return;
            text = new GUIStyle(GUI.skin.label) { font = font, fontSize = 16, richText = true, clipping = TextClipping.Overflow, padding = new RectOffset(0, 0, 0, 0) };
            text.normal.textColor = Color.white;
            small = new GUIStyle(text) { fontSize = 13 };
            big = new GUIStyle(text) { fontSize = 26, fontStyle = FontStyle.Bold };
            center = new GUIStyle(text) { alignment = TextAnchor.MiddleCenter };
            GUI.skin.button.font = font; GUI.skin.button.fontSize = 15; GUI.skin.box.font = font;
        }

        void Rect(Rect r, Color c) { var o = GUI.color; GUI.color = c; GUI.DrawTexture(r, white); GUI.color = o; }
        void Disc(Vector2 c, float rad, Color col) { var o = GUI.color; GUI.color = col; GUI.DrawTexture(new Rect(c.x - rad, c.y - rad, rad * 2, rad * 2), disc); GUI.color = o; }

        void Line(Vector2 a, Vector2 b, float w, Color col)
        {
            var m = GUI.matrix; var o = GUI.color; GUI.color = col;
            float ang = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
            GUIUtility.RotateAroundPivot(ang, a);
            GUI.DrawTexture(new Rect(a.x, a.y - w / 2, Vector2.Distance(a, b), w), white);
            GUI.matrix = m; GUI.color = o;
        }

        void Bar(Rect r, float v, Color c)
        {
            Rect(r, new Color(0, 0, 0, 0.6f));
            Rect(new Rect(r.x + 1, r.y + 1, (r.width - 2) * Mathf.Clamp01(v), r.height - 2), c);
        }

        void OnGUI()
        {
            Styles();
            var e = Event.current;
            GUI.matrix = Matrix4x4.identity;
            mouseScreen = new Vector2(e.mousePosition.x, Screen.height - e.mousePosition.y);
            GUI.matrix = Matrix4x4.Scale(new Vector3(S, S, 1));
            float sw = Screen.width / S;

            DrawWorldLabels();
            DrawAtBar();
            DrawParty(sw);
            DrawTopBar(sw);

            if (mode == Mode.Deploy) DrawDeploy(sw);
            if (mode == Mode.Circuit) DrawCircuit(sw);
            if (mode == Mode.Menu) DrawRing();
            if (mode == Mode.SkillList) DrawSkillList();
            if (mode == Mode.TargetSkill || mode == Mode.TargetAttack || mode == Mode.TargetMove) DrawHoverInfo(sw);
            if (mode == Mode.Idle || mode == Mode.Busy || mode == Mode.Deploy) DrawHoverInfo(sw);
            if (mode == Mode.Victory || mode == Mode.Defeat)
                GUI.Label(new Rect(0, 280, sw, 60), mode == Mode.Victory ? "胜 利" : "败 退", new GUIStyle(big) { alignment = TextAnchor.MiddleCenter, fontSize = 48 });

            // 世界点击与按键放在最后，避免和按钮抢点击
            if (e.type == EventType.MouseDown && mode != Mode.Circuit)
            {
                if (e.button == 0 && grid.In(hover)) { ClickTile(hover); e.Use(); }
                else if (e.button == 1) { Back(); e.Use(); }
            }
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Q) yawTarget -= 90f;
                if (e.keyCode == KeyCode.E) yawTarget += 90f;
                if (e.keyCode == KeyCode.Escape) Back();
                if (e.keyCode == KeyCode.R && (mode == Mode.Victory || mode == Mode.Defeat))
                    UnityEngine.SceneManagement.SceneManager.LoadScene(gameObject.scene.buildIndex);
                if (mode == Mode.Menu)
                {
                    if (e.keyCode == KeyCode.UpArrow || e.keyCode == KeyCode.W) ringSel = (ringSel + RingItems.Length - 1) % RingItems.Length;
                    if (e.keyCode == KeyCode.DownArrow || e.keyCode == KeyCode.S) ringSel = (ringSel + 1) % RingItems.Length;
                    if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.Space) ChooseRing(ringSel);
                }
            }
        }

        void DrawTopBar(float sw)
        {
            Rect(new Rect(0, 0, sw, 30), new Color(0, 0, 0, 0.55f));
            GUI.Label(new Rect(110, 4, sw - 420, 24), log, text);
            GUI.Label(new Rect(sw - 300, 6, 300, 24), "Q/E 旋转镜头 · 右键/Esc 返回", small);
        }

        void DrawWorldLabels()
        {
            foreach (var u in units.Where(u => u.Alive))
            {
                var g = ToGui(views[u].position + Vector3.up * 1.75f);
                Bar(new Rect(g.x - 24, g.y, 48, 6), u.Hp / (float)u.MaxHp, u.Team == Team.Player ? new Color(0.4f, 0.85f, 0.4f) : new Color(0.95f, 0.35f, 0.3f));
                if (u.PendingCast != null)
                    GUI.Label(new Rect(g.x - 50, g.y - 22, 100, 20), $"咏唱·{u.PendingCast.Name}", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Wuxing.Tint(u.PendingCast.Element) } });
            }
            foreach (var p in popups)
            {
                var g = ToGui(p.pos + Vector3.up * p.t * 0.6f);
                var st = new GUIStyle(big) { alignment = TextAnchor.MiddleCenter, fontSize = 20 };
                var shadow = new GUIStyle(st); shadow.normal.textColor = new Color(0, 0, 0, 0.8f);
                GUI.Label(new Rect(g.x - 79, g.y - 11, 160, 24), p.text, shadow);
                st.normal.textColor = new Color(p.color.r, p.color.g, p.color.b, 1f - Mathf.Max(0, p.t - 0.8f) * 2.5f);
                GUI.Label(new Rect(g.x - 80, g.y - 12, 160, 24), p.text, st);
            }
        }

        Texture2D Bust(Unit u) => u.Key switch { "LiuBei" => LiuBeiBust, "GuanYu" => GuanYuBust, "ZhangFei" => ZhangFeiBust, _ => null };

        void DrawAtBar()
        {
            if (mode == Mode.Deploy || mode == Mode.Circuit) return;
            Rect(new Rect(0, 30, 96, 440), new Color(0, 0, 0, 0.45f));
            GUI.Label(new Rect(8, 34, 90, 20), "行动顺序", small);
            float y = 56;
            foreach (var u in units.Where(x => x.Alive).OrderBy(x => x.At).ThenBy(x => x.Team == Team.Player ? 0 : 1))
            {
                var r = new Rect(8, y, 80, 46);
                Rect(r, u == active ? new Color(1f, 0.85f, 0.3f, 0.8f) : (u.Team == Team.Player ? new Color(0.2f, 0.35f, 0.6f, 0.85f) : new Color(0.55f, 0.18f, 0.15f, 0.85f)));
                var b = Bust(u);
                if (b) GUI.DrawTexture(new Rect(r.x + 2, r.y + 2, 42, 42), b, ScaleMode.ScaleAndCrop);
                GUI.Label(new Rect(r.x + (b ? 44 : 4), r.y + 2, 60, 22), u.Name, small);
                GUI.Label(new Rect(r.x + (b ? 44 : 4), r.y + 22, 60, 22), u.PendingCast != null ? "<color=#d9a6ff>咏唱</color>" : $"{u.At - now:0}", small);
                y += 50;
            }
        }

        void DrawParty(float sw)
        {
            var party = units.Where(u => u.Team == Team.Player).ToList();
            float w = 260, x0 = (sw - w * party.Count) / 2f, y = RefH - 100;
            foreach (var u in party)
            {
                var r = new Rect(x0, y, w - 8, 96);
                Rect(r, new Color(0.05f, 0.07f, 0.12f, u == active ? 0.95f : 0.8f));
                if (u == active) Rect(new Rect(r.x, r.y, r.width, 3), new Color(1f, 0.85f, 0.3f));
                var b = Bust(u);
                if (b) GUI.DrawTexture(new Rect(r.x + 4, r.y + 8, 80, 80), b, ScaleMode.ScaleAndCrop);
                float bx = r.x + 92;
                GUI.Label(new Rect(bx, r.y + 4, 160, 20), $"{u.Name} <size=12><color=#bbbbbb>{u.Circuit.Name}·{Wuxing.Name(u.Element)}</color></size>", text);
                void Row(float dy, string label, int v, int max, Color c)
                {
                    GUI.Label(new Rect(bx, r.y + dy, 34, 18), label, small);
                    Bar(new Rect(bx + 34, r.y + dy + 4, 84, 10), v / (float)Mathf.Max(1, max), c);
                    GUI.Label(new Rect(bx + 124, r.y + dy, 40, 18), v.ToString(), small);
                }
                Row(28, "兵力", u.Hp, u.MaxHp, new Color(0.95f, 0.6f, 0.25f));
                Row(50, "策力", u.Ep, u.MaxEp, new Color(0.35f, 0.6f, 1f));
                Row(72, "气势", u.Cp, 100, new Color(0.4f, 0.85f, 0.4f));
                if (!u.Alive) Rect(r, new Color(0, 0, 0, 0.6f));
                x0 += w;
            }
        }

        void DrawRing()
        {
            var c = ToGui(views[active].position + Vector3.up * 0.8f);
            float target = -ringSel * 60f;
            ringAngle = Mathf.LerpAngle(ringAngle, target, 0.25f);
            Disc(c, 112, new Color(0.15f, 0.12f, 0.08f, 0.35f));
            for (int i = 0; i < RingItems.Length; i++)
            {
                float ang = (i * 60f + ringAngle) * Mathf.Deg2Rad;      // 0° 在右侧，为当前选项
                var p = c + new Vector2(Mathf.Cos(ang), -Mathf.Sin(ang)) * 100f;
                bool sel = i == ringSel;
                bool usable = RingUsable(RingItems[i]);
                Disc(p, sel ? 34 : 26, sel ? new Color(0.95f, 0.75f, 0.3f, 0.95f) : new Color(0.12f, 0.12f, 0.14f, 0.9f));
                Disc(p, sel ? 28 : 21, usable ? new Color(0.3f, 0.3f, 0.33f, 0.95f) : new Color(0.2f, 0.2f, 0.2f, 0.9f));
                var st = new GUIStyle(center) { fontSize = sel ? 18 : 15 };
                if (!usable) st.normal.textColor = new Color(0.55f, 0.55f, 0.55f);
                var r = new Rect(p.x - 34, p.y - 34, 68, 68);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) { if (sel && usable) ChooseRing(i); else ringSel = i; }
                GUI.Label(r, RingItems[i], st);
                if (sel)
                {
                    Rect(new Rect(p.x + 36, p.y - 18, 120, 36), new Color(0.35f, 0.22f, 0.08f, 0.92f));
                    GUI.Label(new Rect(p.x + 44, p.y - 16, 110, 32), RingItems[i], new GUIStyle(big) { fontSize = 20 });
                }
            }
            GUI.Label(new Rect(c.x - 120, c.y + 120, 240, 20), "↑↓ 转动 · 回车/再点一次 确定", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });
        }

        bool RingUsable(string item) => item switch
        {
            "攻击" => AttackableTargets(active, active.AtkRange).Count > 0,
            "武技" => active.Crafts.Count > 0,
            "策略" => StrategiesOf(active).Count > 0,
            _ => true
        };

        void DrawSkillList()
        {
            var c = ToGui(views[active].position);
            var r = new Rect(Mathf.Min(c.x + 60, Screen.width / S - 380), Mathf.Clamp(c.y - 160, 40, RefH - 420), 360, 0);
            var rows = new List<(SkillDef s, float mul, string extra, bool ok)>();
            if (showCraftList)
                foreach (var s in active.Crafts) rows.Add((s, 1f, $"气势{s.Cost}", active.Cp >= s.Cost));
            else
                foreach (var l in StrategiesOf(active))
                    rows.Add((l.Def, l.PowerMul, $"策力{l.Def.Cost} 咏唱{l.Def.Cast}" + (l.PowerMul > 1f ? $" 威力×{l.PowerMul:0.00}" : ""), active.Ep >= l.Def.Cost));
            r.height = 34 + rows.Count * 46;
            Rect(r, new Color(0.04f, 0.06f, 0.1f, 0.92f));
            GUI.Label(new Rect(r.x + 10, r.y + 6, 300, 22), showCraftList ? "武技（个人固有）" : "策略（兵书回路点亮）", text);
            float y = r.y + 32;
            foreach (var (s, mul, extra, ok) in rows)
            {
                var row = new Rect(r.x + 6, y, r.width - 12, 42);
                GUI.enabled = ok;
                if (GUI.Button(row, GUIContent.none))
                {
                    pendingSkill = s; pendingMul = mul; mode = Mode.TargetSkill;
                    if (s.Range < 0) reach = ReachOf(active);
                    Log(s.Area == Area.SelfCircle || s.Radius >= 99 ? $"「{s.Name}」：点击自身所在格确认" :
                        s.Area == Area.Line ? $"「{s.Name}」：点击相邻格决定方向" :
                        s.IsStrategy ? $"「{s.Name}」：选择施放位置（橙色为范围）" : $"「{s.Name}」：选择目标");
                }
                GUI.enabled = true;
                string el = s.IsStrategy ? $"<color=#{ColorUtility.ToHtmlStringRGB(Wuxing.Tint(s.Element))}>【{Wuxing.Name(s.Element)}】</color>" : "";
                GUI.Label(new Rect(row.x + 8, row.y + 2, 340, 20), $"{el}{s.Name}  <size=12>{extra}</size>", ok ? text : new GUIStyle(text) { normal = { textColor = Color.gray } });
                GUI.Label(new Rect(row.x + 8, row.y + 21, 340, 20), s.Desc, small);
                y += 46;
            }
        }

        void DrawHoverInfo(float sw)
        {
            var u = At(hover);
            if (u == null) return;
            var r = new Rect(sw - 250, 40, 240, 150);
            Rect(r, new Color(0.04f, 0.06f, 0.1f, 0.88f));
            string troop = u.Troop switch { Troop.Infantry => "步兵", Troop.Cavalry => "骑兵", Troop.Archer => "弓兵", Troop.Rebel => "黄巾军", _ => "文官／军师" };
            GUI.Label(new Rect(r.x + 10, r.y + 6, 220, 22), $"<b>{u.Name}</b>  {troop}", text);
            GUI.Label(new Rect(r.x + 10, r.y + 30, 220, 20), $"兵力 {u.Hp}/{u.MaxHp}   速度 {u.Speed}  移动 {u.Move}", small);
            GUI.Label(new Rect(r.x + 10, r.y + 50, 220, 20), $"物攻 {u.Atk} 物防 {u.Def} 计攻 {u.Mat} 计防 {u.Mdef}", small);
            GUI.Label(new Rect(r.x + 10, r.y + 70, 220, 20), $"五行 <color=#{ColorUtility.ToHtmlStringRGB(Wuxing.Tint(u.Element))}>{Wuxing.Name(u.Element)}</color>（被{Wuxing.Name(Wuxing.All.First(x => Wuxing.Overcomes(x) == u.Element))}克）", small);
            GUI.Label(new Rect(r.x + 10, r.y + 90, 220, 20), u.Statuses.Count > 0 ? "状态：" + string.Join("、", u.Statuses.Select(s => $"{s.Name}{s.Turns}")) : "状态：无", small);
            if (u.PendingCast != null) GUI.Label(new Rect(r.x + 10, r.y + 110, 220, 20), $"咏唱中：{u.PendingCast.Name}", small);
        }

        void DrawDeploy(float sw)
        {
            var r = new Rect(sw / 2 - 260, 40, 520, 64);
            Rect(r, new Color(0.04f, 0.06f, 0.1f, 0.85f));
            GUI.Label(new Rect(r.x + 12, r.y + 6, 500, 22), $"布阵 · 当前选中：{(deploySel != null ? deploySel.Name : "（点选我方武将）")}", text);
            if (GUI.Button(new Rect(r.x + 12, r.y + 32, 150, 26), "兵书回路")) { mode = Mode.Circuit; circuitSlot = -1; }
            if (GUI.Button(new Rect(r.x + 358, r.y + 32, 150, 26), "出阵！")) { mode = Mode.Busy; StartCoroutine(BattleLoop()); }
        }

        // ---------------- 兵书回路界面 ----------------
        Unit Owner(Book b) => units.FirstOrDefault(u => u.Team == Team.Player && u.Slots.Contains(b));

        void SetSchool(Unit u, School s)
        {
            u.School = s; u.Element = Circuits.Of(s).Affinity;
            var c = u.Circuit;
            for (int i = 0; i < u.Slots.Length; i++)
                if (u.Slots[i] != null && (i >= c.Slots.Length || !CircuitMath.Fits(c, i, u.Slots[i]))) u.Slots[i] = null;
        }

        void DrawCircuit(float sw)
        {
            var party = units.Where(u => u.Team == Team.Player).ToList();
            var u = party[circuitUnit];
            var c = u.Circuit;
            Rect(new Rect(0, 30, sw, RefH - 30), new Color(0.03f, 0.04f, 0.07f, 0.94f));

            // 武将页签
            for (int i = 0; i < party.Count; i++)
                if (GUI.Toggle(new Rect(20, 44 + i * 64, 180, 58), i == circuitUnit, GUIContent.none, GUI.skin.button) && i != circuitUnit) { circuitUnit = i; circuitSlot = -1; }
            for (int i = 0; i < party.Count; i++)
            {
                var b = Bust(party[i]);
                if (b) GUI.DrawTexture(new Rect(24, 48 + i * 64, 50, 50), b, ScaleMode.ScaleAndCrop);
                GUI.Label(new Rect(82, 52 + i * 64, 120, 22), party[i].Name, text);
                GUI.Label(new Rect(82, 74 + i * 64, 120, 20), $"{party[i].Circuit.Name}·偏{Wuxing.Name(party[i].Circuit.Affinity)}", small);
            }

            // 学派
            GUI.Label(new Rect(20, 250, 180, 20), "学派（原型可切换比较）", small);
            for (int i = 0; i < Circuits.All.Length; i++)
            {
                var cd = Circuits.All[i];
                if (GUI.Toggle(new Rect(20, 274 + i * 34, 180, 30), cd.School == u.School, $"{cd.Name}·偏{Wuxing.Name(cd.Affinity)}·包容{cd.Inclusivity}", GUI.skin.button) && cd.School != u.School)
                { SetSchool(u, cd.School); circuitSlot = -1; }
            }
            GUI.Label(new Rect(20, 450, 190, 120), c.Note, new GUIStyle(small) { wordWrap = true });

            // 回路图
            var hub = new Vector2(370, 250); float R = 130;
            Disc(hub, R + 34, new Color(0.25f, 0.2f, 0.12f, 0.9f));
            Disc(hub, R + 26, new Color(0.08f, 0.09f, 0.12f, 1f));
            Vector2 SP(int i) => hub + new Vector2(c.Slots[i].Pos.x, -c.Slots[i].Pos.y) * R;
            Color[] lineCols = { new Color(1f, 0.75f, 0.2f), new Color(0.4f, 0.75f, 1f), new Color(0.6f, 1f, 0.5f) };
            for (int l = 0; l < c.Lines.Length; l++)
                for (int k = 1; k < c.Lines[l].Length; k++)
                    Line(SP(c.Lines[l][k - 1]) + Vector2.one * (l * 3 - 3), SP(c.Lines[l][k]) + Vector2.one * (l * 3 - 3), 6, lineCols[l]);
            for (int i = 0; i < c.Slots.Length; i++)
            {
                var p = SP(i); var book = u.Slots[i]; var lockE = c.Slots[i].Locked;
                Disc(p, 28, i == circuitSlot ? Color.white : new Color(0.55f, 0.48f, 0.35f));
                Disc(p, 24, book != null ? Wuxing.Tint(book.Element) : (lockE != Element.None ? Wuxing.Tint(lockE) * 0.45f : new Color(0.18f, 0.18f, 0.2f)));
                var lab = book != null ? $"{Wuxing.Name(book.Element)}{CircuitMath.Value(c, book)}" : (lockE != Element.None ? $"限{Wuxing.Name(lockE)}" : "空");
                var st = new GUIStyle(center) { fontSize = 14 }; st.normal.textColor = book != null ? Color.black : Color.white;
                var rr = new Rect(p.x - 26, p.y - 26, 52, 52);
                if (GUI.Button(rr, GUIContent.none, GUIStyle.none))
                {
                    if (Event.current.button == 1 || (circuitSlot == i && book != null)) u.Slots[i] = null;
                    circuitSlot = i;
                }
                GUI.Label(rr, lab, st);
            }
            GUI.Label(new Rect(hub.x - 150, hub.y + R + 40, 300, 20), "点空槽再点右侧兵书镶嵌；再点一次已镶嵌槽卸下", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });

            // 线路合计（对应参考图3右上）
            float ly = 470;
            GUI.Label(new Rect(230, ly, 300, 20), "线路五行合计（策略看单条线）", small);
            for (int l = 0; l < c.Lines.Length; l++)
            {
                var t = CircuitMath.LineTotals(c, u.Slots, l);
                Rect(new Rect(230, ly + 24 + l * 26, 14, 14), lineCols[l]);
                string s = string.Join("  ", Wuxing.All.Select(e => $"<color=#{ColorUtility.ToHtmlStringRGB(Wuxing.Tint(e))}>{Wuxing.Name(e)}{t[e]}</color>"));
                GUI.Label(new Rect(252, ly + 20 + l * 26, 300, 22), $"线{l + 1}  {s}", text);
            }

            // 兵书库
            float bx = 580;
            GUI.Label(new Rect(bx, 44, 300, 22), circuitSlot >= 0 ? $"选择镶嵌到 {circuitSlot + 1} 号槽的兵书" : "兵书库（先点回路上的槽位）", text);
            int row = 0;
            foreach (var b in library.OrderBy(b => b.Element).ThenByDescending(b => b.Grade))
            {
                var owner = Owner(b);
                bool fits = circuitSlot >= 0 && CircuitMath.Fits(c, circuitSlot, b) && owner == null;
                var r = new Rect(bx + (row % 2) * 186, 72 + (row / 2) * 34, 180, 30);
                GUI.enabled = fits;
                if (GUI.Button(r, GUIContent.none)) { u.Slots[circuitSlot] = b; circuitSlot = -1; }
                GUI.enabled = true;
                Rect(new Rect(r.x + 4, r.y + 6, 18, 18), Wuxing.Tint(b.Element));
                string who = owner != null ? $"<color=#999999>（{owner.Name}）</color>" : "";
                GUI.Label(new Rect(r.x + 26, r.y + 4, 160, 22), $"{b.Name}<size=11>{b.Grade}</size>{who}", new GUIStyle(small) { fontSize = 12 });
                row++;
            }

            // 策略一览
            float sx = 960; var lit = CircuitMath.Lit(c, u.Slots);
            GUI.Label(new Rect(sx, 44, 300, 22), "策略（亮＝已点亮）", text);
            int k2 = 0;
            foreach (var s in Strategies.All)
            {
                var l = lit.FirstOrDefault(x => x.Def == s); bool on = l.Def != null;
                string req = string.Join("", s.Req.Select(kv => $"{Wuxing.Name(kv.Key)}{kv.Value}"));
                var col = on ? Wuxing.Tint(s.Element) : new Color(0.55f, 0.55f, 0.55f);
                GUI.Label(new Rect(sx, 72 + k2 * 40, 320, 20), $"<color=#{ColorUtility.ToHtmlStringRGB(col)}>{s.Name}</color>  <size=12>需{req}{(on ? $" · 线{l.Line + 1} · 威力×{l.PowerMul:0.00}" : "")}</size>", text);
                GUI.Label(new Rect(sx + 10, 93 + k2 * 40, 320, 18), s.Desc, new GUIStyle(small) { fontSize = 11, normal = { textColor = on ? Color.white : Color.gray } });
                k2++;
            }

            if (GUI.Button(new Rect(20, RefH - 60, 180, 36), "完成（回到布阵）")) mode = Mode.Deploy;
        }
    }
}
