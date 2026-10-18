using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jrpg
{
    public sealed class QuestProgress
    {
        public QuestDefinition Definition { get; }
        public int Count { get; private set; }
        public bool Complete => Count >= Math.Max(1, Definition.RequiredCount);
        public QuestProgress(QuestDefinition definition) => Definition = definition;
        public void Record(string targetId)
        {
            if (targetId == Definition.TargetId && !Complete) Count++;
        }
    }

    public sealed class Combatant
    {
        public CharacterDefinition Definition { get; }
        public int Hp { get; private set; }
        public bool Alive => Hp > 0;
        public Combatant(CharacterDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Hp = Math.Max(1, definition.MaxHp);
        }
        public int Hit(Combatant target, SkillDefinition skill)
        {
            if (!Alive || !target.Alive) throw new InvalidOperationException("Combatant is defeated.");
            int damage = Math.Max(1, Definition.Attack + Math.Max(0, skill.Power) - target.Definition.Defense);
            int actual = Math.Min(target.Hp, damage);
            target.Hp -= actual;
            return actual;
        }
    }

    // Stable speed order: equal speeds preserve roster order. No random or frame-dependent rules.
    public sealed class TurnBattle
    {
        readonly List<Combatant> order;
        int cursor;
        public Combatant Current => order[cursor];
        public TurnBattle(IEnumerable<Combatant> roster)
        {
            order = new List<Combatant>(roster);
            if (order.Count == 0) throw new ArgumentException("Empty roster.");
            var indexed = new List<(Combatant unit, int index)>();
            for (int i = 0; i < order.Count; i++) indexed.Add((order[i], i));
            indexed.Sort((a, b) => {
                int speed = b.unit.Definition.Speed.CompareTo(a.unit.Definition.Speed);
                return speed != 0 ? speed : a.index.CompareTo(b.index);
            });
            for (int i = 0; i < order.Count; i++) order[i] = indexed[i].unit;
        }
        public void Next()
        {
            for (int i = 0; i < order.Count; i++)
            {
                cursor = (cursor + 1) % order.Count;
                if (Current.Alive) return;
            }
            throw new InvalidOperationException("No living combatants.");
        }
    }
}

