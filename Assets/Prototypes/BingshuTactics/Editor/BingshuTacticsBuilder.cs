// PROTOTYPE - NOT FOR PRODUCTION
// Question: 空轨式 AT 行动条＋小范围格子战斗，配合“学派回路镶嵌兵书→策略”，是否好玩、是否看得懂？
// Date: 2026-10-22
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Prototype.BingshuTactics.EditorTools
{
    public static class BingshuTacticsBuilder
    {
        const string Dir = "Assets/Prototypes/BingshuTactics";
        const string ScenePath = Dir + "/BingshuTactics.unity";
        const string Chars = "Assets/_Project/Art/Sprites/Characters";
        const string Busts = "Assets/_Project/Art/Portraits";
        const string DefaultOutput = @"D:\BuildArtifacts\Codex\hd2d-jrpg\builds\bingshu-tactics-poc\BingshuTacticsPOC.exe";

        [MenuItem("Prototype/兵书战棋原型/生成场景")]
        public static void BuildScene()
        {
            CreateScene();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("BINGSHU_TACTICS_SCENE_OK");
        }

        // 新建一个只有相机和 TacticsGame 的场景并填好美术引用（不保存）；0.2 主线的三场战斗也用它建场景
        public static TacticsGame CreateScene()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var ground = SaveMat("Ground", new Material(lit) { color = Color.white });
            var rock = SaveMat("Rock", new Material(lit) { color = new Color(0.45f, 0.42f, 0.38f) });
            var overlay = SaveMat("Overlay", new Material(Shader.Find("Sprites/Default")));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            cam.gameObject.AddComponent<AudioListener>();
            var game = new GameObject("TacticsGame").AddComponent<TacticsGame>();
            game.LiuBeiMat = Load<Material>($"{Chars}/LiuBei/LiuBei_Pixel.mat");
            game.GuanYuMat = Load<Material>($"{Chars}/GuanYu/GuanYu_Pixel.mat");
            game.ZhangFeiMat = Load<Material>($"{Chars}/ZhangFei/ZhangFei_Pixel.mat");
            game.RebelMat = Load<Material>($"{Chars}/YellowTurban/YellowTurban_Pixel.mat");
            game.ZhangJiaoMat = Load<Material>($"{Chars}/ZhangJiao/ZhangJiao_Pixel.mat");
            // 虎牢关占位：吕布暂借城门卫兵像素图（放大、偏红），西凉铁骑借巡逻兵，关上弓手借弓手兵
            game.LuBuMat = Load<Material>($"{Chars}/NpcGateGuard/NpcGateGuard_Pixel.mat");
            game.XiliangMat = Load<Material>($"{Chars}/NpcPatrolSoldier/NpcPatrolSoldier_Pixel.mat");
            game.WallArcherMat = Load<Material>($"{Chars}/NpcArcherSoldier/NpcArcherSoldier_Pixel.mat");
            // Codex 八向像素图（battle-poc-8dir-v3，V3 低像素风格）：有就换上，没有就沿用旧四方向图
            var liu8 = ImportArt("char_liubei_idlewalk_8dir_96.png", game.LiuBeiMat); if (liu8) game.LiuBeiMat = liu8;
            var lubu8 = ImportArt("char_lubu_idlewalk_8dir_96.png", game.LiuBeiMat); if (lubu8) game.LuBuMat = lubu8;
            var guan8 = ImportArt("char_guanyu_idlewalk_8dir_96.png", game.LiuBeiMat); if (guan8) game.GuanYuMat = guan8;
            var zhang8 = ImportArt("char_zhangfei_idlewalk_8dir_96.png", game.LiuBeiMat); if (zhang8) game.ZhangFeiMat = zhang8;
            // 骑系人物在战场上骑马（数值主稿 v1.3 §3.14，Codex art/mounted-tactics-8dir，128 格八向）：关羽枣红、张飞乌云踏雪、赵云白马
            var guanM = ImportArt("char_guanyu_mounted_8dir_128.png", game.LiuBeiMat); if (guanM) game.GuanYuMat = guanM;
            var zhangM = ImportArt("char_zhangfei_mounted_8dir_128.png", game.LiuBeiMat); if (zhangM) game.ZhangFeiMat = zhangM;
            var zhaoM = ImportArt("char_zhaoyun_mounted_8dir_128.png", game.LiuBeiMat, "ZhaoYun_Mounted"); if (zhaoM) game.CharMats.Add(zhaoM);
            game.LuBuBust = ImportTexture("char_lubu_bust.png", false);
            game.UiTextures = ImportUiSlices();
            game.DestinyTextures = ImportDestinySlices();
            game.HD2DProfile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>("Assets/_Project/Settings/HD2D_Default.asset");
            Debug.Log("BINGSHU_HD2D_PROFILE " + (game.HD2DProfile ? game.HD2DProfile.name : "missing"));
            game.EnvTextures = ImportEnvTextures();
            // 单挑立绘与动作帧
            game.LiuBeiPortrait = Load<Texture2D>($"{Busts}/Full/LIU_BEI_MAIN_POSE.png");
            game.GuanYuPortrait = Load<Texture2D>($"{Busts}/Full/GUAN_YU_MAIN_POSE.png");
            game.ZhangFeiPortrait = Load<Texture2D>($"{Busts}/Full/ZHANG_FEI_MAIN_POSE.png");
            game.LuBuPortrait = ImportTexture("char_lubu_mainpose.png", false);
            game.LiuBeiDuel = ImportDuel("char_liubei_duel_96.png");
            game.GuanYuDuel = ImportDuel("char_guanyu_duel_96.png");
            game.ZhangFeiDuel = ImportDuel("char_zhangfei_duel_96.png");
            game.LuBuDuel = ImportDuel("char_lubu_duel_96.png");
            game.LiuBeiBust = Load<Texture2D>($"{Busts}/LiuBei_Bust.png");
            game.GuanYuBust = Load<Texture2D>($"{Busts}/GuanYu_Bust.png");
            game.ZhangFeiBust = Load<Texture2D>($"{Busts}/ZhangFei_Bust.png");
            game.GroundMat = ground; game.RockMat = rock; game.OverlayMat = overlay;
            game.Palette = LoadPalette(PalettePath);
            return game;
        }

        [MenuItem("Prototype/兵书战棋原型/构建 Windows")]
        public static void BuildWindows()
        {
            BuildScene();
            string output = System.Environment.GetEnvironmentVariable("JRPG_BUILD_OUTPUT");
            if (string.IsNullOrWhiteSpace(output)) output = DefaultOutput;
            if (!Path.GetFullPath(output).StartsWith(@"D:\BuildArtifacts\Codex\hd2d-jrpg\builds\", System.StringComparison.OrdinalIgnoreCase))
                throw new System.Exception("Build output must be under D:\\BuildArtifacts\\Codex\\hd2d-jrpg\\builds\\");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            string product = PlayerSettings.productName;
            PlayerSettings.productName = "BingshuTacticsPOC";
            try
            {
                var report = BuildPipeline.BuildPlayer(new[] { ScenePath }, output, BuildTarget.StandaloneWindows64, BuildOptions.None);
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                    throw new System.Exception("Build failed: " + report.summary.result);
            }
            finally { PlayerSettings.productName = product; }
            Debug.Log("BINGSHU_TACTICS_BUILD_OK " + output);
        }

        // 城外战斗背景 64 色调色板（前 48 色为街角原表）；按首次出现顺序取不重复颜色
        const string PalettePath = @"D:\BuildArtifacts\Codex\hd2d-jrpg\art\battle-outskirts\palette_outskirts.png";

        static Color32[] LoadPalette(string path)
        {
            var tex = new Texture2D(2, 2);
            if (!File.Exists(path) || !tex.LoadImage(File.ReadAllBytes(path))) throw new FileNotFoundException("Missing palette " + path);
            var seen = new System.Collections.Generic.List<Color32>();
            foreach (var c in tex.GetPixels32())
            {
                if (c.a < 128) continue;
                var k = new Color32(c.r, c.g, c.b, 255);
                if (!seen.Exists(x => x.r == k.r && x.g == k.g && x.b == k.b)) seen.Add(k);
            }
            Object.DestroyImmediate(tex);
            Debug.Log("BINGSHU_PALETTE " + seen.Count);
            return seen.ToArray();
        }

        const string ArtSource = @"D:\BuildArtifacts\Codex\hd2d-jrpg\art\battle-poc-8dir-v3";
        const string ArtDir = Dir + "/Art";

        static Texture2D ImportTexture(string file, bool pixel)
        {
            // 可用环境变量 BINGSHU_ART_SOURCE 指向未批准的暂存修订，只用于本地预看
            var srcDir = System.Environment.GetEnvironmentVariable("BINGSHU_ART_SOURCE");
            var src = Path.Combine(string.IsNullOrEmpty(srcDir) ? ArtSource : srcDir, file);
            // 用户 2026-10-23 要求刘备缩小头部，重画稿在单独目录，优先取
            var rev = Path.Combine(ArtSource + "-liubei-r2", file);
            if (string.IsNullOrEmpty(srcDir) && File.Exists(rev)) src = rev;
            var mounted = Path.Combine(ArtSource.Replace("battle-poc-8dir-v3", "mounted-tactics-8dir"), file);
            if (string.IsNullOrEmpty(srcDir) && File.Exists(mounted)) src = mounted;
            if (!File.Exists(src)) return null;
            Directory.CreateDirectory(ArtDir);
            var dst = $"{ArtDir}/{file}";
            File.Copy(src, dst, true);
            AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(dst);
            imp.textureType = TextureImporterType.Default;
            imp.mipmapEnabled = false; imp.alphaIsTransparency = true; imp.npotScale = TextureImporterNPOTScale.None;
            imp.filterMode = pixel ? FilterMode.Point : FilterMode.Bilinear;
            imp.textureCompression = pixel ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(dst);
        }

        // 单挑动作帧（Codex：art/battle-poc-duel-v3），没有就返回 null
        static Texture2D ImportDuel(string file)
        {
            var src = Path.Combine(ArtSource.Replace("battle-poc-8dir-v3", "battle-poc-duel-v3"), file);
            if (!File.Exists(src)) return null;
            Directory.CreateDirectory(ArtDir);
            var dst = $"{ArtDir}/{file}";
            File.Copy(src, dst, true);
            AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(dst);
            imp.mipmapEnabled = false; imp.alphaIsTransparency = true; imp.npotScale = TextureImporterNPOTScale.None;
            imp.filterMode = FilterMode.Point; imp.textureCompression = TextureImporterCompression.Uncompressed; imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();
            Debug.Log("BINGSHU_DUEL_IMPORTED " + file);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(dst);
        }

        // 战斗界面切图（Codex：art/battle-poc-ui-slices-blue），全部按双线性、无压缩导入
        const string UiSource = @"D:\BuildArtifacts\Codex\hd2d-jrpg\art\battle-poc-ui-slices-blue";

        // 虎牢关战场贴图包（Codex：art/battle-poc-hulao-texpack）：点采样、可读（河水帧要拆）、平铺
        const string EnvSource = @"D:\BuildArtifacts\Codex\hd2d-jrpg\art\battle-poc-hulao-texpack";
        const string EnvRefineSource = @"D:\BuildArtifacts\Codex\hd2d-jrpg\art\battle-poc-hulao-refine";

        const string EnvHD2DSource = @"D:\BuildArtifacts\Codex\hd2d-jrpg\art\battle-poc-hulao-hd2d";   // 虎牢关涿郡同款补件
        const string ZhuojunRoot = @"D:\BuildArtifacts\Codex\hd2d-jrpg\art";
        static readonly string[] ZhuojunDirs = { "zhuojun-map-b5", "zhuojun-map-b4", "zhuojun-map-b3", "zhuojun-map-b2", "zhuojun-town", "zhuojun-corner", "zhuojun-map-b1" };
        static readonly string[] ZhuojunFiles =
        {
            "ground_grass", "ground_earth_ruts", "ground_packed_earth", "ground_earth_gravel", "citywall_face", "citywall_parapet", "citywall_walkway",
            "prop_army_tent", "prop_brazier", "prop_bow_rack", "prop_ox_cart", "prop_grain_sacks", "prop_hay_pile", "prop_sacks_pile",
            "roof_tiles_grey", "prop_tree_cypress", "prop_tree_elm", "prop_tree_willow", "prop_bush_b", "prop_grass_tuft",
        };

        static Texture2D[] ImportEnvTextures()
        {
            if (!Directory.Exists(EnvSource)) return new Texture2D[0];
            var dir = Dir + "/Art/Env";
            Directory.CreateDirectory(dir);
            var list = new System.Collections.Generic.List<Texture2D>();
            // 精修补件（art/battle-poc-hulao-refine）与贴图包同一规格，一起导入
            var sources = System.Linq.Enumerable.ToList(Directory.GetFiles(EnvSource, "*.png"));
            if (Directory.Exists(EnvRefineSource)) sources.AddRange(Directory.GetFiles(EnvRefineSource, "*.png"));
            if (Directory.Exists(EnvHD2DSource)) sources.AddRange(Directory.GetFiles(EnvHD2DSource, "*.png"));
            // 涿郡原生贴图（同为每米 30 像素）：按批次新旧取最新一版；夯土地面用用户认可的原版噪点款（zhuojun-corner）
            foreach (var name in ZhuojunFiles)
            {
                var src = ZhuojunDirs.Select(d => Path.Combine(ZhuojunRoot, d, name + ".png")).FirstOrDefault(File.Exists);
                if (src != null) sources.Add(src);
            }
            foreach (var src in sources)
            {
                var file = Path.GetFileName(src);
                if (file.StartsWith("contact") || file.StartsWith("tile_preview") || file.StartsWith("preview")) continue;
                var dst = $"{dir}/{file}";
                File.Copy(src, dst, true);
                AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceUpdate);
                var imp = (TextureImporter)AssetImporter.GetAtPath(dst);
                imp.textureType = TextureImporterType.Default; imp.mipmapEnabled = false; imp.alphaIsTransparency = true; imp.isReadable = true;
                imp.npotScale = TextureImporterNPOTScale.None; imp.filterMode = FilterMode.Point;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.wrapMode = file.StartsWith("sprite_") || file.StartsWith("fx_") || file.StartsWith("banner_") || file.StartsWith("prop_") || file.StartsWith("gate_tower") || file.StartsWith("gate_passage") ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                imp.SaveAndReimport();
                list.Add(AssetDatabase.LoadAssetAtPath<Texture2D>(dst));
            }
            Debug.Log("BINGSHU_ENV_TEXTURES " + list.Count);
            return list.ToArray();
        }

        // 兵书回路界面切图（Codex：art/battle-poc-destiny-ui-slices）：高清 UI，双线性＋多级纹理，缩小时不起锯齿
        const string DestinySource = @"D:\BuildArtifacts\Codex\hd2d-jrpg\art\battle-poc-destiny-ui-slices";

        static Texture2D[] ImportDestinySlices()
        {
            if (!Directory.Exists(DestinySource)) return new Texture2D[0];
            var dir = Dir + "/Art/Destiny";
            Directory.CreateDirectory(dir);
            var list = new System.Collections.Generic.List<Texture2D>();
            foreach (var src in Directory.GetFiles(DestinySource, "*.png"))
            {
                var file = Path.GetFileName(src);
                if (file.StartsWith("contact") || file.StartsWith("mockup") || file.StartsWith("preview")) continue;
                var dst = $"{dir}/{file}";
                File.Copy(src, dst, true);
                AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceUpdate);
                var imp = (TextureImporter)AssetImporter.GetAtPath(dst);
                imp.textureType = TextureImporterType.Default; imp.mipmapEnabled = true; imp.alphaIsTransparency = true;
                imp.npotScale = TextureImporterNPOTScale.None; imp.filterMode = FilterMode.Trilinear;
                imp.textureCompression = TextureImporterCompression.Uncompressed; imp.wrapMode = TextureWrapMode.Clamp;
                imp.SaveAndReimport();
                list.Add(AssetDatabase.LoadAssetAtPath<Texture2D>(dst));
            }
            Debug.Log("BINGSHU_DESTINY_SLICES " + list.Count);
            return list.ToArray();
        }

        static Texture2D[] ImportUiSlices()
        {
            if (!Directory.Exists(UiSource)) return new Texture2D[0];
            var dir = Dir + "/Art/UI";
            Directory.CreateDirectory(dir);
            var list = new System.Collections.Generic.List<Texture2D>();
            foreach (var src in Directory.GetFiles(UiSource, "*.png"))
            {
                var file = Path.GetFileName(src);
                if (file.StartsWith("contact") || file.StartsWith("mockup")) continue;
                var dst = $"{dir}/{file}";
                File.Copy(src, dst, true);
                AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceUpdate);
                var imp = (TextureImporter)AssetImporter.GetAtPath(dst);
                imp.textureType = TextureImporterType.Default; imp.mipmapEnabled = false; imp.alphaIsTransparency = true;
                imp.npotScale = TextureImporterNPOTScale.None; imp.filterMode = FilterMode.Bilinear;
                imp.textureCompression = TextureImporterCompression.Uncompressed; imp.wrapMode = TextureWrapMode.Clamp;
                imp.SaveAndReimport();
                list.Add(AssetDatabase.LoadAssetAtPath<Texture2D>(dst));
            }
            Debug.Log("BINGSHU_UI_SLICES " + list.Count);
            return list.ToArray();
        }

        // 用旧像素材质（Lit、alpha clip）做模板，换上新图
        static Material ImportArt(string file, Material template, string name = null)
        {
            var tex = ImportTexture(file, true);
            if (!tex) return null;
            var m = new Material(template) { name = name ?? Path.GetFileNameWithoutExtension(file) };
            m.SetTexture("_BaseMap", tex); m.mainTexture = tex;
            m.SetVector("_BaseMap_ST", new Vector4(1, 1, 0, 0));
            Debug.Log("BINGSHU_ART_IMPORTED " + file + " " + tex.width + "x" + tex.height);
            return SaveMat(m.name, m);
        }

        static T Load<T>(string path) where T : Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (!a) throw new FileNotFoundException("Missing asset " + path);
            return a;
        }

        static Material SaveMat(string name, Material m)
        {
            string path = $"{Dir}/Materials/{name}.mat";
            Directory.CreateDirectory($"{Dir}/Materials");
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing) { EditorUtility.CopySerialized(m, existing); return existing; }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }
}
