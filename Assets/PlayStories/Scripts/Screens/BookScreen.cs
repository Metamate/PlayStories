using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PlayStories
{
    /// <summary>
    /// Reads a <see cref="BookDefinition"/>: one page at a time, turned with arrows or by dragging
    /// the page with a finger (page-curl). Character pages: narrator says the name, tap the
    /// character for its sound + animation, speaker button repeats the name.
    /// </summary>
    public class BookScreen : ScreenBase, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler
    {
        public BookDefinition book;
        public int startPage;

        RectTransform _pageLayer, _chrome;
        FxLayer _fx;
        PageTurner _turner;
        PressButton _prev, _next, _speaker;
        TextMeshProUGUI _bookTitle;

        // current page
        int _index;
        RectTransform _page;
        readonly List<CharacterActor> _actors = new List<CharacterActor>();
        TextMeshProUGUI _label;
        WavyText _labelWave;
        Coroutine _pageRoutine;
        float _idle;
        int _hintCount;

        // drag-to-turn
        int _dragDir;
        Vector2 _dragStart;
        float _dragProgress, _dragVelocity, _lastDragMove;

        PageDefinition Page => book.pages[_index];

        // ------------------------------------------------------------------ build
        public override void Build()
        {
            var catcher = gameObject.AddComponent<Image>();
            catcher.color = new Color(0, 0, 0, 0);

            _pageLayer = UI.Rect(Root, "Page");
            UI.Stretch(_pageLayer);

            _chrome = MakeSafeArea(Root);
            _chrome.name = "Chrome";
            Safe = _chrome;

            _bookTitle = UI.Text(_chrome, "BookTitle", book.title.Get(), 120f);
            UI.Anchor(_bookTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(1200f, 160f));
            _bookTitle.enableAutoSizing = true; _bookTitle.fontSizeMin = 60f; _bookTitle.fontSizeMax = 130f;

            AddBackButton(_chrome, App.GoToLibrary, Skin.bookBackHoldSeconds);
            AddSettingsButton(_chrome);

            _prev = PressButton.Create(_chrome, "Prev", Skin.arrowLeft, new Vector2(190f, 210f), () => Turn(-1), null, 30f);
            UI.Anchor((RectTransform)_prev.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, -30f), new Vector2(250f, 270f));
            _next = PressButton.Create(_chrome, "Next", Skin.arrowRight, new Vector2(190f, 210f), () => Turn(1), null, 30f);
            UI.Anchor((RectTransform)_next.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, -30f), new Vector2(250f, 270f));
            _prev.fireOnDown = _next.fireOnDown = true; // instant response for little fingers

            _speaker = PressButton.Create(_chrome, "Speaker", Skin.speaker, new Vector2(190f, 150f), Narrate, Skin.sfxTap, 30f);
            UI.Anchor((RectTransform)_speaker.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-10f, 10f), new Vector2(250f, 210f));

            _fx = FxLayer.Create(Root, Skin); // above the chrome so bubbles never hide behind the title

            _turner = PageTurner.Create(App.Canvas, Root);
            _turner.sound = Skin.sfxPageTurn;

            _index = startPage;
            ShowPage(startPage);
        }

        public override IEnumerator Enter()
        {
            App.Audio.PlayAmbience(book.ambience, book.ambienceVolume);
            yield return new WaitForSeconds(0.25f);
            OnPageRevealed();
        }

        // ------------------------------------------------------------------ pages
        void ShowPage(int index)
        {
            _index = Mathf.Clamp(index, 0, book.pages.Count - 1);
            if (_pageRoutine != null) { StopCoroutine(_pageRoutine); _pageRoutine = null; }
            if (_page != null) Destroy(_page.gameObject);
            _actors.Clear();
            _label = null;
            _labelWave = null;

            _page = UI.Rect(_pageLayer, "Page " + _index);
            UI.Stretch(_page);
            var bg = Page.backgroundOverride != null ? Page.backgroundOverride : book.background;
            UI.CoverImage(_page, "Background", bg);
            AmbientLife.Create(_page, Skin, 3, Page.kind != PageKind.Character);

            switch (Page.kind)
            {
                case PageKind.Cover: BuildCover(); break;
                case PageKind.Character: BuildCharacter(); break;
                case PageKind.End: BuildEnd(); break;
            }

            // chrome state
            _bookTitle.gameObject.SetActive(Page.kind == PageKind.Character);
            _prev.gameObject.SetActive(_index > 0);
            _next.gameObject.SetActive(_index < book.pages.Count - 1);
            _speaker.gameObject.SetActive(Page.NarrationClip != null);
            StopArrowHint();
            _idle = 0f;
            _hintCount = 0;
        }

        void BuildCharacter()
        {
            var slot = UI.Rect(_page, "Slot");
            slot.anchorMin = new Vector2(0.2f, 0.2f);
            slot.anchorMax = new Vector2(0.8f, 0.8f);
            slot.offsetMin = slot.offsetMax = Vector2.zero;
            var actor = CharacterActor.Create(slot, Page.character, Skin);
            actor.Tapped += OnCharacterTapped;
            _actors.Add(actor);

            _label = UI.Text(_page, "Name", Page.LabelText, 170f);
            _label.rectTransform.anchorMin = new Vector2(0.25f, 0.015f);
            _label.rectTransform.anchorMax = new Vector2(0.75f, 0.2f);
            _label.rectTransform.offsetMin = _label.rectTransform.offsetMax = Vector2.zero;
            _label.enableAutoSizing = true; _label.fontSizeMin = 80f; _label.fontSizeMax = 180f;
            _labelWave = _label.gameObject.AddComponent<WavyText>();
        }

        void BuildCover()
        {
            _label = UI.Text(_page, "Title", string.IsNullOrEmpty(Page.LabelText) ? book.title.Get() : Page.LabelText, 200f);
            _label.rectTransform.anchorMin = new Vector2(0.16f, 0.64f);
            _label.rectTransform.anchorMax = new Vector2(0.84f, 0.92f);
            _label.rectTransform.offsetMin = _label.rectTransform.offsetMax = Vector2.zero;
            _label.enableAutoSizing = true; _label.fontSizeMin = 90f; _label.fontSizeMax = 210f;
            _labelWave = _label.gameObject.AddComponent<WavyText>();
            BuildCharacterRow(0.13f, 0.6f, 0.9f, true);
        }

        void BuildEnd()
        {
            _label = UI.Text(_page, "Hurra", string.IsNullOrEmpty(Page.LabelText) ? Skin.theEnd.Get() : Page.LabelText, 210f);
            _label.rectTransform.anchorMin = new Vector2(0.2f, 0.66f);
            _label.rectTransform.anchorMax = new Vector2(0.8f, 0.96f);
            _label.rectTransform.offsetMin = _label.rectTransform.offsetMax = Vector2.zero;
            _label.enableAutoSizing = true; _label.fontSizeMin = 90f; _label.fontSizeMax = 220f;
            _labelWave = _label.gameObject.AddComponent<WavyText>();
            BuildCharacterRow(0.2f, 0.62f, 0.85f, true);

            var replay = PressButton.Create(_page, "Replay", Skin.replay, new Vector2(170f, 170f), () => GoToPage(0), Skin.sfxPop, 20f);
            UI.Anchor((RectTransform)replay.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(210f, 210f));
            var bob = replay.transform.GetChild(0).gameObject.AddComponent<Bobber>();
            bob.bob = 0f; bob.pulse = 0.06f;
        }

        /// <summary>All the book's characters standing side by side (cover / end page).</summary>
        void BuildCharacterRow(float yMin, float yMax, float scale, bool interactive)
        {
            var chars = new List<CharacterDefinition>(book.Characters);
            if (chars.Count == 0) return;
            float x0 = 0.14f, x1 = 0.86f, step = (x1 - x0) / chars.Count; // keep clear of the arrows
            for (int i = 0; i < chars.Count; i++)
            {
                var slot = UI.Rect(_page, "Slot" + i);
                // stagger heights a little so the row looks like a group, not a parade
                float lift = (i % 2 == 1) ? 0.035f : 0f;
                slot.anchorMin = new Vector2(x0 + step * i - step * 0.12f, yMin + lift);
                slot.anchorMax = new Vector2(x0 + step * (i + 1) + step * 0.12f, yMax + lift);
                slot.offsetMin = slot.offsetMax = Vector2.zero;
                var a = CharacterActor.Create(slot, chars[i], Skin, interactive);
                a.Tapped += OnCharacterTapped;
                _actors.Add(a);
            }
            // front row overlap: draw lower (front) ones last
            for (int i = 0; i < chars.Count; i += 2) _actors[i].transform.parent.SetAsLastSibling();
        }

        // ------------------------------------------------------------------ page life cycle
        /// <summary>Called when the new page is (mostly) visible: characters drop in, narrator speaks.</summary>
        void OnPageRevealed()
        {
            if (_pageRoutine != null) StopCoroutine(_pageRoutine);
            _pageRoutine = StartCoroutine(PageRoutine());
        }

        IEnumerator PageRoutine()
        {
            for (int i = 0; i < _actors.Count; i++) _actors[i].PlayEnter(i * 0.12f);
            if (_label != null) StartCoroutine(PopLabel(_label.rectTransform, 0.25f));
            if (Page.kind == PageKind.End)
            {
                _fx.Confetti(45);
                App.Audio.PlaySfx(Skin.sfxSparkle, 0.8f);
            }
            yield return new WaitForSeconds(0.75f + _actors.Count * 0.08f);
            if (Settings.AutoNarrate) Narrate();
            if (Page.kind == PageKind.End)
            {
                yield return new WaitForSeconds(0.6f);
                for (int i = 0; i < _actors.Count; i++)
                {
                    _actors[i].React(false, 1f);
                    yield return new WaitForSeconds(0.18f);
                }
            }
            _pageRoutine = null;
        }

        IEnumerator PopLabel(RectTransform rt, float delay)
        {
            rt.localScale = Vector3.zero;
            yield return new WaitForSeconds(delay);
            yield return Tween.Run(0.45f, Ease.OutBack, k => rt.localScale = Vector3.one * k);
        }

        /// <summary>Speaker button / page open: read the page out loud with bouncing letters.</summary>
        public void Narrate()
        {
            _idle = 0f;
            var clip = Page.NarrationClip;
            if (clip == null) return;
            App.Audio.PlayVoice(clip);
            if (_labelWave != null) _labelWave.Play(clip.length);
            StartCoroutine(Tween.Punch(_speaker.transform.GetChild(0), 0.2f, 0.5f));
        }

        void OnCharacterTapped(CharacterActor actor)
        {
            _idle = 0f;
            if (_turner.Busy) return;
            if (!actor.React()) return;
            var head = _fx.WorldToLocal(actor.HeadWorldPosition);
            var m = actor.Definition.motion;
            _fx.Burst(head, m.particles, Page.kind == PageKind.Character ? 7 : 4, Page.kind == PageKind.Character ? 1f : 0.7f);
            _fx.SpeechBubble(head + new Vector2(m.facing * -40f, 20f), actor.Definition.soundWord.Get(), -m.facing,
                actor.Definition.sound != null ? Mathf.Max(1f, actor.Definition.sound.length) : 1.2f);
        }

        // ------------------------------------------------------------------ turning
        public int PageIndex => _index;
        public int PageCount => book.pages.Count;
        public void Next() => Turn(1);
        public void Prev() => Turn(-1);
        public void TapCharacter(int i = 0) { if (i < _actors.Count) OnCharacterTapped(_actors[i]); }

        void Turn(int dir)
        {
            _idle = 0f;
            int target = _index + dir;
            if (target < 0 || target >= book.pages.Count || _turner.Busy || App.Transitioning) return;
            GoToPage(target);
        }

        void GoToPage(int target)
        {
            if (_turner.Busy) return;
            int dir = target >= _index ? 1 : -1;
            App.Audio.StopVoice();
            StartCoroutine(_turner.Turn(dir, () => ShowPage(target), OnPageRevealed));
        }

        public void OnPointerDown(PointerEventData e)
        {
            // background taps sparkle – rewarding for the littlest readers who tap everywhere
            if (e.pointerCurrentRaycast.gameObject == gameObject && !_turner.Busy)
            {
                _fx.TouchSparkle(_fx.ScreenToLocal(e.position));
                App.Audio.PlaySfx(Skin.sfxSparkle, 0.25f, Random.Range(1.1f, 1.4f));
            }
            _idle = 0f;
        }

        public void OnBeginDrag(PointerEventData e)
        {
            _dragDir = 0;
            if (_turner.Busy || App.Transitioning) return;
            Vector2 d = e.position - e.pressPosition;
            if (Mathf.Abs(d.x) < Mathf.Abs(d.y) * 1.2f) return; // mostly vertical: ignore
            int dir = d.x < 0f ? 1 : -1;                          // swipe left = next page
            int target = _index + dir;
            if (target < 0 || target >= book.pages.Count) return;
            int from = _index;
            App.Audio.StopVoice();
            if (_turner.BeginDrag(dir, () => ShowPage(target), () => { App.Audio.StopVoice(); ShowPage(from); }, OnPageRevealed))
            {
                _dragDir = dir;
                _dragStart = e.pressPosition;
                _dragProgress = 0f;
                _dragVelocity = 0f;
                _lastDragMove = Time.unscaledTime;
            }
        }

        public void OnDrag(PointerEventData e)
        {
            if (_dragDir == 0) return;
            float travel = (_dragStart.x - e.position.x) * _dragDir;
            float p = Mathf.Clamp01(travel / (Screen.width * 0.8f));
            if (!Mathf.Approximately(p, _dragProgress))
            {
                _dragVelocity = Mathf.Lerp(_dragVelocity, (p - _dragProgress) / Mathf.Max(0.001f, Time.unscaledDeltaTime), 0.3f);
                _lastDragMove = Time.unscaledTime;
            }
            _dragProgress = p;
            _turner.Drag(p);
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (_dragDir == 0) return;
            // drag events only arrive while the finger moves: a finger that came to rest is not a flick
            bool flick = _dragVelocity > 1.2f && Time.unscaledTime - _lastDragMove < 0.12f;
            bool complete = _dragProgress > 0.33f || flick;
            _dragDir = 0;
            StartCoroutine(_turner.Release(complete));
        }

        // ------------------------------------------------------------------ idle hints
        Coroutine _arrowHint;

        void Update()
        {
            if (!Settings.Hints || _turner.Busy || App.Audio.IsVoicePlaying) return;
            if (_actors.Exists(a => a.IsReacting)) { _idle = 0f; return; }
            _idle += Time.deltaTime;
            float wait = _hintCount == 0 ? 6f : 9f;
            if (_idle < wait) return;
            _idle = 0f;
            _hintCount++;
            if (Page.kind == PageKind.Character && _hintCount % 2 == 1 && _actors.Count > 0)
                _actors[0].Hint();           // "tap me!"
            else if (_next.gameObject.activeSelf)
                StartArrowHint();            // "turn the page!"
        }

        void StartArrowHint()
        {
            StopArrowHint();
            _arrowHint = StartCoroutine(ArrowHint());
        }

        void StopArrowHint()
        {
            if (_arrowHint != null) StopCoroutine(_arrowHint);
            _arrowHint = null;
            if (_next != null) _next.transform.GetChild(0).localScale = Vector3.one;
        }

        IEnumerator ArrowHint()
        {
            var icon = (RectTransform)_next.transform.GetChild(0);
            for (int i = 0; i < 3; i++)
            {
                yield return Tween.Run(0.5f, Ease.Linear, k =>
                {
                    float s = 1f + 0.18f * Ease.Hump(k);
                    icon.localScale = new Vector3(s, s, 1f);
                    icon.anchoredPosition = new Vector2(Ease.Hump(k) * 18f, 0f);
                });
            }
            icon.anchoredPosition = Vector2.zero;
            _arrowHint = null;
        }
    }
}
