using UnityEngine;
namespace Jrpg {
    [CreateAssetMenu(menuName = "JRPG/Skill")]
    public sealed class SkillDefinition : ScriptableObject {
        public string Id;
        public string DisplayName;
        [Min(0)] public int Power = 5;
    }
}
