using ThreeKingdoms.Gameplay.Story;
using ThreeKingdoms.UI.Battle;
using UnityEngine;
using UnityEngine.UIElements;

namespace ThreeKingdoms.UI.Story
{
    // Overlay for the tavern scene (user 2026-10-22): the shared dialogue box with the speakers' illustrations, and the
    // fade in and out. Reads TavernStage only.
    [RequireComponent(typeof(UIDocument))]
    public sealed class TavernHud : MonoBehaviour
    {
        public TavernStage Stage;
        [Tooltip("Pixel skin (BATTLE_UI_SKIN.md); empty keeps the flat style")] public ThreeKingdoms.UI.Battle.BattleUiSkin Skin;
        public DialoguePortraitSet Portraits;

        VisualElement root, fade;
        DialogueView dialogue;

        public string PortraitSpeaker => dialogue?.PortraitSpeaker;

        void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            root.pickingMode = PickingMode.Ignore;
            BattleFont.ApplyTo(root);
            root.style.flexGrow = 1;
            dialogue = new DialogueView(root, Skin, Portraits);
            fade = new VisualElement { pickingMode = PickingMode.Ignore };
            fade.style.position = Position.Absolute;
            fade.style.left = fade.style.top = fade.style.right = fade.style.bottom = 0;
            fade.style.backgroundColor = Color.black;
            root.Add(fade);
            if (Stage) Stage.Changed += Refresh;
            Refresh();
        }

        void OnDisable() { if (Stage) Stage.Changed -= Refresh; }

        void Refresh()
        {
            if (!Stage) return;
            var l = Stage.Line;
            if (l.HasValue) dialogue.Show(l.Value.Speaker, l.Value.Line);
            else dialogue.Hide();
            fade.style.opacity = Stage.Fade;
        }

        void Update() => dialogue?.Tick();
    }
}
