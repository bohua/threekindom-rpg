using System.IO;
using System.Linq;
using ThreeKingdoms.Gameplay;
using ThreeKingdoms.Gameplay.Battle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ThreeKingdoms.EditorTools
{
    // The 0.2 battle backdrops (V02_HUANGJIN_PLAN §4, Codex art/v02-battle-bg-hd): B1 Yingchuan night camp (and its burning
    // state), B2 Guangzong plain, B3 Anxi foothills; five layers each, high resolution (user 2026-10-25: backgrounds in
    // Codex's painterly quality). Until a set is delivered its battle uses the Zhuojun outskirts with a tint, so the
    // battles play before the art is in.
    public static class ChapterBackdrops
    {
        public const string Root = "Assets/_Project/Art/Chapter2/Battle";
        const string DefaultSource = @"D:\BuildArtifacts\Codex\hd2d-jrpg\art\v02-battle-bg-hd";
        static readonly string[] Layers = { "sky", "far", "mid", "ground", "front" };

        public static string Source
        {
            get
            {
                string env = System.Environment.GetEnvironmentVariable("HD2D_V02_BATTLE_SOURCE");
                return string.IsNullOrEmpty(env) ? DefaultSource : env;
            }
        }

        static string Prefix(string id) => id == "B1" ? "B1_night" : id;
        static string LayerPath(string prefix, string layer) => $"{Root}/{prefix}_{layer}.png";
        public static bool Has(string prefix) => Layers.All(l => File.Exists(LayerPath(prefix, l)));
        static string OwnScene(string id) => $"Assets/_Project/Scenes/Battle_Chapter_{id}.unity";

        // The base scene a battle starts from: the delivered backdrop, else the Zhuojun outskirts.
        public static string ScenePath(string id) => id != null && File.Exists(OwnScene(id)) && Has(Prefix(id)) ? OwnScene(id) : BattleBackdropBaker.OutskirtsScenePath;

        public static void Build()
        {
            BattleBackdropBaker.BuildOutskirts();
            Directory.CreateDirectory(Root);
            if (Directory.Exists(Source))
                foreach (var file in Directory.GetFiles(Source, "*.png"))
                {
                    string name = Path.GetFileName(file);
                    if (name.StartsWith("B") || name.StartsWith("FX") || name.StartsWith("stage_")) File.Copy(file, $"{Root}/{name}", true);
                }
            AssetDatabase.Refresh();
            foreach (var path in Directory.GetFiles(Root, "*.png"))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
                if (importer == null) continue;
                string key = Path.GetFileNameWithoutExtension(path);
                importer.textureType = TextureImporterType.Default;
                importer.filterMode = FilterMode.Bilinear;   // high-resolution paintings, not pixel art
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 2048;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.alphaIsTransparency = !key.EndsWith("_sky") && !key.StartsWith("stage_") && key != "B2_road_scene";
                importer.SaveAndReimport();
            }
            foreach (var id in new[] { "B1", "B2", "B3" })
            {
                string prefix = Prefix(id);
                if (!Has(prefix)) continue;
                var asset = $"{Root}/{id}.asset";
                var backdrop = AssetDatabase.LoadAssetAtPath<BattleBackdrop>(asset);
                if (!backdrop) { backdrop = ScriptableObject.CreateInstance<BattleBackdrop>(); AssetDatabase.CreateAsset(backdrop, asset); }
                backdrop.DisplayName = id;
                backdrop.Layers = Layers.Select(l => AssetDatabase.LoadAssetAtPath<Texture2D>(LayerPath(prefix, l))).ToArray();
                backdrop.LightPitchYaw = id == "B1" ? new Vector2(40f, -30f) : new Vector2(45f, 30f);
                backdrop.LightColor = id == "B1" ? new Color(1f, 0.72f, 0.5f) : new Color(1f, 0.98f, 0.94f);   // firelight / bright noon
                backdrop.LightIntensity = id == "B1" ? 1.8f : 2.4f;
                EditorUtility.SetDirty(backdrop);
                AssetDatabase.SaveAssets();
                BattleBackdropBaker.BuildBattleScene(backdrop, OwnScene(id), Root);
                if (id == "B1" && Has("B1_fire")) AddFireLayers();
            }
        }

        // B1's burning state: the same five layers on fire, under "Backdrop Fire", switched on by the fire event.
        static void AddFireLayers()
        {
            var scene = EditorSceneManager.OpenScene(OwnScene("B1"), OpenSceneMode.Single);
            var night = GameObject.Find("Backdrop");
            night.name = "Backdrop Night";
            var fire = Object.Instantiate(night);
            fire.name = "Backdrop Fire";
            var quads = fire.GetComponentsInChildren<MeshRenderer>();
            for (int i = 0; i < quads.Length && i < Layers.Length; i++)
            {
                var m = new Material(quads[i].sharedMaterial) { name = "B1_fire_" + Layers[i] };
                m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(LayerPath("B1_fire", Layers[i])));
                string path = $"{Root}/{m.name}.mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(path)) AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(m, path);
                quads[i].sharedMaterial = m;
            }
            fire.SetActive(false);
            EditorSceneManager.SaveScene(scene);
        }

        // Called with the battle scene open: what the fire event switches. Delivered B1: night layers off, fire layers on.
        // Before that: a night tint over the outskirts, and an orange glow for the fire.
        public static BattleController.FxSwitch[] FxSwitches(string id, bool night)
        {
            if (!night) return new BattleController.FxSwitch[0];
            var nightGo = GameObject.Find("Backdrop Night");
            var fireGo = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.name == "Backdrop Fire" && g.scene.IsValid());
            if (nightGo && fireGo)
                return new[] { new BattleController.FxSwitch { Id = "fire_wall", On = fireGo, Off = nightGo, Seconds = 1.4f } };
            Tint("Night tint", new Color(0.05f, 0.08f, 0.22f, 0.55f), 12f, true);
            var glow = Tint("Fire glow", new Color(0.95f, 0.38f, 0.10f, 0.32f), 11.5f, false);
            return new[] { new BattleController.FxSwitch { Id = "fire_wall", On = glow, Seconds = 1.4f } };
        }

        static GameObject Tint(string name, Color color, float z, bool active)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.position = new Vector3(0, 0, z);
            q.transform.localScale = new Vector3(24f, 14f, 1f);
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = name };
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent - 10;
            Directory.CreateDirectory(Root);
            string path = $"{Root}/{name}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path)) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
            var r = q.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
            q.SetActive(active);
            return q;
        }
    }
}
