using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PlayStories
{
    /// <summary>
    /// Entry point. Builds the canvas and drives navigation between the three screens:
    /// Welcome → Library → Book. Everything visual is constructed from code + data assets,
    /// so adding a book is purely a data task (see BookDefinition).
    /// </summary>
    public class AppController : MonoBehaviour
    {
        public BookLibrary library;
        public UISkin skin;
        public Camera uiCamera;

        public static AppController Instance { get; private set; }
        public Canvas Canvas { get; private set; }
        public RectTransform ScreenLayer { get; private set; }
        public RectTransform OverlayLayer { get; private set; }
        public ScreenBase Current { get; private set; }
        public AudioManager Audio { get; private set; }
        public bool Transitioning { get; private set; }

        Image _fade;

        void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 60;
            UI.Init(skin);
            Audio = new GameObject("Audio").AddComponent<AudioManager>();
            Audio.transform.SetParent(transform, false);
            EnsureEventSystem();
            BuildCanvas();
        }

        void Start()
        {
            ShowScreen(CreateScreen<WelcomeScreen>(), false);
            Localization.Changed += OnLanguageChanged;
            KidLock.Apply(requestPin: true);
        }

        // Android drops immersive mode / pinning when the app is paused and resumed
        void OnApplicationFocus(bool focus) { if (focus) KidLock.Apply(); }

        void OnDestroy() => Localization.Changed -= OnLanguageChanged;

        /// <summary>Rebuild the visible screen in the new language (keeps the open book and page).</summary>
        void OnLanguageChanged()
        {
            Audio.StopVoice();
            switch (Current)
            {
                case BookScreen b:
                    var s = CreateScreen<BookScreen>();
                    s.book = b.book;
                    s.startPage = b.PageIndex;
                    ShowScreen(s, false);
                    break;
                case LibraryScreen _:
                    ShowScreen(CreateScreen<LibraryScreen>(), false);
                    break;
                default:
                    ShowScreen(CreateScreen<WelcomeScreen>(), false);
                    break;
            }
        }

        void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        void BuildCanvas()
        {
            if (uiCamera == null) uiCamera = Camera.main;
            var go = new GameObject("Canvas", typeof(RectTransform));
            go.layer = 5; // UI
            go.transform.SetParent(transform, false);
            Canvas = go.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceCamera;
            Canvas.worldCamera = uiCamera;
            Canvas.planeDistance = 10f;
            Canvas.pixelPerfect = false;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // 16:10 reference; "Expand" guarantees the whole reference area is always visible,
            // wider phones get extra width, 4:3 tablets get extra height.
            scaler.referenceResolution = new Vector2(1920f, 1200f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            go.AddComponent<GraphicRaycaster>();

            ScreenLayer = UI.Rect(go.transform, "Screens");
            UI.Stretch(ScreenLayer);
            OverlayLayer = UI.Rect(go.transform, "Overlays");
            UI.Stretch(OverlayLayer);

            _fade = UI.Image(go.transform, "Fade", null, new Color(1f, 0.98f, 0.93f, 0f));
            UI.Stretch(_fade.rectTransform);
            _fade.raycastTarget = false;
        }

        // ------------------------------------------------------------------ navigation
        T CreateScreen<T>() where T : ScreenBase
        {
            var rt = UI.Rect(ScreenLayer, typeof(T).Name);
            UI.Stretch(rt);
            var s = rt.gameObject.AddComponent<T>();
            s.Init(this);
            return s;
        }

        void ShowScreen(ScreenBase s, bool animate)
        {
            if (Current != null) Destroy(Current.gameObject);
            Current = s;
            s.Build();
            s.StartCoroutine(s.Enter()); // runs on the screen, so it stops when the screen is replaced
        }

        public void GoToWelcome() => StartCoroutine(FadeTo(() => CreateScreen<WelcomeScreen>()));
        public void GoToLibrary() => StartCoroutine(FadeTo(() => CreateScreen<LibraryScreen>()));

        public void OpenBook(BookDefinition book, RectTransform fromCard)
        {
            if (Transitioning || book == null || !book.IsPlayable) return;
            StartCoroutine(OpenBookRoutine(book, fromCard));
        }

        IEnumerator FadeTo(System.Func<ScreenBase> make, float duration = 0.35f)
        {
            if (Transitioning) yield break;
            Transitioning = true;
            _fade.raycastTarget = true;
            _fade.transform.SetAsLastSibling();
            yield return Tween.Run(duration, Ease.InOutQuad, k => SetFade(k));
            Audio.StopVoice();
            ShowScreen(make(), true);
            yield return null;
            yield return Tween.Run(duration, Ease.InOutQuad, k => SetFade(1f - k));
            _fade.raycastTarget = false;
            Transitioning = false;
        }

        void SetFade(float a) { var c = _fade.color; c.a = a; _fade.color = c; }

        /// <summary>The tapped book cover flies up and grows to fill the screen, then becomes the book.</summary>
        IEnumerator OpenBookRoutine(BookDefinition book, RectTransform fromCard)
        {
            Transitioning = true;
            Audio.PlaySfx(skin.sfxOpen, 0.9f);
            var flyer = UI.Image(OverlayLayer, "CoverFlyer", book.cover);
            flyer.raycastTarget = true;
            var rt = flyer.rectTransform;
            // start exactly where the card's cover is
            Vector3[] corners = new Vector3[4];
            (fromCard != null ? fromCard : ScreenLayer).GetWorldCorners(corners);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            Vector2 a = OverlayLayer.InverseTransformPoint(corners[0]);
            Vector2 b = OverlayLayer.InverseTransformPoint(corners[2]);
            Vector2 startPos = (a + b) * 0.5f, startSize = b - a;
            Vector2 endSize = OverlayLayer.rect.size;
            // keep the cover's aspect while growing, then cross-fade to the real page
            float coverAspect = book.cover != null ? book.cover.rect.width / book.cover.rect.height : 4f / 3f;
            float screenAspect = endSize.x / endSize.y;
            Vector2 fullCover = screenAspect > coverAspect ? new Vector2(endSize.x, endSize.x / coverAspect) : new Vector2(endSize.y * coverAspect, endSize.y);
            rt.anchoredPosition = startPos;
            rt.sizeDelta = startSize;
            yield return Tween.Run(0.6f, Ease.InOutCubic, k =>
            {
                rt.anchoredPosition = Vector2.LerpUnclamped(startPos, Vector2.zero, k);
                rt.sizeDelta = Vector2.LerpUnclamped(startSize, fullCover, k);
                rt.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(k * Mathf.PI) * -4f);
            });
            var screen = CreateScreen<BookScreen>();
            screen.book = book;
            ShowScreen(screen, true);
            yield return null;
            var g = UI.Group(flyer);
            yield return Tween.Run(0.45f, Ease.OutQuad, k => { g.alpha = 1f - k; rt.localScale = Vector3.one * (1f + 0.08f * k); });
            Destroy(flyer.gameObject);
            Transitioning = false;
        }

        // ------------------------------------------------------------------ app-wide actions
        public void OpenSettings() => SettingsPanel.Open(OverlayLayer, skin);

        public void QuitApp()
        {
            KidLock.Release();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>iOS does not allow apps to quit themselves – no close button there.</summary>
        public static bool CanQuit => Application.platform != RuntimePlatform.IPhonePlayer;
    }
}
