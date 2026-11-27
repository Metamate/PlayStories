using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PlayStories
{
    /// <summary>
    /// Big, forgiving, squishy button. Fires on release (or on press when <see cref="fireOnDown"/>),
    /// squashes while held and wobbles back.
    /// </summary>
    public class PressButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, IPointerExitHandler
    {
        public Action onClick;
        public bool fireOnDown;
        public AudioClip sound;
        public bool interactable = true;
        public RectTransform visual; // the thing that gets squished (defaults to self)

        bool _down;
        Coroutine _anim;

        void Awake() { if (visual == null) visual = (RectTransform)transform; }

        public void OnPointerDown(PointerEventData e)
        {
            if (!interactable) return;
            _down = true;
            Animate(new Vector3(1.12f, 0.88f, 1f), 0.08f);
            if (fireOnDown) Fire();
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!_down) return;
            _down = false;
            Restart(Tween.Punch(visual, 0.18f, 0.45f));
        }

        public void OnPointerExit(PointerEventData e)
        {
            if (!_down) return;
            _down = false;
            Animate(Vector3.one, 0.12f);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (!interactable || fireOnDown) return;
            Fire();
        }

        void Fire()
        {
            if (sound != null) AudioManager.Instance?.PlaySfx(sound, 0.8f, UnityEngine.Random.Range(0.95f, 1.08f));
            onClick?.Invoke();
        }

        void Animate(Vector3 to, float d)
        {
            var from = visual.localScale;
            Restart(Tween.Run(d, Ease.OutQuad, k => visual.localScale = Vector3.LerpUnclamped(from, to, k)));
        }

        void Restart(IEnumerator r)
        {
            if (_anim != null) StopCoroutine(_anim);
            if (isActiveAndEnabled) _anim = StartCoroutine(r);
        }

        void OnDisable() { if (visual) visual.localScale = Vector3.one; _down = false; }

        /// <summary>Creates an icon button with a generous invisible hit area.</summary>
        public static PressButton Create(Transform parent, string name, Sprite icon, Vector2 size, Action onClick, AudioClip sound, float hitPadding = 30f)
        {
            var hit = UI.Rect(parent, name);
            hit.sizeDelta = size + Vector2.one * hitPadding * 2f;
            var hitImg = hit.gameObject.AddComponent<Image>();
            hitImg.color = new Color(1, 1, 1, 0); // invisible but raycastable
            var img = UI.Image(hit, "Icon", icon);
            img.raycastTarget = false;
            img.preserveAspect = true;
            UI.Stretch(img.rectTransform, hitPadding);
            var b = hit.gameObject.AddComponent<PressButton>();
            b.visual = img.rectTransform;
            b.onClick = onClick;
            b.sound = sound;
            return b;
        }
    }

    /// <summary>
    /// Parental gate: must be held for N seconds. A ring fills up around the icon while holding;
    /// a short tap only shows a little "hold" hint so curious toddlers don't trigger it.
    /// </summary>
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public Action onComplete;
        public float holdSeconds = 5f;
        public Image ring;
        public RectTransform visual;
        public TextMeshProUGUI hint;
        public AudioClip tick;

        bool _holding;
        float _t;
        int _lastTick;
        Coroutine _hintRoutine;

        public void OnPointerDown(PointerEventData e)
        {
            _holding = true;
            _t = 0f;
            _lastTick = 0;
            ShowHint(true);
        }

        public void OnPointerUp(PointerEventData e) => Cancel();
        public void OnPointerExit(PointerEventData e) => Cancel();

        void Cancel()
        {
            if (!_holding) return;
            _holding = false;
            if (_hintRoutine != null) StopCoroutine(_hintRoutine);
            _hintRoutine = StartCoroutine(HideHintLater());
        }

        void Update()
        {
            if (_holding)
            {
                _t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(_t / holdSeconds);
                ring.fillAmount = k;
                visual.localScale = Vector3.one * (1f + 0.08f * k + 0.03f * Mathf.Sin(_t * 20f) * k);
                int sec = Mathf.FloorToInt(_t);
                if (sec > _lastTick) { _lastTick = sec; AudioManager.Instance?.PlaySfx(tick, 0.6f, 1f + sec * 0.1f); }
                if (k >= 1f)
                {
                    _holding = false;
                    ring.fillAmount = 0f;
                    visual.localScale = Vector3.one;
                    ShowHint(false);
                    onComplete?.Invoke();
                }
            }
            else if (ring.fillAmount > 0f)
            {
                ring.fillAmount = Mathf.MoveTowards(ring.fillAmount, 0f, Time.unscaledDeltaTime * 2f);
                visual.localScale = Vector3.Lerp(visual.localScale, Vector3.one, Time.unscaledDeltaTime * 10f);
            }
        }

        void ShowHint(bool on)
        {
            if (hint == null) return;
            hint.gameObject.SetActive(on);
            if (on) hint.alpha = 1f;
        }

        IEnumerator HideHintLater()
        {
            yield return new WaitForSecondsRealtime(1.2f);
            if (hint == null) yield break;
            yield return Tween.Run(0.3f, Ease.Linear, k => hint.alpha = 1f - k, true);
            hint.gameObject.SetActive(false);
        }

        void OnDisable() { _holding = false; if (ring) ring.fillAmount = 0f; if (visual) visual.localScale = Vector3.one; }

        public static HoldButton Create(Transform parent, string name, Sprite icon, Vector2 size, float seconds, UISkin skin, Action onComplete, bool hintBelow = true)
        {
            var hit = UI.Rect(parent, name);
            hit.sizeDelta = size + Vector2.one * 40f;
            hit.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0);

            var ringBg = UI.Image(hit, "RingBg", skin.ring, new Color(1f, 1f, 1f, 0.0f));
            ringBg.raycastTarget = false;
            UI.Stretch(ringBg.rectTransform, 6f);

            var ring = UI.Image(hit, "Ring", skin.ring, new Color(1f, 0.85f, 0.3f, 1f));
            ring.raycastTarget = false;
            ring.type = UnityEngine.UI.Image.Type.Filled;
            ring.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
            ring.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top;
            ring.fillClockwise = true;
            ring.fillAmount = 0f;
            UI.Stretch(ring.rectTransform, 6f);

            var img = UI.Image(hit, "Icon", icon);
            img.raycastTarget = false;
            img.preserveAspect = true;
            UI.Stretch(img.rectTransform, 40f);

            var hint = UI.Text(hit, "Hint", skin.holdHint.Get(), 34f, UI.TextStyle.OutlinedThin);
            UI.Anchor(hint.rectTransform, new Vector2(0.5f, hintBelow ? 0f : 1f), new Vector2(0.5f, hintBelow ? 1f : 0f),
                Vector2.zero, new Vector2(300f, 50f));
            hint.gameObject.SetActive(false);

            var b = hit.gameObject.AddComponent<HoldButton>();
            b.ring = ring;
            b.visual = img.rectTransform;
            b.hint = hint;
            b.holdSeconds = seconds;
            b.onComplete = onComplete;
            b.tick = skin.sfxTick;
            return b;
        }
    }

    /// <summary>Keeps a RectTransform inside the device safe area (notches, rounded corners, home bar).</summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        Rect _last;
        Vector2Int _lastScreen;

        void OnEnable() => Apply();
        void Update() { if (Screen.safeArea != _last || _lastScreen.x != Screen.width || _lastScreen.y != Screen.height) Apply(); }

        void Apply()
        {
            _last = Screen.safeArea;
            _lastScreen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;
            var rt = (RectTransform)transform;
            Vector2 min = _last.position, max = _last.position + _last.size;
            min.x /= Screen.width; min.y /= Screen.height;
            max.x /= Screen.width; max.y /= Screen.height;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }

    /// <summary>Makes the letters of a TMP label jump one after another (karaoke style) while "speaking".</summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class WavyText : MonoBehaviour
    {
        public float amplitude = 22f;
        public float speed = 9f;
        public float charDelay = 0.09f;
        public Color highlight = new Color(1f, 0.86f, 0.35f);
        [Tooltip("Gentle endless wave instead of a one-shot karaoke bounce (used for titles).")]
        public bool loop;

        TextMeshProUGUI _text;
        float _t = -1f, _duration;

        void Awake() => _text = GetComponent<TextMeshProUGUI>();

        public void Play(float duration = 1.2f) { _t = 0f; _duration = Mathf.Max(0.6f, duration); }

        void LateUpdate()
        {
            if (loop) { LoopWave(); return; }
            if (_t < 0f) return;
            _t += Time.deltaTime;
            _text.ForceMeshUpdate();
            var info = _text.textInfo;
            float total = _duration + info.characterCount * charDelay;
            for (int i = 0; i < info.characterCount; i++)
            {
                var c = info.characterInfo[i];
                if (!c.isVisible) continue;
                float local = _t - i * charDelay;
                float k = local > 0f && local < 0.5f ? Mathf.Sin(local / 0.5f * Mathf.PI) : 0f;
                int mi = c.materialReferenceIndex, vi = c.vertexIndex;
                var verts = info.meshInfo[mi].vertices;
                var cols = info.meshInfo[mi].colors32;
                Vector3 off = new Vector3(0f, amplitude * k, 0f);
                Vector3 mid = (verts[vi] + verts[vi + 2]) * 0.5f;
                float s = 1f + 0.18f * k;
                for (int j = 0; j < 4; j++)
                {
                    verts[vi + j] = mid + (verts[vi + j] - mid) * s + off;
                    cols[vi + j] = Color32.Lerp(cols[vi + j], highlight, k);
                }
            }
            _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
            if (_t > total) { _t = -1f; _text.ForceMeshUpdate(); }
        }

        void LoopWave()
        {
            _text.ForceMeshUpdate();
            var info = _text.textInfo;
            float time = Time.time * speed * 0.35f;
            for (int i = 0; i < info.characterCount; i++)
            {
                var c = info.characterInfo[i];
                if (!c.isVisible) continue;
                var verts = info.meshInfo[c.materialReferenceIndex].vertices;
                int vi = c.vertexIndex;
                float k = Mathf.Sin(time - i * 0.55f);
                Vector3 off = new Vector3(0f, amplitude * 0.45f * k, 0f);
                Vector3 mid = (verts[vi] + verts[vi + 2]) * 0.5f;
                float rot = k * 4f * Mathf.Deg2Rad;
                float cs = Mathf.Cos(rot), sn = Mathf.Sin(rot);
                for (int j = 0; j < 4; j++)
                {
                    Vector3 d = verts[vi + j] - mid;
                    verts[vi + j] = mid + new Vector3(d.x * cs - d.y * sn, d.x * sn + d.y * cs, 0f) + off;
                }
            }
            _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
        }
    }

    /// <summary>Gentle continuous bob/pulse – used for "tap me" hints and idle decorations.</summary>
    public class Bobber : MonoBehaviour
    {
        public float bob = 12f, period = 1.6f, pulse = 0.05f, rotate = 0f, phase;
        RectTransform _rt;
        Vector2 _base;
        bool _init;

        void OnEnable() { _rt = (RectTransform)transform; if (!_init) { _base = _rt.anchoredPosition; _init = true; } }

        public void Rebase() { _base = _rt.anchoredPosition; }

        void Update()
        {
            float t = (Time.time + phase) * 2f * Mathf.PI / period;
            _rt.anchoredPosition = _base + new Vector2(0f, Mathf.Sin(t) * bob);
            _rt.localScale = Vector3.one * (1f + Mathf.Sin(t * 2f) * pulse);
            if (rotate != 0f) _rt.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(t * 0.5f) * rotate);
        }
    }

    /// <summary>Plain click callback without any visual feedback.</summary>
    public class ClickRelay : MonoBehaviour, IPointerClickHandler
    {
        public Action onClick;
        public void OnPointerClick(PointerEventData eventData) => onClick?.Invoke();
    }
}
