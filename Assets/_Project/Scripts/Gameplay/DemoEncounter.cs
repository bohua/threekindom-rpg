using UnityEngine;
using UnityEngine.InputSystem;

namespace Jrpg
{
    public sealed class DemoEncounter : MonoBehaviour
    {
        public PlayerMover Player;
        public Transform Enemy;
        public CharacterDefinition HeroDefinition;
        public CharacterDefinition EnemyDefinition;
        public SkillDefinition AttackSkill;
        public QuestDefinition Quest;
        public InputActionReference InteractAction;
        public InputActionReference MenuAction;
        Combatant hero, enemy;
        TurnBattle battle;
        QuestProgress progress;
        string message = "Explore the glade. Approach the slime and press E.";
        bool fighting;
        void Awake() => progress = new QuestProgress(Quest);
        void OnEnable() { if (InteractAction) InteractAction.action.Enable(); if (MenuAction) MenuAction.action.Enable(); }
        void OnDisable() { if (InteractAction) InteractAction.action.Disable(); if (MenuAction) MenuAction.action.Disable(); }
        void Update()
        {
            if (MenuAction && MenuAction.action.WasPressedThisFrame()) Application.Quit();
            bool interact = InteractAction && InteractAction.action.WasPressedThisFrame();
            if (fighting && interact) Attack();
            else if (!fighting && Enemy && Vector3.Distance(Player.transform.position, Enemy.position) < 3 && interact)
            {
                hero = new Combatant(HeroDefinition);
                enemy = new Combatant(EnemyDefinition);
                battle = new TurnBattle(new[] { hero, enemy });
                fighting = true;
                Player.InputEnabled = false;
                message = "Battle started. Choose Attack.";
                EnemyTurn();
            }
        }
        void EnemyTurn()
        {
            if (battle.Current == enemy && enemy.Alive && hero.Alive)
            {
                message += $" Slime dealt {enemy.Hit(hero, AttackSkill)} damage.";
                battle.Next();
            }
            if (!hero.Alive) Finish(false);
        }
        void Attack()
        {
            if (battle.Current != hero) return;
            message = $"Hero dealt {hero.Hit(enemy, AttackSkill)} damage.";
            if (!enemy.Alive) { Finish(true); return; }
            battle.Next();
            EnemyTurn();
        }
        void Finish(bool won)
        {
            fighting = false;
            Player.InputEnabled = true;
            if (won)
            {
                progress.Record(EnemyDefinition.Id);
                Destroy(Enemy.gameObject);
                message = "Victory! Quest complete: clear the glade.";
            }
            else message = "Defeated. Press E near the slime to retry.";
        }
        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(16, 16, 540, 200), GUI.skin.box);
            GUILayout.Label("MOONLIT GLADE | HD-2D JRPG prototype");
            GUILayout.Label("WASD / arrows / stick: move | E / A: encounter, attack | Esc / Start: quit");
            GUILayout.Label(message);
            GUILayout.Label($"Quest: {Quest.DisplayName} ({progress.Count}/{Quest.RequiredCount})");
            if (fighting)
            {
                GUILayout.Label($"Hero HP {hero.Hp} | Slime HP {enemy.Hp}");
                if (GUILayout.Button("Attack")) Attack();
            }
            GUILayout.EndArea();
        }
    }
}
