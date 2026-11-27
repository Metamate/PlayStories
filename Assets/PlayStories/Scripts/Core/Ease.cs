using System;
using System.Collections;
using UnityEngine;

namespace PlayStories
{
    /// <summary>Easing curves. All take t in [0,1].</summary>
    public static class Ease
    {
        public static float Linear(float t) => t;
        public static float InQuad(float t) => t * t;
        public static float OutQuad(float t) => 1f - (1f - t) * (1f - t);
        public static float InOutQuad(float t) => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
        public static float OutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        public static float InCubic(float t) => t * t * t;
        public static float InOutCubic(float t) => t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
        public static float InOutSine(float t) => -(Mathf.Cos(Mathf.PI * t) - 1f) / 2f;

        public static float OutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        public static float InBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return c3 * t * t * t - c1 * t * t;
        }

        public static float OutElastic(float t)
        {
            const float c4 = 2f * Mathf.PI / 3f;
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
        }

        public static float OutBounce(float t)
        {
            const float n1 = 7.5625f, d1 = 2.75f;
            if (t < 1f / d1) return n1 * t * t;
            if (t < 2f / d1) return n1 * (t -= 1.5f / d1) * t + 0.75f;
            if (t < 2.5f / d1) return n1 * (t -= 2.25f / d1) * t + 0.9375f;
            return n1 * (t -= 2.625f / d1) * t + 0.984375f;
        }

        /// <summary>Damped spring "settle" from 1 to 0 - nice for wobbles.</summary>
        public static float Wobble(float t, float frequency = 3f, float damping = 5f)
            => Mathf.Exp(-damping * t) * Mathf.Cos(frequency * 2f * Mathf.PI * t);

        /// <summary>0 → 1 → 0 hump.</summary>
        public static float Hump(float t) => Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
    }

    /// <summary>Tiny coroutine-based tweening helpers (no external dependencies).</summary>
    public static class Tween
    {
        /// <summary>Runs <paramref name="step"/> with eased progress over <paramref name="duration"/> seconds.</summary>
        public static IEnumerator Run(float duration, Func<float, float> ease, Action<float> step, bool unscaled = false)
        {
            if (duration <= 0f) { step(1f); yield break; }
            float t = 0f;
            while (t < duration)
            {
                step(ease(Mathf.Clamp01(t / duration)));
                yield return null;
                t += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            }
            step(ease(1f));
        }

        public static IEnumerator Delay(float seconds, Action then)
        {
            yield return new WaitForSeconds(seconds);
            then?.Invoke();
        }

        public static IEnumerator Scale(Transform tr, Vector3 from, Vector3 to, float duration, Func<float, float> ease)
            => Run(duration, ease, k => { if (tr) tr.localScale = Vector3.LerpUnclamped(from, to, k); });

        public static IEnumerator Fade(CanvasGroup cg, float to, float duration, Func<float, float> ease = null)
        {
            float from = cg.alpha;
            return Run(duration, ease ?? Ease.Linear, k => { if (cg) cg.alpha = Mathf.LerpUnclamped(from, to, k); });
        }

        public static IEnumerator Move(RectTransform rt, Vector2 from, Vector2 to, float duration, Func<float, float> ease)
            => Run(duration, ease, k => { if (rt) rt.anchoredPosition = Vector2.LerpUnclamped(from, to, k); });

        /// <summary>Quick squash-and-stretch "pop" used for button presses and appearances.</summary>
        public static IEnumerator Punch(Transform tr, float amount = 0.15f, float duration = 0.35f)
        {
            Vector3 baseScale = Vector3.one;
            return Run(duration, Ease.Linear, k =>
            {
                if (!tr) return;
                float w = Ease.Wobble(k, 2.2f, 4.5f) * amount * (1f - k);
                tr.localScale = new Vector3(baseScale.x * (1f - w * 0.6f), baseScale.y * (1f + w), 1f);
            });
        }
    }
}
