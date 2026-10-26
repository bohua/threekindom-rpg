using System;
using System.Collections.Generic;

namespace ThreeKingdoms.Gameplay.Story
{
    // One step of a 0.2 cutscene (V02_HUANGJIN_PLAN §5): a line, a choice with branches, a place-and-year card, a march
    // on the realm map, a memory, a scene change … Scripts are data (ChapterScripts); StagePlayer plays them in the story
    // stage scene and in the walkable scenes alike. Text is only keys (ChapterText in the UI layer).
    public enum StageOp
    {
        Line, Choice, Card, Background, Actor, Move, Hide, Face, Remember, Flag, Scroll, March, Fade, Rations,
        Advance, Load, Battle, Wait, Music, Reputation, Item, Autosave, Shake, Flash, Call, Comic, Pose, Letterbox, Focus,
    }

    public sealed class StageStep
    {
        public StageOp Op;
        public string A, B, C, D;          // meaning per op (see the factory methods)
        public float X, Y, Seconds;
        public string[] Options = Array.Empty<string>();
        public StageStep[][] Branches = Array.Empty<StageStep[]>();
        // Call: game logic run when the step is reached (a walkable scene's checks: copper, flags, panels).
        public Action Callback;
        // Plays only when this flag is set ("!" in front: only when it is not).
        public string If;

        public StageStep When(string flag) { If = flag; return this; }
        public override string ToString() => $"{Op} {A} {B}";
    }

    // Factory methods so the scripts read like a screenplay.
    public static class S
    {
        // speaker, line key
        public static StageStep L(string speaker, string key) => new StageStep { Op = StageOp.Line, A = speaker, B = key };
        // A timed line (automatic scenes like the oath, user 2026-10-26): moves on by itself after seconds; confirm moves on early.
        public static StageStep TL(string speaker, string key, float seconds) => new StageStep { Op = StageOp.Line, A = speaker, B = key, Seconds = seconds };
        // A story pose (the 鞭督邮 set, DuyouPoseImporter) held until changed; -1 back to standing. flip: face screen left.
        public static StageStep Pose(string id, int pose, bool flip = false) => new StageStep { Op = StageOp.Pose, A = id, X = pose, C = flip ? "flip" : null };
        // Letterbox bars and subtitle dialogue while an automatic scene plays.
        public static StageStep Letterbox(bool on) => new StageStep { Op = StageOp.Letterbox, A = on ? "on" : null };
        // The camera looks at a point of the scene from a distance (0 = back to following the player).
        public static StageStep Focus(float x, float z, float distance) => new StageStep { Op = StageOp.Focus, X = x, Y = z, Seconds = distance };
        // Like Move, but the script goes on while the actor walks (two people moving together).
        public static StageStep MoveWith(string id, float x, float y, float seconds = 1f) => new StageStep { Op = StageOp.Move, A = id, X = x, Y = y, Seconds = seconds, D = "async" };
        // Several lines in a row: pairs of speaker, key.
        public static IEnumerable<StageStep> Lines(params string[] pairs)
        {
            for (int i = 0; i + 1 < pairs.Length; i += 2) yield return L(pairs[i], pairs[i + 1]);
        }
        // A choice: the prompt line, the option keys and one branch per option; Flag(choiceFlag + index) is set.
        public static StageStep Choice(string speaker, string prompt, string choiceFlag, string[] options, params StageStep[][] branches) =>
            new StageStep { Op = StageOp.Choice, A = speaker, B = prompt, C = choiceFlag, Options = options, Branches = branches };
        // A black card with white text (place and year), seconds on screen.
        public static StageStep Card(string key, float seconds = 2f) => new StageStep { Op = StageOp.Card, A = key, Seconds = seconds };
        // A comic (连环画) panel stays on screen behind the following lines until the next panel or ComicEnd (user 2026-10-25).
        public static StageStep Comic(string panel) => new StageStep { Op = StageOp.Comic, A = panel };
        public static StageStep ComicEnd() => new StageStep { Op = StageOp.Comic, A = null };
        public static StageStep Background(string id) => new StageStep { Op = StageOp.Background, A = id };
        // An actor (id, sprite key) appears at a normalised stage position (0–1, left to right / bottom to top), facing
        // "left", "right", "front" or "back". In a walkable scene, x and y are ignored when the actor already stands there.
        public static StageStep Actor(string id, string sprite, float x, float y, string facing = "front") =>
            new StageStep { Op = StageOp.Actor, A = id, B = sprite, C = facing, X = x, Y = y };
        public static StageStep Move(string id, float x, float y, float seconds = 1f) => new StageStep { Op = StageOp.Move, A = id, X = x, Y = y, Seconds = seconds };
        public static StageStep Face(string id, string facing) => new StageStep { Op = StageOp.Face, A = id, C = facing };
        public static StageStep Hide(string id) => new StageStep { Op = StageOp.Hide, A = id };
        // who, key, value, text key (SYSTEMS_V02 §3.3).
        public static StageStep Remember(string who, string key, string value, string textKey) =>
            new StageStep { Op = StageOp.Remember, A = who, B = key, C = value, D = textKey };
        public static StageStep Flag(string flag) => new StageStep { Op = StageOp.Flag, A = flag };
        public static StageStep Scroll(string id) => new StageStep { Op = StageOp.Scroll, A = id };
        // from node, to node, year label key; via event flag optional.
        public static StageStep March(string from, string to, string yearKey) => new StageStep { Op = StageOp.March, A = from, B = to, C = yearKey };
        public static StageStep Fade(float to, float seconds = 0.4f) => new StageStep { Op = StageOp.Fade, X = to, Seconds = seconds };
        public static StageStep Rations(int count) => new StageStep { Op = StageOp.Rations, X = count };
        public static StageStep Advance(Core.Story.ChapterStep from, Core.Story.ChapterStep to) =>
            new StageStep { Op = StageOp.Advance, A = ((int)from).ToString(), B = ((int)to).ToString() };
        // scene name, arrival (GameRun.Arrival name), stage script for Story_Stage.
        public static StageStep Load(string scene, string stage = null) => new StageStep { Op = StageOp.Load, A = scene, B = stage };
        public static StageStep Fight(string encounterId) => new StageStep { Op = StageOp.Battle, A = encounterId };
        public static StageStep Wait(float seconds) => new StageStep { Op = StageOp.Wait, Seconds = seconds };
        public static StageStep Music(string cue) => new StageStep { Op = StageOp.Music, A = cue };
        public static StageStep Reputation(int delta) => new StageStep { Op = StageOp.Reputation, X = delta };
        // An equipment gift (id) into the bag.
        public static StageStep Item(string id) => new StageStep { Op = StageOp.Item, A = id };
        public static StageStep Autosave(string location) => new StageStep { Op = StageOp.Autosave, A = location };
        public static StageStep Shake(float seconds = 0.4f) => new StageStep { Op = StageOp.Shake, Seconds = seconds };
        public static StageStep Flash() => new StageStep { Op = StageOp.Flash };
        public static StageStep Call(Action action) => new StageStep { Op = StageOp.Call, Callback = action };
    }
}
