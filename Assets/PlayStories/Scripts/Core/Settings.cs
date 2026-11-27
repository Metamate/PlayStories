using UnityEngine;

namespace PlayStories
{
    /// <summary>Parent-controlled settings, persisted in PlayerPrefs.</summary>
    public static class Settings
    {
        const string KeyMaster = "ps.masterVolume";
        const string KeyAmbience = "ps.ambience";
        const string KeySfx = "ps.sfx";
        const string KeyAutoNarrate = "ps.autoNarrate";
        const string KeyHints = "ps.hints";
        const string KeyKidLock = "ps.kidLock";

        public static float MasterVolume
        {
            get => PlayerPrefs.GetFloat(KeyMaster, 1f);
            set { PlayerPrefs.SetFloat(KeyMaster, Mathf.Clamp01(value)); PlayerPrefs.Save(); }
        }

        /// <summary>Farm birdsong etc. in the background.</summary>
        public static bool AmbienceEnabled
        {
            get => PlayerPrefs.GetInt(KeyAmbience, 1) == 1;
            set { PlayerPrefs.SetInt(KeyAmbience, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>UI pops, page-turn swooshes, sparkles.</summary>
        public static bool SoundEffectsEnabled
        {
            get => PlayerPrefs.GetInt(KeySfx, 1) == 1;
            set { PlayerPrefs.SetInt(KeySfx, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Read the page out loud as soon as it opens.</summary>
        public static bool AutoNarrate
        {
            get => PlayerPrefs.GetInt(KeyAutoNarrate, 1) == 1;
            set { PlayerPrefs.SetInt(KeyAutoNarrate, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Locked mode: block OS gestures / pin the app (see <see cref="PlayStories.KidLock"/>).</summary>
        public static bool KidLock
        {
            get => PlayerPrefs.GetInt(KeyKidLock, 1) == 1;
            set { PlayerPrefs.SetInt(KeyKidLock, value ? 1 : 0); PlayerPrefs.Save(); PlayStories.KidLock.Apply(requestPin: true); }
        }

        /// <summary>Pulse the arrow / wiggle the animal when the child has been idle for a while.</summary>
        public static bool Hints
        {
            get => PlayerPrefs.GetInt(KeyHints, 1) == 1;
            set { PlayerPrefs.SetInt(KeyHints, value ? 1 : 0); PlayerPrefs.Save(); }
        }
    }
}
