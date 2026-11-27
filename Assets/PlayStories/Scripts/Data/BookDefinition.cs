using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlayStories
{
    public enum PageKind
    {
        /// <summary>Title page: big book title, a few characters, the narrator reads the title.</summary>
        Cover,
        /// <summary>One animal / object to tap. Narrator says its name.</summary>
        Character,
        /// <summary>"Hurra!" page with every character from the book and a replay button.</summary>
        End,
    }

    [Serializable]
    public class PageDefinition
    {
        public PageKind kind = PageKind.Character;

        [Tooltip("Word shown under the character (cover: the book title). Empty on a character page = the character's name.")]
        public LocalizedText label = new LocalizedText();

        [Tooltip("Voice-over per language, played when the page opens and when the speaker button is pressed.")]
        public LocalizedClip narration = new LocalizedClip();

        [Header("Character")]
        public CharacterDefinition character;

        [Tooltip("Optional override for the page background. Leave empty to use the book background.")]
        public Sprite backgroundOverride;

        /// <summary>The label in the current language (falls back to the character's name).</summary>
        public string LabelText => label != null && !label.IsEmpty ? label.Get()
                                 : character != null ? character.displayName.Get() : string.Empty;

        public AudioClip NarrationClip => narration?.Get();
    }

    /// <summary>
    /// A book in the library. Create via Assets ▸ Create ▸ PlayStories ▸ Book.
    /// </summary>
    [CreateAssetMenu(menuName = "PlayStories/Book", fileName = "Book")]
    public class BookDefinition : ScriptableObject
    {
        public string id = "book";
        public LocalizedText title = new LocalizedText("Ny bog", "New book");
        public Sprite cover;
        public Color themeColor = new Color(0.47f, 0.69f, 0.36f);

        [Tooltip("Shown greyed out with a lock in the library until it has pages.")]
        public bool comingSoon;

        [Header("Age range (months) – used by the library filter")]
        public int minAgeMonths;
        public int maxAgeMonths = 12;

        [Tooltip("Topics used by the library filter, e.g. 'dyr', 'kjøretøy'.")]
        public List<string> topics = new List<string>();

        [Header("Presentation")]
        public Sprite background;
        public AudioClip ambience;
        [Range(0f, 1f)] public float ambienceVolume = 0.35f;

        public List<PageDefinition> pages = new List<PageDefinition>();

        public bool IsPlayable => !comingSoon && pages != null && pages.Count > 0;

        public IEnumerable<CharacterDefinition> Characters
        {
            get
            {
                foreach (var p in pages)
                    if (p.kind == PageKind.Character && p.character != null)
                        yield return p.character;
            }
        }
    }
}
