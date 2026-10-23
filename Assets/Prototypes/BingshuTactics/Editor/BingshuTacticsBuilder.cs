// PROTOTYPE - NOT FOR PRODUCTION
// Question: 空轨式 AT 行动条＋小范围格子战斗，配合“学派回路镶嵌兵书→策略”，是否好玩、是否看得懂？
// Date: 2026-10-22
using System.IO;
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
            game.LiuBeiBust = Load<Texture2D>($"{Busts}/LiuBei_Bust.png");
            game.GuanYuBust = Load<Texture2D>($"{Busts}/GuanYu_Bust.png");
            game.ZhangFeiBust = Load<Texture2D>($"{Busts}/ZhangFei_Bust.png");
            game.GroundMat = ground; game.RockMat = rock; game.OverlayMat = overlay;
            game.Palette = LoadPalette(PalettePath);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("BINGSHU_TACTICS_SCENE_OK");
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
