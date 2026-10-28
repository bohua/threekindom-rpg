using System.Collections;
using UnityEngine;

namespace ThreeKingdoms.Gameplay.Story
{
    // Hooks between the story (this layer) and the full-screen cinematics drawn by the UI (user 2026-10-24): the ink
    // scroll that closes each main story beat, and the opening film after the Yellow Turbans are beaten. The UI view
    // registers itself here; with nothing registered (tests, a scene without the view) the story simply goes on.
    public static class StoryCinematics
    {
        // The scroll ids (InkScrollText holds the title and the text for each).
        public const string OathScroll = "taoyuan", YellowTurbanScroll = "huangjin";

        public interface IView
        {
            // Bullet time: freeze, darken, unroll the scroll, type the story, stamp 完成, wait for confirm.
            IEnumerator PlayScroll(string id);
            // The opening film (opening-assembled-20261021-v1); skippable; ends at once when the file is missing.
            IEnumerator PlayOpening();
            // The prologue 天下大势 (design/gdd/prologue.md): nine shots over scrolls, the world map and the hero cards;
            // the player may skip it at any time.
            IEnumerator PlayPrologue();
            // Confirm from the story side (tests, the auto pilot): types the rest, or closes the waiting scroll.
            void Advance();
        }

        public static void Advance() => View?.Advance();

        public static IView View { get; set; }
        // The last scroll asked for, so tests can see it was reached without the view.
        public static string LastScroll { get; private set; }
        public static bool OpeningRequested { get; private set; }
        public static int PrologueCount { get; private set; }
        // Tests and the auto pilot: end the prologue at once, as if the player chose to skip it.
        public static bool SkipPrologue { get; set; }

        public static IEnumerator Scroll(string id)
        {
            LastScroll = id;
            if (View != null) yield return View.PlayScroll(id);
        }

        public static IEnumerator Opening()
        {
            OpeningRequested = true;
            if (View != null) yield return View.PlayOpening();
        }

        public static IEnumerator Prologue()
        {
            PrologueCount++;
            if (View != null && !SkipPrologue) yield return View.PlayPrologue();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { View = null; LastScroll = null; OpeningRequested = false; PrologueCount = 0; SkipPrologue = false; }
    }

    // Music and sound cues (user 2026-10-24: the oath and the scroll get music later, made with Suno). A cue is an
    // AudioClip at Resources/Audio/Music/<cue> or Resources/Audio/Sfx/<cue>; drop the file in and it plays, with no code
    // change. A missing clip is silence, so the game runs before the music exists. Cue names and the Suno prompts:
    // design/cinematics/story-cinematics.md.
    public static class StoryMusic
    {
        public const string OathCue = "taoyuan_oath", ScrollCue = "scroll_finale", AspirationCue = "victory_aspiration", PrologueCue = "prologue_tianxia";
        public const string UnrollSfx = "scroll_unroll", SealSfx = "seal_stamp", InkSfx = "ink_drop";

        static AudioSource music, sfx;
        static float fadeTo, fadeSpeed;

        // The cue now asked to play (also when its clip is missing), or null.
        public static string Current { get; private set; }
        // 0..1 multiplier under narration (the prologue lowers the music while a line is spoken).
        public static float Duck { get; set; } = 1f;

        public static void Play(string cue, bool loop = false, float fadeIn = 0.6f)
        {
            Current = cue;
            var clip = Resources.Load<AudioClip>("Audio/Music/" + cue);
            if (!clip) return;
            Ensure();
            music.clip = clip;
            music.loop = loop;
            music.volume = fadeIn > 0f ? 0f : 1f;
            music.Play();
            Fade(1f, fadeIn);
        }

        public static void Stop(float fadeOut = 0.8f)
        {
            Current = null;
            if (music && music.isPlaying) Fade(0f, fadeOut);
        }

        public static void Sfx(string cue)
        {
            var clip = Resources.Load<AudioClip>("Audio/Sfx/" + cue);
            if (!clip) return;
            Ensure();
            sfx.PlayOneShot(clip);
        }

        static void Fade(float to, float seconds)
        {
            fadeTo = to;
            fadeSpeed = seconds > 0f ? 1f / seconds : float.PositiveInfinity;
        }

        static void Ensure()
        {
            if (music) return;
            var go = new GameObject("Story music") { hideFlags = HideFlags.DontSave };
            Object.DontDestroyOnLoad(go);
            music = go.AddComponent<AudioSource>();
            sfx = go.AddComponent<AudioSource>();
            music.playOnAwake = sfx.playOnAwake = false;
            // Unscaled: the scroll freezes time while its music plays.
            music.ignoreListenerPause = sfx.ignoreListenerPause = true;
            go.AddComponent<Fader>();
        }

        sealed class Fader : MonoBehaviour
        {
            void Update()
            {
                if (!music) return;
                music.volume = Mathf.MoveTowards(music.volume, fadeTo * Duck, fadeSpeed * Time.unscaledDeltaTime);
                if (fadeTo <= 0f && music.volume <= 0f && music.isPlaying) music.Stop();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { music = sfx = null; Current = null; fadeTo = 0f; Duck = 1f; }
    }
}
