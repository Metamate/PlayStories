using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlayStories
{
    /// <summary>
    /// Lightweight UI particle system (pooled Images): bursts of hearts / notes / stars,
    /// speech bubbles and touch sparkles. Lives on a full-screen, non-raycast layer.
    /// </summary>
    public class FxLayer : MonoBehaviour
    {
        class Particle
        {
            public RectTransform rt;
            public Image img;
            public Vector2 vel;
            public float life, age, spin, size, gravity, drag;
            public bool wobble;
        }

        UISkin _skin;
        RectTransform _rt;
        readonly List<Particle> _live = new List<Particle>();
        readonly Stack<Particle> _pool = new Stack<Particle>();

        public static FxLayer Create(Transform parent, UISkin skin)
        {
            var rt = UI.Rect(parent, "FX");
            UI.Stretch(rt);
            var fx = rt.gameObject.AddComponent<FxLayer>();
            fx._skin = skin;
            fx._rt = rt;
            return fx;
        }

        Sprite SpriteFor(ParticleStyle s)
        {
            switch (s)
            {
                case ParticleStyle.Hearts: return _skin.heart;
                case ParticleStyle.Notes: return _skin.note;
                case ParticleStyle.Stars: return _skin.star;
                default: return _skin.sparkle;
            }
        }

        public Vector2 WorldToLocal(Vector3 world)
        {
            var cam = GetComponentInParent<Canvas>().worldCamera;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rt, screen, cam, out var local);
            return local;
        }

        public Vector2 ScreenToLocal(Vector2 screen)
        {
            var cam = GetComponentInParent<Canvas>().worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rt, screen, cam, out var local);
            return local;
        }

        /// <summary>Things float up and out from a point (tap reactions).</summary>
        public void Burst(Vector2 local, ParticleStyle style, int count = 7, float scale = 1f)
        {
            var sprite = SpriteFor(style);
            for (int i = 0; i < count; i++)
            {
                float a = Mathf.Lerp(-70f, 70f, (i + Random.value * 0.6f) / count) * Mathf.Deg2Rad;
                float speed = Random.Range(380f, 620f) * scale;
                Spawn(sprite, local, new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * speed,
                    Random.Range(1.1f, 1.6f), Random.Range(60f, 95f) * scale, -320f, 1.4f, true,
                    style == ParticleStyle.Sparkles ? Color.white : Color.white);
            }
            // a few twinkles in the middle for extra sparkle
            for (int i = 0; i < 3; i++)
                Spawn(_skin.sparkle, local + Random.insideUnitCircle * 60f, Random.insideUnitCircle * 120f,
                    0.6f, Random.Range(70f, 110f) * scale, 0f, 2f, false, new Color(1f, 0.95f, 0.7f));
        }

        /// <summary>Small sparkle where a finger touches the background – rewarding random taps.</summary>
        public void TouchSparkle(Vector2 local)
        {
            for (int i = 0; i < 5; i++)
            {
                var dir = Random.insideUnitCircle.normalized;
                Spawn(i % 2 == 0 ? _skin.sparkle : _skin.star, local, dir * Random.Range(150f, 320f),
                    Random.Range(0.45f, 0.7f), Random.Range(34f, 60f), -200f, 3f, false,
                    i % 2 == 0 ? Color.white : new Color(1f, 1f, 1f, 0.95f));
            }
        }

        /// <summary>Celebration shower from the top of the screen.</summary>
        public void Confetti(int count = 40)
        {
            var r = _rt.rect;
            var sprites = new[] { _skin.star, _skin.heart, _skin.sparkle, _skin.note };
            for (int i = 0; i < count; i++)
            {
                var pos = new Vector2(Random.Range(r.xMin, r.xMax), r.yMax + Random.Range(20f, 300f));
                Spawn(sprites[i % sprites.Length], pos, new Vector2(Random.Range(-80f, 80f), Random.Range(-200f, -50f)),
                    Random.Range(2.5f, 4f), Random.Range(50f, 90f), -260f, 0.4f, true, Color.white);
            }
        }

        void Spawn(Sprite sprite, Vector2 pos, Vector2 vel, float life, float size, float gravity, float drag, bool wobble, Color color)
        {
            var p = _pool.Count > 0 ? _pool.Pop() : NewParticle();
            p.rt.gameObject.SetActive(true);
            p.img.sprite = sprite;
            p.img.color = color;
            p.rt.anchoredPosition = pos;
            p.rt.localScale = Vector3.zero;
            float aspect = sprite != null ? sprite.rect.width / sprite.rect.height : 1f;
            p.rt.sizeDelta = new Vector2(size * aspect, size);
            p.vel = vel; p.life = life; p.age = 0f; p.size = size; p.gravity = gravity; p.drag = drag; p.wobble = wobble;
            p.spin = Random.Range(-90f, 90f);
            p.rt.localEulerAngles = new Vector3(0f, 0f, Random.Range(-20f, 20f));
            _live.Add(p);
        }

        Particle NewParticle()
        {
            var img = UI.Image(_rt, "p", null);
            img.raycastTarget = false;
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            return new Particle { rt = img.rectTransform, img = img };
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var p = _live[i];
                p.age += dt;
                float k = p.age / p.life;
                if (k >= 1f)
                {
                    p.rt.gameObject.SetActive(false);
                    _live.RemoveAt(i);
                    _pool.Push(p);
                    continue;
                }
                // gravity is negative = floats up (hearts), positive pulls down
                p.vel.y -= p.gravity * dt;
                p.vel *= Mathf.Exp(-p.drag * dt);
                Vector2 pos = p.rt.anchoredPosition + p.vel * dt;
                if (p.wobble) pos.x += Mathf.Sin(p.age * 7f + i) * 40f * dt;
                p.rt.anchoredPosition = pos;
                p.rt.localEulerAngles += new Vector3(0f, 0f, p.spin * dt);
                float appear = Ease.OutBack(Mathf.Clamp01(p.age / 0.18f));
                float vanish = 1f - Ease.InQuad(Mathf.Clamp01((k - 0.7f) / 0.3f));
                p.rt.localScale = Vector3.one * appear * Mathf.Lerp(0.4f, 1f, vanish);
                var c = p.img.color; c.a = vanish; p.img.color = c;
            }
        }

        // ------------------------------------------------------------------ speech bubble
        public void SpeechBubble(Vector2 local, string word, int facing, float duration = 1.4f)
        {
            if (string.IsNullOrEmpty(word)) return;
            StartCoroutine(BubbleRoutine(local, word, facing, duration));
        }

        IEnumerator BubbleRoutine(Vector2 local, string word, int facing, float duration)
        {
            var img = UI.Image(_rt, "Bubble", _skin.bubble);
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            // the tail points down-left; mirror when the bubble sits to the left of the head
            bool mirror = facing < 0;
            rt.pivot = new Vector2(mirror ? 0.85f : 0.15f, 0f);
            float w = Mathf.Clamp(130f + word.Length * 52f, 240f, 520f);
            rt.sizeDelta = new Vector2(w, w * 0.6f);
            // keep the whole bubble on screen and below the page title
            var area = _rt.rect;
            float bh = w * 0.6f;
            Vector2 pos = local + new Vector2(mirror ? -30f : 30f, 10f);
            pos.y = Mathf.Min(pos.y, area.yMax - 230f - bh);
            pos.x = mirror ? Mathf.Max(pos.x, area.xMin + 260f + w * 0.85f) : Mathf.Min(pos.x, area.xMax - 260f - w * 0.85f);
            rt.anchoredPosition = pos;
            var txt = UI.Text(rt, "Word", word, 88f, UI.TextStyle.Plain, new Color(0.25f, 0.18f, 0.18f));
            UI.Stretch(txt.rectTransform);
            txt.rectTransform.offsetMin = new Vector2(10f, w * 0.6f * 0.22f);
            txt.enableAutoSizing = true;
            txt.fontSizeMin = 40f; txt.fontSizeMax = 92f;
            if (mirror)
            {
                rt.localScale = new Vector3(-1f, 1f, 1f);
                txt.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            }
            float sx = mirror ? -1f : 1f;
            yield return Tween.Run(0.35f, Ease.OutBack, k => rt.localScale = new Vector3(sx * k, k, 1f));
            float t = 0f;
            Vector2 start = rt.anchoredPosition;
            while (t < duration)
            {
                t += Time.deltaTime;
                rt.anchoredPosition = start + new Vector2(0f, t * 25f);
                rt.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(t * 6f) * 3f);
                yield return null;
            }
            yield return Tween.Run(0.2f, Ease.InBack, k => rt.localScale = new Vector3(sx * (1f - k), 1f - k, 1f));
            Destroy(rt.gameObject);
        }
    }

    /// <summary>Slowly drifting clouds and a fluttering butterfly to make the backdrop feel alive.</summary>
    public class AmbientLife : MonoBehaviour
    {
        class Drifter { public RectTransform rt; public float speed, bob, phase, y; }

        readonly List<Drifter> _clouds = new List<Drifter>();
        RectTransform _rt, _butterfly;
        Vector2 _bfTarget, _bfPos, _bfVel;
        float _bfTimer;

        public static AmbientLife Create(Transform parent, UISkin skin, int clouds = 3, bool butterfly = true)
        {
            var rt = UI.Rect(parent, "AmbientLife");
            UI.Stretch(rt);
            var a = rt.gameObject.AddComponent<AmbientLife>();
            a._rt = rt;
            for (int i = 0; i < clouds; i++)
            {
                var img = UI.Image(rt, "Cloud", skin.cloud, new Color(1f, 1f, 1f, Random.Range(0.75f, 0.95f)));
                img.raycastTarget = false;
                var crt = img.rectTransform;
                crt.anchorMin = crt.anchorMax = new Vector2(0f, 1f);
                float s = Random.Range(0.55f, 1f);
                crt.sizeDelta = new Vector2(450f, 200f) * s;
                a._clouds.Add(new Drifter
                {
                    rt = crt, speed = Random.Range(12f, 26f) * (1.2f - s * 0.4f), bob = Random.Range(4f, 10f),
                    phase = Random.value * 10f, y = -Random.Range(40f, 230f),
                });
                crt.anchoredPosition = new Vector2(Random.Range(0f, 2600f), a._clouds[i].y);
            }
            if (butterfly && skin.butterfly != null)
            {
                var bf = UI.Image(rt, "Butterfly", skin.butterfly);
                bf.raycastTarget = false;
                a._butterfly = bf.rectTransform;
                a._butterfly.anchorMin = a._butterfly.anchorMax = new Vector2(0.5f, 0.5f);
                a._butterfly.sizeDelta = new Vector2(110f, 82f);
                a._bfPos = new Vector2(-600f, 100f);
            }
            return a;
        }

        void Update()
        {
            float w = _rt.rect.width;
            foreach (var c in _clouds)
            {
                var p = c.rt.anchoredPosition;
                p.x += c.speed * Time.deltaTime;
                if (p.x > w + 300f) p.x = -500f;
                p.y = c.y + Mathf.Sin(Time.time * 0.3f + c.phase) * c.bob;
                c.rt.anchoredPosition = p;
            }
            if (_butterfly != null)
            {
                var r = _rt.rect;
                _bfTimer -= Time.deltaTime;
                if (_bfTimer <= 0f)
                {
                    _bfTimer = Random.Range(1.5f, 3.5f);
                    _bfTarget = new Vector2(Random.Range(r.xMin * 0.9f, r.xMax * 0.9f), Random.Range(r.yMin * 0.1f, r.yMax * 0.6f));
                }
                Vector2 to = _bfTarget - _bfPos;
                _bfVel += to.normalized * 260f * Time.deltaTime;
                _bfVel = Vector2.ClampMagnitude(_bfVel, 170f);
                _bfPos += _bfVel * Time.deltaTime + new Vector2(0f, Mathf.Sin(Time.time * 9f) * 50f * Time.deltaTime);
                _butterfly.anchoredPosition = _bfPos;
                float flap = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(Time.time * 14f));
                _butterfly.localScale = new Vector3(flap * (_bfVel.x < 0f ? -1f : 1f), 1f, 1f);
                _butterfly.localEulerAngles = new Vector3(0f, 0f, Mathf.Clamp(_bfVel.y * 0.08f, -20f, 20f));
            }
        }
    }
}
