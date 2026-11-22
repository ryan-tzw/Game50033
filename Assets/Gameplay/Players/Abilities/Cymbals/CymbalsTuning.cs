namespace Game50033
{
    using System;
    using UnityEngine;

    [CreateAssetMenu(menuName = "Game50033/Tuning/Cymbals", fileName = "CymbalsTuning")]
    public sealed class CymbalsTuning : ScriptableObject
    {
        public ComboSettings combo = new();
        [InspectorName("Throw")] public ThrowSettings cymbalThrow = new();
        public CrashSettings crash = new();
        public ParrySettings parry = new();
        public WeaponSettings weapon = new();

        [Header("Debug")]
        [Tooltip("Draw the hit areas as gizmos in play mode, following the aim. Turn on Gizmos in the Game view toolbar to see them there.")]
        public bool showHitboxes = true;
        [Tooltip("How long the area a hit actually used stays drawn in red after it resolves.")]
        [Min(0f)] public float hitboxFlashTime = 0.5f;

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
        public sealed class CrashSettings
        {
            [Header("Charges")]
            [Min(1)] public int maxCharges = 2;
            [Tooltip("Seconds to refill one charge; charges refill one at a time.")]
            [Min(0.1f)] public float rechargeTime = 6f;

            [Header("Timing")]
            [Tooltip("Cymbals rise overhead. A dash cancels it and no charge is spent.")]
            [Min(0.01f)] public float windup = 0.25f;
            [Tooltip("Committed time after the slam; a dash cannot interrupt it.")]
            [Min(0.01f)] public float impactHold = 0.1f;
            [Tooltip("Back to guard. A dash may cancel it.")]
            [Min(0.01f)] public float recovery = 0.35f;

            [Header("Area")]
            [Tooltip("Damage once per target in the triangle.")]
            [Min(0f)] public float damage = 4f;
            [Tooltip("Triangle length, from its tip at the combo hitting point forward along the aim.")]
            [Min(0.1f)] public float length = 3f;
            [Tooltip("Full width of the triangle at its far end.")]
            [Min(0.1f)] public float width = 3f;

            [Header("Waves")]
            [Tooltip("Unlit, vertex-coloured material for the slanted shockwaves.")]
            public Material waveMaterial;
            [Min(1)] public int waveCount = 3;
            [Min(0.01f)] public float waveDuration = 0.35f;
            [Tooltip("Delay between successive waves.")]
            [Min(0f)] public float waveStagger = 0.07f;
            [Tooltip("How far each wave's centre leads its side ends, giving the slant.")]
            public float waveSlant = 0.45f;
            [Min(0f)] public float waveWidth = 0.08f;
            public Color waveColor = new(1f, 0.95f, 0.75f, 0.9f);

            // triangle corners on the ground plane: tip at the hitting point, base at the far end.
            public void GetTriangle(Vector3 tip, Vector3 aim, out Vector3 left, out Vector3 right)
            {
                var side = Vector3.Cross(Vector3.up, aim) * (width * 0.5f);
                var far = tip + aim * length;
                left = far - side;
                right = far + side;
            }
        }

        [Serializable]
        public sealed class ParrySettings
        {
            [Header("Timing")]
            [Tooltip("How long the parry can block frontal damage. A dash ends it early.")]
            [Min(0.05f)] public float duration = 0.2f;
            [Tooltip("Cooldown, counted from when the parry ends (time out or dash cancel).")]
            [Min(0f)] public float cooldown = 10f;
            [Tooltip("Time for both cymbals to come together in front.")]
            [Min(0.01f)] public float raiseTime = 0.08f;

            [Header("Cone")]
            [Tooltip("Full angle of the cone, centred on the aim locked when parry starts.")]
            [Range(1f, 180f)] public float coneAngle = 60f;
            [Tooltip("Ground reach of the parry visual and its Amp field.")]
            [Min(0.1f)] public float range = 2.5f;
            [Header("Reward")]
            [Tooltip("Crash charges granted by the first parry of a press; later parries in the same press give nothing more.")]
            [Min(0)] public int crashChargeReward = 1;
            public Reverberation ampReverberation;
            [Tooltip("Played where each projectile is parried. Optional; use one without damage numbers.")]
            public HitEffect parryEffect;
            [Tooltip("Scale pop on both cymbals (and a flash on the parry line) when a projectile is parried.")]
            [Min(1f)] public float parryPopScale = 1.4f;
            [Min(0.01f)] public float parryPopTime = 0.15f;

            [Header("Stance")]
            [Tooltip("Sideways gap between the two cymbals while parrying; wider reads as covering more area.")]
            [Min(0f)] public float cymbalSpread = 0.45f;
            [Tooltip("How far each cymbal shakes from its stance position.")]
            [Min(0f)] public float shakeAmplitude = 0.025f;
            [Tooltip("Shakes per second.")]
            [Min(0f)] public float shakeFrequency = 28f;

            [Header("Parry line")]
            [Tooltip("Unlit, vertex-coloured material for the arc drawn along the end of the cone.")]
            public Material arcMaterial;
            [Min(0f)] public float arcWidth = 0.06f;
            [Min(2)] public int arcSegments = 12;
            public Color arcColor = new(0.8f, 0.95f, 1f, 0.85f);
            [Tooltip("Glow cycles per second while the parry is up.")]
            [Min(0f)] public float glowRate = 2.5f;
            [Tooltip("Brightness at the dimmest point of the glow, as a fraction of the arc colour's alpha.")]
            [Range(0f, 1f)] public float dimAlpha = 0.35f;
            [Tooltip("Width multiplier at the brightest point of the glow.")]
            [Min(1f)] public float glowWidth = 1.4f;
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
