using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PlayStories
{
    /// <summary>
    /// Title screen: animated title, a big storybook "hero image" with characters peeking out,
    /// tap anywhere to open the library (the book bursts open).
    /// </summary>
    public class WelcomeScreen : ScreenBase, IPointerClickHandler
    {
        TextMeshProUGUI _title, _tapHint;
        RectTransform _book, _bookInner;
        readonly List<CharacterActor> _actors = new List<CharacterActor>();
        FxLayer _fx;
        bool _leaving;

        public override void Build()
        {
            var catcher = gameObject.AddComponent<Image>();
            catcher.color = new Color(0, 0, 0, 0); // whole screen is tappable

            UI.CoverImage(Root, "Background", Skin.welcomeBackground);
            AmbientLife.Create(Root, Skin, 3, true);

            // --- hero: a storybook with the farm cover
            _book = UI.Rect(Root, "HeroBook");
            UI.Anchor(_book, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(700f, 540f));
            var shadow = UI.Image(_book, "Shadow", Skin.shadow);
            UI.Anchor(shadow.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(780f, 120f));
            _bookInner = UI.Rect(_book, "Inner");
            UI.Stretch(_bookInner);
            var frame = UI.Image(_bookInner, "Frame", Skin.panel);
            UI.Stretch(frame.rectTransform);
            var cover = UI.Image(_bookInner, "Cover", Skin.welcomeCover != null ? Skin.welcomeCover : Skin.welcomeBackground);
            UI.Stretch(cover.rectTransform, 26f);
            var play = UI.Image(_bookInner, "Play", Skin.play);
            UI.Anchor(play.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(190f, 190f));
            var bob = play.gameObject.AddComponent<Bobber>();
            bob.bob = 0f; bob.pulse = 0.08f; bob.period = 1.4f;
            var bookBob = _bookInner.gameObject.AddComponent<Bobber>();
            bookBob.bob = 8f; bookBob.pulse = 0f; bookBob.period = 3.2f; bookBob.rotate = 1.5f;
            foreach (var g in _bookInner.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
            shadow.raycastTarget = false;

            // --- characters standing either side of the book
            var chars = Skin.welcomeCharacters ?? new CharacterDefinition[0];
            var slots = new[] { new Vector2(-0.33f, 0f), new Vector2(0.33f, 0f), new Vector2(-0.44f, 0f), new Vector2(0.44f, 0f) };
            for (int i = 0; i < chars.Length && i < slots.Length; i++)
            {
                if (chars[i] == null) continue;
                var slot = UI.Rect(Root, "Slot" + i);
                slot.anchorMin = slot.anchorMax = new Vector2(0.5f + slots[i].x, 0.06f);
                slot.pivot = new Vector2(0.5f, 0f);
                float h = i < 2 ? 420f : 300f;
                slot.sizeDelta = new Vector2(h * 1.3f, h);
                if (i >= 2) slot.SetSiblingIndex(_book.GetSiblingIndex()); // smaller ones further back
                var a = CharacterActor.Create(slot, chars[i], Skin, false);
                foreach (var g in a.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
                _actors.Add(a);
            }

            // --- title
            _title = UI.Text(Root, "Title", Skin.appTitle.Get(), 190f);
            UI.Anchor(_title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(1400f, 230f));
            var wave = _title.gameObject.AddComponent<WavyText>();
            wave.loop = true; wave.amplitude = 26f; wave.speed = 7f;

            _tapHint = UI.Text(Root, "TapHint", Skin.tapToStart.Get(), 64f, UI.TextStyle.OutlinedThin);
            UI.Anchor(_tapHint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1000f, 90f));
            var hb = _tapHint.gameObject.AddComponent<Bobber>();
            hb.bob = 6f; hb.pulse = 0.04f; hb.period = 1.8f;

            _fx = FxLayer.Create(Root, Skin);

            Safe = MakeSafeArea(Root);
            AddSettingsButton(Safe);
        }

        public override IEnumerator Enter()
        {
            App.Audio.PlayAmbience(Skin.welcomeAmbience, 0.35f);
            _book.localScale = Vector3.zero;
            _title.alpha = 0f;
            _tapHint.alpha = 0f;
            for (int i = 0; i < _actors.Count; i++) _actors[i].PlayEnter(0.45f + i * 0.15f);
            yield return Tween.Run(0.3f, Ease.Linear, k => _title.alpha = k);
            App.Audio.PlaySfx(Skin.sfxPop, 0.7f);
            yield return Tween.Run(0.55f, Ease.OutBack, k => _book.localScale = Vector3.one * k);
            yield return new WaitForSeconds(0.8f);
            yield return Tween.Run(0.4f, Ease.Linear, k => _tapHint.alpha = k);
            // every now and then one of the animals does its trick (silently)
            while (!_leaving)
            {
                yield return new WaitForSeconds(Random.Range(2.5f, 4.5f));
                if (_actors.Count > 0 && !_leaving) _actors[Random.Range(0, _actors.Count)].React(false, 0.8f);
            }
        }

        public void OnPointerClick(PointerEventData e) => Open();

        /// <summary>Opens the library with the "book bursts open" animation.</summary>
        public void Open()
        {
            if (_leaving || App.Transitioning) return;
            _leaving = true;
            StartCoroutine(OpenRoutine());
        }

        IEnumerator OpenRoutine()
        {
            App.Audio.PlaySfx(Skin.sfxOpen, 0.9f);
            _fx.Burst(_fx.WorldToLocal(_book.position), ParticleStyle.Stars, 10, 1.3f);
            foreach (var a in _actors) a.React(false, 1.2f);
            // the book "opens": squash, swell and flip towards the viewer
            yield return Tween.Run(0.18f, Ease.OutQuad, k => _book.localScale = new Vector3(1f + 0.1f * k, 1f - 0.08f * k, 1f));
            yield return Tween.Run(0.45f, Ease.InBack, k =>
            {
                _book.localScale = Vector3.one * Mathf.LerpUnclamped(1.05f, 1.9f, k);
                _bookInner.localEulerAngles = new Vector3(0f, 0f, -6f * k);
            });
            App.GoToLibrary();
        }
    }
}
