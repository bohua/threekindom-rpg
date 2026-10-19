using System.Collections.Generic;
using System.Linq;

namespace Jrpg.Battle
{
    // 首版固定、可解释的敌方AI（V1_SPEC §4.3）。
    // 只读取回合开始时的战场状态，必须在我方指令执行前调用，且不接收我方指令。
    // 规则：
    //  1. 目标＝本兵种克制的我方存活部队；没有则取当前兵力最少者；同值按出战位靠前。
    //  2. 若掌握可负担的单体伤害技能，对该目标使用第一项；否则若掌握可负担的全体伤害技能且我方存活≥2，使用之；
    //     其余情况普通攻击。
    public static class EnemyAi
    {
        public static List<BattleCommand> Decide(BattleState state)
        {
            var commands = new List<BattleCommand>();
            var living = state.Players.Where(u => u.Active).ToList();
            foreach (var enemy in state.Enemies.Where(u => u.Active))
            {
                if (living.Count == 0) break;
                var target = PickTarget(enemy, living);
                var single = enemy.Skills.FirstOrDefault(s => s.Target == SkillTarget.SingleEnemy && s.PowerPercent > 0 && s.TechCost <= enemy.Tech);
                var area = enemy.Skills.FirstOrDefault(s => s.Target == SkillTarget.AllEnemies && s.PowerPercent > 0 && s.TechCost <= enemy.Tech);
                if (single != null) commands.Add(BattleCommand.UseSkill(enemy, single, target));
                else if (area != null && living.Count >= 2) commands.Add(BattleCommand.UseSkill(enemy, area));
                else commands.Add(BattleCommand.Attack(enemy, target));
            }
            return commands;
        }

        public static BattleUnit PickTarget(BattleUnit enemy, IReadOnlyList<BattleUnit> living)
        {
            var countered = living.Where(u => BattleRules.HasAdvantage(enemy.Troop, u.Troop)).OrderBy(u => u.Slot).FirstOrDefault();
            return countered ?? living.OrderBy(u => u.Troops).ThenBy(u => u.Slot).First();
        }
    }
}
