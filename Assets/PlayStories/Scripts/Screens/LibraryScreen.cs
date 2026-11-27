using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlayStories
{
    /// <summary>
    /// Bookshelf: a horizontally scrolling row of big book cards (landscape-friendly, easy for
    /// small fingers to swipe) with an age filter.
    /// </summary>
    public class LibraryScreen : ScreenBase
    {
        class Card
        {
            public BookDefinition book;
            public RectTransform rt, coverRt;
        }

        struct AgeFilter { public string label; public int min, max; }

        readonly List<Card> _cards = new List<Card>();
        readonly List<(Image bg, TextMeshProUGUI text)> _chips = new List<(Image, TextMeshProUGUI)>();
        RectTransform _content, _chipRow;
        ScrollRect _scroll;
        AgeFilter[] _filters;
        int _filter;
        bool _chipsOpen = true;

        static readonly Color ChipOn = new Color(1f, 0.8f, 0.3f);
        static readonly Color ChipOff = new Color(1f, 1f, 1f, 0.9f);

        public override void Build()
        {
            _filters = new[]
            {
                new AgeFilter { label = Skin.filterAll.Get(), min = 0, max = 999 },
                new AgeFilter { label = Skin.ageFormat.Format(0, 1), min = 0, max = 12 },
                new AgeFilter { label = Skin.ageFormat.Format(1, 3), min = 12, max = 36 },
            };

            UI.CoverImage(Root, "Background", Skin.welcomeBackground);
            var tint = UI.Image(Root, "Tint", null, new Color(1f, 0.97f, 0.9f, 0.55f));
            UI.Stretch(tint.rectTransform);
            tint.raycastTarget = false;
            AmbientLife.Create(Root, Skin, 2, false);

            Safe = MakeSafeArea(Root);

            // --- header
            var title = UI.Text(Safe, "Title", Skin.libraryTitle.Get(), 120f);
            UI.Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(230f, -40f), new Vector2(700f, 150f));
            title.alignment = TextAlignmentOptions.Left;

            var filterBtn = PressButton.Create(Safe, "Filter", Skin.filter, new Vector2(90f, 90f), ToggleChips, Skin.sfxTap, 20f);
            UI.Anchor((RectTransform)filterBtn.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(810f, -48f), new Vector2(130f, 130f));

            _chipRow = UI.Rect(Safe, "Chips");
            UI.Anchor(_chipRow, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(950f, -60f), new Vector2(900f, 110f));
            var h = _chipRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 20f; h.childForceExpandWidth = false; h.childControlWidth = true; h.childControlHeight = true;
            h.childAlignment = TextAnchor.MiddleLeft;
            for (int i = 0; i < _filters.Length; i++)
            {
                int idx = i;
                var chip = UI.Image(_chipRow, "Chip " + _filters[i].label, Skin.panelPlain, ChipOff);
                var le = chip.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 220f; le.preferredHeight = 96f;
                var txt = UI.Text(chip.transform, "Label", _filters[i].label, 46f, UI.TextStyle.Plain, Skin.darkTextColor);
                UI.Stretch(txt.rectTransform);
                var pb = chip.gameObject.AddComponent<PressButton>();
                pb.onClick = () => SetFilter(idx);
                pb.sound = Skin.sfxTap;
                _chips.Add((chip, txt));
            }

            // --- shelf
            var viewport = UI.Rect(Root, "Shelf");
            viewport.anchorMin = new Vector2(0f, 0.14f);
            viewport.anchorMax = new Vector2(1f, 0.8f);
            viewport.offsetMin = viewport.offsetMax = Vector2.zero;
            viewport.gameObject.AddComponent<RectMask2D>().softness = new Vector2Int(60, 0);
            var vpImg = viewport.gameObject.AddComponent<Image>();
            vpImg.color = new Color(0, 0, 0, 0);

            _content = UI.Rect(viewport, "Content");
            _content.anchorMin = new Vector2(0f, 0f);
            _content.anchorMax = new Vector2(0f, 1f);
            _content.pivot = new Vector2(0f, 0.5f);
            var row = _content.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 70f;
            row.padding = new RectOffset(160, 160, 10, 10);
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = false; row.childControlHeight = false;
            row.childForceExpandWidth = false; row.childForceExpandHeight = false;
            var fitter = _content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scroll = viewport.gameObject.AddComponent<ScrollRect>();
            _scroll.content = _content;
            _scroll.viewport = viewport;
            _scroll.horizontal = true;
            _scroll.vertical = false;
            _scroll.movementType = ScrollRect.MovementType.Elastic;
            _scroll.decelerationRate = 0.12f;
            _scroll.scrollSensitivity = 30f;

            foreach (var book in App.library.books)
                if (book != null) _cards.Add(BuildCard(book));

            AddBackButton(Safe, App.GoToWelcome);
            AddSettingsButton(Safe);
            SetFilter(0, false);
        }

        Card BuildCard(BookDefinition book)
        {
            var card = new Card { book = book };
            var rt = UI.Rect(_content, "Card " + book.id);
            rt.sizeDelta = new Vector2(560f, 560f);
            card.rt = rt;

            var visual = UI.Rect(rt, "Visual");
            UI.Stretch(visual);
            var shadow = UI.Image(visual, "Shadow", Skin.shadow);
            UI.Anchor(shadow.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(560f, 90f));
            shadow.raycastTarget = false;

            var frame = UI.Image(visual, "Frame", Skin.panel, Color.white);
            UI.Anchor(frame.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(540f, 430f));
            var cover = UI.Image(frame.transform, "Cover", book.cover);
            UI.Stretch(cover.rectTransform, 22f);
            cover.preserveAspect = false;
            cover.raycastTarget = false;
            card.coverRt = cover.rectTransform;

            // age badge
            var badge = UI.Image(frame.transform, "Age", Skin.panelPlain, new Color(1f, 0.83f, 0.35f));
            UI.Anchor(badge.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(6f, 14f), new Vector2(170f, 70f));
            badge.raycastTarget = false;
            var ageTxt = UI.Text(badge.transform, "Text", AgeLabel(book), 38f, UI.TextStyle.Plain, Skin.darkTextColor);
            UI.Stretch(ageTxt.rectTransform);

            var label = UI.Text(visual, "Title", book.title.Get(), 64f, UI.TextStyle.OutlinedThin);
            UI.Anchor(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(560f, 100f));
            label.enableAutoSizing = true; label.fontSizeMin = 36f; label.fontSizeMax = 64f;

            if (!book.IsPlayable)
            {
                cover.color = new Color(0.72f, 0.72f, 0.72f);
                var lockImg = UI.Image(frame.transform, "Lock", Skin.lockIcon);
                UI.Anchor(lockImg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(110f, 140f));
                lockImg.raycastTarget = false;
                var soon = UI.Image(frame.transform, "Soon", Skin.panelPlain, new Color(1f, 1f, 1f, 0.92f));
                UI.Anchor(soon.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(330f, 70f));
                soon.raycastTarget = false;
                var st = UI.Text(soon.transform, "Text", Skin.comingSoon.Get(), 40f, UI.TextStyle.Plain, new Color(0.4f, 0.4f, 0.45f));
                UI.Stretch(st.rectTransform);
            }

            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            var btn = rt.gameObject.AddComponent<PressButton>();
            btn.visual = visual;
            btn.onClick = () => OnCardTapped(card);
            return card;
        }

        string AgeLabel(BookDefinition b)
        {
            string Fmt(int months) => months % 12 == 0 ? (months / 12).ToString() : (months / 12f).ToString("0.#");
            return Skin.ageFormat.Format(Fmt(b.minAgeMonths), Fmt(b.maxAgeMonths));
        }

        void OnCardTapped(Card card)
        {
            if (App.Transitioning) return;
            if (!card.book.IsPlayable)
            {
                App.Audio.PlaySfx(Skin.sfxBoing, 0.7f);
                StartCoroutine(Shake(card.rt));
                return;
            }
            App.OpenBook(card.book, card.coverRt);
        }

        IEnumerator Shake(RectTransform rt)
        {
            var v = rt.GetChild(0);
            yield return Tween.Run(0.5f, Ease.Linear, k => v.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(k * Mathf.PI * 6f) * 6f * (1f - k)));
        }

        void ToggleChips()
        {
            _chipsOpen = !_chipsOpen;
            StartCoroutine(Tween.Run(0.25f, _chipsOpen ? Ease.OutBack : Ease.InQuad, k =>
            {
                float s = _chipsOpen ? k : 1f - k;
                _chipRow.localScale = new Vector3(s, s, 1f);
            }));
        }

        void SetFilter(int index, bool animate = true)
        {
            _filter = index;
            var f = _filters[index];
            for (int i = 0; i < _chips.Count; i++)
            {
                _chips[i].bg.color = i == index ? ChipOn : ChipOff;
                _chips[i].text.color = i == index ? Color.white : Skin.darkTextColor;
            }
            int shown = 0;
            foreach (var c in _cards)
            {
                bool match = c.book.minAgeMonths < f.max && c.book.maxAgeMonths > f.min;
                c.rt.gameObject.SetActive(match);
                if (match && animate) StartCoroutine(PopIn(c.rt, shown * 0.06f));
                if (match) shown++;
            }
            _scroll.horizontalNormalizedPosition = 0f;
        }

        IEnumerator PopIn(RectTransform rt, float delay)
        {
            var v = rt.GetChild(0);
            v.localScale = Vector3.zero;
            yield return new WaitForSeconds(delay);
            yield return Tween.Run(0.4f, Ease.OutBack, k => v.localScale = Vector3.one * k);
        }

        public override IEnumerator Enter()
        {
            App.Audio.PlayAmbience(Skin.welcomeAmbience, 0.25f);
            for (int i = 0; i < _cards.Count; i++)
                if (_cards[i].rt.gameObject.activeSelf) StartCoroutine(PopIn(_cards[i].rt, 0.1f + i * 0.08f));
            yield break;
        }
    }
}
