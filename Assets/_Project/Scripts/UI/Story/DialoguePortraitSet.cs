using UnityEngine;

namespace ThreeKingdoms.UI.Story
{
    // Full-body illustrations for the speakers that have one (user 2026-10-22), shared by the town and battle dialogue.
    // Liu Bei stands at the left facing in, everyone he talks to at the right. Crop is the shown fraction from the top;
    // Axis the figure's centre line (0..1 of the width), around which the wind sway is weakest.
    [CreateAssetMenu(menuName = "ThreeKingdoms/UI/Dialogue Portraits")]
    public sealed class DialoguePortraitSet : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public string Speaker;
            public Texture2D Texture;
            public bool Right, Flip;
            [Range(0.3f, 1f)] public float Crop;
            [Range(0f, 1f)] public float Axis;
        }

        public Entry[] Entries = new Entry[0];

        public bool TryGet(string speaker, out Entry entry)
        {
            foreach (var e in Entries)
                if (e.Speaker == speaker && e.Texture) { entry = e; return true; }
            entry = default;
            return false;
        }
    }
}
