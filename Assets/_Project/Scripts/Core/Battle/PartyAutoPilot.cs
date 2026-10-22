using System.Linq;

namespace ThreeKingdoms.Core.Battle
{
    // Auto battle for the player's side (user 2026-10-22): a plain, predictable rule per general, the same each time.
    // Rally a unit below half troops when the actor can heal; otherwise the strongest affordable damage skill;
    // otherwise a free attack. Single targets: the weakest enemy left.
    // Rations are never spent automatically.
    public static class PartyAutoPilot
    {
        public static BattleCommand Choose(BattleState state, BattleUnit actor)
        {
            var target = state.Enemies.Where(e => e.Active).OrderBy(e => e.Troops).ThenBy(e => e.Slot).FirstOrDefault();
            var hurt = state.Players.Where(u => u.Active && u.Troops * 2 < u.Stats.MaxTroops).OrderBy(u => u.Troops).FirstOrDefault();
            var heal = actor.Skills.FirstOrDefault(k => k.HealPercent > 0 && state.CanUseSkill(actor, k));
            if (hurt != null && heal != null) return BattleCommand.UseSkill(actor, heal, hurt);
            if (target == null) return BattleCommand.Defend(actor);
            var strike = actor.Skills.Where(k => k.Damage != DamageKind.None && state.CanUseSkill(actor, k))
                .OrderByDescending(k => k.IsSingleTarget ? k.PowerPercent : k.PowerPercent * state.Enemies.Count(e => e.Active))
                .FirstOrDefault();
            if (strike != null) return BattleCommand.UseSkill(actor, strike, strike.IsSingleTarget ? target : null);
            return BattleCommand.Attack(actor, target);
        }
    }
}
