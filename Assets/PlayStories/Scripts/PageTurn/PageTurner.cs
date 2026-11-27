using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace PlayStories
{
    /// <summary>
    /// "Real paper" page turns. The current screen is rendered into a texture, the page content
    /// underneath is swapped to the new page, and the snapshot is peeled away with
    /// <see cref="PageCurlMesh"/>. Works with an automatic animation (arrow buttons) or follows the
    /// child's finger (drag), and can be cancelled half-way.
    /// </summary>
    public class PageTurner : MonoBehaviour
    {
        public float duration = 0.95f;
        public AudioClip sound;
        public Color paperColor = new Color(1f, 0.985f, 0.95f);

        public bool Busy { get; private set; }
        public bool Dragging { get; private set; }

        Canvas _canvas;
        RectTransform _root;
        RawImage _front;
        Image _back, _shadow;
        PageCurlMesh _frontMesh, _backMesh, _shadowMesh;
        readonly PageCurlState _state = new PageCurlState();
        RenderTexture _rt;
        int _dir;
        float _progress;
        Action _revert;
        Action _onRevealed;
        bool _revealedFired;

        public static PageTurner Create(Canvas canvas, Transform parent)
        {
            var root = UI.Rect(parent, "PageTurnOverlay");
            UI.Stretch(root);
            var t = root.gameObject.AddComponent<PageTurner>();
            t._canvas = canvas;
            t._root = root;

            t._shadow = UI.Image(root, "Shadow", null, Color.white);
            UI.Stretch(t._shadow.rectTransform);
            t._shadow.raycastTarget = false;
            t._shadowMesh = t._shadow.gameObject.AddComponent<PageCurlMesh>();
            t._shadowMesh.part = PageCurlMesh.Part.Shadow;

            var frontRt = UI.Rect(root, "Front");
            UI.Stretch(frontRt);
            t._front = frontRt.gameObject.AddComponent<RawImage>();
            t._front.raycastTarget = true; // blocks taps while the page is moving
            t._frontMesh = frontRt.gameObject.AddComponent<PageCurlMesh>();
            t._frontMesh.part = PageCurlMesh.Part.Front;

            t._back = UI.Image(root, "Back", null, t.paperColor);
            UI.Stretch(t._back.rectTransform);
            t._back.raycastTarget = false;
            t._backMesh = t._back.gameObject.AddComponent<PageCurlMesh>();
            t._backMesh.part = PageCurlMesh.Part.Back;

            t._frontMesh.state = t._backMesh.state = t._shadowMesh.state = t._state;
            root.gameObject.SetActive(false);
            return t;
        }

        // ------------------------------------------------------------------ public API
        /// <summary>Animated turn. <paramref name="swap"/> must replace the page content underneath.</summary>
        public IEnumerator Turn(int direction, Action swap, Action onRevealed = null)
        {
            if (Busy) yield break;
            Begin(direction, swap, null, onRevealed);
            float from = _progress;
            yield return Tween.Run(duration * (1f - from), Ease.InOutSine, k => SetProgress(Mathf.Lerp(from, 1f, k)));
            Finish();
        }

        /// <summary>Starts a finger-driven turn. Follow with <see cref="Drag"/> and <see cref="Release"/>.</summary>
        public bool BeginDrag(int direction, Action swap, Action revert, Action onRevealed = null)
        {
            if (Busy) return false;
            Begin(direction, swap, revert, onRevealed);
            Dragging = true;
            return true;
        }

        public void Drag(float progress)
        {
            if (!Dragging) return;
            SetProgress(Mathf.Clamp(progress, 0.02f, 0.98f));
        }

        public IEnumerator Release(bool complete)
        {
            if (!Dragging) yield break;
            Dragging = false;
            float from = _progress;
            if (complete)
            {
                yield return Tween.Run(Mathf.Max(0.2f, duration * 0.7f * (1f - from)), Ease.OutQuad, k => SetProgress(Mathf.Lerp(from, 1f, k)));
                Finish();
            }
            else
            {
                yield return Tween.Run(Mathf.Max(0.15f, 0.45f * from), Ease.OutQuad, k => SetProgress(Mathf.Lerp(from, 0f, k)));
                _revert?.Invoke();       // put the old page back underneath while the snapshot still covers everything
                yield return null;
                Finish();
            }
        }

        // ------------------------------------------------------------------ internals
        void Begin(int direction, Action swap, Action revert, Action onRevealed)
        {
            Busy = true;
            _dir = direction >= 0 ? 1 : -1;
            _revert = revert;
            _onRevealed = onRevealed;
            _revealedFired = false;

            Capture();
            _root.SetAsLastSibling();
            _root.gameObject.SetActive(true);
            _front.texture = _rt;
            SetProgress(0f);
            swap?.Invoke();
            if (sound != null) AudioManager.Instance?.PlaySfx(sound, 0.9f, UnityEngine.Random.Range(0.92f, 1.08f));
        }

        void Finish()
        {
            _root.gameObject.SetActive(false);
            _front.texture = null;
            Busy = false;
            Dragging = false;
            if (!_revealedFired && _progress > 0.5f) { _revealedFired = true; _onRevealed?.Invoke(); }
        }

        void SetProgress(float p)
        {
            _progress = p;
            Rect r = _root.rect;
            // tilt so the bottom corner lifts first – like a thumb grabbing the corner of a page
            _state.normal = new Vector2(_dir, -0.22f).normalized;
            float dmin = float.MaxValue, dmax = float.MinValue;
            foreach (var corner in new[] { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax) })
            {
                float d = Vector2.Dot(corner, _state.normal);
                dmin = Mathf.Min(dmin, d); dmax = Mathf.Max(dmax, d);
            }
            float span = dmax - dmin;
            _state.radius = Mathf.Lerp(span * 0.04f, span * 0.11f, Mathf.Sqrt(p));
            float endC = dmin - Mathf.PI * span * 0.11f - 40f;
            _state.position = Mathf.Lerp(dmax + 2f, endC, p);
            _state.flippedLength = Mathf.Max(0f, dmax - _state.position - Mathf.PI * _state.radius);
            _state.shadowStrength = 0.38f * Mathf.Clamp01(p * 6f) * Mathf.Clamp01((1f - p) * 4f);

            _front.SetVerticesDirty();
            _back.SetVerticesDirty();
            _shadow.SetVerticesDirty();

            if (!_revealedFired && p > 0.55f)
            {
                _revealedFired = true;
                _onRevealed?.Invoke();
            }
        }

        void Capture()
        {
            var cam = _canvas.worldCamera;
            int w = Mathf.Max(16, cam.pixelWidth), h = Mathf.Max(16, cam.pixelHeight);
            if (_rt == null || _rt.width != w || _rt.height != h)
            {
                if (_rt != null) _rt.Release();
                _rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default)
                {
                    name = "PageSnapshot",
                    antiAliasing = 1,
                };
                _rt.Create();
            }
            _root.gameObject.SetActive(false);
            Canvas.ForceUpdateCanvases();

            var request = new RenderPipeline.StandardRequest();
            if (RenderPipeline.SupportsRenderRequest(cam, request))
            {
                request.destination = _rt;
                RenderPipeline.SubmitRenderRequest(cam, request);
            }
            else
            {
                var prev = cam.targetTexture;
                cam.targetTexture = _rt;
                cam.Render();
                cam.targetTexture = prev;
            }
        }

        void OnDestroy()
        {
            if (_rt != null) _rt.Release();
        }
    }
}
