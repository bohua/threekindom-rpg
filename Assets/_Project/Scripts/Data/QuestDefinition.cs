using UnityEngine;
namespace Jrpg {
    [CreateAssetMenu(menuName = "JRPG/Quest")]
    public sealed class QuestDefinition : ScriptableObject {
        public string Id;
        public string DisplayName;
        [TextArea] public string Description;
        public string TargetId;
        [Min(1)] public int RequiredCount = 1;
    }
}
