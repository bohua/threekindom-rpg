using System;
using System.Collections.Generic;
using System.Linq;
using ThreeKingdoms.Core.Economy;

namespace ThreeKingdoms.Core.Town
{
    // Life in Zhuojun outside the main line (user 2026-10-22): Liu Bei's reputation, raised by giving alms, answering
    // the scholar's questions and doing well at the archery range. Saved with the play-through. Values are first
    // drafts, to be tuned in playtests.
    public sealed class TownLife
    {
        public int Reputation { get; private set; }
        // The scholar quizzes and the range pays out once between battles: the number of battles won when last done,
        // or −1 (keeps reputation and copper from being farmed in one stay).
        public int QuizTakenAt { get; private set; } = -1;
        public int ArcheryRewardedAt { get; private set; } = -1;

        public TownLife(int reputation = 0, int quizTakenAt = -1, int archeryRewardedAt = -1)
        {
            Reputation = Math.Max(0, reputation);
            QuizTakenAt = quizTakenAt;
            ArcheryRewardedAt = archeryRewardedAt;
        }

        public bool CanTakeQuiz(int battlesDone) => QuizTakenAt != battlesDone;
        public void TookQuiz(int battlesDone) => QuizTakenAt = battlesDone;
        public bool CanEarnArchery(int battlesDone) => ArcheryRewardedAt != battlesDone;
        public void EarnedArchery(int battlesDone) => ArcheryRewardedAt = battlesDone;

        public void AddReputation(int delta) => Reputation = Math.Max(0, Reputation + delta);
    }

    // Alms for the beggar: how much, and what it does for Liu Bei's name.
    public static class Charity
    {
        public static readonly int[] Amounts = { 5, 20, 50 };
        public static readonly int[] Reputation = { 1, 3, 6 };

        public enum Result { Given, NotEnoughCopper, Declined }

        // amountIndex −1 = gives nothing.
        public static Result Give(Inventory inventory, TownLife life, int amountIndex)
        {
            if (amountIndex < 0 || amountIndex >= Amounts.Length) return Result.Declined;
            if (!inventory.SpendCopper(Amounts[amountIndex])) return Result.NotEnoughCopper;
            life.AddReputation(Reputation[amountIndex]);
            return Result.Given;
        }
    }

    // The scholar's three questions on the Romance (user 2026-10-22): one point of reputation per right answer, and
    // a loss of two when all three are wrong.
    public static class ScholarQuiz
    {
        public const int Questions = 3, AllWrongPenalty = -2;

        public static int ReputationFor(int correct) => correct <= 0 ? AllWrongPenalty : correct;

        // Three different questions from the bank, in random order.
        public static IReadOnlyList<int> Pick(int bankSize, Random rng) =>
            Enumerable.Range(0, bankSize).OrderBy(_ => rng.Next()).Take(Questions).ToList();
    }

    // Big or small with three dice (user 2026-10-22): 4–10 small, 11–17 big, a triple and the house takes all.
    // A won bet pays even money.
    public static class DiceGame
    {
        public static readonly int[] Stakes = { 10, 20, 50 };

        public enum Outcome { Won, Lost, NotEnoughCopper }

        public static int[] Roll(Random rng) => new[] { rng.Next(1, 7), rng.Next(1, 7), rng.Next(1, 7) };

        public static bool IsTriple(int[] dice) => dice[0] == dice[1] && dice[1] == dice[2];
        public static bool IsBig(int[] dice) => !IsTriple(dice) && dice.Sum() >= 11;
        public static bool IsSmall(int[] dice) => !IsTriple(dice) && dice.Sum() <= 10;

        public static Outcome Bet(Inventory inventory, int stake, bool onBig, int[] dice)
        {
            if (stake <= 0 || stake > inventory.Copper) return Outcome.NotEnoughCopper;
            bool won = onBig ? IsBig(dice) : IsSmall(dice);
            if (won) inventory.AddCopper(stake);
            else inventory.SpendCopper(stake);
            return won ? Outcome.Won : Outcome.Lost;
        }
    }
}
