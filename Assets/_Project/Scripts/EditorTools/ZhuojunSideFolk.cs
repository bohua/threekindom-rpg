using System.Linq;
using ThreeKingdoms.Gameplay.Story;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ThreeKingdoms.EditorTools
{
    // The chapter 1 side-quest people who stand in Zhuojun only at some points of the story (npc-sidequests R1: the
    // Zhongshan horse merchants, Gu Sou, Lu Yu and his sister-in-law), with their own Q sprites (Codex
    // art/ch1-npc-sprites, ResidentImporter.Chapter1Side): hidden templates in the town scene that
    // TownStoryController.Sidequests copies to their posts when the story calls for them.
    public static class ZhuojunSideFolk
    {
        // Name the controller looks up, sprite key.
        static readonly (string name, string key)[] Folk =
        {
            ("zhang_shiping 张世平", "NpcCh1ZhangShiping"), ("su_shuang 苏双", "NpcCh1SuShuang"), ("gu_sou 鼓叟", "NpcCh1GuSou"),
            ("lu_yu 卢毓", "NpcCh1LuYu"), ("lu_sao 卢家长嫂", ResidentImporter.KeyFor("N08")),
        };

        // Adds (or replaces) the templates in the open town scene and hands them to its controller.
        public static void AddTo(TownStoryController c)
        {
            var old = GameObject.Find("Side quest folk");
            if (old) Object.DestroyImmediate(old);
            var group = new GameObject("Side quest folk").transform;
            c.SideFolk = Folk.Select(f =>
            {
                var go = HD2DSampleBuilder.Character(f.name, f.key, new Vector3(0, -50f, 0), 0, isPlayer: false);
                go.transform.SetParent(group, true);
                go.SetActive(false);
                return go.transform;
            }).ToArray();
            EditorUtility.SetDirty(c);
        }

        // -executeMethod ThreeKingdoms.EditorTools.ZhuojunSideFolk.AddToTownFromCommandLine
        public static void AddToTownFromCommandLine()
        {
            try
            {
                var scene = EditorSceneManager.OpenScene(ZhuojunTownBuilder.ScenePath, OpenSceneMode.Single);
                AddTo(Object.FindFirstObjectByType<TownStoryController>());
                EditorSceneManager.SaveScene(scene);
                EditorApplication.Exit(0);
            }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
