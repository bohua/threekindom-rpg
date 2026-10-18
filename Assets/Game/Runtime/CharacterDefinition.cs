using UnityEngine;
namespace Jrpg {
    [CreateAssetMenu(menuName = "JRPG/Character")]
    public sealed class CharacterDefinition : ScriptableObject {
        public string Id;
        public string DisplayName;
        [Min(1)] public int MaxHp = 30;
        [Min(0)] public int Attack = 8;
        [Min(0)] public int Defense = 2;
        [Min(0)] public int Speed = 5;
        public SkillDefinition[] Skills;
    }
}
