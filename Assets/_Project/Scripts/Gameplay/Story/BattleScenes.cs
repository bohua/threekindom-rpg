using System;
using System.Collections.Generic;

namespace ThreeKingdoms.Gameplay.Story
{
    // The talk before and after each Yellow Turban battle (user 2026-10-22): the leader blocking the south gate calls
    // out, the brothers answer, and the battle starts; on victory the leader retreats with a parting line (V1_SPEC §2.2:
    // Zhang Liang and the three brothers withdraw, nobody dies). Keys only; StoryText holds the text.
    public static class BattleScenes
    {
        static readonly Dictionary<string, DialogueLine[]> Before = new Dictionary<string, DialogueLine[]>
        {
            ["battle_1"] = new[]
            {
                new DialogueLine("cheng_yuanzhi", "foe.battle_1.1"), new DialogueLine("liu_bei", "foe.battle_1.2"),
                new DialogueLine("zhang_fei", "foe.battle_1.3"),
            },
            ["battle_2"] = new[]
            {
                new DialogueLine("zhang_liang", "foe.battle_2.1"), new DialogueLine("guan_yu", "foe.battle_2.2"),
                new DialogueLine("liu_bei", "foe.battle_2.3"),
            },
            ["battle_3"] = new[]
            {
                new DialogueLine("zhang_jiao", "foe.battle_3.1"), new DialogueLine("zhang_bao", "foe.battle_3.2"),
                new DialogueLine("liu_bei", "foe.battle_3.3"), new DialogueLine("zhang_fei", "foe.battle_3.4"),
            },
        };

        static readonly Dictionary<string, DialogueLine[]> After = new Dictionary<string, DialogueLine[]>
        {
            ["battle_1"] = new[]
            {
                new DialogueLine("cheng_yuanzhi", "won.battle_1.1"), new DialogueLine("liu_bei", "won.battle_1.2"),
            },
            ["battle_2"] = new[]
            {
                new DialogueLine("zhang_liang", "won.battle_2.1"), new DialogueLine("guan_yu", "won.battle_2.2"),
                new DialogueLine("liu_bei", "won.battle_2.3"),
            },
            ["battle_3"] = new[]
            {
                new DialogueLine("zhang_jiao", "won.battle_3.1"), new DialogueLine("zhang_bao", "won.battle_3.2"),
                new DialogueLine("liu_bei", "won.battle_3.3"),
            },
        };

        public static IReadOnlyList<DialogueLine> BeforeBattle(string battleId) =>
            battleId != null && Before.TryGetValue(battleId, out var l) ? l : Array.Empty<DialogueLine>();

        public static IReadOnlyList<DialogueLine> AfterVictory(string battleId) =>
            battleId != null && After.TryGetValue(battleId, out var l) ? l : Array.Empty<DialogueLine>();
    }
}
