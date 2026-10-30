// PROTOTYPE - NOT FOR PRODUCTION
// Question: 空轨式 AT 行动条＋小范围格子战斗，配合“学派回路镶嵌兵书→策略”，是否好玩、是否看得懂？
// Date: 2026-10-22
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace Prototype.BingshuTactics
{
    public sealed partial class TacticsGame : MonoBehaviour
    {
        // 由编辑器构建脚本填入（只引用美术资源，不引用正式代码）
        public Material LiuBeiMat, GuanYuMat, ZhangFeiMat, RebelMat, ZhangJiaoMat, LuBuMat, XiliangMat, WallArcherMat;
        public Texture2D LiuBeiBust, GuanYuBust, ZhangFeiBust, LuBuBust;
        public Material GroundMat, OverlayMat, RockMat;

        const float T = 1.2f;                       // 每格边长（米）
        const float HS = 0.5f;                      // 每层高度（米）
        int W, H;
        // 关卡：0＝黄巾旷野（平地），1＝虎牢关（高低与地形，用户 2026-10-23）；重开场景时保留
        static int stage = 1;
        readonly Dictionary<Collider, Vector2Int> tileOf = new Dictionary<Collider, Vector2Int>();
        readonly Dictionary<Unit, float> sizeOf = new Dictionary<Unit, float>();
        readonly Dictionary<Unit, int> turnCount = new Dictionary<Unit, int>();
        const float RefH = 720f;

        enum Mode { Deploy, Circuit, Idle, Menu, SkillList, TargetMove, TargetAttack, TargetSkill, Busy, Victory, Defeat, Duel }
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
        Texture2D plate;
        // 连线用传统颜料色：朱红、石青、藤黄、石绿、赭石、胭脂
        static readonly Color[] lineCols = { new Color(0.80f, 0.22f, 0.16f), new Color(0.22f, 0.50f, 0.66f), new Color(0.88f, 0.68f, 0.16f),
                                             new Color(0.30f, 0.62f, 0.42f), new Color(0.66f, 0.40f, 0.22f), new Color(0.70f, 0.20f, 0.42f) };

        static readonly string[] RingItems = { "攻击", "策略", "移动", "待机", "防御", "武技" };

        // ---------------- 初始化 ----------------
        void Start()
        {
            Application.targetFrameRate = 60;
            block = new MaterialPropertyBlock();
            if (IsChapter) stage = 2;
            grid = IsChapter ? Grid.FromRows(Chapter.Rows, ChapterLegend) : stage == 1 ? HulaoGrid() : FieldGrid();
            W = grid.W; H = grid.H;
            disc = MakeDisc(64);
            BuildWorld();
            BuildLibrary();
            BuildUnits();
            BuildBench();
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Microsoft YaHei UI", "SimHei", "SimSun", "DengXian" }, 18);
            white = Texture2D.whiteTexture;
            Log("布阵：点选我方武将，再点蓝色格子调整站位。可先打开「兵书回路」配书。");
            if (IsChapter && Chapter.AutoPlay) { autoPlay = true; mode = Mode.Busy; StartCoroutine(BattleLoop()); }
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
            yawTarget = 135f; yield return new WaitForSeconds(0.8f); yield return Shot("1b-deploy-rotated");
            yawTarget = 45f; yield return new WaitForSeconds(0.8f);
            // 全景与关门近景，对照 Codex 效果图
            zoomLevel = 1.9f; FocusOn(new Vector3(W * T / 2f, 0, H * T / 2f)); yield return new WaitForSeconds(1.0f); yield return Shot("1c-overview");
            zoomLevel = 0.8f; FocusOn(Tile(new Vector2Int(7, 11))); yield return new WaitForSeconds(1.0f); yield return Shot("1d-gate");
            zoomLevel = 1f; manual = false; yield return new WaitForSeconds(0.5f);
            mode = Mode.Circuit; circuitUnit = 1; circuitSlot = 3;
            yield return new WaitForSeconds(0.5f); yield return Shot("2-circuit-guanyu");
            circuitUnit = 0; circuitSlot = -1;
            yield return new WaitForSeconds(0.3f); yield return Shot("3-circuit-liubei");
            circuitSlot = 5; circuitFocus = 1; bookSel = 4; yield return new WaitForSeconds(0.3f); yield return Shot("3d-circuit-choose-book");
            circuitFocus = 0; circuitUnit = 3; yield return new WaitForSeconds(0.3f); yield return Shot("3e-circuit-zhaoyun"); circuitUnit = 0;
            circuitUnit = 2;
            yield return new WaitForSeconds(0.3f); yield return Shot("3b-circuit-zhangfei-int48");
            var zf = units.First(x => x.Name == "张飞"); int keep = zf.Int; zf.Int = 100;
            yield return new WaitForSeconds(0.3f); yield return Shot("3c-circuit-zhangfei-int100");
            zf.Int = keep; circuitUnit = 0;
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
            // 用虚拟手柄走一遍：十字键下两次转到“移动”，A 确认，十字键移动光标，A 移动
            pendingSkill = null; forcedHover = null; ringSel = DefaultRing(); mode = Mode.Menu;
            Debug.Log($"POC_RING_DEFAULT start={RingItems[ringSel]} attackUsable={RingUsable("攻击")}");
            var vpad = InputSystem.AddDevice<Gamepad>(); vpad.MakeCurrent(); testPad = vpad;
            yield return new WaitForSeconds(0.3f);   // 等新设备生效
            for (int k = 0; k < 6 && RingItems[ringSel] != "移动"; k++) yield return Tap(vpad, GamepadButton.DpadDown);
            yield return new WaitForSeconds(0.4f); yield return Shot("4c-pad-ring");
            bool ringOk = RingItems[ringSel] == "移动";
            yield return Tap(vpad, GamepadButton.South);
            bool targetOk = mode == Mode.TargetMove;
            var startPos = active.Pos;
            yield return Tap(vpad, GamepadButton.DpadUp); yield return Tap(vpad, GamepadButton.DpadUp);
            yield return new WaitForSeconds(0.3f); yield return Shot("4d-pad-cursor");
            yield return Tap(vpad, GamepadButton.South);
            float tw = Time.time; while (mode != Mode.Menu && Time.time - tw < 3f) yield return null;
            Debug.Log($"POC_PAD_RESULT ring={ringOk} target={targetOk} moved={active.Pos != startPos} cursor={cursor} mode={mode}");
            Debug.Log($"POC_RING_AFTER_MOVE default={RingItems[ringSel]} moveUsable={RingUsable("移动")} stratUsable={RingUsable("策略")}");
            if (mode != Mode.Menu)
            {
                mode = Mode.Menu; ChooseRing(2);
                var dest = reach.Keys.Where(p => p != active.Pos && CanStop(active, p)).OrderByDescending(p => p.y).First();
                yield return MoveThenAct(Grid.Path(reach, dest));
            }
            yield return new WaitForSeconds(0.6f); yield return Shot("4b-after-move");
            // 取消键退回原位
            var moveSpot = active.Pos;
            yield return Tap(vpad, GamepadButton.East);
            testPad = null;
            Debug.Log($"POC_UNDO_MOVE back={active.Pos == startPos} from={moveSpot} to={active.Pos} moved={moved}");
            mode = Mode.Menu; ChooseRing(System.Array.IndexOf(RingItems, "移动"));
            var dest2 = reach.Keys.Where(p => p != active.Pos && CanStop(active, p)).OrderByDescending(p => p.y).First();
            yield return MoveThenAct(Grid.Path(reach, dest2));
            autoPlay = true; ChooseRing(4);
            float t0 = Time.time;
            while (mode != Mode.Victory && mode != Mode.Defeat && Time.time - t0 < 240f)
            {
                if (units.Any(u => u.Alive && u.PendingCast != null && u.Team == Team.Enemy) && !shotCast) { shotCast = true; yield return Shot("7-enemy-casting"); }
                if (!shotRing && castRings.Any(r => r.enabled)) { shotRing = true; yield return new WaitForSeconds(0.3f); yield return Shot("7b-cast-ring"); }
                if (mode == Mode.Duel && duel != null && duel.Choosing && shotDuel < 2) { shotDuel++; yield return Shot("10-duel-" + shotDuel); }
                if (stage == 1 && !shotFace && units.Any(u => u.Alive && u.Team == Team.Player && u.Pos == new Vector2Int(7, 9))) { shotFace = true; yield return new WaitForSeconds(0.8f); yield return Shot("9-face-lubu"); }
                yield return null;
            }
            yield return new WaitForSeconds(0.5f); yield return Shot("8-end");
            Debug.Log($"POC_AUTOPLAY_RESULT {mode} turns={turns} time={Time.time - t0:0}s duels={string.Join(",", dueled)} trio={trioDone}");
            Application.Quit();
        }
        bool shotCast, shotFace, shotRing; int shotDuel; Vector2Int? forcedHover;

        static IEnumerator Tap(Gamepad pad, GamepadButton b)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(b));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null; yield return null;
        }

        // 虎牢关：北面关墙（三层高），吕布守在关门前一格（2026-10-24 前移一排，关门前留出一格门道）；左右拒马两排，只有正前方一格能与他近身交锋。
        // 两侧营寨驻西凉铁骑，关墙上有弓手；中段河流只有两处浅滩和一座桥可过；东西两侧有山坡。
        public static readonly string[] HulaoRows =
        {
            "WWWWWWWGWWWWWW",
            "cccxxxx:xxxxcc",
            "ccc.xxx:xxx.cc",
            "^^.,...:.,..^^",
            "A^.,,..:..,,^A",
            "A^..,,.:.,,.^A",
            "^^...,.:.,...^",
            "~~~=~~~b~~~=~~",
            "...,...:...,..",
            "..r....:....r.",
            ".......:......",
            ".......:......",
            ".......:......",
        };

        public static (Terrain, int) Legend(char ch) => ch switch
        {
            ',' => (Terrain.Sand, 0), '~' => (Terrain.River, 0), '=' => (Terrain.Ford, 0), 'b' => (Terrain.Bridge, 0),
            ':' => (Terrain.Road, 0), 'c' => (Terrain.Camp, 0), 'W' => (Terrain.Wall, 3), 'G' => (Terrain.Gate, 3),
            'x' => (Terrain.JuMa, 0), 'r' => (Terrain.Rock, 0), '^' => (Terrain.Grass, 1), 'A' => (Terrain.Grass, 2), _ => (Terrain.Grass, 0)
        };

        public static Grid HulaoGrid() => Grid.FromRows(HulaoRows, Legend);

        static Grid FieldGrid()
        {
            var g = new Grid(12, 10);
            foreach (var p in new[] { new Vector2Int(5, 2), new Vector2Int(5, 3), new Vector2Int(6, 7), new Vector2Int(4, 7), new Vector2Int(8, 4), new Vector2Int(3, 4) })
                g.Ter[p.x, p.y] = Terrain.Rock;
            return g;
        }

        bool InDeploy(Vector2Int p) => IsChapter ? Chapter.Deploy.Contains(p) : stage == 1 ? p.y <= 1 && p.x >= 3 && p.x <= 10 : p.x < 3;

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
            SetupHD2D(sun);
            if (IsChapter) ChapterLighting(sun);

            BuildWorld3D();

            var ov = new GameObject("Overlay");
            overlayMesh = new Mesh();
            ov.AddComponent<MeshFilter>().sharedMesh = overlayMesh;
            var mr = ov.AddComponent<MeshRenderer>();
            mr.sharedMaterial = OverlayMat; mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
        }

        void BuildLibrary()
        {
            // 书库与开局镶嵌和行旅菜单的兵书页共用一份（Bookcase）；篇章战斗用玩家当前的书库（同一批 Book 对象）
            library.Clear();
            library.AddRange(IsChapter && Chapter.Library != null ? Chapter.Library : Bookcase.NewLibrary());
        }

        Book L(string name, int skip = 0) => library.Where(b => b.Name == name).Skip(skip).First();

        // 刘关张默认配书（原型配置；兵书页的改动只在本次游玩内存中）
        void DefaultBooks(Unit u)
        {
            Bookcase.Default(u.Name, u.Slots, library);
            RefreshTechniques(u);
        }

        void BuildUnits()
        {
            if (IsChapter) { BuildChapterUnits(); return; }
            var liu = NewUnit("刘备", "LiuBei", Team.Player, Troop.Infantry, School.Ru, 160, 50, 40, 56, 35, 6, 3, 1, 58, new Vector2Int(1, 5));
            liu.Int = 76; liu.Wu = 68;   // 武将表 v0.3 固定四维
            liu.Crafts.Add(Craft("整队", "单体友军回复30%兵力", Effect.Heal, Area.Single, 3, 0, 0.30f, 20));
            liu.Crafts.Add(Craft("火矢", "单体计策1.3倍（射程4）", Effect.Damage, Area.Single, 4, 0, 1.3f, 25));

            var guan = NewUnit("关羽", "GuanYu", Team.Player, Troop.Cavalry, School.Bing, 170, 64, 36, 47, 29, 10, 5, 1, 40, new Vector2Int(2, 7));
            guan.Int = 70; guan.Wu = 96;
            guan.Crafts.Add(Craft("突击", "近战单体物理1.5倍", Effect.Damage, Area.Single, -1, 0, 1.5f, 25, might: true));
            guan.Crafts.Add(Craft("破阵", "近战单体1.1倍，物防-20% 2回合", Effect.Debuff, Area.Single, -1, 0, 1.1f, 30, might: true, turns: 2, amount: 0.2f));

            var zhang = NewUnit("张飞", "ZhangFei", Team.Player, Troop.Cavalry, School.Fa, 170, 66, 36, 40, 24, 10, 5, 1, 40, new Vector2Int(2, 3));
            zhang.Int = 40; zhang.Wu = 97;
            zhang.Crafts.Add(Craft("横扫", "自身周围2格内敌军物理0.8倍", Effect.Damage, Area.SelfCircle, 0, 2, 0.8f, 30, might: true));
            zhang.Crafts.Add(Craft("威慑", "自身周围3格内敌军物攻-20% 2回合", Effect.Debuff, Area.SelfCircle, 0, 3, 0f, 25, turns: 2, amount: 0.2f));
            foreach (var pu in new[] { liu, guan, zhang }) DefaultBooks(pu);

            if (stage == 1)
            {
                liu.Pos = new Vector2Int(5, 0); guan.Pos = new Vector2Int(7, 1); zhang.Pos = new Vector2Int(9, 0);
                foreach (var pu in new[] { liu, guan, zhang }) views[pu].position = Tile(pu.Pos);
                var lubu = NewUnit("吕布", "LuBu", Team.Enemy, Troop.Cavalry, School.Bing, 700, 92, 45, 30, 22, 11, 0, 1, 0, new Vector2Int(7, 10), 1.25f, new Color(1f, 0.78f, 0.78f));
                lubu.Int = 26; lubu.Wu = 100;
                lubu.Crafts.Add(Craft("天下无双", "方天画戟直劈：正前方2格物理1.3倍", Effect.Damage, Area.Line, 1, 0, 1.3f, 0, might: true));
                lubu.Crafts[0].Length = 2;
                foreach (var ap in new[] { new Vector2Int(4, 12), new Vector2Int(10, 12) })
                    NewUnit("关上弓手", "WallArcher", Team.Enemy, Troop.Archer, School.Mo, 90, 40, 22, 20, 18, 7, 0, 4, 0, ap);
                foreach (var xp in new[] { new Vector2Int(1, 10), new Vector2Int(12, 10) })
                    NewUnit("西凉铁骑", "Xiliang", Team.Enemy, Troop.Cavalry, School.Bing, 140, 55, 30, 20, 20, 10, 5, 1, 0, xp, 1f, new Color(0.85f, 0.88f, 1f));
                return;
            }
            var rebels = new[] { new Vector2Int(9, 2), new Vector2Int(10, 4), new Vector2Int(9, 6), new Vector2Int(10, 8) };
            for (int i = 0; i < rebels.Length; i++)
                NewUnit("黄巾兵" + "甲乙丙丁"[i], "Rebel", Team.Enemy, Troop.Rebel, School.Fa, 130, 45, 28, 30, 22, 6, 3, 1, 0, rebels[i]).Element = Element.Earth;
            var jiao = NewUnit("张角", "ZhangJiao", Team.Enemy, Troop.Strategist, School.Dao, 200, 35, 26, 60, 35, 7, 2, 2, 40, new Vector2Int(11, 5));
            jiao.Element = Element.Earth;
        }

        Unit NewUnit(string name, string key, Team team, Troop troop, School school, int hp, int atk, int def, int mat, int mdef,
            int speed, int move, int range, int ep, Vector2Int pos, float scale = 1f, Color? tint = null)
        {
            var u = new Unit
            {
                Name = name, Key = key, Team = team, Troop = troop, School = school, Element = Circuits.Of(school).Affinity,
                MaxHp = hp, Hp = hp, Atk = atk, Def = def, Mat = mat, Mdef = mdef, Speed = speed, Move = move, AtkRange = range,
                MaxEp = ep, Ep = ep, Cp = 30, Pos = pos, At = AtClock.Delay(100, speed)
            };
            units.Add(u);
            var material = key switch
            {
                "LiuBei" => LiuBeiMat, "GuanYu" => GuanYuMat, "ZhangFei" => ZhangFeiMat, "ZhangJiao" => ZhangJiaoMat,
                "LuBu" => LuBuMat, "Xiliang" => XiliangMat, "WallArcher" => WallArcherMat, _ => CharMat(key) ?? RebelMat
            };
            var root = new GameObject(name).transform;
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(root, false);
            var r = quad.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            views[u] = root; quads[u] = r;
            InitRig(u, root, r, scale, tint);
            sizeOf[u] = rigs[u].Size;
            root.position = Tile(pos);
            return u;
        }

        static SkillDef Craft(string name, string desc, Effect fx, Area area, int range, int radius, float power, int cost,
            bool might = false, int turns = 0, float amount = 0f) =>
            new SkillDef { Name = name, Desc = desc, Effect = fx, Area = area, Range = range, Radius = radius, Power = power, Cost = cost, UsesMight = might, Turns = turns, Amount = amount, Recover = 110 };

        // ---------------- 坐标与绘制工具 ----------------
        Vector3 Tile(Vector2Int p) => new Vector3((p.x + 0.5f) * T, grid.In(p) ? Mathf.Max(0f, TopOf(p)) : 0f, (p.y + 0.5f) * T);
        bool InCamp(Unit u) => grid.TerrainAt(u.Pos) == Terrain.Camp;
        float HMul(Unit a, Unit t) => Combat.HeightMul(grid.HeightAt(a.Pos), grid.HeightAt(t.Pos));
        // 弓兵站在两层以上的高处，射程 +1
        int RangeAt(Unit u, Vector2Int p, int baseRange) => baseRange + (baseRange > 1 && grid.HeightAt(p) >= 2 ? 1 : 0);
        Unit At(Vector2Int p) => units.FirstOrDefault(u => u.Alive && u.Pos == p);
        float S => Screen.height / RefH;

        Vector2 ToGui(Vector3 world)
        {
            var sp = cam.WorldToScreenPoint(world);
            return new Vector2(sp.x / S, (Screen.height - sp.y) / S);
        }

        // 八卦盘底图：八角铜盘、金边、外圈后天八卦三爻、内圈槽位环、中心太极
        static Texture2D MakeBaguaPlate(int n)
        {
            var t = new Texture2D(n, n) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            Color gold = new Color(0.80f, 0.64f, 0.30f), rim = new Color(0.62f, 0.47f, 0.20f), clear = new Color(0, 0, 0, 0);
            Color ink = new Color(0.10f, 0.09f, 0.08f), paper = new Color(0.91f, 0.86f, 0.74f);
            float[,] bands = { { 0.78f, 0.82f }, { 0.835f, 0.875f }, { 0.89f, 0.93f } };
            for (int px = 0; px < n; px++)
                for (int py = 0; py < n; py++)
                {
                    float x = (px + 0.5f) / n * 2f - 1f, y = (py + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y), (Mathf.Abs(x) + Mathf.Abs(y)) / Mathf.Sqrt(2f));
                    float r = Mathf.Sqrt(x * x + y * y);
                    Color c;
                    if (d > 1f) c = clear;
                    else if (d > 0.965f) c = rim;
                    else
                    {
                        c = Color.Lerp(new Color(0.20f, 0.15f, 0.10f), new Color(0.12f, 0.09f, 0.06f), r);
                        if (d > 0.945f && d < 0.955f) c = gold * 0.8f;
                        if (d > 0.735f && d < 0.75f) c = gold * 0.8f;
                        // 外圈三爻
                        float ang = Mathf.Atan2(y, x) * Mathf.Rad2Deg;
                        float k = Mathf.Round(ang / 45f) * 45f;
                        var g = Bagua.All.First(q => Mathf.Abs(Mathf.DeltaAngle(q.Angle, k)) < 1f);
                        var u = g.Dir; float along = Vector2.Dot(new Vector2(x, y), new Vector2(-u.y, u.x));
                        for (int b = 0; b < 3; b++)
                            if (d >= bands[b, 0] && d <= bands[b, 1] && Mathf.Abs(along) < 0.27f * d && !(g.Yao[b] == 0 && Mathf.Abs(along) < 0.05f * d))
                                c = gold;
                        // 槽位环
                        if (Mathf.Abs(r - 0.6f) < 0.006f) c = Color.Lerp(c, gold, 0.5f);
                        // 太极
                        if (r < 0.30f)
                        {
                            float tx = x / 0.27f, ty = y / 0.27f;
                            if (r > 0.27f) c = gold;
                            else
                            {
                                bool dark = tx < 0;
                                if (Vector2.Distance(new Vector2(tx, ty), new Vector2(0, 0.5f)) < 0.5f) dark = true;
                                if (Vector2.Distance(new Vector2(tx, ty), new Vector2(0, -0.5f)) < 0.5f) dark = false;
                                if (Vector2.Distance(new Vector2(tx, ty), new Vector2(0, 0.5f)) < 0.14f) dark = false;
                                if (Vector2.Distance(new Vector2(tx, ty), new Vector2(0, -0.5f)) < 0.14f) dark = true;
                                c = dark ? ink : paper;
                            }
                        }
                    }
                    t.SetPixel(px, py, c);
                }
            t.Apply();
            return t;
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

        void Log(string s)
        {
            log = s;
            if (Chapter != null && Chapter.AutoPlay && Application.isBatchMode) Debug.Log($"TACTICS r{Rounds} {s} | " + string.Join(" ", units.Where(u => u.Alive).Select(u => $"{u.Name}:{u.Hp}")));   // tests: the AI play-through in the log
        }

        // ---------------- 每帧 ----------------
        void Update()
        {
            UpdateCameraYaw();
            UpdateCamera();   // 跟随、自由查看、对准人物、单挑拉近（TacticsGame.Camera.cs）

            UpdateRigs();
            UpdateScenery();
            UpdateCastRings();
            UpdateChapter();

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
            if (Physics.Raycast(ray, out var hitInfo, 300f) && tileOf.TryGetValue(hitInfo.collider, out var hp)) hover = hp;
            HandlePadAndKeys();
            if (padMode && grid.In(cursor)) hover = cursor;
            if (forcedHover.HasValue) hover = forcedHover.Value;
            RebuildOverlay();
        }

        Vector2 mouseScreen, lastMouse; Vector3 duelLook;

        // ---------------- 键盘与手柄（Input System） ----------------
        // 方向：方向键／WASD／十字键／左摇杆；确认：回车／空格／Z／手柄 A；取消：Esc／退格／X／手柄 B
        // 镜头：Q/E／LB/RB；布阵：Tab／Y 开兵书回路，F／Start 出阵，T／Select 换关
        Vector2Int cursor = new Vector2Int(-1, -1);
        bool padMode; Vector2Int lastDir; float repeatT;
        int circuitFocus, bookSel;

        static Keyboard Kb => Keyboard.current;
        static Gamepad testPad;   // 自检用虚拟手柄（机器上接着真手柄时，Gamepad.current 可能仍是真手柄）
        static Gamepad Pad => testPad ?? Gamepad.current;
        static bool Pressed(params ButtonControl[] bs) => bs.Any(b => b != null && b.wasPressedThisFrame);

        bool ConfirmPressed => Pressed(Kb?.enterKey, Kb?.numpadEnterKey, Kb?.spaceKey, Kb?.zKey, PadButton("Submit"));
        bool CancelPressed => Pressed(Kb?.escapeKey, Kb?.backspaceKey, Kb?.xKey, PadButton("Cancel"));

        // 手柄确认／取消按设备自身的 Submit／Cancel 用途取键：Xbox 为下／右，任天堂布局为右（A）／下（B）（用户 2026-10-23：A、B 反了）
        static ButtonControl PadButton(string usage)
        {
            var pad = Pad;
            if (pad == null) return null;
            foreach (var c in pad.allControls)
                if (c is ButtonControl b && c.usages.Contains(new UnityEngine.InputSystem.Utilities.InternedString(usage))) return b;
            bool nintendo = (pad.description.manufacturer + pad.description.product + pad.layout).ToLowerInvariant().Contains("nintendo")
                         || (pad.description.product ?? "").ToLowerInvariant().Contains("pro controller") || pad.layout.ToLowerInvariant().Contains("switch");
            return usage == "Submit" ? (nintendo ? pad.buttonEast : pad.buttonSouth) : (nintendo ? pad.buttonSouth : pad.buttonEast);
        }

        Vector2Int ReadDir()
        {
            int x = 0, y = 0;
            if (Kb != null)
            {
                if (Kb.upArrowKey.isPressed || Kb.wKey.isPressed) y++;
                if (Kb.downArrowKey.isPressed || Kb.sKey.isPressed) y--;
                if (Kb.leftArrowKey.isPressed || Kb.aKey.isPressed) x--;
                if (Kb.rightArrowKey.isPressed || Kb.dKey.isPressed) x++;
            }
            if (Pad != null)
            {
                var v = Pad.dpad.ReadValue() + Pad.leftStick.ReadValue();
                if (v.y > 0.5f) y++; if (v.y < -0.5f) y--; if (v.x < -0.5f) x--; if (v.x > 0.5f) x++;
            }
            return new Vector2Int(Mathf.Clamp(x, -1, 1), Mathf.Clamp(y, -1, 1));
        }

        // 按住时自动连发
        Vector2Int DirPulse()
        {
            var d = ReadDir();
            if (d == Vector2Int.zero) { lastDir = d; return d; }
            if (d != lastDir) { lastDir = d; repeatT = 0.28f; return d; }
            repeatT -= Time.deltaTime;
            if (repeatT <= 0f) { repeatT = 0.09f; return d; }
            return Vector2Int.zero;
        }

        // 屏幕方向换算成格子方向（随镜头旋转）
        Vector2Int ScreenToGrid(Vector2Int d) => Grid.ScreenDirToGrid(yawTarget, d);

        void HandlePadAndKeys()
        {
            if ((mouseScreen - lastMouse).sqrMagnitude > 4f) padMode = false;
            lastMouse = mouseScreen;
            var pulse = DirPulse();
            bool ok = ConfirmPressed, cancel = CancelPressed;
            bool lb = Pressed(Kb?.qKey, Pad?.leftShoulder), rb = Pressed(Kb?.eKey, Pad?.rightShoulder);
            if (pulse != Vector2Int.zero || ok || cancel) padMode = true;

            if (mode == Mode.Circuit) { CircuitInput(pulse, ok, cancel, lb, rb); return; }
            if (mode == Mode.Duel) { DuelInput(pulse, ok); return; }
            if (lb) yawTarget -= 90f;
            if (rb) yawTarget += 90f;

            switch (mode)
            {
                case Mode.Menu:
                    if (cancel && moved) { UndoMove(); return; }
                    if (pulse.y != 0) ringSel = NextUsable(ringSel, pulse.y);   // 下键顺时针转（用户 2026-10-25）；跳过不可用的选项
                    if (ok && RingUsable(RingItems[ringSel])) ChooseRing(ringSel);
                    return;
                case Mode.SkillList:
                    var rows = SkillRows();
                    if (pulse.y != 0 && rows.Count > 0) listSel = (listSel - pulse.y + rows.Count) % rows.Count;
                    if (ok && rows.Count > 0) ChooseSkill(rows[Mathf.Clamp(listSel, 0, rows.Count - 1)]);
                    if (cancel) Back();
                    return;
                case Mode.Victory:
                case Mode.Defeat:
                    if (IsChapter) { if (ok && !(Chapter.AutoPlay && mode == Mode.Victory)) FinishChapter(); return; }
                    if (ok || Pressed(Kb?.rKey, Pad?.startButton)) UnityEngine.SceneManagement.SceneManager.LoadScene(gameObject.scene.buildIndex);
                    return;
                case Mode.Busy:
                    return;
            }
            // 地图光标：布阵与各种选目标
            if (pulse != Vector2Int.zero)
            {
                if (!grid.In(cursor)) cursor = active != null ? active.Pos : units.First(u => u.Team == Team.Player).Pos;
                var n = cursor + ScreenToGrid(pulse);
                bool allowed = grid.In(n);
                if (mode == Mode.TargetMove && reach != null) allowed &= reach.ContainsKey(n);   // 只能在可移动范围内
                if (mode == Mode.TargetSkill && pendingSkill != null && pendingSkill.Range >= 0) allowed &= SkillRange(active, pendingSkill).Contains(n);
                if (allowed) cursor = n;
            }
            if (ok && grid.In(cursor)) ClickTile(cursor);
            if (cancel) Back();
            if (mode == Mode.Deploy)
            {
                if (Pressed(Kb?.tabKey, Pad?.buttonNorth)) { mode = Mode.Circuit; circuitSlot = 0; circuitFocus = 0; }
                else if (Pressed(Kb?.fKey, Pad?.startButton)) { mode = Mode.Busy; StartCoroutine(BattleLoop()); }
                else if (!IsChapter && Pressed(Kb?.tKey, Pad?.selectButton)) { stage = 1 - stage; UnityEngine.SceneManagement.SceneManager.LoadScene(gameObject.scene.buildIndex); }
            }
        }

        List<Book> ShelfOrder() => library.OrderBy(b => b.Element).ThenByDescending(b => b.Grade).ToList();

        void RebuildOverlay()
        {
            var tiles = new List<(Vector2Int p, Color c, float inset)>();
            void Add(IEnumerable<Vector2Int> ps, Color c, float inset = 0.06f) { foreach (var p in ps) tiles.Add((p, c, inset)); }

            foreach (var u in units.Where(u => u.Alive))
                Add(new[] { u.Pos }, u.Team == Team.Player ? new Color(0.3f, 0.6f, 1f, 0.35f) : new Color(1f, 0.3f, 0.25f, 0.35f), 0.18f);

            if (mode == Mode.Deploy)
                for (int x = 0; x < W; x++)
                    for (int y = 0; y < H; y++)
                    {
                        var dp = new Vector2Int(x, y);
                        if (InDeploy(dp) && !Grid.Impassable(grid.TerrainAt(dp))) Add(new[] { dp }, new Color(0.35f, 0.65f, 1f, 0.22f));
                    }

            foreach (var u in units.Where(u => u.Alive && u.PendingCast != null))
                Add(CastArea(u), u.Team == Team.Player ? new Color(0.7f, 0.4f, 1f, 0.30f) : new Color(1f, 0.15f, 0.55f, 0.35f));

            if (mode == Mode.TargetMove && reach != null) Add(reach.Keys.Where(p => CanStop(active, p)), new Color(0.3f, 0.65f, 1f, 0.38f));
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
            if (grid.In(hover) && mode != Mode.Circuit) Add(new[] { hover }, padMode ? new Color(1f, 0.95f, 0.5f, 0.35f + 0.2f * Mathf.Sin(Time.time * 8f)) : new Color(1f, 1f, 1f, 0.25f), 0.0f);

            var v = new List<Vector3>(); var c = new List<Color>(); var idx = new List<int>();
            foreach (var (p, col, inset) in tiles)
            {
                float x0 = (p.x + inset) * T, x1 = (p.x + 1 - inset) * T, z0 = (p.y + inset) * T, z1 = (p.y + 1 - inset) * T, y = TopOf(p) + 0.02f + 0.001f * (v.Count / 4 % 50);
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
        // 本回合已移动过的单位，攻击与近战武技只能在原地出手
        // 友军可穿过但不能停留；敌方单位挡路；敌方单位与敌方防守建筑（关墙、关门）旁边有 ZOC，进入即停
        Dictionary<Vector2Int, Vector2Int> ReachOf(Unit u) => u == active && moved
            ? new Dictionary<Vector2Int, Vector2Int> { [u.Pos] = u.Pos }
            : grid.Reachable(u.Pos, u.Move, p => At(p) != null && At(p).Team != u.Team, u.Troop, p => InZoc(u, p));

        bool InZoc(Unit u, Vector2Int p)
        {
            foreach (var d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
            {
                var n = p + d;
                if (!grid.In(n)) continue;
                var o = At(n);
                if (o != null && o.Team != u.Team) return true;
                if (u.Team == Team.Player && stage == 1 && (grid.TerrainAt(n) == Terrain.Wall || grid.TerrainAt(n) == Terrain.Gate)) return true;
            }
            return false;
        }

        bool CanStop(Unit u, Vector2Int p) => p == u.Pos || At(p) == null;

        IEnumerable<Unit> Foes(Unit u) => units.Where(x => x.Alive && x.Team != u.Team);
        IEnumerable<Unit> Friends(Unit u) => units.Where(x => x.Alive && x.Team == u.Team);

        List<Unit> AttackableTargets(Unit u, int range)
        {
            var r = range > 1 ? AttackReach(u) : ReachOf(u);
            return Foes(u).Where(f => r.Keys.Any(p => CanStop(u, p) && Grid.Dist(p, f.Pos) <= RangeAt(u, p, range))).ToList();
        }

        Vector2Int ApproachTile(Unit u, Unit target, int range, Dictionary<Vector2Int, Vector2Int> r) =>
            r.Keys.Where(p => CanStop(u, p) && Grid.Dist(p, target.Pos) <= RangeAt(u, p, range))
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

        static readonly SkillDef Zonghuo = new SkillDef
        {
            Name = "纵火", Desc = "单体纵火（张角策略绝学）", IsStrategy = true, Element = Element.Fire, Effect = Effect.Damage, Area = Area.Single,
            Range = 5, Power = 1.25f, Cost = 6, Cast = 40,
        };

        List<LitStrategy> StrategiesOf(Unit u)
        {
            if (u.Name == "张角")   // 主稿 §3.9：纵火（单体计策 1.25，策力 6，咏唱 40）与火烧营寨
                return new[] { Zonghuo }.Concat(Strategies.All.Where(s => s.Name == "火烧营寨")).Select(s => new LitStrategy(s, 0, 0)).ToList();
            return CircuitMath.Lit(u.Circuit, u.Slots, u.Int);
        }

        // ---------------- 战斗主循环 ----------------
        // 功法书点出的武技（设计稿 15.9）随回路更新：先去掉旧的书武技，再按当前镶嵌重新加上
        void RefreshTechniques(Unit u)
        {
            var bookSkills = new HashSet<SkillDef>(TechniqueTable.All);
            u.Crafts.RemoveAll(bookSkills.Contains);
            foreach (var t in CircuitMath.Techniques(u.Circuit, u.Slots, u.Int)) u.Crafts.Add(t.Def);
        }

        IEnumerator BattleLoop()
        {
            foreach (var pu in units.Where(x => x.Team == Team.Player)) RefreshTechniques(pu);
            Log("开战！左侧行动条从上到下为行动顺序；策略需要咏唱，读条期间会被行动条挤后。");
            if (IsChapter) yield return OpeningLines();
            while (true)
            {
                var u = AtClock.Next(units);
                now = u.At; active = u; u.Defending = false; moved = false; u.Charged = false;
                u.Ep = Mathf.Min(u.MaxEp, u.Ep + TroopTraits.EpRegen(u.Int));   // 主稿 §3.4：每次轮到自己回 ⌊智÷20⌋ 策力
                if (IsChapter)
                {
                    yield return ChapterRoundEvents();
                    if (mode == Mode.Victory || mode == Mode.Defeat) yield break;
                    if (!u.Alive) continue;
                }
                yield return new WaitForSeconds(0.25f);

                if (ChaosHoldsTurn(u))
                {
                    Log($"{u.Name} 陷入混乱，原地不动");
                    popups.Add((views[u].position + Vector3.up * (2.2f * HeadK), "混乱", new Color(1f, 0.55f, 0.5f), 0f));
                    yield return new WaitForSeconds(0.3f);
                    u.At = now + AtClock.Delay(100, u.Speed);
                }
                else if (u.PendingCast != null)
                {
                    yield return ExecuteCast(u);
                    u.At = now + AtClock.Delay(u.PendingCast?.Recover ?? 80, u.Speed);
                    u.PendingCast = null; u.PendingFollow = null;
                }
                else if (u.Team == Team.Player && (autoPlay || u.Ai))
                {
                    mode = Mode.Busy;
                    yield return EnemyTurn(u);
                }
                else if (u.Team == Team.Player)
                {
                    actionDone = false; moved = false; ringSel = DefaultRing(); cursor = u.Pos; mode = Mode.Menu;
                    Log($"{u.Name} 的回合");
                    while (!actionDone) yield return null;
                }
                else
                {
                    mode = Mode.Busy;
                    yield return EnemyTurn(u);
                }
                if (u.Team == Team.Player) yield return MaybeDuel(u);
                if (u.Troop == Troop.Dancer)   // 舞姬：每次行动为半径 2 友军回 3 气势
                    foreach (var f in Friends(u).Where(f => f != u && Grid.Dist(f.Pos, u.Pos) <= 2)) GainCp(f, 3);
                u.TickStatuses();
                mode = Mode.Busy; turns++;

                if (IsChapter)
                {
                    yield return ChapterHpEvents();
                    if (CheckChapterEnd()) yield break;
                    continue;
                }
                if (stage == 1 && !units.First(x => x.Name == "吕布").Alive) { mode = Mode.Victory; Log("吕布力敌三英不支，拨马退入虎牢关！按 R 重来。"); yield break; }
                if (!Foes(units.First(x => x.Team == Team.Player)).Any()) { mode = Mode.Victory; Log("胜利！黄巾溃退。按 R 重来。"); yield break; }
                if (!units.Any(x => x.Alive && x.Team == Team.Player)) { mode = Mode.Defeat; Log("我军败退……按 R 重来。"); yield break; }
            }
        }

        void EndTurn(int cost)
        {
            active.At = now + AtClock.Delay(cost + (moved ? MoveCost : 0), active.Speed);
            actionDone = true; mode = Mode.Busy; pendingSkill = null; reach = null;
        }

        // 命中的种类决定气势：普攻全额、武技／绝学减半并能打断咏唱、策略每次施放另计、反击不回气势
        public enum HitKind { Attack, Technique, Strategy, Counter }

        void GainCp(Unit u, int amount) => u.Cp = Mathf.Min(100, u.Cp + amount + TroopTraits.CpBonus(u.Troop));

        // 咏唱被武技／绝学命中或被水系推迟时中断，退还一半策力（主稿 §3.3）
        void Interrupt(Unit t)
        {
            if (t.PendingCast == null || !t.Alive) return;
            t.Ep = Mathf.Min(t.MaxEp, t.Ep + t.PendingCast.Cost / 2);
            Log($"{t.Name} 的「{t.PendingCast.Name}」被打断");
            popups.Add((views[t].position + Vector3.up * (2.6f * HeadK), "打断", new Color(0.7f, 0.85f, 1f), 0f));
            t.PendingCast = null; t.PendingFollow = null;
        }

        void Hit(Unit a, Unit t, int dmg, HitKind kind = HitKind.Attack)
        {
            HitFx(a, t);
            // 中流砥柱：相邻友军所受伤害的一半由立柱者承受
            var pillar = units.FirstOrDefault(g => g.Alive && g != t && g.Team == t.Team && Grid.Dist(g.Pos, t.Pos) <= 1 && g.Statuses.Any(s => s.Name == "砥柱"));
            if (pillar != null && dmg > 1)
            {
                int share = dmg / 2; dmg -= share;
                pillar.Hp = Mathf.Max(0, pillar.Hp - Floored(pillar, share));
                popups.Add((views[pillar].position + Vector3.up * (1.8f * HeadK), share.ToString(), new Color(0.95f, 0.8f, 0.5f), 0f));
            }
            dmg = Floored(t, dmg);   // 0.3：不可击败的首领
            t.Hp = Mathf.Max(0, t.Hp - dmg);
            int gain = 5 + a.Wu / 10;
            if (kind == HitKind.Attack) GainCp(a, gain);
            else if (kind == HitKind.Technique) GainCp(a, gain / 2);
            GainCp(t, 6);
            popups.Add((views[t].position + Vector3.up * (1.8f * HeadK), dmg.ToString(), new Color(1f, 0.95f, 0.8f), 0f));
            if (!t.Alive)
            {
                t.PendingCast = null; t.PendingFollow = null;
                if (kind != HitKind.Counter) GainCp(a, 10);   // 击破 +10
                popups.Add((views[t].position + Vector3.up * (2.3f * HeadK), "撤退", new Color(1f, 0.5f, 0.4f), 0f));
                return;
            }
            if (kind == HitKind.Technique) Interrupt(t);
            // 枪兵拒马：被骑系近战命中后反击一次普攻（不推后行动条）
            float counter = kind == HitKind.Counter || kind == HitKind.Strategy ? 0f : TroopTraits.CounterPower(t.Troop, a.Troop, Grid.Dist(a.Pos, t.Pos) <= 1, t.Rank);
            if (counter > 0f && a.Alive)
            {
                popups.Add((views[t].position + Vector3.up * (2.6f * HeadK), "反击", new Color(1f, 0.7f, 0.4f), 0f));
                Hit(t, a, Combat.Damage(t, a, counter, true, Element.None, HMul(t, a), InCamp(a)), HitKind.Counter);
            }
        }

        // 弩兵不能移动后射击：只能从原地出手
        Dictionary<Vector2Int, Vector2Int> AttackReach(Unit u) => u.Troop == Troop.Crossbow ? new Dictionary<Vector2Int, Vector2Int> { [u.Pos] = u.Pos } : ReachOf(u);

        IEnumerator DoAttack(Unit u, Unit target)
        {
            var r = AttackReach(u);
            var path = Grid.Path(r, ApproachTile(u, target, u.AtkRange, r));
            if (path.Count > 1) u.Charged = true;
            yield return Walk(u, path);
            yield return Lunge(u, target.Pos);
            var victim = Cover(u, target);   // 0.3 众望
            Hit(u, victim, Combat.Damage(u, victim, 1f, true, Element.None, HMul(u, victim), InCamp(victim)));
        }

        IEnumerator DoCraft(Unit u, SkillDef s, Vector2Int targetTile)
        {
            if (s.Name == "三英") { yield return SanYing(u); yield break; }   // 0.3 合体技
            if (s.Name.StartsWith("换上·")) { yield return SwapOut(u, s.Name.Substring(3)); yield break; }   // 0.5 后备换人
            u.Cp -= s.Cost;
            popups.Add((views[u].position + Vector3.up * (2.4f * HeadK), "武技·" + s.Name, new Color(1f, 0.85f, 0.3f), 0f));
            if (s.Range < 0)
            {
                var target = At(targetTile);
                var r = ReachOf(u);
                var path = Grid.Path(r, ApproachTile(u, target, 1, r));
                if (path.Count > 1) u.Charged = true;
                yield return Walk(u, path);
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
            var covered = new HashSet<Unit>();
            foreach (var t0 in targets)
            {
                var t = t0;
                if (s.Name == "众望") { ApplyZhongwang(u, t); continue; }   // 0.3 刘备招牌技
                if (s.Name == "中流砥柱")   // 主稿 D4：2 回合内相邻友军所受伤害的一半由自己承受
                {
                    u.AddStatus(new Status { Name = "砥柱", Turns = s.Turns });
                    popups.Add((views[u].position + Vector3.up * 2f, "中流砥柱", new Color(0.9f, 0.75f, 0.4f), 0f));
                    break;
                }
                if (s.Effect == Effect.Damage || (s.Effect == Effect.Debuff && s.Power > 0)) t = Cover(u, t0, covered);
                switch (s.Effect)
                {
                    case Effect.Damage:
                    case Effect.Debuff when s.Power > 0:
                        float lineMul = s.IsStrategy ? TroopTraits.StrategyDamageMul(u.Troop) : 1f;   // 文官三线
                        Hit(u, t, Combat.Damage(u, t, s.Power * mul * lineMul, s.UsesMight, s.Element, HMul(u, t), InCamp(t), s.IsStrategy), s.IsStrategy ? HitKind.Strategy : HitKind.Technique);
                        if (s.AtDelay > 0 && t.Alive)
                        {
                            if (s.IsStrategy) Interrupt(t);   // 水系推迟也打断咏唱
                            t.At += s.AtDelay * mul;
                            popups.Add((views[t].position + Vector3.up * (2.6f * HeadK), "迟滞", new Color(0.5f, 0.75f, 1f), 0f));
                        }
                        if (s.Effect == Effect.Debuff && t.Alive) t.AddStatus(new Status { Name = "破阵", DefMul = 1f - s.Amount - TroopTraits.DebuffBonus(u.Troop), Turns = s.Turns + TroopTraits.DebuffTurns(u.Troop) });
                        break;
                    case Effect.Debuff when s.Name == "惑乱":   // 妖术师：使敌军混乱，原地不动
                        t.Chaos = true; t.ChaosForced = Mathf.Max(t.ChaosForced, 1 + TroopTraits.DebuffTurns(u.Troop));
                        popups.Add((views[t].position + Vector3.up * 2f, "混乱", new Color(1f, 0.55f, 0.5f), 0f));
                        break;
                    case Effect.Debuff:
                        t.AddStatus(new Status { Name = "威慑", AtkMul = 1f - s.Amount - TroopTraits.DebuffBonus(u.Troop), Turns = s.Turns + TroopTraits.DebuffTurns(u.Troop) });
                        popups.Add((views[t].position + Vector3.up * 2f, "物攻↓", new Color(0.8f, 0.6f, 1f), 0f));
                        break;
                    case Effect.Heal:
                        int h = Combat.Heal(t, Mathf.Min(0.6f, s.Power * mul * TroopTraits.HealMul(u.Troop)));
                        t.Hp += h;
                        popups.Add((views[t].position + Vector3.up * (1.8f * HeadK), "+" + h, new Color(0.5f, 1f, 0.5f), 0f));
                        if (s.Amount > 0) t.AddStatus(new Status { Name = "鼓舞", AtkMul = 1f + s.Amount, Turns = s.Turns + TroopTraits.BuffTurns(u.Troop) });
                        break;
                    case Effect.Guard:
                        t.AddStatus(new Status { Name = "阵", Guard = Mathf.Min(0.45f, s.Amount * mul), Turns = s.Turns + TroopTraits.BuffTurns(u.Troop) });
                        popups.Add((views[t].position + Vector3.up * 2f, "结阵", new Color(0.9f, 0.75f, 0.4f), 0f));
                        break;
                    case Effect.Buff when s.Name == "筹粮":   // 孙乾：为友军补策力
                        t.Ep = Mathf.Min(t.MaxEp, t.Ep + Mathf.RoundToInt(s.Amount));
                        popups.Add((views[t].position + Vector3.up * 2f, "策力+" + Mathf.RoundToInt(s.Amount), new Color(0.5f, 0.75f, 1f), 0f));
                        break;
                    case Effect.Buff when s.Amount > 0:      // 结盟：物防提升
                        t.AddStatus(new Status { Name = s.Name, DefMul = 1f + s.Amount, Turns = s.Turns + TroopTraits.BuffTurns(u.Troop) });
                        popups.Add((views[t].position + Vector3.up * 2f, "物防↑", new Color(0.6f, 0.9f, 1f), 0f));
                        break;
                }
            }
            yield return new WaitForSeconds(0.55f);
        }

        IEnumerator ExecuteCast(Unit u)
        {
            var s = u.PendingCast;   // 策力在开始咏唱时已扣除（被打断时退一半）
            Log($"{u.Name} 发动策略「{s.Name}」{(u.PendingMul > 1f ? $"（威力×{u.PendingMul:0.00}）" : "")}");
            popups.Add((views[u].position + Vector3.up * (2.4f * HeadK), "策略·" + s.Name, Wuxing.Tint(s.Element), 0f));
            yield return Lunge(u, u.Pos + Grid.DirTo(u.Pos, u.PendingTarget));
            yield return Apply(u, s, CastArea(u), u.PendingMul);
            GainCp(u, 4);   // 策略命中 +4（每次施放一次）
        }

        void BeginCast(Unit u, SkillDef s, float mul, Vector2Int target, int castCut = 0)
        {
            u.Ep = Mathf.Max(0, u.Ep - s.Cost);
            u.PendingCast = s; u.PendingMul = mul; u.PendingTarget = target;
            u.PendingDir = Grid.DirTo(u.Pos, target);
            var t = At(target);
            u.PendingFollow = s.Area == Area.Line || s.Radius >= 99 ? null : t;
            u.At = now + AtClock.Delay(Mathf.Max(10, s.Cast - castCut - TroopTraits.CastCut(u.Troop)), u.Speed);
            Log($"{u.Name} 开始咏唱「{s.Name}」，将在行动条上的位置发动");
        }

        // ---------------- 敌方 AI（固定规则） ----------------
        IEnumerator EnemyTurn(Unit u)
        {
            yield return new WaitForSeconds(0.2f);
            turnCount[u] = turnCount.TryGetValue(u, out int tc) ? tc + 1 : 1;
            if (u.Name == "吕布")
            {
                var foe = Foes(u).FirstOrDefault(f => Grid.Dist(f.Pos, u.Pos) == 1);
                if (foe == null) { Log("吕布横戟立马，守住关门"); u.At = now + AtClock.Delay(60, u.Speed); yield break; }
                if (turnCount[u] % 2 == 0 && u.Crafts.Count > 0)
                {
                    var craft = u.Crafts[0];
                    Log($"吕布 武技「{craft.Name}」！");
                    popups.Add((views[u].position + Vector3.up * (2.6f * HeadK), "天下无双", new Color(1f, 0.4f, 0.3f), 0f));
                    yield return Lunge(u, foe.Pos);
                    yield return Apply(u, craft, grid.LineArea(u.Pos, Grid.DirTo(u.Pos, foe.Pos), craft.Length, 1).ToList(), 1f);
                    u.At = now + AtClock.Delay(110, u.Speed);
                }
                else
                {
                    Log($"吕布 攻击 {foe.Name}");
                    yield return Lunge(u, foe.Pos);
                    var victim = Cover(u, foe);
                    Hit(u, victim, Combat.Damage(u, victim, 1f, true, Element.None, HMul(u, victim), InCamp(victim)));
                    u.At = now + AtClock.Delay(100, u.Speed);
                }
                yield break;
            }
            if (AutoCombo(u, out var combo)) { yield return combo; u.At = now + AtClock.Delay(100, u.Speed); yield break; }   // 0.3 自动代打用合体技
            if (u.Escort && u.Hp * 2 < u.MaxHp)
            {
                u.Defending = true; Log($"{u.Name} 收拢残兵固守");
                popups.Add((views[u].position + Vector3.up * 2f, "防御", Color.white, 0f));
                u.At = now + AtClock.Delay(50, u.Speed); yield break;
            }
            if (ExitTarget(u) is Vector2Int exitAt && u.Move > 0)   // 0.4：撤离区开放后，电脑代打的我方朝撤离区走（先于策略）
            {
                var er = ReachOf(u);
                var ed = er.Keys.Where(p => CanStop(u, p)).OrderBy(p => Grid.Dist(p, exitAt)).First();
                yield return Walk(u, Grid.Path(er, ed));
                u.At = now + AtClock.Delay(70, u.Speed);
                yield break;
            }
            // 妖术师（敌我皆可）：隔次行动对射程内未混乱的目标施“惑乱”
            var hex = u.Crafts.FirstOrDefault(c => c.Name == "惑乱" && u.Cp >= c.Cost);
            var hexed = hex == null || turnCount[u] % 2 != 0 ? null
                : Foes(u).Where(f => !f.Chaos && Grid.Dist(f.Pos, u.Pos) <= hex.Range).OrderByDescending(f => f.Atk).FirstOrDefault();
            if (hexed != null)
            {
                Log($"{u.Name} 绝学「惑乱」");
                yield return DoCraft(u, hex, hexed.Pos);
                u.At = now + AtClock.Delay(hex.Recover, u.Speed);
                yield break;
            }
            // 电脑代打的我方武将优先武技与普攻，策略留给文官（主稿 T2）；敌军仍按原型规则
            bool caster = u.Team != Team.Player || u.Int >= 75 || u.Troop == Troop.Strategist || u.Troop == Troop.Shaman || u.Troop == Troop.Geomancer;
            var strats = caster ? StrategiesOf(u) : new List<LitStrategy>();
            var bigLit = strats.FirstOrDefault(l => l.Def.Area == Area.Circle && l.Def.Effect == Effect.Damage && u.Ep >= l.Def.Cost);
            if (bigLit.Def != null)
            {
                var big = bigLit.Def;
                var best = grid.Diamond(u.Pos, big.Range)
                    .Select(p => (p, n: grid.Diamond(p, big.Radius).Count(q => At(q) != null && At(q).Team != u.Team)))
                    .OrderByDescending(x => x.n).First();
                if (best.n >= 2) { BeginCast(u, big, bigLit.PowerMul, best.p, bigLit.CastCut); yield break; }
            }
            var singleLit = strats.FirstOrDefault(l => l.Def.Area == Area.Single && l.Def.Effect == Effect.Damage && u.Ep >= l.Def.Cost);
            if (singleLit.Def != null)
            {
                var single = singleLit.Def;
                var t = units.Where(x => x.Alive && x.Team != u.Team && Grid.Dist(x.Pos, u.Pos) <= single.Range).OrderBy(x => x.Hp).FirstOrDefault();
                // 武将优先武技与普攻，策略只给文官或够不着时用（主稿 T2）
                bool strategist = u.Int >= 75 || u.Troop == Troop.Strategist || u.Troop == Troop.Shaman || u.Troop == Troop.Geomancer;
                bool engaged = u.Team == Team.Player && Foes(u).Any(f => Grid.Dist(f.Pos, u.Pos) <= 1);   // 代打：被贴身时不站着咏唱
                if (t != null && !engaged && (strategist || AttackableTargets(u, u.AtkRange).Count == 0) && Random.value < 0.6f) { BeginCast(u, single, singleLit.PowerMul, t.Pos, singleLit.CastCut); yield break; }
            }

            // 电脑代打的我方：气势够就放伤害武技／绝学（横扫要两人以上在身边）
            if (u.Team == Team.Player)
            {
                // 辅助绝学：射程内有友军兵力过半折损就先救（安民、犒军、整队）
                var heal = u.Crafts.FirstOrDefault(c => c.Effect == Effect.Heal && c.Area == Area.Single && u.Cp >= c.Cost);
                var hurt = heal == null ? null : Friends(u).Where(f => Grid.Dist(f.Pos, u.Pos) <= heal.Range && f.Hp * 2 < f.MaxHp).OrderBy(f => f.Hp).FirstOrDefault();
                if (hurt != null)
                {
                    Log($"{u.Name} 绝学「{heal.Name}」");
                    yield return DoCraft(u, heal, hurt.Pos);
                    u.At = now + AtClock.Delay(heal.Recover, u.Speed);
                    yield break;
                }
                var near = AttackableTargets(u, 1);
                var craft = u.Crafts.Where(c => c.Effect == Effect.Damage && u.Cp >= c.Cost && !c.IsStrategy &&
                        (c.Range < 0 ? near.Count > 0 : c.Area == Area.SelfCircle && grid.Diamond(u.Pos, c.Radius).Count(p => At(p) != null && At(p).Team != u.Team) >= 2))
                    .OrderByDescending(c => c.Power).FirstOrDefault();
                if (craft != null)
                {
                    var tp = craft.Range < 0 ? near.OrderBy(x => x.Hp).First().Pos : u.Pos;
                    Log($"{u.Name} 武技「{craft.Name}」");
                    yield return DoCraft(u, craft, tp);
                    u.At = now + AtClock.Delay(craft.Recover, u.Speed);
                    yield break;
                }
            }
            var targets = AttackableTargets(u, u.AtkRange);
            if (targets.Count > 0)
            {
                var t = targets.FirstOrDefault(x => x.Name == u.Focus) ?? targets.OrderBy(x => x.Hp).First();
                Log($"{u.Name} 攻击 {t.Name}");
                yield return DoAttack(u, t);
                u.At = now + AtClock.Delay(100, u.Speed);
                yield break;
            }
            if (u.Move == 0) { u.At = now + AtClock.Delay(60, u.Speed); yield break; }   // 守关单位不离位
            var r = ReachOf(u);
            var focus = Foes(u).FirstOrDefault(f => f.Name == u.Focus);
            var dest = r.Keys.Where(p => CanStop(u, p)).OrderBy(p => focus != null ? Grid.Dist(p, focus.Pos) : Foes(u).Min(f => Grid.Dist(p, f.Pos))).First();
            yield return Walk(u, Grid.Path(r, dest));
            u.At = now + AtClock.Delay(70, u.Speed);
        }

        // ---------------- 玩家输入 ----------------
        void ChooseRing(int i)
        {
            switch (RingItems[i])
            {
                case "移动": reach = ReachOf(active); cursor = active.Pos; mode = Mode.TargetMove; Log("选择移动目的地（右键／B 返回）"); break;
                case "攻击": reach = ReachOf(active); cursor = AttackableTargets(active, active.AtkRange).OrderBy(t => Grid.Dist(t.Pos, active.Pos)).Select(t => t.Pos).DefaultIfEmpty(active.Pos).First(); mode = Mode.TargetAttack; Log(moved ? "选择攻击目标：已移动，只能在原地出手" : "选择攻击目标：会先走到射程内再攻击"); break;
                case "武技": showCraftList = true; listSel = 0; mode = Mode.SkillList; break;
                case "策略": showCraftList = false; listSel = 0; mode = Mode.SkillList; break;
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
                    if (deploySel != null && InDeploy(p) && !Grid.Impassable(grid.TerrainAt(p)) && u == null)
                    {
                        deploySel.Pos = p; views[deploySel].position = Tile(p);
                    }
                    return;
                case Mode.TargetMove:
                    if (reach.ContainsKey(p) && p != active.Pos && CanStop(active, p)) StartCoroutine(MoveThenAct(Grid.Path(reach, p)));
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
                    if (pendingSkill.IsStrategy) { BeginCast(active, pendingSkill, pendingMul, p, StrategiesOf(active).FirstOrDefault(l => l.Def == pendingSkill).CastCut); actionDone = true; mode = Mode.Busy; pendingSkill = null; }
                    else StartCoroutine(Run(DoCraft(active, pendingSkill, p), pendingSkill.Recover, $"{active.Name} 武技「{pendingSkill.Name}」"));
                    return;
            }
        }

        // 移动不结束回合（用户 2026-10-23）：走完回到环形菜单，还能攻击、用武技、防御或待机；策略须原地咏唱，移动后不能再用
        const int MoveCost = 30;
        bool moved;

        Vector2Int preMovePos; Vector3 preMoveDir;

        // 移动后按取消：退回移动前的位置重新输入（用户 2026-10-23）
        void UndoMove()
        {
            active.Pos = preMovePos; rigs[active].Root.position = Tile(preMovePos); rigs[active].Dir = preMoveDir;
            moved = false; active.Charged = false; reach = null; ringSel = DefaultRing(); cursor = active.Pos; mode = Mode.Menu;
            Log($"{active.Name} 退回原位，重新下令");
        }

        IEnumerator MoveThenAct(List<Vector2Int> path)
        {
            preMovePos = active.Pos; preMoveDir = rigs[active].Dir;
            mode = Mode.Busy; Log($"{active.Name} 移动");
            yield return Walk(active, path);
            moved = true; active.Charged = true; reach = null; ringSel = DefaultRing(); mode = Mode.Menu;
            Log($"{active.Name} 已移动：可攻击、用武技、防御或待机（策略须原地咏唱）");
        }

        IEnumerator Run(IEnumerator action, int cost, string msg)
        {
            mode = Mode.Busy; Log(msg);
            yield return action;
            EndTurn(cost);
        }

        void Back()
        {
            if (mode == Mode.Menu && moved) UndoMove();
            else if (mode == Mode.TargetMove || mode == Mode.TargetAttack || mode == Mode.SkillList) { mode = Mode.Menu; reach = null; }
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
            // 在当前 GUI 矩阵（界面缩放）之内绕 a 旋转；RotateAroundPivot 按屏幕坐标取轴心，缩放不为 1 时会偏
            GUI.matrix = m * Matrix4x4.TRS(a, Quaternion.Euler(0, 0, ang), Vector3.one) * Matrix4x4.TRS(-(Vector3)a, Quaternion.identity, Vector3.one);
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
            DrawAtBarV();
            if (mode != Mode.Duel && mode != Mode.Circuit) DrawPartyV(sw);
            DrawTopBarV(sw);
            DrawMinimap(sw);

            if (mode == Mode.Deploy) DrawDeploy(sw);
            if (mode == Mode.Circuit) DrawCircuit(sw);
            if (mode == Mode.Menu) DrawRingV();
            if (mode == Mode.Duel) DrawDuel(sw);
            if (mode == Mode.SkillList) DrawSidePanel(true);
            if (mode == Mode.TargetSkill || mode == Mode.TargetAttack || mode == Mode.TargetMove) DrawHoverInfo(sw);
            if (mode == Mode.Idle || mode == Mode.Busy || mode == Mode.Deploy) DrawHoverInfo(sw);
            if (IsChapter) DrawChapterOverlay(sw);
            if (mode == Mode.Victory || mode == Mode.Defeat)
                GUI.Label(new Rect(0, 280, sw, 60), mode == Mode.Victory ? "胜 利" : "败 退", new GUIStyle(big) { alignment = TextAnchor.MiddleCenter, fontSize = 48 });

            // 世界点击与按键放在最后，避免和按钮抢点击
            if (e.type == EventType.MouseDown && mode != Mode.Circuit)
            {
                if (e.button == 0 && grid.In(hover)) { ClickTile(hover); e.Use(); }
                else if (e.button == 1) { Back(); e.Use(); }
            }
        }

        void DrawWorldLabels()
        {
            if (padMode && grid.In(cursor) && mode != Mode.Circuit && mode != Mode.Busy)
            {
                // 键盘／手柄光标：格子上方跳动的箭头
                var cp = ToGui(Tile(cursor) + Vector3.up * (At(cursor) != null ? 2.1f : 0.6f + 0.15f * Mathf.Sin(Time.time * 6f)));
                var st = new GUIStyle(center) { fontSize = 26 }; st.normal.textColor = new Color(1f, 0.85f, 0.25f);
                var sh = new GUIStyle(st); sh.normal.textColor = new Color(0, 0, 0, 0.8f);
                GUI.Label(new Rect(cp.x - 19, cp.y - 15, 40, 32), "▼", sh);
                GUI.Label(new Rect(cp.x - 20, cp.y - 16, 40, 32), "▼", st);
            }
            if (IsChapter) DrawChapterLabels();
            if (stage == 1)
            {
                var gp = ToGui(Tile(new Vector2Int(7, 12)) + Vector3.up * 1.1f);
                Rect(new Rect(gp.x - 34, gp.y - 13, 68, 26), new Color(0.12f, 0.06f, 0.04f, 0.9f));
                GUI.Label(new Rect(gp.x - 34, gp.y - 13, 68, 26), "<color=#e8c56a>虎牢关</color>", new GUIStyle(center) { fontSize = 16 });
                var lb = units.FirstOrDefault(x => x.Name == "吕布" && x.Alive);
                if (lb != null)
                {
                    var g2 = ToGui(views[lb].position + Vector3.up * (2.25f * HeadK));
                    GUI.Label(new Rect(g2.x - 40, g2.y - 10, 80, 20), "<color=#ff8a70><b>吕布</b></color>", new GUIStyle(center) { fontSize = 15 });
                }
            }
            foreach (var u in units.Where(u => u.Alive))
            {
                var g = ToGui(views[u].position + Vector3.up * (1.75f * HeadK));
                Bar(new Rect(g.x - 24, g.y, 48, 6), u.Hp / (float)u.MaxHp, u.Team == Team.Player ? new Color(0.4f, 0.85f, 0.4f) : new Color(0.95f, 0.35f, 0.3f));
                if (u.Chaos)
                {
                    var cr = new Rect(g.x - 22, g.y - 24, 44, 20);
                    Rect(cr, new Color(0.85f, 0.42f, 0.44f, 0.9f));
                    GUI.Label(cr, u.ChaosForced > 0 ? $"混乱{u.ChaosForced}" : "混乱", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold });
                }
                else if (u.PendingCast != null)
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

        Texture2D Bust(Unit u) => u.Key switch { "LiuBei" => LiuBeiBust, "GuanYu" => GuanYuBust, "ZhangFei" => ZhangFeiBust, "LuBu" => LuBuBust, _ => null };

        // 选项不成立就置灰、不能选：没有可攻击的敌人、气势或策力不够、已移动过（策略须原地咏唱）
        bool RingUsable(string item) => item switch
        {
            "攻击" => AttackableTargets(active, active.AtkRange).Count > 0,
            "武技" => active.Crafts.Any(c => active.Cp >= c.Cost && HasTargets(active, c)),
            "策略" => !moved && StrategiesOf(active).Any(l => active.Ep >= l.Def.Cost && HasTargets(active, l.Def)),
            "移动" => !moved,
            _ => true
        };

        // 范围内是否有可作用的对象：伤害、减益要有敌人，回复、结阵要有友军
        bool HasTargets(Unit u, SkillDef s)
        {
            if (s.Name == "三英") return ComboReady(u);   // 0.3
            if (s.Name.StartsWith("换上·")) return BenchReady(u, s.Name.Substring(3));   // 0.5
            bool Ok(Unit t) => t != null && t.Alive && (s.TargetsAllies ? t.Team == u.Team : t.Team != u.Team) && !(s.Name == "众望" && t == u);
            if (s.Range < 0) return !s.TargetsAllies && AttackableTargets(u, 1).Count > 0;
            if (s.Radius >= 99) return Friends(u).Any();
            if (s.Area == Area.SelfCircle) return grid.Diamond(u.Pos, s.Radius).Any(p => Ok(At(p)));
            foreach (var p in SkillRange(u, s))
            {
                if (s.Area == Area.Single) { if (Ok(At(p))) return true; continue; }
                if (grid.AreaOf(s, u.Pos, p).Any(q => Ok(At(q)))) return true;
            }
            return false;
        }

        // 默认选项（用户 2026-10-23）：轮到行动时，攻击范围内有敌人选“攻击”，否则选“移动”；移动后有敌人选“攻击”，否则选“待机”
        int DefaultRing()
        {
            int attack = System.Array.IndexOf(RingItems, "攻击");
            if (RingUsable("攻击")) return attack;
            return System.Array.IndexOf(RingItems, moved ? "待机" : "移动");
        }

        // 从 from 朝 step 方向找下一个可用选项
        int NextUsable(int from, int step)
        {
            for (int k = 1; k <= RingItems.Length; k++)
            {
                int i = ((from + step * k) % RingItems.Length + RingItems.Length) % RingItems.Length;
                if (RingUsable(RingItems[i])) return i;
            }
            return from;
        }

        int listSel;

        List<(SkillDef s, float mul, string extra, bool ok)> SkillRows()
        {
            var rows = new List<(SkillDef s, float mul, string extra, bool ok)>();
            if (showCraftList)
                foreach (var s in active.Crafts) rows.Add((s, 1f, $"气势{s.Cost}" + (HasTargets(active, s) ? "" : " · 范围内无目标"), active.Cp >= s.Cost && HasTargets(active, s)));
            else
                foreach (var l in StrategiesOf(active))
                    rows.Add((l.Def, l.PowerMul, $"策力{l.Def.Cost} 咏唱{l.Def.Cast}" + (l.PowerMul > 1f ? $" 威力×{l.PowerMul:0.00}" : "") + (HasTargets(active, l.Def) ? "" : " · 范围内无目标"),
                        active.Ep >= l.Def.Cost && HasTargets(active, l.Def)));
            return rows;
        }

        void ChooseSkill((SkillDef s, float mul, string extra, bool ok) row)
        {
            if (!row.ok) return;
            var s = row.s;
            pendingSkill = s; pendingMul = row.mul; mode = Mode.TargetSkill;
            if (s.Range < 0) reach = ReachOf(active);
            // 光标落在最合适的位置：自身范围技落在自己，单体落在最近的合法目标
            cursor = s.Area == Area.SelfCircle || s.Radius >= 99 ? active.Pos
                : SkillRange(active, s).Concat(s.Range < 0 ? AttackableTargets(active, 1).Select(t => t.Pos) : Enumerable.Empty<Vector2Int>())
                    .Where(p => s.Range < 0 ? At(p) != null && At(p).Team != active.Team : ValidSkillTarget(active, s, p))
                    .OrderBy(p => Grid.Dist(p, active.Pos)).DefaultIfEmpty(active.Pos).First();
            Log(s.Area == Area.SelfCircle || s.Radius >= 99 ? $"「{s.Name}」：确认自身所在格" :
                s.Area == Area.Line ? $"「{s.Name}」：选相邻格决定方向" :
                s.IsStrategy ? $"「{s.Name}」：选择施放位置（橙色为范围）" : $"「{s.Name}」：选择目标");
        }

        void DrawHoverInfo(float sw)
        {
            if (grid.In(hover))
            {
                var tr = new Rect(sw - 250, 400, 240, 44);
                Rect(tr, new Color(0.04f, 0.06f, 0.1f, 0.85f));
                var tt = grid.TerrainAt(hover);
                string fx = tt switch
                {
                    Terrain.Sand => "移动耗2（骑兵3）", Terrain.Ford => "移动耗2", Terrain.Camp => "驻守受伤−15%",
                    Terrain.River or Terrain.Wall or Terrain.Gate or Terrain.JuMa or Terrain.Rock => "不可通行", _ => "移动耗1"
                };
                GUI.Label(new Rect(tr.x + 10, tr.y + 4, 230, 20), $"{Grid.Name(tt)} · 高{grid.HeightAt(hover)}层", text);
                GUI.Label(new Rect(tr.x + 10, tr.y + 24, 230, 18), fx + "；登高每层+1，居高每层+10%", small);
            }
            var u = At(hover);
            if (u == null) return;
            var r = new Rect(sw - 250, 244, 240, 150);
            Rect(r, new Color(0.04f, 0.06f, 0.1f, 0.88f));
            string troop = TroopTraits.Name(u.Troop, u.Rank);
            GUI.Label(new Rect(r.x + 10, r.y + 6, 220, 22), $"<b>{u.Name}</b>  {troop}", text);
            GUI.Label(new Rect(r.x + 10, r.y + 30, 220, 20), $"兵力 {u.Hp}/{u.MaxHp}  速度 {u.Speed}  移动 {u.Move}  智力 {u.Int}", small);
            GUI.Label(new Rect(r.x + 10, r.y + 50, 220, 20), $"物攻 {u.Atk} 物防 {u.Def} 计攻 {u.Mat} 计防 {u.Mdef}", small);
            GUI.Label(new Rect(r.x + 10, r.y + 70, 220, 20), $"五行 <color=#{ColorUtility.ToHtmlStringRGB(Wuxing.Tint(u.Element))}>{Wuxing.Name(u.Element)}</color>（被{Wuxing.Name(Wuxing.All.First(x => Wuxing.Overcomes(x) == u.Element))}克）", small);
            GUI.Label(new Rect(r.x + 10, r.y + 90, 220, 20), u.Statuses.Count > 0 ? "状态：" + string.Join("、", u.Statuses.Select(s => $"{s.Name}{s.Turns}")) : "状态：无", small);
            if (u.PendingCast != null) GUI.Label(new Rect(r.x + 10, r.y + 110, 220, 20), $"咏唱中：{u.PendingCast.Name}", small);
            // 主稿 D6：对敌卡标出兵种项与可克它的招式（“克”字提示）
            else if (active != null && active.Team != u.Team)
            {
                int troopPts = TroopTraits.Points(active.Troop, u.Troop, true, active.Charged, active.Rank);
                var counters = StrategiesOf(active).Select(l => l.Def).Concat(active.Crafts)
                    .Where(s => s.Effect == Effect.Damage && s.Element != Element.None && Wuxing.Overcomes(s.Element) == u.Element).Select(s => s.Name).Distinct().ToList();
                string troopText = troopPts == 0 ? "兵种 ±0" : $"兵种 {(troopPts > 0 ? "+" : "")}{troopPts}%";
                GUI.Label(new Rect(r.x + 10, r.y + 110, 230, 20), troopText + (counters.Count > 0 ? $"  <color=#ffcf5a>克</color>：{string.Join("、", counters)}（五行+20%）" : ""), small);
            }
        }

        void DrawDeploy(float sw)
        {
            var r = new Rect(sw / 2 - 260, 40, 520, 64);
            Rect(r, new Color(0.04f, 0.06f, 0.1f, 0.85f));
            GUI.Label(new Rect(r.x + 12, r.y + 6, 500, 22), $"{(IsChapter ? Chapter.Title : stage == 1 ? "虎牢关·三英战吕布" : "黄巾旷野")} · 布阵 · 当前选中：{(deploySel != null ? deploySel.Name : "（点选我方武将）")}", text);
            if (GUI.Button(new Rect(r.x + 12, r.y + 32, 150, 26), "兵书回路（Tab/Y）")) { mode = Mode.Circuit; circuitSlot = -1; circuitFocus = 0; }
            if (!IsChapter && GUI.Button(new Rect(r.x + 185, r.y + 32, 150, 26), stage == 1 ? "换关：黄巾旷野（T）" : "换关：虎牢关（T）"))
            {
                stage = 1 - stage;
                UnityEngine.SceneManagement.SceneManager.LoadScene(gameObject.scene.buildIndex);
            }
            if (GUI.Button(new Rect(r.x + 358, r.y + 32, 150, 26), "出阵！（F/Start）")) { mode = Mode.Busy; StartCoroutine(BattleLoop()); }
        }

        // ---------------- 兵书回路界面 ----------------
        Unit Owner(Book b) => units.Concat(bench).FirstOrDefault(u => u.Team == Team.Player && u.Slots.Contains(b));

        void SetSchool(Unit u, School s)
        {
            u.School = s; u.Element = Circuits.Of(s).Affinity;
            var c = u.Circuit;
            for (int i = 0; i < u.Slots.Length; i++)
                if (u.Slots[i] != null && (i >= c.Slots.Length || !CircuitMath.Fits(c, i, u.Slots[i]))) u.Slots[i] = null;
        }
    }
}
