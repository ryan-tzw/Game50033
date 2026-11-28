namespace Game50033
{
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.Pool;

    public sealed class Boss3Controller : BaseBoss
    {
        private static readonly int BatonWindupState = Animator.StringToHash("Base Layer.BatonWindup");
        private static readonly int BatonChargeState = Animator.StringToHash("Base Layer.BatonCharge");
        private static readonly int BatonWindupSpeed = Animator.StringToHash("BatonWindupSpeed");
        private static readonly int SpiralWindupState = Animator.StringToHash("Base Layer.SpiralWindup");
        private static readonly int SpiralChargeState = Animator.StringToHash("Base Layer.SpiralCharge");
        private static readonly int SpiralRecoveryState = Animator.StringToHash("Base Layer.SpiralRecovery");
        private static readonly int SpiralPoseSpeed = Animator.StringToHash("SpiralPoseSpeed");
        private static readonly int ShootState = Animator.StringToHash("Base Layer.Shoot");
        private static readonly int IdleState = Animator.StringToHash("Base Layer.Idle");
        private const float BatonWindupClipDuration = 3f / 12f;
        private const float SpiralLandingPoseDuration = 0.15f;
        private const int SpiralWindupFrameCount = 5;

        [SerializeField] private Boss3Tuning tuning;
        [SerializeField] private BoxCollider arenaFloor;
        [SerializeField] private SpriteRenderer kunaiOverlay;

        private Animator _batonAnimator;
        private bool _chargingBaton;
        private float _spriteRestY;
        private float _spiralRiseAt, _spiralApexAt, _spiralDropAt, _spiralLandAt, _spiralChargeAt;
        private float _spiralJumpHeight, _spiralSpriteHeight;
        private int _spiralWindupFrame = -1;
        private bool _spiralWindupActive;
        private readonly List<HomingSparkProjectile> _activeSparks = new(8);
        private ObjectPool<HomingSparkProjectile> _sparkPool;
        private readonly List<SpiralSurgeProjectile> _activeSpiralBolts = new(24);
        private ObjectPool<SpiralSurgeProjectile> _spiralPool;
        private readonly List<ElectricKnifeProjectile> _activeKnives = new(8);
        private ObjectPool<ElectricKnifeProjectile> _knifePool;

        internal Boss3Tuning Tuning => tuning;
        internal BoxCollider ArenaFloor => arenaFloor;
        internal SpriteRenderer KunaiOverlay => kunaiOverlay;
        internal BouncingArcAttack ActiveArc { get; set; }
        internal HomingSparkAttack ActiveSparkAttack { get; set; }
        internal SpiralSurgeAttack ActiveSpiralSurge { get; set; }
        internal ElectricKnivesAttack ActiveElectricKnives { get; set; }
        protected override bool PlayAttackAnimationOnStart => false;

        protected override void InitializeHealth()
        {
            Movement.UpdatesFacing = false;
            _batonAnimator = GetComponentInChildren<Animator>();
            if (_batonAnimator) _spriteRestY = _batonAnimator.transform.localPosition.y;
            Health.Initialize(BossTotalPhases, tuning.maxHealth, Mathf.Clamp(tuning.phaseTwoHealth, 0f, tuning.maxHealth));
            Defeated += HandleDefeat;
        }

        protected override void Update()
        {
            if (_spiralWindupActive)
            {
                var now = Time.time;
                if (now >= _spiralChargeAt)
                {
                    ResetSpiralHop();
                    _batonAnimator.Play(SpiralChargeState, 0, 0f);
                }
                else if (now >= _spiralRiseAt)
                {
                    int frame;
                    if (now < _spiralApexAt)
                    {
                        frame = 0;
                        _spiralSpriteHeight = _spiralJumpHeight * Mathf.InverseLerp(_spiralRiseAt, _spiralApexAt, now);
                    }
                    else if (now < _spiralDropAt)
                    {
                        frame = now < (_spiralApexAt + _spiralDropAt) * 0.5f ? 1 : 2;
                        _spiralSpriteHeight = _spiralJumpHeight;
                    }
                    else if (now < _spiralLandAt)
                    {
                        frame = 2;
                        _spiralSpriteHeight = _spiralJumpHeight * (1f - Mathf.InverseLerp(_spiralDropAt, _spiralLandAt, now));
                    }
                    else
                    {
                        frame = now < (_spiralLandAt + _spiralChargeAt) * 0.5f ? 3 : 4;
                        _spiralSpriteHeight = 0f;
                    }

                    if (frame != _spiralWindupFrame)
                    {
                        _spiralWindupFrame = frame;
                        // sample inside the sprite key so floating-point rounding cannot select the previous pose.
                        _batonAnimator.Play(SpiralWindupState, 0, (frame + 0.25f) / SpiralWindupFrameCount);
                        _batonAnimator.Update(0f);
                    }
                }
            }

            var target = Movement.FollowTarget;
            if (!Attacks.IsAttacking && target && target.isActiveAndEnabled && !target.Health.IsDowned)
            {
                var toTargetX = target.transform.position.x - transform.position.x;
                if (Mathf.Abs(toTargetX) > 0.01f) _sr.flipX = toTargetX < 0f;
            }
            // choose facing before the shared update can start the next cast.
            base.Update();
        }

        private void LateUpdate()
        {
            if (kunaiOverlay && kunaiOverlay.sprite) kunaiOverlay.flipX = _sr.flipX;
            if (!_spiralWindupActive) return;
            // apply the lift after the Animator writes its pose. the boss root and collider stay on the ground.
            var sprite = _batonAnimator.transform;
            var position = sprite.localPosition;
            position.y = _spriteRestY + _spiralSpriteHeight;
            sprite.localPosition = position;
        }

        internal void BeginBatonWindup(float duration)
        {
            _chargingBaton = true;
            if (!_batonAnimator) return;

            if (duration <= 0f)
            {
                _batonAnimator.Play(BatonChargeState, 0, 0f);
                return;
            }

            _batonAnimator.SetFloat(BatonWindupSpeed, BatonWindupClipDuration / duration);
            _batonAnimator.Play(BatonWindupState, 0, 0f);
        }

        internal void SlashBaton()
        {
            _chargingBaton = false;
            if (_batonAnimator) _batonAnimator.Play(ShootState, 0, 0f);
        }

        internal void EndBatonCharge()
        {
            if (!_chargingBaton) return;
            _chargingBaton = false;
            if (_batonAnimator && _batonAnimator.isActiveAndEnabled)
                _batonAnimator.Play(IdleState, 0, 0f);
        }

        internal float BeginSpiralCharge(float windupDuration)
        {
            if (windupDuration <= 0f)
            {
                if (_batonAnimator) _batonAnimator.Play(SpiralChargeState, 0, 0f);
                return 0f;
            }

            var takeoffDelay = Mathf.Max(0f, tuning.spiralTakeoffDelay);
            var rise = Mathf.Max(0.01f, tuning.spiralRiseDuration);
            var hold = Mathf.Max(0f, tuning.spiralApexHoldDuration);
            var drop = Mathf.Max(0.01f, tuning.spiralDropDuration);
            var landing = takeoffDelay + rise + hold + drop;
            // keep touchdown ahead of the volley if a shorter telegraph is set in tuning.
            var scale = Mathf.Min(1f, windupDuration / (landing + SpiralLandingPoseDuration));
            if (_batonAnimator)
            {
                var now = Time.time;
                _spiralRiseAt = now + takeoffDelay * scale;
                _spiralApexAt = now + (takeoffDelay + rise) * scale;
                _spiralDropAt = now + (takeoffDelay + rise + hold) * scale;
                _spiralLandAt = now + landing * scale;
                _spiralChargeAt = now + (landing + SpiralLandingPoseDuration) * scale;
                _spiralJumpHeight = Mathf.Max(0f, tuning.spiralJumpHeight);
                _spiralSpriteHeight = 0f;
                _spiralWindupFrame = -1;
                _spiralWindupActive = true;
                _batonAnimator.SetFloat(SpiralPoseSpeed, 0f);
                _batonAnimator.Play(IdleState, 0, 0f);
            }
            return landing * scale;
        }

        internal void FinishSpiralCharge(float recoveryDuration)
        {
            ResetSpiralHop();
            if (!_batonAnimator || !_batonAnimator.isActiveAndEnabled) return;
            if (recoveryDuration <= 0f)
            {
                _batonAnimator.Play(IdleState, 0, 0f);
                return;
            }

            _batonAnimator.SetFloat(SpiralPoseSpeed, Mathf.Max(1f, (6f / 12f) / recoveryDuration));
            _batonAnimator.Play(SpiralRecoveryState, 0, 0f);
        }

        private void ResetSpiralHop()
        {
            if (!_spiralWindupActive) return;
            _spiralWindupActive = false;
            if (!_batonAnimator) return;
            var sprite = _batonAnimator.transform;
            var position = sprite.localPosition;
            position.y = _spriteRestY;
            sprite.localPosition = position;
        }

        protected override void HandlePhaseChange(int phaseNumber)
        {
            if (ActiveArc != null) ActiveArc.Cancel();
            if (ActiveSparkAttack != null) ActiveSparkAttack.Cancel();
            if (ActiveSpiralSurge != null) ActiveSpiralSurge.Cancel();
            if (ActiveElectricKnives != null) ActiveElectricKnives.Cancel();
            ClearSparks();
            ClearSpiralBolts();
            ClearKnives();
            Attacks.SetPhase(phaseNumber);
        }

        internal void LaunchSpark(HomingSparkProjectile prefab, Player target, Vector3 position, float speed)
        {
            _sparkPool ??= new ObjectPool<HomingSparkProjectile>(
                () => Instantiate(prefab, transform),
                actionOnRelease: spark =>
                {
                    spark.gameObject.SetActive(false);
                    spark.transform.SetParent(transform, false);
                },
                actionOnDestroy: spark => { if (spark != null) Destroy(spark.gameObject); },
                defaultCapacity: 4,
                maxSize: 12);

            var projectile = _sparkPool.Get();
            _activeSparks.Add(projectile);
            projectile.transform.SetParent(null, true);
            projectile.Launch(this, target, position, speed);
        }

        internal void ReturnSpark(HomingSparkProjectile projectile)
        {
            if (_activeSparks.Remove(projectile))
                _sparkPool.Release(projectile);
        }

        private void ClearSparks()
        {
            for (var i = _activeSparks.Count - 1; i >= 0; i--)
                ReturnSpark(_activeSparks[i]);
        }

        internal void LaunchSpiralBolt(SpiralSurgeProjectile prefab, Vector3 center, Vector3 direction, float speed)
        {
            _spiralPool ??= new ObjectPool<SpiralSurgeProjectile>(
                () => Instantiate(prefab, transform),
                actionOnRelease: bolt =>
                {
                    bolt.gameObject.SetActive(false);
                    bolt.transform.SetParent(transform, false);
                },
                actionOnDestroy: bolt => { if (bolt != null) Destroy(bolt.gameObject); },
                defaultCapacity: 24,
                maxSize: 48);

            var launched = _spiralPool.Get();
            _activeSpiralBolts.Add(launched);
            launched.transform.SetParent(null, true);
            launched.Launch(this, center, direction, speed);
        }

        internal void ReturnSpiralBolt(SpiralSurgeProjectile bolt)
        {
            if (_activeSpiralBolts.Remove(bolt))
                _spiralPool.Release(bolt);
        }

        private void ClearSpiralBolts()
        {
            for (var i = _activeSpiralBolts.Count - 1; i >= 0; i--)
                ReturnSpiralBolt(_activeSpiralBolts[i]);
        }

        private void HandleDefeat(IEncounterBoss boss)
        {
            ClearSparks();
            ClearSpiralBolts();
            ClearKnives();
        }

        internal void LaunchKnife(ElectricKnifeProjectile prefab, Vector3 origin, Vector3 landing, float speed, float releaseHeight,
            float impactRadius, MeshFilter warning, float lockedAt, float flashDuration, float flashIntensity)
        {
            _knifePool ??= new ObjectPool<ElectricKnifeProjectile>(
                () => Instantiate(prefab, transform),
                actionOnRelease: knife =>
                {
                    knife.gameObject.SetActive(false);
                    knife.transform.SetParent(transform, false);
                },
                actionOnDestroy: knife => { if (knife != null) Destroy(knife.gameObject); },
                defaultCapacity: 8,
                maxSize: 24);

            var projectile = _knifePool.Get();
            _activeKnives.Add(projectile);
            projectile.transform.SetParent(null, true);
            projectile.Launch(this, origin, landing, speed, releaseHeight, impactRadius, warning, lockedAt, flashDuration, flashIntensity);
        }

        internal void KnifeEmbedded()
        {
            var count = 0;
            for (var i = _activeKnives.Count - 1; i >= 0; i--)
            {
                var knife = _activeKnives[i];
                if (knife.IsEmbedded && ++count > Mathf.Max(ElectricKnivesAttack.KnivesPerCast, tuning.knifeMaxActive))
                    ReturnKnife(knife);
            }
        }

        internal void ReturnKnife(ElectricKnifeProjectile knife)
        {
            if (_activeKnives.Remove(knife))
                _knifePool.Release(knife);
        }

        private void ClearKnives()
        {
            for (var i = _activeKnives.Count - 1; i >= 0; i--)
                ReturnKnife(_activeKnives[i]);
        }

        private void OnDestroy()
        {
            foreach (var spark in _activeSparks)
                if (spark != null) Destroy(spark.gameObject);
            _sparkPool?.Clear();
            foreach (var bolt in _activeSpiralBolts)
                if (bolt != null) Destroy(bolt.gameObject);
            _spiralPool?.Clear();
            foreach (var knife in _activeKnives)
                if (knife != null) Destroy(knife.gameObject);
            _knifePool?.Clear();
        }
    }
}
