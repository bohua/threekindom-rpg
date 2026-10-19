using NUnit.Framework;
using UnityEngine;
using Jrpg;

public sealed class BattleTests
{
    CharacterDefinition fast, slow;
    SkillDefinition skill;
    [SetUp] public void Setup()
    {
        fast = ScriptableObject.CreateInstance<CharacterDefinition>(); fast.MaxHp = 20; fast.Speed = 7; fast.Attack = 10;
        slow = ScriptableObject.CreateInstance<CharacterDefinition>(); slow.MaxHp = 9; slow.Speed = 2; slow.Defense = 1;
        skill = ScriptableObject.CreateInstance<SkillDefinition>(); skill.Power = 5;
    }
    [TearDown] public void Cleanup() { Object.DestroyImmediate(fast); Object.DestroyImmediate(slow); Object.DestroyImmediate(skill); }
    [Test] public void DamageClampsToRemainingHpAndDoesNotMutateAsset()
    {
        var a = new Combatant(fast); var b = new Combatant(slow);
        Assert.AreEqual(9, a.Hit(b, skill)); Assert.AreEqual(0, b.Hp); Assert.IsFalse(b.Alive); Assert.AreEqual(9, slow.MaxHp);
        Assert.Throws<System.InvalidOperationException>(() => a.Hit(b, skill));
    }
    [Test] public void TurnOrderUsesSpeedAndSkipsDefeatedUnits()
    {
        var a = new Combatant(fast); var b = new Combatant(slow); var battle = new TurnBattle(new[] { b, a });
        Assert.AreSame(a, battle.Current); a.Hit(b, skill); battle.Next(); Assert.AreSame(a, battle.Current);
    }
    [Test] public void QuestIgnoresOtherTargetsAndCapsProgress()
    {
        var quest = ScriptableObject.CreateInstance<QuestDefinition>();
        try {
            quest.TargetId = "slime"; quest.RequiredCount = 2; var state = new QuestProgress(quest);
            state.Record("wolf"); Assert.AreEqual(0, state.Count);
            state.Record("slime"); Assert.IsFalse(state.Complete);
            state.Record("slime"); state.Record("slime"); Assert.AreEqual(2, state.Count); Assert.IsTrue(state.Complete);
        } finally { Object.DestroyImmediate(quest); }
    }
}
