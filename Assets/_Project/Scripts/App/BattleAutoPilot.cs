using System.Linq;
using ThreeKingdoms.Core.Battle;
using ThreeKingdoms.Gameplay.Battle;
using UnityEngine;

namespace ThreeKingdoms.App
{
    // Development only: issues simple commands whenever the battle waits for input, so builds can be captured
    // mid-battle without a player. Optionally stops before a given round to leave the command menu on screen.
    public sealed class BattleAutoPilot : MonoBehaviour
    {
        public int StopAtRound = int.MaxValue;
        BattleController controller;

        void Update()
        {
            if (!controller) { controller = FindAnyObjectByType<BattleController>(); return; }
            if (controller.Current != BattleController.Phase.Commanding || controller.Actor == null) return;
            var state = controller.Session.State;
            if (state.Round >= StopAtRound)
            {
                // Leave the menu up and preview a target, so screenshots show the selection feedback.
                controller.PreviewTarget(state.Enemies.Where(e => e.Active).OrderBy(e => e.Troops).ThenBy(e => e.Slot).First());
                return;
            }
            var actor = controller.Actor;
            var target = state.Enemies.Where(e => e.Active).OrderBy(e => e.Troops).ThenBy(e => e.Slot).First();
            var hurt = state.Players.Where(u => u.Active && u.Troops * 2 < u.Stats.MaxTroops).OrderBy(u => u.Troops).FirstOrDefault();
            var heal = actor.Skills.FirstOrDefault(s => s.HealPercent > 0 && state.CanUseSkill(actor, s));
            var strike = actor.Skills.FirstOrDefault(s => s.Damage != DamageKind.None && state.CanUseSkill(actor, s));
            if (hurt != null && heal != null) controller.Choose(BattleCommand.UseSkill(actor, heal, hurt));
            else if (strike != null) controller.Choose(BattleCommand.UseSkill(actor, strike, strike.IsSingleTarget ? target : null));
            else controller.Choose(BattleCommand.Attack(actor, target));
        }
    }
}
