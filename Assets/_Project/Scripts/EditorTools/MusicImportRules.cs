using UnityEditor;
using UnityEngine;

namespace ThreeKingdoms.EditorTools
{
    // Import settings for music cues (ADR-0011). tools/music-inbox already sets loudness to -16 LUFS and writes WAV, so
    // Unity must not normalise again and compresses once, to Vorbis. Loop bodies stream (long, one at a time); intros
    // and jingles (jng_) stay compressed in memory so they start on the scheduled sample. Applied when a file is first
    // imported; the menu item re-applies to every cue after a rule change.
    public sealed class MusicImportRules : AssetPostprocessor
    {
        public const string MusicDir = "Assets/_Project/Resources/Audio/Music";
        const float VorbisQuality = 0.6f;

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(MusicDir + "/") || !assetImporter.importSettingsMissing) return;
            Apply((AudioImporter)assetImporter);
        }

        public static void Apply(AudioImporter importer)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(importer.assetPath);
            bool small = name.EndsWith(Gameplay.Story.StoryMusic.IntroSuffix) || name.StartsWith("jng_");
            var s = importer.defaultSampleSettings;
            s.loadType = small ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.Streaming;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = VorbisQuality;
            s.preloadAudioData = small;
            s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = s;
            importer.forceToMono = false;
            importer.loadInBackground = !small;
            importer.ambisonic = false;
            var so = new SerializedObject(importer);
            var normalize = so.FindProperty("normalize");
            if (normalize != null) { normalize.boolValue = false; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        [MenuItem("JRPG/Re-apply music import rules")]
        public static void ReapplyAll()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { MusicDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not AudioImporter importer) continue;
                Apply(importer);
                importer.SaveAndReimport();
            }
        }
    }
}
