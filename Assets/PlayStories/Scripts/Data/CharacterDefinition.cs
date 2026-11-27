using System;
using UnityEngine;

namespace PlayStories
{
    /// <summary>What a character does when it is tapped.</summary>
    public enum ReactionStyle
    {
        /// <summary>Head nods while the sound plays (cow).</summary>
        Nod,
        /// <summary>Crouch, jump, land with a squash (sheep).</summary>
        Hop,
        /// <summary>Body shimmy + fast tail wag + small bounces (pig).</summary>
        Wiggle,
        /// <summary>Quick head pecks then puffs up proudly (hen).</summary>
        Peck,
        /// <summary>Rocks side to side like it is walking (duck).</summary>
        Waddle,
        /// <summary>Jump with a full spin (good for silly/happy moments).</summary>
        Spin,
    }

    public enum ParticleStyle { Hearts, Notes, Stars, Sparkles }

    /// <summary>
    /// A "soft bone": vertices inside a circular region of the sprite are rotated around a pivot,
    /// falling off smoothly at the edge. Lets a single flat drawing nod its head or wag its tail
    /// without being cut into pieces. Coordinates are normalised sprite UVs (0,0 = bottom-left).
    /// </summary>
    [Serializable]
    public struct SoftBone
    {
        public bool enabled;
        [Tooltip("Centre of the affected region (normalised sprite coordinates).")]
        public Vector2 center;
        [Tooltip("Radius of full influence, as a fraction of the sprite height.")]
        public float radius;
        [Tooltip("Extra radius over which the influence fades to zero.")]
        public float falloff;
        [Tooltip("Rotation pivot (e.g. the neck or the base of the tail).")]
        public Vector2 pivot;

        public static SoftBone Make(Vector2 center, float radius, float falloff, Vector2 pivot)
            => new SoftBone { enabled = true, center = center, radius = radius, falloff = falloff, pivot = pivot };
    }

    /// <summary>Everything that makes a single still drawing feel alive.</summary>
    [Serializable]
    public class MotionProfile
    {
        [Header("Rig (normalised sprite coordinates)")]
        public SoftBone head = SoftBone.Make(new Vector2(0.3f, 0.7f), 0.28f, 0.14f, new Vector2(0.38f, 0.5f));
        public SoftBone tail = SoftBone.Make(new Vector2(0.92f, 0.5f), 0.1f, 0.08f, new Vector2(0.86f, 0.55f));

        [Header("Idle")]
        [Range(0f, 0.08f)] public float breathAmount = 0.025f;
        [Range(0.5f, 6f)] public float breathPeriod = 2.6f;
        [Range(0f, 8f)] public float idleHeadDegrees = 3f;
        [Range(0f, 30f)] public float idleTailDegrees = 8f;
        public Vector2 blinkInterval = new Vector2(2.2f, 5.5f);

        [Header("Tap reaction")]
        public ReactionStyle reaction = ReactionStyle.Hop;
        [Range(0.5f, 2f)] public float reactionStrength = 1f;
        [Tooltip("How much the body stretches with the loudness of the sound, so it looks like it is 'saying' it.")]
        [Range(0f, 0.25f)] public float voiceStretch = 0.08f;
        public ParticleStyle particles = ParticleStyle.Hearts;
        [Tooltip("Which side the face is on (-1 = facing left, 1 = facing right). Used to place the speech bubble.")]
        public int facing = -1;
    }

    /// <summary>
    /// An animal (or any tappable character). Reusable across books.
    /// Create via Assets ▸ Create ▸ PlayStories ▸ Character.
    /// </summary>
    [CreateAssetMenu(menuName = "PlayStories/Character", fileName = "Character")]
    public class CharacterDefinition : ScriptableObject
    {
        public LocalizedText displayName = new LocalizedText("Ko", "Cow");
        public Sprite sprite;
        [Tooltip("Optional: same size as the sprite, transparent except closed eyes. Enables blinking.")]
        public Sprite blinkSprite;

        [Tooltip("The animal sound, e.g. a moo.")]
        public AudioClip sound;
        [Tooltip("Text in the speech bubble when tapped, e.g. 'Muh!' / 'Moo!'")]
        public LocalizedText soundWord = new LocalizedText("Muh!", "Moo!");
        [Range(0.5f, 1.5f)] public float soundPitch = 1f;

        [Tooltip("Relative size on the page (1 = fill the available space).")]
        [Range(0.3f, 1.2f)] public float scale = 0.9f;

        public MotionProfile motion = new MotionProfile();
    }
}
