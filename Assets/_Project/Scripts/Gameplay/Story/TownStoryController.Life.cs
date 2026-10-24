using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ThreeKingdoms.Core.Town;
using UnityEngine;

namespace ThreeKingdoms.Gameplay.Story
{
    // Town life (user 2026-10-22): the beggar (alms raise reputation), the scholar (three questions on the Romance),
    // the ruffian by the gambling house (big or small with three dice) and the archer at the barracks (the archery
    // range). Lines are StoryText keys; a key starting with "raw:" carries its text (quiz questions and answers).
    public sealed partial class TownStoryController
    {
        [Header("Town life (user 2026-10-22)")]
        [Tooltip("More townsfolk (N13–N32): they speak in bubbles like the residents")] public Transform[] Ambient = Array.Empty<Transform>();
        public Transform Beggar, Scholar, Ruffian, Archer;

        public const string ArcheryPanel = "archery";

        // The dice on the table while a bet is on (shaking, then the throw), or null.
        public int[] Dice { get; private set; }
        public bool DiceRolling { get; private set; }
        // The round on the archery range while its panel is open.
        public ArcheryGame Archery { get; private set; }

        int lastChoice;

        // Townsfolk who offer a mini-game wear a "?" (user 2026-10-24): the scholar's quiz, the ruffian's dice and the
        // archer's range. Hidden while the town is busy with a scene or a panel.
        public IEnumerable<Transform> GameMarks
        {
            get
            {
                if (Run == null || Busy || Panel != null) yield break;
                foreach (var t in new[] { Scholar, Ruffian, Archer }) if (t) yield return t;
            }
        }

        string LifePrompt(Vector3 p)
        {
            if (Near(Beggar, p)) return "beggar";
            if (Near(Scholar, p)) return "scholar";
            if (Near(Ruffian, p)) return "casino";
            if (Near(Archer, p)) return "archery";
            return null;
        }

        bool StartLife(string prompt)
        {
            switch (prompt)
            {
                case "beggar": StartCoroutine(Hold(Alms())); return true;
                case "scholar": StartCoroutine(Hold(Quiz())); return true;
                case "casino": StartCoroutine(Hold(Gamble())); return true;
                case "archery": StartCoroutine(Hold(OfferArchery())); return true;
            }
            return false;
        }

        // Runs a town-life sequence with the town held (no walking, no prompts) and gives control back after.
        IEnumerator Hold(IEnumerator body)
        {
            Busy = true;
            SetControl(false);
            Prompt = null;
            Changed?.Invoke();
            try { yield return body; }
            finally
            {
                Line = null;
                Choices = null;
                Dice = null;
                DiceRolling = false;
                if (Panel == null) { Busy = false; SetControl(true); }
                Changed?.Invoke();
            }
        }

        IEnumerator Say(string speaker, string key) => Lines(new[] { new DialogueLine(speaker, key) });

        // A line with options under it; the answer lands in lastChoice.
        IEnumerator Ask(string speaker, string key, IReadOnlyList<string> options)
        {
            Line = new DialogueLine(speaker, key);
            Choices = options;
            chosen = null;
            Changed?.Invoke();
            while (chosen == null) yield return null;
            lastChoice = chosen.Value;
            Choices = null;
            Changed?.Invoke();
        }

        IEnumerator ReputationChanged(int delta)
        {
            if (delta == 0) yield break;
            yield return Say("system", (delta > 0 ? "rep.up:" : "rep.down:") + Math.Abs(delta) + ":" + Run.Life.Reputation);
        }

        // ---------- the beggar ----------

        IEnumerator Alms()
        {
            Face(Beggar);
            yield return Ask(Speaker(Beggar), "beggar.ask", new[] { "alms.5", "alms.20", "alms.50", "alms.none" });
            int pick = lastChoice < Charity.Amounts.Length ? lastChoice : -1;
            int before = Run.Life.Reputation;
            var result = Charity.Give(Run.Progress.Inventory, Run.Life, pick);
            switch (result)
            {
                case Charity.Result.Given: yield return Say(Speaker(Beggar), "beggar.thanks." + pick); break;
                case Charity.Result.NotEnoughCopper: yield return Say("liu_bei", "beggar.poor"); break;
                default: yield return Say(Speaker(Beggar), "beggar.declined"); break;
            }
            yield return ReputationChanged(Run.Life.Reputation - before);
        }

        // ---------- the scholar ----------

        IEnumerator Quiz()
        {
            Face(Scholar);
            string who = Speaker(Scholar);
            if (!Run.Life.CanTakeQuiz(Run.Story.BattlesDone)) { yield return Say(who, "scholar.later"); yield break; }
            yield return Ask(who, "scholar.ask", new[] { "scholar.yes", "scholar.no" });
            if (lastChoice != 0) { yield return Say(who, "scholar.bye"); yield break; }
            Run.Life.TookQuiz(Run.Story.BattlesDone);
            int right = 0;
            var picked = ScholarQuiz.Pick(QuizBank.All.Length, Run.Random);
            for (int i = 0; i < picked.Count; i++)
            {
                var (text, options, answer) = QuizBank.Ask(picked[i], Run.Random);
                yield return Ask(who, $"raw:（第{i + 1}题）{text}", options.Select(o => "raw:" + o).ToArray());
                bool ok = lastChoice == answer;
                if (ok) right++;
                yield return Say(who, ok ? "scholar.right" : "scholar.wrong:" + options[answer]);
            }
            yield return Say(who, "scholar.result." + right);
            int before = Run.Life.Reputation;
            Run.Life.AddReputation(ScholarQuiz.ReputationFor(right));
            yield return ReputationChanged(Run.Life.Reputation - before);
        }

        // ---------- the gambling house ----------

        IEnumerator Gamble()
        {
            Face(Ruffian);
            string who = Speaker(Ruffian);
            yield return Say(who, "casino.greet");
            while (true)
            {
                yield return Ask(who, "casino.ask", new[] { "casino.big", "casino.small", "casino.leave" });
                if (lastChoice == 2) { yield return Say(who, "casino.bye"); yield break; }
                bool onBig = lastChoice == 0;
                yield return Ask(who, "casino.stake", DiceGame.Stakes.Select(st => "stake:" + st).Append("casino.back").ToArray());
                if (lastChoice >= DiceGame.Stakes.Length) continue;
                int stake = DiceGame.Stakes[lastChoice];
                if (stake > Run.Progress.Inventory.Copper) { yield return Say(who, "casino.broke"); continue; }

                // The cup shakes, then lifts.
                var dice = DiceGame.Roll(Run.Random);
                DiceRolling = true;
                for (float t = 0f; t < 1.1f; t += 0.08f)
                {
                    Dice = DiceGame.Roll(Run.Random);
                    Changed?.Invoke();
                    yield return new WaitForSeconds(0.08f);
                }
                DiceRolling = false;
                Dice = dice;
                var outcome = DiceGame.Bet(Run.Progress.Inventory, stake, onBig, dice);
                Changed?.Invoke();
                string throwKey = $"{dice[0]}+{dice[1]}+{dice[2]}={dice.Sum()}";
                string result = DiceGame.IsTriple(dice) ? "triple" : DiceGame.IsBig(dice) ? "big" : "small";
                yield return Say(who, $"casino.result:{result}:{throwKey}");
                yield return Say("system", (outcome == DiceGame.Outcome.Won ? "casino.won:" : "casino.lost:") + stake + ":" + Run.Progress.Inventory.Copper);
                Dice = null;
                Changed?.Invoke();
            }
        }

        // ---------- the archery range ----------

        IEnumerator OfferArchery()
        {
            Face(Archer);
            string who = Speaker(Archer);
            yield return Ask(who, "archery.ask", new[] { "archery.try", "archery.no" });
            if (lastChoice != 0) { yield return Say(who, "archery.bye"); yield break; }
            Line = null;
            Busy = false;               // OpenPanel takes the town again
            OpenArchery();
        }

        // A fresh round on the range in its panel (also the -panel archery screenshot probe).
        public void OpenArchery()
        {
            Archery = new ArcheryGame(Run.Random);
            OpenPanel(ArcheryPanel);
        }

        // The archery panel calls this when the round is over (or abandoned): rewards once between battles.
        public void FinishArchery()
        {
            if (Panel != ArcheryPanel) return;
            var game = Archery;
            ClosePanel();
            StartCoroutine(Hold(ArcheryResult(game)));
        }

        IEnumerator ArcheryResult(ArcheryGame game)
        {
            string who = Speaker(Archer);
            if (game == null || !game.Finished) { yield return Say(who, "archery.quit"); yield break; }
            yield return Say(who, "archery.score:" + game.Score);
            if (game.Score > 0 && Run.Life.CanEarnArchery(Run.Story.BattlesDone))
            {
                Run.Life.EarnedArchery(Run.Story.BattlesDone);
                Run.Progress.Inventory.AddCopper(game.CopperReward);
                yield return Say("system", "archery.reward:" + game.CopperReward + ":" + Run.Progress.Inventory.Copper);
                int before = Run.Life.Reputation;
                Run.Life.AddReputation(game.ReputationReward);
                yield return ReputationChanged(Run.Life.Reputation - before);
            }
            else if (game.Score > 0) yield return Say(who, "archery.no_reward");
            Archery = null;
        }

        // "N30 乞丐" → "npc.N30" (StoryText names the speaker).
        static string Speaker(Transform npc) => npc ? "npc." + ResidentId(npc) : "narrator";
    }
}
