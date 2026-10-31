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
            // Bullet time: freeze, darken, unroll the scroll, type the story, stamp 完成, wait for confirm; a first
            // unlock also shows the achievement line.
            IEnumerator PlayScroll(string id, bool firstUnlock);
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
            // Recorded before it plays, so skipping or closing the scroll still keeps the achievement in this play-through.
            bool first = GameRun.Current?.Chapter2.UnlockAchievement(id) ?? false;
            if (View != null) yield return View.PlayScroll(id, first);
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
    // design/cinematics/story-cinematics.md. A looping cue may come with <cue>__intro: the intro plays once, then the
    // loop starts on the exact sample after it (both files are cut by tools/music-inbox, ADR-0011).
    public static class StoryMusic
    {
        public const string IntroSuffix = "__intro";

        public const string OathCue = "taoyuan_oath", ScrollCue = "scroll_finale", AspirationCue = "victory_aspiration", PrologueCue = "prologue_tianxia";
        public const string UnrollSfx = "scroll_unroll", SealSfx = "seal_stamp", InkSfx = "ink_drop";

        static AudioSource music, intro, sfx;
        static float fadeTo, fadeSpeed;

        // The cue now asked to play (also when its clip is missing), or null.
        public static string Current { get; private set; }
        // 0..1 multiplier on the music (kept for narration that wants the music lower; the prologue leaves it at 1).
        public static float Duck { get; set; } = 1f;
        // The place's own music, looped whenever no story cue plays (user 2026-10-28: Zhuo keeps the prologue's piece
        // so the town does not break off from it). Null where a place has none.
        public static string Background { get; set; }

        // Lets the cue now playing run on in a loop (the prologue's piece carrying on into the town).
        public static void Loop()
        {
            if (music && music.isPlaying) music.loop = true;
        }

        // Called by a place each frame: starts its background piece again once a story cue has ended.
        public static void KeepBackground()
        {
            if (Background != null && Current == null) Play(Background, loop: true, fadeIn: 2f);
        }

        public static void Play(string cue, bool loop = false, float fadeIn = 0.6f)
        {
            Current = cue;
            var clip = Resources.Load<AudioClip>("Audio/Music/" + cue);
            if (!clip) return;
            Ensure();
            intro.Stop();
            music.Stop();
            music.clip = clip;
            music.loop = loop;
            music.volume = intro.volume = fadeIn > 0f ? 0f : 1f;
            var head = loop ? Resources.Load<AudioClip>("Audio/Music/" + cue + IntroSuffix) : null;
            if (head)
            {
                // Scheduled on the DSP clock so the loop joins the intro without a gap.
                double at = AudioSettings.dspTime + 0.1;
                intro.clip = head;
                intro.PlayScheduled(at);
                music.PlayScheduled(at + (double)head.samples / head.frequency);
            }
            else music.Play();
            Fade(1f, fadeIn);
        }

        public static void Stop(float fadeOut = 0.8f)
        {
            Current = null;
            if (music && (music.isPlaying || intro.isPlaying)) Fade(0f, fadeOut);
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
            intro = go.AddComponent<AudioSource>();
            sfx = go.AddComponent<AudioSource>();
            music.playOnAwake = intro.playOnAwake = sfx.playOnAwake = false;
            // Unscaled: the scroll freezes time while its music plays.
            music.ignoreListenerPause = intro.ignoreListenerPause = sfx.ignoreListenerPause = true;
            go.AddComponent<Fader>();
            KeepListener();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, __) => KeepListener();
        }

        // The town and story scenes have no AudioListener, so nothing was heard (user 2026-10-26: "还是没有任何音乐和
        // 音效"). The music object carries one, switched on only while no other listener (the battle cameras) is around.
        static void KeepListener()
        {
            if (!music) return;
            var own = music.GetComponent<AudioListener>();
            if (!own) own = music.gameObject.AddComponent<AudioListener>();
            bool other = false;
            foreach (var l in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) if (l != own && l.isActiveAndEnabled) other = true;
            own.enabled = !other;
        }

        sealed class Fader : MonoBehaviour
        {
            void Update()
            {
                if (!music) return;
                music.volume = intro.volume = Mathf.MoveTowards(music.volume, fadeTo * Duck, fadeSpeed * Time.unscaledDeltaTime);
                if (fadeTo <= 0f && music.volume <= 0f && (music.isPlaying || intro.isPlaying)) { music.Stop(); intro.Stop(); }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { music = intro = sfx = null; Current = null; fadeTo = 0f; Duck = 1f; Background = null; }

        // Most scenes are built by code without an AudioListener, so nothing at all was heard (user 2026-10-28 “没有音乐和
        // 人声”). Every loaded scene gets one on its main camera when it has none.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void WatchScenes()
        {
            EnsureListener();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, __) => EnsureListener();
        }

        public static void EnsureListener()
        {
            if (Object.FindAnyObjectByType<AudioListener>()) return;
            var cam = Camera.main;
            Debug.Log("No AudioListener in this scene; adding one to " + (cam ? cam.name : "a listener object"));
            if (cam) cam.gameObject.AddComponent<AudioListener>();
            else Object.DontDestroyOnLoad(new GameObject("Audio listener").AddComponent<AudioListener>().gameObject);
        }
    }
}
