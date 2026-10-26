using UnityEngine;

namespace ThreeKingdoms.UI.Menu
{
    // The 系统 page's settings (GAME_MENU_UI_REDESIGN 05): kept in PlayerPrefs, per machine, not in the save files.
    // The game has no music or sound effects yet, so the two volumes are only remembered (and the effect volume
    // drives AudioListener.volume for when sound arrives); dialogue has no typewriter yet, so text speed is only
    // remembered too. Full screen applies at once.
    public static class GameSettings
    {
        public const int DefaultMusic = 80, DefaultEffects = 90, DefaultTextSpeed = 1;
        public static readonly string[] TextSpeeds = { "慢", "标准", "快" };

        public static int Music { get => PlayerPrefs.GetInt("settings.music", DefaultMusic); set => PlayerPrefs.SetInt("settings.music", Mathf.Clamp(value, 0, 100)); }

        public static int Effects
        {
            get => PlayerPrefs.GetInt("settings.effects", DefaultEffects);
            set { PlayerPrefs.SetInt("settings.effects", Mathf.Clamp(value, 0, 100)); AudioListener.volume = Effects / 100f; }
        }

        public static int TextSpeed { get => PlayerPrefs.GetInt("settings.textSpeed", DefaultTextSpeed); set => PlayerPrefs.SetInt("settings.textSpeed", (value % TextSpeeds.Length + TextSpeeds.Length) % TextSpeeds.Length); }

        public static bool FullScreen { get => Screen.fullScreen; set => Screen.fullScreen = value; }

        public static void Restore()
        {
            Music = DefaultMusic; Effects = DefaultEffects; TextSpeed = DefaultTextSpeed;
            FullScreen = true;
        }
    }
}
