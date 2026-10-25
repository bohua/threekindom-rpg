using UnityEditor;
using UnityEngine;

namespace ThreeKingdoms.EditorTools
{
    // One batch entry for the 0.3 content (V03_TAODONG_PLAN §8): imports the Codex art that has landed (scroll paintings,
    // people, portraits), then rebuilds the story stage, the walkable chapter scenes and the chapter battles.
    public static class Chapter3Build
    {
        [MenuItem("JRPG/Build 0.3 content (art, scenes, battles)")]
        public static void Build()
        {
            CinematicsArtImporter.Import();
            ResidentImporter.Import();
            DialoguePortraitImporter.Import();
            BattleSceneBuilder.BuildChapterBattles();
            ChapterFieldBuilder.Build();
            AssetDatabase.SaveAssets();
            Debug.Log("CHAPTER3_BUILT");
        }

        public static void BuildFromCommandLine()
        {
            try { Build(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
