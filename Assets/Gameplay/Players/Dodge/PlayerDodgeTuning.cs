namespace Game50033
{
    using UnityEngine;

    [CreateAssetMenu(menuName = "Game50033/Tuning/Player Dodge", fileName = "PlayerDodgeTuning")]
    public sealed class PlayerDodgeTuning : ScriptableObject
    {
        [Header("Movement")]
        [Tooltip("Distance in world units before collision limits the dodge.")]
        [Min(0f)] public float dodgeDistance = 1f;
        [Tooltip("Travel time in seconds, excluding startup hitstop.")]
        [Min(0.01f)] public float dodgeDuration = 0.2f;

        [Header("Charge Recovery")]
        [Min(1)] public int maxDodgeCharges = 2;
        [Tooltip("Seconds to restore one dodge charge. Spending another charge does not restart recovery.")]
        [Min(0.01f)] public float rechargeTime = 6f;

        [Header("Startup Hitstop")]
        public bool hitstopEnabled = true;
        [Min(0f)] public float hitstopDuration = 0.05f;

        [Header("Invulnerability Flash")]
        [Range(0f, 1f)] public float invulnerabilityFlashIntensity = 1f;
        public AnimationCurve invulnerabilityFlashDecay = new(
            new Keyframe(0f, 1f, 0f, 0f), new Keyframe(1f, 0f, -3f, -3f));

        [Header("Directional Flash")]
        public bool flashEnabled = true;
        public GameObject flashPrefab;
        [Min(0.01f)] public float flashDuration = 0.12f;
        [Min(0.01f)] public float flashLength = 0.55f;
        [Min(0.01f)] public float flashWidth = 0.18f;
        public float flashHeight = 0.15f;
        [Min(0f)] public float flashForwardDrift = 0.08f;
        public Color flashColor = Color.white;
        [Range(0f, 1f)] public float flashOpacity = 1f;
        [Tooltip("Sampled once per held pose; the flash has four poses over its total duration.")]
        public AnimationCurve flashFadeCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

        [Header("Distortion Shockwave")]
        public bool shockwaveEnabled = true;
        [Min(0.01f)] public float shockwaveDuration = 0.25f;
        [Min(0f)] public float shockwaveInitialRadius = 0.05f;
        [Min(0f)] public float shockwaveFinalRadius = 0.8f;
        [Min(0.001f)] public float shockwaveThickness = 0.12f;
        public float shockwaveHeight = 0.15f;
        [Tooltip("Peak displacement in pixels of the pixel renderer's reference grid.")]
        [Min(0f)] public float shockwaveStrengthPixels = 1f;
        [Tooltip("Limits the total offset where multiple waves overlap, in reference-grid pixels.")]
        [Min(0f)] public float shockwaveCombinedLimitPixels = 2f;
        public AnimationCurve shockwaveExpansion = new(new Keyframe(0f, 0f, 2f, 2f), new Keyframe(1f, 1f, 0f, 0f));
        public AnimationCurve shockwaveFalloff = AnimationCurve.Linear(0f, 1f, 1f, 0f);

        [Header("Afterimage Emission")]
        public bool afterimagesEnabled = true;
        [Min(0)] public int maxAfterimages = 8;
        [Min(0.001f)] public float spacing = 0.12f;
        [Min(0.01f)] public float lifetime = 0.2f;
        public Material afterimageMaterial;

        [Header("Silhouette and Fade")]
        [Tooltip("Multiplies the player's colour, independently of the source sprite RGB.")]
        public Color colorMultiplier = Color.white;
        [Range(0f, 1f)] public float opacity = 0.6f;
        [Tooltip("0 follows the fade curve smoothly. Positive values quantize opacity into that many steps.")]
        [Min(0)] public int fadeSteps;
        public AnimationCurve fadeCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    }
}
