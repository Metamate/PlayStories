using System;
using System.Collections;
using UnityEngine;

namespace PlayStories
{
    /// <summary>Base for full-screen pages of the app. Provides the shared corner buttons.</summary>
    public abstract class ScreenBase : MonoBehaviour
    {
        protected AppController App;
        protected UISkin Skin;
        protected RectTransform Root;
        protected RectTransform Safe;   // inside the device safe area – put buttons here

        public void Init(AppController app)
        {
            App = app;
            Skin = app.skin;
            Root = (RectTransform)transform;
        }

        public abstract void Build();
        public virtual IEnumerator Enter() { yield break; }

        protected RectTransform MakeSafeArea(Transform parent)
        {
            var rt = UI.Rect(parent, "SafeArea");
            UI.Stretch(rt);
            rt.gameObject.AddComponent<SafeArea>();
            return rt;
        }

        // ------------------------------------------------------------------ shared chrome
        protected const float Margin = 36f;
        protected static readonly Vector2 CornerIcon = new Vector2(150f, 150f);

        /// <summary>Bottom-left: settings / parental controls (parental hold). Closing the app lives inside settings.</summary>
        protected HoldButton AddSettingsButton(Transform parent)
        {
            var b = HoldButton.Create(parent, "Settings", Skin.gear, new Vector2(110f, 110f), Skin.parentalHoldSeconds, Skin, App.OpenSettings, false);
            UI.Anchor((RectTransform)b.transform, Vector2.zero, Vector2.zero, new Vector2(Margin - 20f, Margin - 20f), ((RectTransform)b.transform).sizeDelta);
            return b;
        }

        /// <summary>Top-left curly "back" arrow.</summary>
        protected Component AddBackButton(Transform parent, Action onBack, float holdSeconds = 0f)
        {
            RectTransform rt;
            Component c;
            if (holdSeconds > 0f)
            {
                var h = HoldButton.Create(parent, "Back", Skin.back, new Vector2(170f, 150f), holdSeconds, Skin, onBack);
                rt = (RectTransform)h.transform; c = h;
            }
            else
            {
                var b = PressButton.Create(parent, "Back", Skin.back, new Vector2(170f, 150f), onBack, Skin.sfxTap);
                rt = (RectTransform)b.transform; c = b;
            }
            UI.Anchor(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Margin - 30f, -Margin + 30f), rt.sizeDelta);
            return c;
        }
    }
}
