namespace Game50033
{
    using System;
    using UnityEngine;

    [CreateAssetMenu(menuName = "Game50033/Tuning/Cymbals", fileName = "CymbalsTuning")]
    public sealed class CymbalsTuning : ScriptableObject
    {
        public ComboSettings combo = new();
        [InspectorName("Throw")] public ThrowSettings cymbalThrow = new();
        public WeaponSettings weapon = new();

        // a pose relative to the hit's locked aim: x = degrees around the player (+ = aim-right), y = distance.
        [Serializable]
        public struct HandMotion
        {
            [Tooltip("Whether this hand swings through the hitting point. A hand that does not swing holds its guard pose.")]
            public bool swings;
            [Tooltip("Where the cymbal starts the approach. X: degrees around the player (+ = aim-right). Y: distance from the player.")]
            public Vector2 startPose;
            [Tooltip("Where the follow-through ends. X: degrees around the player (+ = aim-right). Y: distance from the player.")]
            public Vector2 endPose;
        }

        [Serializable]
        public struct Hit
        {
            public string label;
            [Tooltip("Rest pose to start pose. A dash may cancel this phase; the combo step is kept.")]
            [Min(0.01f)] public float windup;
            [Tooltip("Start pose to the hitting point. Damage resolves when this phase ends.")]
            [Min(0.01f)] public float approach;
            [Tooltip("Hitting point to end pose. Cannot be cancelled.")]
            [Min(0.01f)] public float followThrough;
            [Tooltip("End pose back to rest. A dash may cancel this phase; the combo continues afterwards.")]
            [Min(0.01f)] public float recovery;
            [Min(0f)] public float damage;
            [Tooltip("Forward advance during the approach, so the player arrives as the hit lands.")]
            [Min(0f)] public float lungeDistance;
            [Tooltip("Uses the hit effect's big-hit style (bigger impact, damage number and sound).")]
            public bool bigHit;
            public HandMotion left;
            public HandMotion right;
        }

        [Serializable]
        public sealed class ComboSettings
        {
            public Hit[] hits =
            {
                new Hit
                {
                    label = "Right forward bash",
                    windup = 0.12f, approach = 0.08f, followThrough = 0.08f, recovery = 0.16f,
                    damage = 1f, lungeDistance = 0.25f,
                    right = new HandMotion { swings = true, startPose = new Vector2(25f, 0.2f), endPose = new Vector2(5f, 0.9f) },
                },
                new Hit
                {
                    label = "Left slash to right",
                    windup = 0.1f, approach = 0.07f, followThrough = 0.09f, recovery = 0.14f,
                    damage = 1f, lungeDistance = 0.2f,
                    left = new HandMotion { swings = true, startPose = new Vector2(-80f, 0.7f), endPose = new Vector2(80f, 0.65f) },
                },
                new Hit
                {
                    label = "Right slash to left",
                    windup = 0.1f, approach = 0.07f, followThrough = 0.09f, recovery = 0.14f,
                    damage = 1f, lungeDistance = 0.2f,
                    right = new HandMotion { swings = true, startPose = new Vector2(80f, 0.7f), endPose = new Vector2(-80f, 0.65f) },
                },
                new Hit
                {
                    label = "Left slash to right",
                    windup = 0.1f, approach = 0.07f, followThrough = 0.09f, recovery = 0.14f,
                    damage = 1f, lungeDistance = 0.2f,
                    left = new HandMotion { swings = true, startPose = new Vector2(-80f, 0.7f), endPose = new Vector2(80f, 0.65f) },
                },
                new Hit
                {
                    label = "Both cymbals clash",
                    windup = 0.18f, approach = 0.08f, followThrough = 0.12f, recovery = 0.3f,
                    damage = 2.5f, lungeDistance = 0.35f, bigHit = true,
                    left = new HandMotion { swings = true, startPose = new Vector2(-75f, 0.55f), endPose = new Vector2(0f, 0.85f) },
                    right = new HandMotion { swings = true, startPose = new Vector2(75f, 0.55f), endPose = new Vector2(0f, 0.85f) },
                },
            };

            [Header("Hitting point")]
            [Tooltip("Distance from the player to the hitting point, along the hit's aim.")]
            [Min(0f)] public float hitDistance = 0.75f;
            [Tooltip("Radius of the damage circle around the hitting point.")]
            [Min(0.01f)] public float hitRadius = 0.45f;
            [Tooltip("Visual height of the hitting point above the player's origin; the cymbals meet here.")]
            public float hitHeight = 0.25f;
            [Tooltip("Height of the damage column, measured up from the player's feet. Damage only checks distance on the ground plane within it.")]
            [Min(0.01f)] public float hitColumnHeight = 2f;
            public LayerMask hitMask = ~0;
            [Tooltip("Played at the contact point of every target hit. Presentation only.")]
            public HitEffect hitEffect;

            [Header("Chaining")]
            [Tooltip("Time after a hit's recovery in which the next press continues the combo.")]
            [Min(0f)] public float comboResetDelay = 0.8f;
            [Tooltip("Cooldown after the fifth hit before the combo can start again.")]
            [Min(0f)] public float comboCooldown = 0.45f;
            public AnimationCurve lungeEasing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

            // the single definition of the hitting point, shared by damage and the weapon visual.
            public Vector3 GetHitPoint(Vector3 playerPosition, Vector3 aim) =>
                playerPosition + Vector3.up * hitHeight + aim * hitDistance;
        }

        [Serializable]
        public sealed class ThrowSettings
        {
            [Header("Throw")]
            [Tooltip("Throw motion before the cymbal leaves the hand. A dash during it cancels the throw.")]
            [Min(0.01f)] public float windup = 0.12f;
            [Tooltip("Distance to the apex, along the aim at release.")]
            [Min(0.1f)] public float range = 3.5f;
            [Tooltip("Seconds from release to the apex.")]
            [Min(0.05f)] public float outboundTime = 0.45f;
            [Tooltip("Seconds from the apex back to the player.")]
            [Min(0.05f)] public float returnTime = 0.45f;
            [Tooltip("Progress to the apex over normalised time. The default slope falls from 2 to 0: fast out, stopping exactly at the apex.")]
            public AnimationCurve outboundEasing = new(new Keyframe(0f, 0f, 2f, 2f), new Keyframe(1f, 1f, 0f, 0f));
            [Tooltip("Progress from the apex to the player over normalised time. The default slope rises from 0: it speeds up on the way back.")]
            public AnimationCurve returnEasing = new(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2f, 2f));
            [Tooltip("Cooldown after the cymbal is caught.")]
            [Min(0f)] public float cooldown = 0.6f;

            [Header("Flight damage")]
            [Tooltip("Damage once per target on the way out, and again on the way back.")]
            [Min(0f)] public float damage = 1.5f;
            [Tooltip("Radius of the flying cymbal's damage column on the ground plane.")]
            [Min(0.01f)] public float hitRadius = 0.35f;

            [Header("Recast")]
            [Tooltip("Total seconds, centred on the apex, in which a recast also dashes to the cymbal.")]
            [Min(0f)] public float apexWindow = 0.5f;
            [Tooltip("Spawned at the cymbal on any recast.")]
            public Reverberation burstReverberation;
            [Min(0.01f)] public float burstStrength = 1f;

            [Header("Recast dash")]
            [Min(0.1f)] public float dashSpeed = 14f;
            [Tooltip("The dash stops this far short of the cymbal, where it is caught.")]
            [Min(0f)] public float catchOffset = 0.3f;
            [Tooltip("Damage once per target, checked at the combo's hitting point every frame of the dash.")]
            [Min(0f)] public float dashDamage = 2f;
            public AnimationCurve dashEasing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

            [Header("Visual")]
            [Tooltip("Spin in degrees per second at full flight speed. Spin follows travel speed, slowing near the apex.")]
            public float spinSpeed = 1440f;
            [Tooltip("Share of the spin kept at zero speed, so the cymbal never looks frozen.")]
            [Range(0f, 1f)] public float minimumSpin = 0.2f;
            [Tooltip("Flash on the cymbal while the apex window is open.")]
            public Color apexFlashColor = new(1f, 0.95f, 0.6f, 1f);
            [Range(0f, 1f)] public float apexFlashAmount = 0.6f;
            [Tooltip("Flash pulses per second while the apex window is open.")]
            [Min(0f)] public float apexFlashRate = 10f;
        }

        [Serializable]
        public sealed class WeaponSettings
        {
            [Header("Guard pose")]
            [Tooltip("Rest position of each cymbal: degrees to the side of the facing direction, mirrored per hand.")]
            [Range(0f, 180f)] public float restAngle = 75f;
            [Min(0f)] public float restDistance = 0.32f;
            public float restHeight = 0.3f;
            [Tooltip("Sideways gap kept between the two cymbals when both meet at the hitting point.")]
            [Min(0f)] public float clashSpacing = 0.12f;

            [Header("Motion")]
            public AnimationCurve windupEasing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            [Tooltip("Start pose to hitting point. An ease-in reads as accelerating into the hit.")]
            public AnimationCurve approachEasing = new(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2f, 2f));
            [Tooltip("Hitting point to end pose. An ease-out reads as the swing losing speed after contact.")]
            public AnimationCurve followThroughEasing = new(new Keyframe(0f, 0f, 2f, 2f), new Keyframe(1f, 1f, 0f, 0f));
            public AnimationCurve recoveryEasing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            [Tooltip("How quickly idle cymbals follow the guard pose when not attacking.")]
            [Min(0.01f)] public float followSharpness = 18f;

            [Header("Sprite")]
            [Min(0.01f)] public float spriteScale = 1f;
            [Tooltip("Scale multiplier on a swinging cymbal at the hitting point, easing back over the follow-through.")]
            [Min(0.01f)] public float impactScale = 1.25f;

            [Header("Slash trail")]
            [Tooltip("Unlit, vertex-coloured material for the trail behind each swinging cymbal.")]
            public Material trailMaterial;
            [Tooltip("Seconds a trail point lasts; longer gives a longer streak.")]
            [Min(0.01f)] public float trailTime = 0.09f;
            [Min(0f)] public float trailStartWidth = 0.2f;
            [Min(0f)] public float trailEndWidth = 0f;
            public Gradient trailColor = new()
            {
                colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                alphaKeys = new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) },
            };
        }
    }
}
