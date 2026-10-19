using System.IO;
using Jrpg;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class ProjectSetup
{
    const string ScenePath = "Assets/_Project/Scenes/Glade.unity";
    [MenuItem("JRPG/Create First Scene (only if absent)")]
    public static void Create()
    {
        if (File.Exists(ScenePath)) { Debug.Log("Existing scene preserved."); return; }
        Directory.CreateDirectory("Assets/_Project/Scenes");
        Directory.CreateDirectory("Assets/_Project/Data/Characters"); Directory.CreateDirectory("Assets/_Project/Data/Skills"); Directory.CreateDirectory("Assets/_Project/Data/Quests");
        Directory.CreateDirectory("Assets/_Project/Art/Materials"); Directory.CreateDirectory("Assets/_Project/Art/Sprites");
        Directory.CreateDirectory("Assets/_Project/Settings");
        var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
        AssetDatabase.CreateAsset(renderer, "Assets/_Project/Settings/GladeRenderer.asset");
        var pipeline = UniversalRenderPipelineAsset.Create(renderer);
        pipeline.name = "GladeURP";
        pipeline.renderScale = 1;
        pipeline.msaaSampleCount = 1;
        pipeline.shadowDistance = 25;
        AssetDatabase.CreateAsset(pipeline, "Assets/_Project/Settings/GladeURP.asset");
        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;
        QualitySettings.vSyncCount = 1;
        PlayerSettings.companyName = "Independent Studio";
        PlayerSettings.productName = "Moonlit Glade";
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        EditorSettings.serializationMode = SerializationMode.ForceText;
        string code = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "Programs", "Microsoft VS Code", "Code.exe");
        if (File.Exists(code)) EditorPrefs.SetString("kScriptsDefaultApp", code);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientLight = new Color(.35f, .43f, .58f);
        var ground = Material("Moss", new Color(.14f, .25f, .23f));
        var stone = Material("Stone", new Color(.25f, .31f, .38f));
        Cube("Ground", new Vector3(0, -.5f, 0), new Vector3(20, 1, 20), ground);
        Cube("North wall", new Vector3(0, 1, 10), new Vector3(21, 3, 1), stone);
        Cube("South wall", new Vector3(0, 1, -10), new Vector3(21, 3, 1), stone);
        Cube("West wall", new Vector3(-10, 1, 0), new Vector3(1, 3, 20), stone);
        Cube("East wall", new Vector3(10, 1, 0), new Vector3(1, 3, 20), stone);
        for (int i = 0; i < 6; i++) Cube("Ruined pillar", new Vector3(-6 + i % 3 * 6, 1, 5 + i / 3 * 2), new Vector3(.7f, 2, .7f), stone);
        var player = new GameObject("Hero");
        player.transform.position = new Vector3(0, .05f, -2);
        var controller = player.AddComponent<CharacterController>();
        controller.height = 1.4f;
        controller.radius = .28f;
        controller.center = new Vector3(0, .7f, 0);
        var mover = player.AddComponent<PlayerMover>();
        SpriteObject("Hero pixels", player.transform, HeroSprite(), new Vector3(0, .8f, 0));
        var enemy = new GameObject("Slime");
        enemy.transform.position = new Vector3(3, 0, 1);
        var blocker = enemy.AddComponent<BoxCollider>();
        blocker.center = new Vector3(0, .4f, 0); blocker.size = new Vector3(.8f, .8f, .8f);
        SpriteObject("Slime pixels", enemy.transform, SlimeSprite(), new Vector3(0, .5f, 0));
        var cam = new GameObject("Main Camera");
        cam.tag = "MainCamera";
        var camera = cam.AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 6; camera.farClipPlane = 100;
        camera.backgroundColor = new Color(.06f, .09f, .16f); camera.clearFlags = CameraClearFlags.SolidColor;
        cam.AddComponent<UniversalAdditionalCameraData>();
        cam.transform.rotation = Quaternion.Euler(35, 0, 0);
        var follow = cam.AddComponent<FollowCamera>(); follow.Target = player.transform;
        cam.transform.position = player.transform.position + follow.Offset;
        var sun = new GameObject("Moonlight").AddComponent<Light>();
        sun.type = LightType.Directional; sun.intensity = 1.4f; sun.color = new Color(.74f, .84f, 1);
        sun.shadows = LightShadows.Soft; sun.transform.rotation = Quaternion.Euler(50, -35, 0);
        var lantern = new GameObject("Warm lantern").AddComponent<Light>();
        lantern.type = LightType.Point; lantern.range = 8; lantern.intensity = 5;
        lantern.color = new Color(1, .58f, .24f); lantern.transform.position = new Vector3(-3, 2, 0);
        var skill = ScriptableObject.CreateInstance<SkillDefinition>(); skill.Id = "strike"; skill.DisplayName = "Strike"; skill.Power = 3;
        AssetDatabase.CreateAsset(skill, "Assets/_Project/Data/Skills/Strike.asset");
        var hero = Character("hero", "Hero", 35, 8, 3, 7, skill);
        var slime = Character("slime", "Slime", 20, 5, 1, 3, skill);
        var quest = ScriptableObject.CreateInstance<QuestDefinition>(); quest.Id = "clear-glade"; quest.DisplayName = "Clear the glade";
        quest.Description = "Defeat the slime near the ruins."; quest.TargetId = "slime";
        AssetDatabase.CreateAsset(quest, "Assets/_Project/Data/Quests/ClearGlade.asset");
        var encounter = new GameObject("Encounter").AddComponent<DemoEncounter>();
        encounter.Player = mover; encounter.Enemy = enemy.transform; encounter.HeroDefinition = hero;
        encounter.EnemyDefinition = slime; encounter.AttackSkill = skill; encounter.Quest = quest;
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("JRPG_SETUP_COMPLETE");
    }
    static CharacterDefinition Character(string id, string label, int hp, int attack, int defense, int speed, SkillDefinition skill)
    {
        var value = ScriptableObject.CreateInstance<CharacterDefinition>();
        value.Id = id; value.DisplayName = label; value.MaxHp = hp; value.Attack = attack; value.Defense = defense; value.Speed = speed;
        value.Skills = new[] { skill }; AssetDatabase.CreateAsset(value, $"Assets/_Project/Data/Characters/{label}.asset"); return value;
    }
    static Material Material(string name, Color color)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
        AssetDatabase.CreateAsset(m, $"Assets/_Project/Art/Materials/{name}.mat"); return m;
    }
    static void Cube(string name, Vector3 at, Vector3 size, Material material)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); cube.name = name;
        cube.transform.position = at; cube.transform.localScale = size; cube.GetComponent<Renderer>().sharedMaterial = material;
    }
    static void SpriteObject(string name, Transform parent, Sprite sprite, Vector3 at)
    {
        var obj = new GameObject(name); obj.transform.SetParent(parent); obj.transform.localPosition = at;
        obj.transform.rotation = Quaternion.Euler(35, 0, 0);
        var renderer = obj.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
        var material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
        AssetDatabase.CreateAsset(material, $"Assets/_Project/Art/Materials/{name.Replace(" ", "")}.mat"); renderer.sharedMaterial = material;
    }
    static Sprite Pixels(string name, bool slime)
    {
        var tex = new Texture2D(16, 24, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var pixels = new Color[16 * 24];
        for (int y = 0; y < 24; y++) for (int x = 0; x < 16; x++)
        {
            Color color = Color.clear;
            if (slime) { if (y >= 5 && y <= 14 && Mathf.Abs(x - 7.5f) < (y < 10 ? 7 : 17 - y)) color = new Color(.35f, .9f, .65f); if (y == 11 && (x == 5 || x == 10)) color = Color.black; }
            else {
                if (y >= 2 && y <= 7 && ((x >= 4 && x <= 6) || (x >= 9 && x <= 11))) color = new Color(.18f, .17f, .29f);
                if (y >= 8 && y <= 15 && x >= 3 && x <= 12) color = new Color(.3f, .52f, .85f);
                if (y >= 16 && y <= 21 && x >= 5 && x <= 10) color = new Color(1, .77f, .55f);
                if (y >= 20 && y <= 23 && x >= 4 && x <= 11) color = new Color(.24f, .16f, .2f);
            }
            pixels[y * 16 + x] = color;
        }
        tex.SetPixels(pixels); tex.Apply();
        string path = $"Assets/_Project/Art/Sprites/{name}.png"; File.WriteAllBytes(path, tex.EncodeToPNG()); Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = 16; importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    static Sprite HeroSprite() => Pixels("Hero", false);
    static Sprite SlimeSprite() => Pixels("Slime", true);
    public static void BuildWindows()
    {
        Create();
        string output = System.Environment.GetEnvironmentVariable("JRPG_BUILD_OUTPUT");
        if (string.IsNullOrWhiteSpace(output) || !Path.GetFullPath(output).StartsWith(@"D:\BuildArtifacts\Codex\hd2d-jrpg\", System.StringComparison.OrdinalIgnoreCase))
            throw new System.Exception("Set JRPG_BUILD_OUTPUT to the approved physical D drive artifact directory.");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report = BuildPipeline.BuildPlayer(new[] { ScenePath }, output, BuildTarget.StandaloneWindows64, BuildOptions.None);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new System.Exception("Build failed: " + report.summary.result);
    }
}
