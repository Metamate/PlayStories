using TMPro;
using UnityEngine;

namespace PlayStories
{
    /// <summary>Shared look &amp; feel: UI sprites, fonts, sound effects and text strings.</summary>
    [CreateAssetMenu(menuName = "PlayStories/Skin", fileName = "Skin")]
    public class UISkin : ScriptableObject
    {
        [Header("Font")]
        [Tooltip("Source font. The SDF font asset + materials below are generated from it by PlayStories ▸ Build Content.")]
        public Font font;
        public TMP_FontAsset fontAsset;
        [Tooltip("White text with a thick dark outline + soft drop shadow (titles, animal names).")]
        public Material textOutlined;
        [Tooltip("Thinner outline for smaller labels.")]
        public Material textOutlinedThin;
        public Color textColor = Color.white;
        public Color outlineColor = new Color(0.1f, 0.08f, 0.08f);
        public Color darkTextColor = new Color(0.24f, 0.37f, 0.2f);

        [Header("Icons")]
        public Sprite arrowLeft;
        public Sprite arrowRight;
        public Sprite back;
        public Sprite close;
        public Sprite speaker;
        public Sprite gear;
        public Sprite play;
        public Sprite replay;
        public Sprite filter;
        public Sprite lockIcon;

        [Header("Shapes")]
        public Sprite panel;        // rounded, outlined (9-sliced)
        public Sprite panelPlain;   // rounded, no outline (9-sliced)
        public Sprite ring;
        public Sprite circle;
        public Sprite bubble;
        public Sprite shadow;

        [Header("FX")]
        public Sprite heart;
        public Sprite star;
        public Sprite sparkle;
        public Sprite note;
        public Sprite cloud;
        public Sprite butterfly;

        [Header("Welcome screen")]
        public Sprite welcomeBackground;
        public Sprite welcomeCover;
        public CharacterDefinition[] welcomeCharacters;
        public AudioClip welcomeAmbience;

        [Header("Sound effects")]
        public AudioClip sfxPop;
        public AudioClip sfxTap;
        public AudioClip sfxPageTurn;
        public AudioClip sfxSparkle;
        public AudioClip sfxOpen;
        public AudioClip sfxBoing;
        public AudioClip sfxTick;
        public AudioClip sfxWhoosh;

        [Header("Parental gate")]
        [Tooltip("Seconds to hold the close / settings buttons.")]
        public float parentalHoldSeconds = 5f;
        [Tooltip("Seconds to hold the back arrow inside a book (stops tiny hands from leaving by accident).")]
        public float bookBackHoldSeconds = 1f;

        [Header("Texts (all languages)")]
        public LocalizedText appTitle = new LocalizedText("PlayStories", "PlayStories");
        public LocalizedText tapToStart = new LocalizedText("Tryk for at starte", "Tap to start");
        public LocalizedText libraryTitle = new LocalizedText("Bibliotek", "Library");
        public LocalizedText filterAll = new LocalizedText("Alle", "All");
        [Tooltip("{0} = from, {1} = to (years)")]
        public LocalizedText ageFormat = new LocalizedText("{0}–{1} år", "{0}–{1} yrs");
        public LocalizedText comingSoon = new LocalizedText("Kommer snart", "Coming soon");
        public LocalizedText holdHint = new LocalizedText("Hold inde", "Hold");
        public LocalizedText settingsTitle = new LocalizedText("Indstillinger", "Settings");
        public LocalizedText theEnd = new LocalizedText("Hurra!", "Hooray!");
        public LocalizedText settingAutoNarrate = new LocalizedText("Læs ordene højt automatisk", "Read words aloud automatically");
        public LocalizedText settingAmbience = new LocalizedText("Baggrundslyde (fuglesang)", "Background sounds (birdsong)");
        public LocalizedText settingSfx = new LocalizedText("Lydeffekter", "Sound effects");
        public LocalizedText settingHints = new LocalizedText("Hjælp, når barnet venter", "Hints when the child waits");
        public LocalizedText settingVolume = new LocalizedText("Lydstyrke", "Volume");
        public LocalizedText settingLanguage = new LocalizedText("Sprog", "Language");
        public LocalizedText settingKidLock = new LocalizedText("Børnelås (lås appen)", "Child lock (lock the app)");
        public LocalizedText kidLockHintIOS = new LocalizedText(
            "På iPad/iPhone: slå Guidet adgang til under Indstillinger ▸ Tilgængelighed, og tryk tre gange på sideknappen i appen.",
            "On iPad/iPhone: turn on Guided Access in Settings ▸ Accessibility, then triple-click the side button in the app.");
        public LocalizedText kidLockHintAndroid = new LocalizedText(
            "Appen fastgøres til skærmen. Luk den her under indstillinger.",
            "The app is pinned to the screen. Close it from here in settings.");
        public LocalizedText closeApp = new LocalizedText("Luk appen", "Close the app");
    }
}
