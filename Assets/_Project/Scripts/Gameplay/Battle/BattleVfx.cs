using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ThreeKingdoms.Core.Battle;
using UnityEngine;

namespace ThreeKingdoms.Gameplay.Battle
{
    // Attack and skill effects (user 2026-10-22: showy but never slow, 0.3–0.6 s each, faster on 「加速」). Built at
    // runtime from line strokes and square pixel particles in one additive material, keyed off the playback events:
    //   attack: infantry a slash arc, cavalry a thrust streak with a ring, archers an arrow with a trail;
    //   skills: 突击 a green blade rush, 破阵 a cracking ring, 横扫 a crescent wave over all enemies, 威慑 red roar
    //   rings, 火矢 a fire arrow bursting into flame, 整队 a rising golden ring; plus hit sparks, heals, debuff
    //   marks, the guard shield and dust when a unit falls back. The Codex sprite-sheet effects (art/battle-vfx)
    //   replace these strokes when delivered.
    public sealed class BattleVfx : MonoBehaviour
    {
        public BattleController Controller;
        [Tooltip("Additive particle material (URP Particles/Unlit) for strokes and sparks")] public Material Additive;
        public Transform ShakeTarget;

        static readonly Color Gold = new Color(1f, 0.85f, 0.45f), White = new Color(1f, 0.97f, 0.9f);
        static readonly Color Jade = new Color(0.35f, 1f, 0.65f), Flame = new Color(1f, 0.5f, 0.12f), Ember = new Color(1f, 0.25f, 0.05f);
        static readonly Color Blood = new Color(0.9f, 0.15f, 0.12f), Heal = new Color(0.55f, 1f, 0.45f), Sky = new Color(0.45f, 0.75f, 1f);

        // Effects started, for tests: "slash", "fire_arrows", …
        public readonly List<string> Played = new List<string>();

        float Rate => Controller ? Mathf.Max(0.5f, Controller.PlaybackRate) : 1f;

        void OnEnable() { if (Controller) Controller.EventPlayed += OnEvent; }
        void OnDisable() { if (Controller) Controller.EventPlayed -= OnEvent; }

        Vector3 At(BattleUnit u)
        {
            var v = Controller ? Controller.ViewOf(u) : null;
            return v ? v.transform.position + new Vector3(0f, 0.9f, -0.4f) : Vector3.zero;
        }

        void OnEvent(BattleEvent e)
        {
            if (!Additive) return;
            switch (e.Kind)
            {
                case BattleEventKind.ActionStarted: StartCoroutine(Action(e)); break;
                case BattleEventKind.Damage: Spark(At(e.Target), e.Advantage); break;
                case BattleEventKind.Heal:
                case BattleEventKind.ItemUsed: HealGlow(At(e.Target), e.Kind == BattleEventKind.ItemUsed); break;
                case BattleEventKind.ModifierApplied: Debuff(At(e.Target)); break;
                case BattleEventKind.Defend: Shield(At(e.Actor)); break;
                case BattleEventKind.Retreated: Dust(At(e.Target)); break;
            }
        }

        // Screenshots (-vfx): play one effect between the first general and the first enemy.
        public void Preview(string what)
        {
            var s = Controller.Session.State;
            var actor = what == "slash" ? s.Players.First(u => u.Troop == TroopType.Infantry)
                : what == "thrust" ? s.Players.First(u => u.Troop == TroopType.Cavalry) : s.Players.First(u => u.Troop == TroopType.Archer);
            var target = s.Enemies.First(u => u.Active);
            var skill = actor.Skills.Concat(s.Players.SelectMany(p => p.Skills)).FirstOrDefault(k => k.Id == what);
            if (what == "heal") { HealGlow(At(actor), false); return; }
            StartCoroutine(Action(actor, target, skill != null ? CommandKind.Skill : CommandKind.Attack, skill));
        }

        IEnumerator Action(BattleEvent e) => Action(e.Actor, e.Target, e.Command, e.Skill);

        IEnumerator Action(BattleUnit actorUnit, BattleUnit targetUnit, CommandKind command, SkillSpec skill)
        {
            var e = (Actor: actorUnit, Target: targetUnit, Command: command, Skill: skill);
            Vector3 from = At(e.Actor), to = e.Target != null ? At(e.Target) : from;
            if (e.Command == CommandKind.Attack)
            {
                switch (e.Actor.Troop)
                {
                    case TroopType.Archer:
                        Played.Add("arrow");
                        yield return Projectile(from, to, White, Gold, 0.22f, false);
                        break;
                    case TroopType.Cavalry:
                        Played.Add("thrust");
                        yield return Wait(0.12f);
                        Streak(to - (to - from).normalized * 1.8f, to + (to - from).normalized * 0.8f, White, 0.45f, 0.16f);
                        Ring(to, Gold, 0.9f, 0.3f, 0.15f);
                        break;
                    default:
                        Played.Add("slash");
                        yield return Wait(0.12f);
                        Slash(to, from.x < to.x ? 1f : -1f, White, 1.1f, 0.18f);
                        break;
                }
                yield break;
            }
            if (e.Command != CommandKind.Skill || e.Skill == null) yield break;
            Played.Add(e.Skill.Id);
            Shake(0.12f, 0.06f);
            switch (e.Skill.Id)
            {
                case "charge":
                case "enemy_assault":
                    Burst(from, Jade, White, 27, 3f, 0.24f, 0.3f);
                    yield return Wait(0.1f);
                    Streak(from, to, Jade, 0.7f, 0.2f);
                    Slash(to, from.x < to.x ? 1f : -1f, Jade, 1.4f, 0.2f);
                    break;
                case "break_formation":
                    yield return Wait(0.12f);
                    Slash(to, from.x < to.x ? 1f : -1f, Jade, 1.2f, 0.16f);
                    Ring(to, Jade, 1.3f, 0.35f, 0.20f);
                    Burst(to, Sky, White, 36, 5f, 0.21f, 0.35f);
                    break;
                case "sweep":
                case "enemy_rampage":
                {
                    // A crescent wave rolls across the whole enemy line.
                    var foes = e.Actor.Side == Side.Player ? Controller.Session.State.Enemies : Controller.Session.State.Players;
                    float dir = Mathf.Sign(to.x - from.x); if (dir == 0f) dir = e.Actor.Side == Side.Player ? 1f : -1f;
                    foreach (var f in foes) if (f.Active) Slash(At(f), dir, Gold, 1.6f, 0.22f);
                    foreach (var f in foes) if (f.Active) Burst(At(f) + Vector3.down * 0.7f, new Color(0.8f, 0.65f, 0.4f), Gold, 21, 2.5f, 0.30f, 0.4f);
                    break;
                }
                case "intimidate":
                case "enemy_rumour":
                    for (int i = 0; i < 3; i++) { Ring(from, Blood, 2.2f + i * 0.8f, 0.4f, 0.25f); yield return Wait(0.08f); }
                    break;
                case "fire_arrows":
                case "enemy_arson":
                    yield return Projectile(from + Vector3.up * 0.4f, to, Flame, Ember, 0.26f, true);
                    Burst(to, Flame, Ember, 60, 5f, 0.36f, 0.45f);
                    Ring(to, Flame, 1.2f, 0.3f, 0.25f);
                    break;
                case "regroup":
                    Ring(to + Vector3.down * 0.7f, Gold, 1.1f, 0.4f, 0.20f);
                    break;
                default:
                    Burst(from, Gold, White, 24, 3f, 0.24f, 0.3f);
                    break;
            }
        }

        WaitForSeconds Wait(float seconds) => new WaitForSeconds(seconds / Rate);

        // ---------- pieces ----------

        LineRenderer Line(string name, float width, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = Additive;
            lr.useWorldSpace = true;
            lr.widthMultiplier = width;
            lr.numCapVertices = 2;
            lr.startColor = lr.endColor = color;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return lr;
        }

        // A sword stroke: an arc swept across the target and faded.
        void Slash(Vector3 at, float dir, Color color, float radius, float duration) => StartCoroutine(SlashRoutine(at, dir, color, radius, duration / Rate));

        IEnumerator SlashRoutine(Vector3 at, float dir, Color color, float radius, float duration)
        {
            var lr = Line("Slash", 0.5f, color);
            const int n = 14;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration, sweep = Mathf.Clamp01(k * 1.6f);
                lr.positionCount = n;
                for (int i = 0; i < n; i++)
                {
                    float a = Mathf.Lerp(130f, -40f, sweep * i / (n - 1f)) * Mathf.Deg2Rad;
                    lr.SetPosition(i, at + new Vector3(Mathf.Cos(a) * radius * dir * 0.8f, Mathf.Sin(a) * radius, 0f));
                }
                lr.widthMultiplier = 0.55f * (1f - k) + 0.08f;
                var c = color; c.a = 1f - k * k;
                lr.startColor = new Color(c.r, c.g, c.b, c.a * 0.2f); lr.endColor = c;
                yield return null;
            }
            Destroy(lr.gameObject);
            Burst(at, color, White, 18, 4f, 0.2f, 0.3f);
        }

        // A straight streak whose head runs from a to b, the tail following.
        void Streak(Vector3 a, Vector3 b, Color color, float width, float duration) => StartCoroutine(StreakRoutine(a, b, color, width, duration / Rate));

        IEnumerator StreakRoutine(Vector3 a, Vector3 b, Color color, float width, float duration)
        {
            var lr = Line("Streak", width, color);
            lr.positionCount = 2;
            for (float t = 0f; t < duration * 1.5f; t += Time.deltaTime)
            {
                float head = Mathf.Clamp01(t / duration), tail = Mathf.Clamp01((t - duration * 0.5f) / duration);
                lr.SetPosition(0, Vector3.Lerp(a, b, tail));
                lr.SetPosition(1, Vector3.Lerp(a, b, head));
                lr.startColor = new Color(color.r, color.g, color.b, 0.1f); lr.endColor = color;
                yield return null;
            }
            Destroy(lr.gameObject);
        }

        // An expanding ring that thins and fades.
        void Ring(Vector3 at, Color color, float radius, float duration, float width) => StartCoroutine(RingRoutine(at, color, radius, duration / Rate, width));

        IEnumerator RingRoutine(Vector3 at, Color color, float radius, float duration, float width)
        {
            var lr = Line("Ring", width, color);
            lr.loop = true;
            const int n = 28;
            lr.positionCount = n;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration, r = radius * (0.3f + 0.7f * Mathf.Sqrt(k));
                for (int i = 0; i < n; i++)
                {
                    float a = i * Mathf.PI * 2f / n;
                    lr.SetPosition(i, at + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.45f, 0f));
                }
                var c = color; c.a = 1f - k;
                lr.startColor = lr.endColor = c;
                lr.widthMultiplier = width * (1.4f - k);
                yield return null;
            }
            Destroy(lr.gameObject);
        }

        // An arrow (or fire arrow) flying a shallow arc to the target, leaving a trail of sparks.
        IEnumerator Projectile(Vector3 from, Vector3 to, Color color, Color trail, float duration, bool burning)
        {
            duration /= Rate;
            var lr = Line("Arrow", burning ? 0.32f : 0.16f, color);
            lr.positionCount = 2;
            var sparks = Emitter("Arrow trail", trail, color, burning ? 0.3f : 0.14f, burning ? 0.4f : 0.22f);
            var em = sparks.emission; em.rateOverDistance = burning ? 26f : 10f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                Vector3 p = Vector3.Lerp(from, to, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.8f;
                Vector3 back = Vector3.Lerp(from, to, Mathf.Max(0f, k - 0.12f)) + Vector3.up * Mathf.Sin(Mathf.Max(0f, k - 0.12f) * Mathf.PI) * 0.8f;
                lr.SetPosition(0, back); lr.SetPosition(1, p);
                lr.startColor = new Color(color.r, color.g, color.b, 0f); lr.endColor = color;
                sparks.transform.position = p;
                yield return null;
            }
            Destroy(lr.gameObject);
            em.rateOverDistance = 0f;
            Destroy(sparks.gameObject, 0.6f);
            Burst(to, color, White, burning ? 30 : 14, 4f, 0.2f, 0.3f);
        }

        void Spark(Vector3 at, bool advantage)
        {
            Burst(at, advantage ? Gold : White, advantage ? Flame : Gold, advantage ? 30 : 18, advantage ? 6f : 4.5f, 0.22f, 0.3f);
            if (advantage) Shake(0.1f, 0.05f);
        }

        void HealGlow(Vector3 at, bool ration)
        {
            Played.Add(ration ? "ration" : "heal");
            var ps = Emitter("Heal", Heal, Gold, 0.24f, 0.6f);
            var main = ps.main; main.startSpeed = 0f; main.gravityModifier = -0.6f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = 0.6f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            ps.transform.position = at + Vector3.down * 0.8f;
            ps.Emit(28);
            Destroy(ps.gameObject, 1f);
            Ring(at + Vector3.down * 0.7f, Heal, 1f, 0.45f, 0.15f);
        }

        void Debuff(Vector3 at)
        {
            Played.Add("debuff");
            var ps = Emitter("Debuff", Blood, new Color(0.5f, 0.1f, 0.6f), 0.26f, 0.5f);
            var main = ps.main; main.startSpeed = 0f; main.gravityModifier = 0.5f;
            ps.transform.position = at + Vector3.up * 0.8f;
            ps.Emit(14);
            Destroy(ps.gameObject, 0.9f);
        }

        void Shield(Vector3 at)
        {
            Played.Add("defend");
            Ring(at, Sky, 0.9f, 0.4f, 0.25f);
            Burst(at, Sky, White, 15, 1.5f, 0.18f, 0.3f);
        }

        void Dust(Vector3 at)
        {
            var ps = Emitter("Dust", new Color(0.7f, 0.6f, 0.45f), new Color(0.45f, 0.38f, 0.3f), 0.4f, 0.6f);
            var main = ps.main; main.startSpeed = 1.2f; main.gravityModifier = -0.1f;
            ps.transform.position = at + Vector3.down * 0.8f;
            ps.Emit(20);
            Destroy(ps.gameObject, 1f);
        }

        void Burst(Vector3 at, Color a, Color b, int count, float speed, float size, float life)
        {
            var ps = Emitter("Burst", a, b, size, life);
            var main = ps.main; main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            ps.transform.position = at;
            ps.Emit(count);
            Destroy(ps.gameObject, life / Rate + 0.3f);
        }

        // A one-shot emitter of square pixel sparks that shrink and fade.
        ParticleSystem Emitter(string name, Color a, Color b, float size, float life)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 1f; main.loop = false; main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.startColor = new ParticleSystem.MinMaxGradient(a, b);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.simulationSpeed = Rate;
            main.maxParticles = 200;
            var em = ps.emission; em.rateOverTime = 0f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.15f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Additive;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        void Shake(float duration, float amount) { if (ShakeTarget) StartCoroutine(ShakeRoutine(duration / Rate, amount)); }

        IEnumerator ShakeRoutine(float duration, float amount)
        {
            var home = ShakeTarget.localPosition;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                ShakeTarget.localPosition = home + (Vector3)(Random.insideUnitCircle * amount * (1f - t / duration));
                yield return null;
            }
            ShakeTarget.localPosition = home;
        }
    }
}
