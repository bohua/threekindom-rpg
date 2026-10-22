using System.Collections;
using System.Linq;
using NUnit.Framework;
using ThreeKingdoms.Core.Battle;
using ThreeKingdoms.Gameplay.Battle;
using ThreeKingdoms.Gameplay.Story;
using ThreeKingdoms.UI.Battle;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

// The battle HUD draws with the Codex pixel skin (BATTLE_UI_SKIN.md): the acting general's card uses the active frame,
// frames are tiled nine-slices at 2×, and a routed unit's card switches to the routed frame.
public sealed class BattleUiSkinTests
{
    [SetUp] public void Fresh() => GameRun.Clear();
    [TearDown] public void Clean() => GameRun.Clear();

    static bool Uses(VisualElement e, Texture2D t) => t && e.resolvedStyle.backgroundImage.texture == t;

    [UnityTest] public IEnumerator BattleHudUsesThePixelSkin()
    {
        yield return SceneManager.LoadSceneAsync("Battle_Tutorial01");
        yield return null; yield return null;
        var hud = Object.FindFirstObjectByType<BattleHud>();
        Assert.IsNotNull(hud.Skin, "battle scenes carry the skin");
        Assert.IsTrue(hud.Skin.OptionNormal && hud.Skin.OptionSelected && hud.Skin.OptionDisabled, "blank option bars imported (art/ui-bubbles-bars)");
        Assert.IsTrue(hud.Skin.HasBubbles && hud.Skin.BubbleAlertGlint, "pixel bubbles imported");
        var all = hud.GetComponent<UIDocument>().rootVisualElement.Query<VisualElement>().ToList();
        Assert.IsTrue(all.Any(e => Uses(e, hud.Skin.CardActive)), "the acting general's card has the active frame");
        Assert.IsTrue(all.Any(e => Uses(e, hud.Skin.CardNormal)), "the others have the normal frame");
        // Fixed commands are whole cards with the label drawn in (user 2026-10-21): the focused one shows its selected card.
        Assert.IsTrue(all.Any(e => Uses(e, hud.Skin.CommandSelected[0])), "攻击 is focused and shows its selected card");
        Assert.IsTrue(all.Any(e => Uses(e, hud.Skin.CommandNormal[2])), "防御 shows its normal card");
        Assert.IsTrue(all.Any(e => Uses(e, hud.Skin.CommandDisabled[3]) || Uses(e, hud.Skin.CommandNormal[3])), "道具 is a card too (disabled while no one needs rations)");
        Assert.IsFalse(all.OfType<Button>().Any(x => x.text == "攻击" && x.style.width.value.value != 224f), "cards are drawn at exactly 2× (224 px)");
        Assert.IsTrue(all.Any(e => Uses(e, hud.Skin.EnemyPlateBoss)), "Cheng Yuanzhi has the boss nameplate");
        Assert.IsTrue(all.Any(e => Uses(e, hud.Skin.EnemyPlate)), "the headmen have the normal nameplate");
        var panel = all.First(e => Uses(e, hud.Skin.CommandPanel));
        Assert.AreEqual(SliceType.Tiled, panel.resolvedStyle.unitySliceType, "edges tile instead of stretching");
        Assert.AreEqual(2f, panel.resolvedStyle.unitySliceScale);

        // A routed general's card changes frame and shows the seal.
        var b = Object.FindFirstObjectByType<BattleController>();
        var zhang = b.Session.State.Players.First(u => u.Id == "zhang_fei");
        typeof(BattleUnit).GetProperty("Troops").SetValue(zhang, 0);
        b.Undo(); // any Changed event refreshes the cards
        b.Choose(ThreeKingdoms.Core.Battle.BattleCommand.Defend(b.Actor));
        yield return null;
        all = hud.GetComponent<UIDocument>().rootVisualElement.Query<VisualElement>().ToList();
        Assert.IsTrue(all.Any(e => Uses(e, hud.Skin.CardRouted)), "routed card frame");
    }
}
