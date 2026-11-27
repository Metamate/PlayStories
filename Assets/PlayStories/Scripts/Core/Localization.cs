using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlayStories
{
    /// <summary>
    /// Supported languages. To add one: add a value here, add a code in <see cref="Localization.Code"/>,
    /// and fill in the new entries on the Skin / Book / Character assets (empty entries fall back to Danish).
    /// </summary>
    public enum Language
    {
        Danish = 0,
        English = 1,
    }

    /// <summary>Current app language (parent setting, defaults to the device language).</summary>
    public static class Localization
    {
        const string Key = "ps.language";
        public static event Action Changed;

        public static Language Fallback => Language.Danish;

        public static Language Current
        {
            get
            {
                if (PlayerPrefs.HasKey(Key)) return (Language)PlayerPrefs.GetInt(Key);
                return Application.systemLanguage == SystemLanguage.Danish ? Language.Danish
                     : Application.systemLanguage == SystemLanguage.English ? Language.English
                     : Language.Danish;
            }
            set
            {
                if (PlayerPrefs.HasKey(Key) && Current == value) return;
                PlayerPrefs.SetInt(Key, (int)value);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        public static string Code(Language l) => l == Language.English ? "en" : "da";

        /// <summary>Name of a language written in that language (for the picker).</summary>
        public static string NativeName(Language l) => l == Language.English ? "English" : "Dansk";

        public static IEnumerable<Language> All => (Language[])Enum.GetValues(typeof(Language));
    }

    /// <summary>A piece of text in several languages.</summary>
    [Serializable]
    public class LocalizedText
    {
        [Serializable]
        public struct Entry
        {
            public Language language;
            [TextArea(1, 3)] public string text;
        }

        public List<Entry> entries = new List<Entry>();

        public LocalizedText() { }

        public LocalizedText(string danish, string english)
        {
            entries.Add(new Entry { language = Language.Danish, text = danish });
            entries.Add(new Entry { language = Language.English, text = english });
        }

        public string Get() => Get(Localization.Current);

        public string Get(Language language)
        {
            string fallback = null;
            foreach (var e in entries)
            {
                if (e.language == language && !string.IsNullOrEmpty(e.text)) return e.text;
                if (e.language == Localization.Fallback) fallback = e.text;
            }
            return fallback ?? (entries.Count > 0 ? entries[0].text : string.Empty);
        }

        /// <summary>string.Format with the localized pattern.</summary>
        public string Format(params object[] args) => string.Format(Get(), args);

        public bool IsEmpty => entries.Count == 0 || entries.TrueForAll(e => string.IsNullOrEmpty(e.text));

        public override string ToString() => Get();
    }

    /// <summary>An audio clip (narration) in several languages.</summary>
    [Serializable]
    public class LocalizedClip
    {
        [Serializable]
        public struct Entry
        {
            public Language language;
            public AudioClip clip;
        }

        public List<Entry> entries = new List<Entry>();

        public LocalizedClip() { }

        public LocalizedClip(AudioClip danish, AudioClip english)
        {
            entries.Add(new Entry { language = Language.Danish, clip = danish });
            entries.Add(new Entry { language = Language.English, clip = english });
        }

        public AudioClip Get() => Get(Localization.Current);

        public AudioClip Get(Language language)
        {
            AudioClip fallback = null;
            foreach (var e in entries)
            {
                if (e.language == language && e.clip != null) return e.clip;
                if (e.language == Localization.Fallback) fallback = e.clip;
            }
            return fallback;
        }
    }
}
