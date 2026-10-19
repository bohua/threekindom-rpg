using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ThreeKingdoms.EditorTools
{
    // Town beauty factor ② (ADR-0005 §8): baked indirect light and ambient occlusion for the static town,
    // light probes for the dynamic pixel characters. Direct light and shadows stay realtime (Mixed lights in
    // Baked Indirect mode) so characters keep casting shadows.
    public static class HD2DLighting
    {
        public const string SettingsPath = "Assets/_Project/Settings/HD2D_TownLighting.lighting";
        const float GroundLightmapScale = 0.15f;

        public static void MarkStatic(Transform root)
        {
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,
                    StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic);
                renderer.receiveGI = ReceiveGI.Lightmaps;
                // The huge ground plane would eat most of the atlas; keep it coarse.
                if (renderer.name == "Ground") renderer.scaleInLightmap = GroundLightmapScale;
            }
        }

        public static void AddProbeGrid(Vector3 center, Vector3 size, float spacing)
        {
            var positions = new List<Vector3>();
            for (float x = -size.x / 2; x <= size.x / 2 + 0.01f; x += spacing)
            for (float z = -size.z / 2; z <= size.z / 2 + 0.01f; z += spacing)
            foreach (float y in new[] { 0.6f, size.y })
                positions.Add(new Vector3(x, y, z));
            var go = new GameObject("Light probes");
            go.transform.position = center;
            go.AddComponent<LightProbeGroup>().probePositions = positions.ToArray();
        }

        public static void Bake(string scenePath)
        {
            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(SettingsPath);
            if (!settings)
            {
                settings = new LightingSettings { name = "HD2D_TownLighting" };
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            settings.bakedGI = true;
            settings.realtimeGI = false;
            settings.mixedBakeMode = MixedLightingMode.IndirectOnly;
            settings.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
            settings.lightmapResolution = 8f;               // texels per unit; ADR-0005 caps town lightmaps at 64 MB
            settings.lightmapPadding = 2;
            settings.lightmapMaxSize = 1024;
            settings.directionalityMode = LightmapsMode.NonDirectional;
            settings.ao = true;
            settings.aoMaxDistance = 1.2f;
            settings.aoExponentIndirect = 1.4f;
            settings.aoExponentDirect = 0f;
            settings.maxBounces = 2;
            settings.directSampleCount = 32;
            settings.indirectSampleCount = 256;
            settings.environmentSampleCount = 256;
            settings.filteringMode = LightingSettings.FilterMode.Auto;
            EditorUtility.SetDirty(settings);

            Lightmapping.lightingSettings = settings;
            if (!Lightmapping.Bake()) throw new System.Exception("Lightmap bake failed");
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), scenePath);

            long bytes = 0;
            foreach (var data in LightmapSettings.lightmaps)
                if (data.lightmapColor) bytes += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(data.lightmapColor);
            Debug.Log($"HD2D_LIGHTMAPS count={LightmapSettings.lightmaps.Length} memoryMB={bytes / 1048576.0:F1}");
        }
    }
}
