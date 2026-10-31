using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ThreeKingdoms.EditorTools
{
    // The dreamy morning of Lousang (user 2026-10-30: "精楼桑村，需要在细节和贴图上做到高分辨率以及梦幻的感觉"; the
    // runtime half of art/lousang-hd-concept LOUSANG_HD_NOTES.md): peach and apricot petals drifting down, motes in the
    // light under the great mulberry, leaf-dappled light through its crown, and a local warm grade with soft bloom and a
    // little haze, all inside the village only. Built into the open scene (Region_R1) by ChapterFieldBuilderR1.
    public static class LousangAmbienceBuilder
    {
        const string Fx = @"D:\BuildArtifacts\Codex\hd2d-jrpg\art\lousang-hd-trees\fx";
        const string Dir = "Assets/_Project/Art/Environment/Lousang";
        public static readonly Vector3 Centre = new Vector3(170f, 0f, 64f);
        public static readonly Vector3 Mulberry = new Vector3(182f, 0f, 76.5f);

        public static void Build(System.Func<float, float, float> ground)
        {
            if (!Directory.Exists(Fx)) { Debug.LogWarning("Lousang ambience: art/lousang-hd-trees/fx not delivered."); return; }
            var old = GameObject.Find("Lousang Ambience");
            if (old) Object.DestroyImmediate(old);
            Directory.CreateDirectory(Dir);
            var petals = Texture("petal_atlas.png", false);
            var mote = Texture("light_mote.png", false);
            var cookie = Texture("leaf_shadow_cookie.png", true);

            var root = new GameObject("Lousang Ambience").transform;
            root.position = new Vector3(Centre.x, ground(Centre.x, Centre.z), Centre.z);

            // Petals: slow, sideways, turning; a 4×4 atlas of peach and apricot petals.
            var p = Particles(root, "Petals", Material("lousang_petals", petals, false), new Vector3(0, 7f, 0));
            var main = p.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(9f, 14f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.14f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0.012f;
            main.maxParticles = 600;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.prewarm = true;
            var em = p.emission; em.rateOverTime = 34f;
            var shape = p.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(52f, 6f, 52f);
            var vel = p.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0.25f, 0.6f); vel.y = new ParticleSystem.MinMaxCurve(-0.35f, -0.2f); vel.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.2f);
            var rot = p.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);
            var noise = p.noise; noise.enabled = true; noise.strength = 0.35f; noise.frequency = 0.35f; noise.scrollSpeed = 0.2f;
            var sheet = p.textureSheetAnimation; sheet.enabled = true; sheet.numTilesX = 4; sheet.numTilesY = 4;
            sheet.animation = ParticleSystemAnimationType.WholeSheet; sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 15.99f);
            var fade = p.colorOverLifetime; fade.enabled = true; fade.color = new ParticleSystem.MinMaxGradient(Fade());

            // Motes in the light around the great mulberry: small, warm, slow, additive.
            var m = Particles(root, "Light motes", Material("lousang_motes", mote, true), Mulberry - Centre + new Vector3(0, 3f, 0));
            var mm = m.main;
            mm.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
            mm.startSpeed = 0f;
            mm.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.13f);
            mm.startColor = new Color(1f, 0.9f, 0.62f, 0.75f);
            mm.maxParticles = 220;
            mm.simulationSpace = ParticleSystemSimulationSpace.World;
            mm.prewarm = true;
            var me = m.emission; me.rateOverTime = 26f;
            var ms = m.shape; ms.shapeType = ParticleSystemShapeType.Box; ms.scale = new Vector3(16f, 5f, 16f);
            var mn = m.noise; mn.enabled = true; mn.strength = 0.25f; mn.frequency = 0.5f;
            var mf = m.colorOverLifetime; mf.enabled = true; mf.color = new ParticleSystem.MinMaxGradient(Fade());

            // Leaf-dappled light: a soft warm spot under the crown, its cookie the gaps between mulberry leaves.
            var light = new GameObject("Leaf light").AddComponent<Light>();
            light.transform.SetParent(root, false);
            light.transform.position = new Vector3(Mulberry.x - 3f, ground(Mulberry.x, Mulberry.z) + 22f, Mulberry.z - 4f);
            light.transform.rotation = Quaternion.LookRotation(new Vector3(Mulberry.x, ground(Mulberry.x, Mulberry.z), Mulberry.z) - light.transform.position);
            light.type = LightType.Spot;
            light.spotAngle = 70f;
            light.range = 40f;
            light.intensity = 9f;
            light.color = new Color(1f, 0.86f, 0.62f);
            light.cookie = cookie;
            light.shadows = LightShadows.None;

            // The village's own grade: warm, soft bloom on the highlights, a light haze; full inside, gone 14 m out.
            var vol = new GameObject("Lousang grade").AddComponent<Volume>();
            vol.transform.SetParent(root, false);
            vol.isGlobal = false;
            vol.blendDistance = 14f;
            vol.priority = 5f;
            var box = vol.gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(56f, 40f, 56f);
            box.center = new Vector3(0, 10f, 0);
            vol.gameObject.layer = 2;   // ignore raycasts
            vol.sharedProfile = Profile();
            Debug.Log("LOUSANG_AMBIENCE_BUILT");
        }

        static VolumeProfile Profile()
        {
            string path = $"{Dir}/Lousang_Morning.asset";
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(path)) AssetDatabase.DeleteAsset(path);
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = p.Add<Bloom>(true);
            bloom.threshold.Override(0.92f); bloom.intensity.Override(0.55f); bloom.scatter.Override(0.72f); bloom.tint.Override(new Color(1f, 0.92f, 0.8f));
            var grade = p.Add<ColorAdjustments>(true);
            grade.postExposure.Override(0.12f); grade.contrast.Override(-6f); grade.saturation.Override(8f); grade.colorFilter.Override(new Color(1f, 0.965f, 0.9f));
            var white = p.Add<WhiteBalance>(true);
            white.temperature.Override(9f); white.tint.Override(2f);
            var lift = p.Add<LiftGammaGain>(true);
            lift.lift.Override(new Vector4(1f, 0.98f, 1.03f, 0.03f));
            var vignette = p.Add<Vignette>(true);
            vignette.intensity.Override(0.16f); vignette.smoothness.Override(0.55f); vignette.color.Override(new Color(0.24f, 0.17f, 0.12f));
            AssetDatabase.CreateAsset(p, path);
            foreach (var c in p.components) AssetDatabase.AddObjectToAsset(c, p);
            AssetDatabase.SaveAssets();
            return p;
        }

        static Gradient Fade()
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        static ParticleSystem Particles(Transform root, string name, Material mat, Vector3 local)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = local;
            var ps = go.AddComponent<ParticleSystem>();
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        static Texture2D Texture(string file, bool cookie)
        {
            string to = $"{Dir}/{file}";
            File.Copy(Path.Combine(Fx, file), to, true);
            AssetDatabase.ImportAsset(to, ImportAssetOptions.ForceUpdate);
            var ti = (TextureImporter)AssetImporter.GetAtPath(to);
            ti.textureType = cookie ? TextureImporterType.Cookie : TextureImporterType.Default;
            ti.alphaIsTransparency = !cookie;
            ti.wrapMode = cookie ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            ti.mipmapEnabled = true;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(to);
        }

        static Material Material(string name, Texture2D tex, bool additive)
        {
            string path = $"{Dir}/{name}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path)) AssetDatabase.DeleteAsset(path);
            var m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = name };
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (additive) m.EnableKeyword("_ALPHAMODULATE_ON");
            m.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }
}
