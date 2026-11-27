using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PlayStories
{
    /// <summary>The pose produced by the procedural animation layers for one frame.</summary>
    public struct CharacterPose
    {
        public Vector2 offset;      // hop / shuffle, in canvas units
        public float rotation;      // degrees around the feet
        public float squash;        // 1 = neutral, <1 squashed, >1 stretched (volume preserving)
        public float lean;          // horizontal shear of the top relative to the feet
        public float breath;        // belly/chest bulge
        public float headAngle;     // degrees around the head soft-bone pivot
        public Vector2 headOffset;  // fraction of sprite height
        public float tailAngle;     // degrees around the tail soft-bone pivot
        public float flipX;         // 1 = normal, -1 = mirrored (used for "turn around")
        public float scale;         // overall pop scale
        public bool eyesClosed;

        public static CharacterPose Neutral => new CharacterPose { squash = 1f, flipX = 1f, scale = 1f };
    }

    /// <summary>
    /// A living character built from ONE drawing (+ optional blink overlay).
    ///
    /// Animation is layered procedurally every frame:
    ///   idle (breathing, head drift, tail swish, blinking)
    /// + reaction (per-character style: hop, nod, wiggle, peck, waddle, spin)
    /// + voice (body stretches with the loudness of its own sound)
    /// + enter / hint layers.
    /// The pose bends the drawing's mesh via <see cref="SoftRigDeformer"/> (squash/stretch, lean,
    /// breathing and two "soft bones" for head and tail), and moves the rig transform (hops, rotation).
    /// </summary>
    public class CharacterActor : MonoBehaviour
    {
        public CharacterDefinition Definition { get; private set; }
        public event Action<CharacterActor> Tapped;
        public bool Interactable = true;
        public bool IsReacting => _reactionTime >= 0f;

        RectTransform _rt, _rig, _slot;
        Image _body, _blink, _shadow;
        SoftRigDeformer _bodyDeform, _blinkDeform;
        CanvasGroup _group;
        CharacterPose _pose = CharacterPose.Neutral;
        MotionProfile M => Definition.motion;

        float _time, _seed;
        float _reactionTime = -1f, _reactionDuration, _reactionStrength = 1f;
        ReactionStyle _reactionStyle;
        AudioSource _soundSource;
        float _enterTime = -1f, _enterDelay;
        float _hintTime = -1f;
        float _blinkTimer, _blinkHold;
        Vector2 _lastSlotSize;

        // ------------------------------------------------------------------ construction
        public static CharacterActor Create(RectTransform slot, CharacterDefinition def, UISkin skin, bool interactable = true)
        {
            var go = new GameObject(def.name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(slot, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            var a = go.AddComponent<CharacterActor>();
            a.Build(slot, def, skin);
            a.Interactable = interactable;
            return a;
        }

        void Build(RectTransform slot, CharacterDefinition def, UISkin skin)
        {
            Definition = def;
            _slot = slot;
            _rt = (RectTransform)transform;
            _seed = UnityEngine.Random.value * 100f;

            _shadow = UI.Image(_rt, "Shadow", skin.shadow);
            _shadow.raycastTarget = false;
            var srt = _shadow.rectTransform;
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0f);
            srt.pivot = new Vector2(0.5f, 0.5f);

            _rig = UI.Rect(_rt, "Rig");
            UI.Stretch(_rig);
            _rig.pivot = new Vector2(0.5f, 0f);
            _group = _rig.gameObject.AddComponent<CanvasGroup>();

            _body = UI.Image(_rig, "Body", def.sprite);
            UI.Stretch(_body.rectTransform);
            _bodyDeform = _body.gameObject.AddComponent<SoftRigDeformer>();
            _bodyDeform.actor = this;
            _body.gameObject.AddComponent<PointerRelay>().onDown = OnPointerDown;

            if (def.blinkSprite != null)
            {
                _blink = UI.Image(_rig, "Blink", def.blinkSprite);
                UI.Stretch(_blink.rectTransform);
                _blink.raycastTarget = false;
                _blinkDeform = _blink.gameObject.AddComponent<SoftRigDeformer>();
                _blinkDeform.actor = this;
                _blink.enabled = false;
            }
            ScheduleBlink();
            FitToSlot();
        }

        void FitToSlot()
        {
            if (_slot == null || Definition.sprite == null) return;
            Vector2 s = _slot.rect.size;
            _lastSlotSize = s;
            float aspect = Definition.sprite.rect.width / Definition.sprite.rect.height;
            float h = Mathf.Min(s.y, s.x / aspect) * Definition.scale;
            _rt.sizeDelta = new Vector2(h * aspect, h);
            _rt.anchoredPosition = Vector2.zero;
            _shadow.rectTransform.sizeDelta = new Vector2(h * aspect * 0.85f, h * 0.16f);
            _shadow.rectTransform.anchoredPosition = new Vector2(0f, h * 0.025f);
        }

        // ------------------------------------------------------------------ public API
        /// <summary>Drop in from above with a squashy landing.</summary>
        public void PlayEnter(float delay = 0f)
        {
            _enterTime = 0f;
            _enterDelay = delay;
            _group.alpha = 0f;
        }

        /// <summary>Plays the tap reaction (sound + motion). Returns false if ignored.</summary>
        public bool React(bool withSound = true, float strength = 1f)
        {
            if (IsReacting && _reactionTime < 0.3f) return false; // ignore frantic double taps
            _reactionStyle = M.reaction;
            _reactionStrength = strength * M.reactionStrength;
            float soundLen = Definition.sound != null ? Definition.sound.length / Mathf.Max(0.1f, Definition.soundPitch) : 0f;
            _reactionDuration = Mathf.Max(BaseDuration(_reactionStyle), withSound ? soundLen + 0.15f : 0f);
            _reactionTime = 0f;
            _soundSource = null;
            if (withSound && Definition.sound != null && AudioManager.Instance != null)
                _soundSource = AudioManager.Instance.PlayCharacterSound(Definition.sound, Definition.soundPitch);
            return true;
        }

        /// <summary>Small "look at me" wiggle used when the child has been idle.</summary>
        public void Hint() { if (!IsReacting) _hintTime = 0f; }

        /// <summary>Approximate world position of the top of the head (for speech bubbles / particles).</summary>
        public Vector3 HeadWorldPosition
        {
            get
            {
                var r = _body.rectTransform.rect;
                var c = M.head.enabled ? M.head.center : new Vector2(0.5f, 0.85f);
                var top = new Vector2(c.x, Mathf.Min(1f, c.y + M.head.radius * 0.8f));
                return _body.rectTransform.TransformPoint(Deform(top.x, top.y, r));
            }
        }

        public float Height => _rt.rect.height;

        // ------------------------------------------------------------------ input
        void OnPointerDown(PointerEventData e)
        {
            if (!Interactable) return;
            Tapped?.Invoke(this);
        }

        // ------------------------------------------------------------------ per-frame
        void Update()
        {
            if (Definition == null) return;
            if (_slot != null && _slot.rect.size != _lastSlotSize) FitToSlot();

            float dt = Time.deltaTime;
            _time += dt;
            var p = CharacterPose.Neutral;

            // ---- idle
            float breathT = (_time + _seed) * 2f * Mathf.PI / M.breathPeriod;
            p.breath = M.breathAmount * Mathf.Sin(breathT);
            p.squash = 1f + M.breathAmount * 0.35f * Mathf.Sin(breathT);
            p.headAngle = M.idleHeadDegrees * (Mathf.PerlinNoise(_seed, _time * 0.35f) * 2f - 1f) * 1.4f;
            p.tailAngle = M.idleTailDegrees * Mathf.Sin((_time + _seed) * 2.3f);
            p.lean = 0.012f * Mathf.Sin((_time + _seed) * 0.8f);

            // ---- enter
            if (_enterTime >= 0f)
            {
                _enterTime += dt;
                float t = _enterTime - _enterDelay;
                if (t < 0f) { _group.alpha = 0f; }
                else ApplyEnter(ref p, t);
            }

            // ---- reaction
            if (_reactionTime >= 0f)
            {
                _reactionTime += dt;
                float t = _reactionTime;
                ApplyReaction(ref p, _reactionStyle, t, _reactionDuration, _reactionStrength);
                if (t >= _reactionDuration) _reactionTime = -1f;
            }

            // ---- voice: stretch with the loudness of our own sound
            if (_soundSource != null && _soundSource.isPlaying && _soundSource.clip == Definition.sound)
            {
                float env = AudioManager.Instance.SampleEnvelope(Definition.sound, _soundSource.time);
                p.squash += env * M.voiceStretch;
                p.headAngle += -Face * env * 7f;
                p.headOffset.y += env * 0.015f;
                if (env > 0.45f) p.eyesClosed = true;
            }

            // ---- hint
            if (_hintTime >= 0f)
            {
                _hintTime += dt;
                ApplyReaction(ref p, ReactionStyle.Wiggle, _hintTime, 0.8f, 0.35f);
                if (_hintTime > 0.8f) _hintTime = -1f;
            }

            // ---- blink
            _blinkTimer -= dt;
            if (_blinkTimer <= 0f)
            {
                _blinkHold = 0.13f;
                ScheduleBlink();
                if (UnityEngine.Random.value < 0.2f) _blinkTimer = 0.25f; // sometimes a double blink
            }
            if (_blinkHold > 0f) { _blinkHold -= dt; p.eyesClosed = true; }

            _pose = p;
            ApplyPose();
        }

        void ScheduleBlink() => _blinkTimer = UnityEngine.Random.Range(M.blinkInterval.x, M.blinkInterval.y);

        /// <summary>+1 when "nodding down" rotates the head clockwise. Depends on which way the face points.</summary>
        float Face => M.facing < 0 ? 1f : -1f;

        void ApplyPose()
        {
            float h = _rt.rect.height;
            _rig.anchoredPosition = new Vector2(_pose.offset.x, _pose.offset.y);
            _rig.localEulerAngles = new Vector3(0f, 0f, _pose.rotation);
            _rig.localScale = new Vector3(_pose.scale * _pose.flipX, _pose.scale, 1f);

            // shadow shrinks and fades as the character leaves the ground
            float lift = Mathf.Clamp01(_pose.offset.y / Mathf.Max(1f, h * 0.6f));
            _shadow.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 0.55f, lift) * _pose.scale;
            var sc = _shadow.color; sc.a = Mathf.Lerp(1f, 0.35f, lift) * _group.alpha; _shadow.color = sc;

            if (_blink != null) _blink.enabled = _pose.eyesClosed;
            _body.SetVerticesDirty();
            if (_blink != null && _blink.enabled) _blink.SetVerticesDirty();
        }

        // ------------------------------------------------------------------ deformation
        /// <summary>Maps a normalised sprite coordinate to its deformed local position.</summary>
        public Vector2 Deform(float u, float v, Rect rect)
        {
            float w = rect.width, h = rect.height;
            // work relative to the feet (bottom centre)
            Vector2 p = new Vector2((u - 0.5f) * w, v * h);

            if (M.head.enabled) p = ApplyBone(p, u, v, w, h, M.head, _pose.headAngle, _pose.headOffset * h);
            if (M.tail.enabled) p = ApplyBone(p, u, v, w, h, M.tail, _pose.tailAngle, Vector2.zero);

            // breathing: belly bulges sideways, chest rises a little
            float belly = Mathf.Sin(Mathf.PI * Mathf.Clamp01(v * 1.1f));
            p.x *= 1f + _pose.breath * belly;
            p.y *= 1f + _pose.breath * 0.5f * v;

            // squash & stretch, roughly volume-preserving, feet stay planted
            float s = Mathf.Max(0.2f, _pose.squash);
            float sx = 1f + (1f / Mathf.Sqrt(s) - 1f) * (0.35f + 0.65f * v);
            p.x *= sx;
            p.y *= s;

            // lean: shear the top, feet planted
            p.x += _pose.lean * p.y;

            return new Vector2(rect.xMin + w * 0.5f + p.x, rect.yMin + p.y);
        }

        static Vector2 ApplyBone(Vector2 p, float u, float v, float w, float h, SoftBone b, float angle, Vector2 offset)
        {
            // distance in units of sprite height so the region is round on screen
            float dx = (u - b.center.x) * w / h, dy = v - b.center.y;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float weight = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(b.radius, b.radius + b.falloff, d));
            if (weight <= 0f) return p;
            Vector2 pivot = new Vector2((b.pivot.x - 0.5f) * w, b.pivot.y * h);
            float a = angle * weight * Mathf.Deg2Rad;
            float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
            Vector2 r = p - pivot;
            r = new Vector2(r.x * cs - r.y * sn, r.x * sn + r.y * cs);
            return pivot + r + offset * weight;
        }

        // ------------------------------------------------------------------ animation layers
        void ApplyEnter(ref CharacterPose p, float t)
        {
            const float fall = 0.38f, settle = 0.55f;
            float h = _rt.rect.height;
            _group.alpha = Mathf.Clamp01(t / 0.12f);
            if (t < fall)
            {
                float k = t / fall;
                p.offset.y += (1f - k * k) * h * 0.9f;
                p.squash *= 1.12f; // stretched while falling
            }
            else if (t < fall + settle)
            {
                float k = (t - fall) / settle;
                p.squash *= 1f - 0.22f * Ease.Wobble(k, 2.2f, 5f);
                p.headAngle += Face * 6f * Ease.Wobble(k, 1.6f, 4f);
            }
            else _enterTime = -1f;
        }

        static float BaseDuration(ReactionStyle s)
        {
            switch (s)
            {
                case ReactionStyle.Hop: return 1.05f;
                case ReactionStyle.Nod: return 1.4f;
                case ReactionStyle.Wiggle: return 1.3f;
                case ReactionStyle.Peck: return 1.5f;
                case ReactionStyle.Waddle: return 1.6f;
                case ReactionStyle.Spin: return 1.15f;
            }
            return 1f;
        }

        void ApplyReaction(ref CharacterPose p, ReactionStyle style, float t, float dur, float str)
        {
            float h = _rt.rect.height;
            float k = Mathf.Clamp01(t / dur);
            float fadeOut = 1f - Ease.InQuad(Mathf.Clamp01((k - 0.75f) / 0.25f));
            switch (style)
            {
                case ReactionStyle.Hop:
                {
                    // anticipation -> jump -> land -> settle
                    float tt = t;
                    if (tt < 0.14f) p.squash *= 1f - 0.2f * str * Ease.OutQuad(tt / 0.14f);
                    else if (tt < 0.56f)
                    {
                        float j = (tt - 0.14f) / 0.42f;
                        p.offset.y += Ease.Hump(j) * h * 0.28f * str;
                        p.squash *= Mathf.Lerp(1.18f, 1f, Ease.Hump(j)) * (1f + 0.04f * str);
                        p.headAngle += Face * -8f * Ease.Hump(j);
                        p.tailAngle += 25f * Mathf.Sin(j * 18f);
                    }
                    else
                    {
                        float l = Mathf.Clamp01((tt - 0.56f) / 0.5f);
                        p.squash *= 1f - 0.24f * str * Ease.Wobble(l, 2f, 5f);
                        p.tailAngle += 20f * Ease.Wobble(l, 3f, 4f);
                    }
                    break;
                }
                case ReactionStyle.Nod:
                {
                    float env = Ease.Hump(Mathf.Clamp01(k * 1.2f));
                    p.headAngle += Face * 11f * str * Mathf.Sin(t * 2f * Mathf.PI * 1.6f) * env;
                    p.headOffset.y += 0.01f * env;
                    p.tailAngle += 28f * Mathf.Sin(t * 2f * Mathf.PI * 2.6f) * fadeOut;
                    p.lean += -Face * 0.02f * env;
                    break;
                }
                case ReactionStyle.Wiggle:
                {
                    float env = Ease.Hump(Mathf.Clamp01(k * 1.1f));
                    p.lean += 0.09f * str * Mathf.Sin(t * 2f * Mathf.PI * 4.5f) * env;
                    p.tailAngle += 45f * Mathf.Sin(t * 2f * Mathf.PI * 7f) * fadeOut;
                    p.offset.y += Mathf.Abs(Mathf.Sin(t * 2f * Mathf.PI * 2.2f)) * h * 0.06f * str * env;
                    p.squash *= 1f + 0.05f * Mathf.Sin(t * 2f * Mathf.PI * 4.4f) * env;
                    break;
                }
                case ReactionStyle.Peck:
                {
                    if (t < 0.95f)
                    {
                        float peck = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 2f * Mathf.PI * 3.2f)), 2f);
                        p.headAngle += Face * 24f * str * peck;
                        p.headOffset.y -= 0.03f * peck;
                        p.lean += -Face * 0.04f * peck;
                    }
                    else
                    {
                        float q = Mathf.Clamp01((t - 0.95f) / 0.55f);
                        p.breath += 0.07f * str * Ease.Hump(q);
                        p.squash *= 1f + 0.08f * Ease.Hump(q);
                        p.tailAngle += 25f * Ease.Hump(q);
                        p.headAngle += -Face * 8f * Ease.Hump(q);
                    }
                    break;
                }
                case ReactionStyle.Waddle:
                {
                    float env = Ease.Hump(Mathf.Clamp01(k * 1.05f));
                    float step = Mathf.Sin(t * 2f * Mathf.PI * 2.4f);
                    p.rotation += 7f * str * step * env;
                    p.offset.y += Mathf.Abs(step) * h * 0.035f * env;
                    p.offset.x += Mathf.Sin(t * 2f * Mathf.PI * 1.2f) * h * 0.05f * env;
                    p.tailAngle += 35f * Mathf.Sin(t * 2f * Mathf.PI * 4.8f) * fadeOut;
                    p.headAngle += Face * 5f * step * env;
                    break;
                }
                case ReactionStyle.Spin:
                {
                    if (t < 0.12f) p.squash *= 1f - 0.18f * (t / 0.12f);
                    else if (t < 0.72f)
                    {
                        float j = (t - 0.12f) / 0.6f;
                        p.offset.y += Ease.Hump(j) * h * 0.32f * str;
                        p.flipX = Mathf.Cos(Ease.InOutQuad(j) * 2f * Mathf.PI);
                        if (Mathf.Abs(p.flipX) < 0.15f) p.flipX = 0.15f * Mathf.Sign(p.flipX == 0 ? 1 : p.flipX);
                    }
                    else
                    {
                        float l = Mathf.Clamp01((t - 0.72f) / 0.43f);
                        p.squash *= 1f - 0.22f * Ease.Wobble(l, 2f, 5f);
                    }
                    break;
                }
            }
        }
    }

    /// <summary>Forwards pointer-down from a child graphic (so the actor reacts instantly on touch).</summary>
    public class PointerRelay : MonoBehaviour, IPointerDownHandler
    {
        public Action<PointerEventData> onDown;
        public void OnPointerDown(PointerEventData eventData) => onDown?.Invoke(eventData);
    }
}
