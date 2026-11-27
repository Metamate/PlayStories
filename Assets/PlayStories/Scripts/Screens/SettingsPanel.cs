using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlayStories
{
    /// <summary>Parent settings (opened via the hold-to-open gear).</summary>
    public class SettingsPanel : MonoBehaviour
    {
        static SettingsPanel s_Open;
        UISkin _skin;
        RectTransform _card;
        CanvasGroup _group;

        bool _animateIn = true;

        public static void CloseIfOpen() { if (s_Open != null) s_Open.Close(); }

        public static void Open(RectTransform layer, UISkin skin, bool animate = true)
        {
            if (s_Open != null) return;
            var rt = UI.Rect(layer, "Settings");
            UI.Stretch(rt);
            var p = rt.gameObject.AddComponent<SettingsPanel>();
            p._skin = skin;
            p._animateIn = animate;
            p.Build(rt);
            s_Open = p;
        }

        void Build(RectTransform root)
        {
            _group = root.gameObject.AddComponent<CanvasGroup>();
            var dim = UI.Image(root, "Dim", null, new Color(0.1f, 0.15f, 0.1f, 0.55f));
            UI.Stretch(dim.rectTransform);
            dim.gameObject.AddComponent<ClickRelay>().onClick = Close;

            _card = UI.Image(root, "Card", _skin.panel, new Color(1f, 0.98f, 0.94f)).rectTransform;
            UI.Anchor(_card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200f, 1150f));

            var title = UI.Text(_card, "Title", _skin.settingsTitle.Get(), 90f);
            UI.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(900f, 120f));

            var close = PressButton.Create(_card, "Close", _skin.close, new Vector2(100f, 100f), Close, _skin.sfxTap, 20f);
            UI.Anchor((RectTransform)close.transform, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(-30f, -30f), new Vector2(140f, 140f));

            var list = UI.Rect(_card, "Rows");
            list.anchorMin = new Vector2(0f, 0f); list.anchorMax = new Vector2(1f, 1f);
            list.offsetMin = new Vector2(80f, 40f); list.offsetMax = new Vector2(-80f, -170f);
            var v = list.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 12f; v.childControlHeight = true; v.childControlWidth = true; v.childForceExpandHeight = false;

            LanguagePicker(list, _skin.settingLanguage.Get());
            Toggle(list, _skin.settingKidLock.Get(), Settings.KidLock, b => Settings.KidLock = b);
            var lockHint = KidLock.IsIOS ? _skin.kidLockHintIOS.Get() : _skin.kidLockHintAndroid.Get();
            var lh = UI.Text(list, "KidLockHint", lockHint, 30f, UI.TextStyle.Plain, new Color(0.4f, 0.45f, 0.4f), TextAlignmentOptions.Left);
            lh.textWrappingMode = TextWrappingModes.Normal;
            lh.margin = new Vector4(30f, 0f, 30f, 0f);
            lh.gameObject.AddComponent<LayoutElement>().preferredHeight = 70f;
            Toggle(list, _skin.settingAutoNarrate.Get(), Settings.AutoNarrate, b => Settings.AutoNarrate = b);
            Toggle(list, _skin.settingAmbience.Get(), Settings.AmbienceEnabled, b => Settings.AmbienceEnabled = b);
            Toggle(list, _skin.settingSfx.Get(), Settings.SoundEffectsEnabled, b => Settings.SoundEffectsEnabled = b);
            Toggle(list, _skin.settingHints.Get(), Settings.Hints, b => Settings.Hints = b);
            Slider(list, _skin.settingVolume.Get(), Settings.MasterVolume, f => Settings.MasterVolume = f);

            if (AppController.CanQuit) CloseAppButton(list);

            var info = UI.Text(list, "Info", "PlayStories – prototype", 30f, UI.TextStyle.Plain, new Color(0.5f, 0.5f, 0.5f));
            info.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;

            if (_animateIn) StartCoroutine(Show());
        }

        IEnumerator Show()
        {
            _group.alpha = 0f;
            _card.localScale = Vector3.one * 0.7f;
            yield return Tween.Run(0.3f, Ease.OutBack, k => { _group.alpha = Mathf.Clamp01(k * 2f); _card.localScale = Vector3.one * Mathf.LerpUnclamped(0.7f, 1f, k); }, true);
        }

        void Close() => StartCoroutine(CloseRoutine());

        IEnumerator CloseRoutine()
        {
            yield return Tween.Run(0.2f, Ease.InQuad, k => { _group.alpha = 1f - k; _card.localScale = Vector3.one * (1f - 0.2f * k); }, true);
            s_Open = null;
            Destroy(gameObject);
        }

        void LanguagePicker(Transform parent, string label)
        {
            var row = Row(parent, label);
            var group = UI.Rect(row.transform, "Languages");
            UI.Anchor(group, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(520f, 84f));
            var h = group.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 14f; h.childControlWidth = h.childControlHeight = true; h.childForceExpandWidth = true;
            foreach (var lang in Localization.All)
            {
                var l = lang;
                bool on = Localization.Current == l;
                var chip = UI.Image(group, Localization.Code(l), _skin.panelPlain, on ? new Color(1f, 0.8f, 0.3f) : new Color(1f, 1f, 1f, 0.95f));
                var t = UI.Text(chip.transform, "Label", Localization.NativeName(l), 42f, UI.TextStyle.Plain, on ? Color.white : _skin.darkTextColor);
                UI.Stretch(t.rectTransform);
                var b = chip.gameObject.AddComponent<PressButton>();
                b.sound = _skin.sfxTap;
                b.onClick = () =>
                {
                    if (Localization.Current == l) return;
                    Localization.Current = l;              // screens rebuild themselves (AppController)
                    var layer = (RectTransform)transform.parent;
                    s_Open = null;
                    Destroy(gameObject);
                    Open(layer, _skin, false);             // re-open the panel in the new language
                };
            }
        }

        /// <summary>The settings panel is already behind the 5-second hold, so closing is a normal tap here.</summary>
        void CloseAppButton(Transform parent)
        {
            var holder = UI.Rect(parent, "CloseApp");
            holder.gameObject.AddComponent<LayoutElement>().preferredHeight = 100f;
            var btn = UI.Image(holder, "Button", _skin.panelPlain, new Color(0.86f, 0.33f, 0.28f));
            UI.Anchor(btn.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460f, 92f));
            var t = UI.Text(btn.transform, "Label", _skin.closeApp.Get(), 44f, UI.TextStyle.Plain, Color.white);
            UI.Stretch(t.rectTransform);
            var b = btn.gameObject.AddComponent<PressButton>();
            b.sound = _skin.sfxTap;
            b.onClick = () => AppController.Instance.QuitApp();
        }

        Image Row(Transform parent, string label)
        {
            var row = UI.Image(parent, label, _skin.panelPlain, new Color(0.93f, 0.95f, 0.88f));
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 92f;
            var t = UI.Text(row.transform, "Label", label, 46f, UI.TextStyle.Plain, _skin.darkTextColor, TextAlignmentOptions.Left);
            t.rectTransform.anchorMin = new Vector2(0f, 0f); t.rectTransform.anchorMax = new Vector2(0.7f, 1f);
            t.rectTransform.offsetMin = new Vector2(40f, 0f); t.rectTransform.offsetMax = Vector2.zero;
            return row;
        }

        void Toggle(Transform parent, string label, bool value, Action<bool> set)
        {
            var row = Row(parent, label);
            var track = UI.Image(row.transform, "Track", _skin.panelPlain);
            UI.Anchor(track.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-30f, 0f), new Vector2(170f, 80f));
            var knob = UI.Image(track.transform, "Knob", _skin.circle);
            knob.raycastTarget = false;
            var krt = knob.rectTransform;
            krt.anchorMin = krt.anchorMax = new Vector2(0.5f, 0.5f);
            krt.sizeDelta = new Vector2(66f, 66f);
            bool state = value;
            void Apply(bool animate)
            {
                Color on = new Color(0.45f, 0.75f, 0.35f), off = new Color(0.75f, 0.75f, 0.75f);
                track.color = state ? on : off;
                Vector2 target = new Vector2(state ? 45f : -45f, 0f);
                if (animate) StartCoroutine(Tween.Move(krt, krt.anchoredPosition, target, 0.18f, Ease.OutBack));
                else krt.anchoredPosition = target;
            }
            Apply(false);
            var b = track.gameObject.AddComponent<PressButton>();
            b.sound = _skin.sfxTap;
            b.onClick = () => { state = !state; set(state); Apply(true); };
        }

        void Slider(Transform parent, string label, float value, Action<float> set)
        {
            var row = Row(parent, label);
            var sliderRt = UI.Rect(row.transform, "Slider");
            UI.Anchor(sliderRt, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-30f, 0f), new Vector2(520f, 80f));
            var bg = UI.Image(sliderRt, "Bg", _skin.panelPlain, new Color(0.8f, 0.8f, 0.8f));
            bg.rectTransform.anchorMin = new Vector2(0f, 0.35f); bg.rectTransform.anchorMax = new Vector2(1f, 0.65f);
            bg.rectTransform.offsetMin = bg.rectTransform.offsetMax = Vector2.zero;
            var fillArea = UI.Rect(sliderRt, "FillArea");
            fillArea.anchorMin = new Vector2(0f, 0.35f); fillArea.anchorMax = new Vector2(1f, 0.65f);
            fillArea.offsetMin = fillArea.offsetMax = Vector2.zero;
            var fill = UI.Image(fillArea, "Fill", _skin.panelPlain, new Color(0.45f, 0.75f, 0.35f));
            fill.rectTransform.sizeDelta = Vector2.zero;
            var handleArea = UI.Rect(sliderRt, "HandleArea");
            UI.Stretch(handleArea);
            handleArea.offsetMin = new Vector2(35f, 0f); handleArea.offsetMax = new Vector2(-35f, 0f);
            var handle = UI.Image(handleArea, "Handle", _skin.circle);
            handle.rectTransform.sizeDelta = new Vector2(70f, 0f);
            var s = sliderRt.gameObject.AddComponent<Slider>();
            s.fillRect = fill.rectTransform;
            s.handleRect = handle.rectTransform;
            s.targetGraphic = handle;
            s.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            s.minValue = 0f; s.maxValue = 1f;
            s.value = value;
            s.onValueChanged.AddListener(f => set(f));
        }
    }
}
