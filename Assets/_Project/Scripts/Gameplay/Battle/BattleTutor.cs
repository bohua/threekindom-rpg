using System;
using System.Collections.Generic;
using System.Linq;
using ThreeKingdoms.Core.Battle;
using UnityEngine;

namespace ThreeKingdoms.Gameplay.Battle
{
    // Step-by-step teaching hints for the three Yellow Turban battles (story-events GDD answer 8). Watches the
    // battle and raises each hint at its moment, once per play-through; it never blocks or changes a command
    // (game-ui rule 25). The UI turns the keys into text and keeps the list for the 「提示」 menu.
    public sealed class BattleTutor : MonoBehaviour
    {
        public BattleController Controller;

        public event Action<string> HintShown;

        // Hints already shown this play-through (held by the run so they are not repeated across battles).
        public IReadOnlyList<string> Seen => Controller && Controller.Run != null ? Controller.Run.SeenHints : (IReadOnlyList<string>)Array.Empty<string>();

        string Battle => Controller.EncounterId;

        void OnEnable()
        {
            if (!Controller) return;
            Controller.Changed += OnChanged;
            Controller.EventPlayed += OnEvent;
            Controller.TargetPreviewed += OnTargetPreviewed;
            Controller.CommandChosen += OnCommandChosen;
        }

        void OnDisable()
        {
            if (!Controller) return;
            Controller.Changed -= OnChanged;
            Controller.EventPlayed -= OnEvent;
            Controller.TargetPreviewed -= OnTargetPreviewed;
            Controller.CommandChosen -= OnCommandChosen;
        }

        void Show(string key)
        {
            if (Controller.Run == null || Controller.Run.SeenHints.Contains(key)) return;
            Controller.Run.SeenHints.Add(key);
            HintShown?.Invoke(key);
        }

        bool AnyBelowHalf() => Controller.Session.State.Players.Any(u => u.Active && u.Troops * 2 < u.Stats.MaxTroops);

        void OnChanged()
        {
            if (Controller.Session == null) return;
            switch (Controller.Current)
            {
                case BattleController.Phase.Commanding:
                    if (Battle == "battle_1") Show("b1.command");
                    if (Battle == "battle_2") { Show("b2.tech"); if (AnyBelowHalf()) Show("b2.defend"); }
                    if (Battle == "battle_3") { Show("b3.skills"); if (AnyBelowHalf()) Show("b3.ration"); }
                    break;
                case BattleController.Phase.Playback:
                    if (Battle == "battle_1") Show("b1.order");
                    break;
                case BattleController.Phase.Finished:
                    if (Battle == "battle_1" && Controller.Session.Result?.Outcome == BattleOutcome.Victory) Show("b1.recover");
                    break;
            }
        }

        void OnEvent(BattleEvent e)
        {
            if (Battle == "battle_1" && e.Kind == BattleEventKind.Retreated) Show("b1.retreat");
            if (Battle == "battle_3" && e.Kind == BattleEventKind.ModifierApplied && e.ModifierPercent < 0) Show("b3.debuff");
        }

        void OnTargetPreviewed(BattleUnit actor, BattleUnit target)
        {
            if (Battle == "battle_1") Show("b1.target");
            if (Battle == "battle_2" && actor != null && target != null && target.Side != actor.Side && Controller.Session.State.HasAdvantage(actor, target))
                Show("b2.advantage");
        }

        void OnCommandChosen(BattleCommand command)
        {
            if (Battle != "battle_2") return;
            bool focused = Controller.Commands.Values.Where(c => c.Target != null && c.Target.Side == Side.Enemy)
                .GroupBy(c => c.Target).Any(g => g.Count() >= 2);
            if (focused) Show("b2.focus");
        }
    }
}
