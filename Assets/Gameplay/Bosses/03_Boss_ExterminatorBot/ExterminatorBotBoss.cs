namespace Game50033
{
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.Pool;

    public sealed class ExterminatorBotBoss : BaseBoss
    {
        [SerializeField] private Boss3Tuning tuning;
        [SerializeField] private BoxCollider arenaFloor;

        private readonly List<HomingSparkProjectile> _activeSparks = new(8);
        private ObjectPool<HomingSparkProjectile> _sparkPool;
        private readonly List<SpiralSurgeProjectile> _activeSpiralBolts = new(24);
        private ObjectPool<SpiralSurgeProjectile> _spiralPool;

        internal Boss3Tuning Tuning => tuning;
        internal BoxCollider ArenaFloor => arenaFloor;
        internal BouncingArcAttack ActiveArc { get; set; }
        internal HomingSparkAttack ActiveSparkAttack { get; set; }
        internal SpiralSurgeAttack ActiveSpiralSurge { get; set; }
        protected override bool PlayAttackAnimationOnStart => false;

        protected override void InitializeHealth()
        {
            Health.Initialize(BossTotalPhases, tuning.maxHealth, Mathf.Clamp(tuning.phaseTwoHealth, 0f, tuning.maxHealth));
            Defeated += HandleDefeat;
        }

        protected override void Update()
        {
            var velocityX = _rb.linearVelocity.x;
            if (!Attacks.IsAttacking && Mathf.Abs(velocityX) > 0.01f) FaceArc(velocityX);
            base.Update();
        }

        internal void FaceArc(float x)
        {
            if (Mathf.Abs(x) > 0.01f) _sr.flipX = x < 0f;
        }

        protected override void HandlePhaseChange(int phaseNumber)
        {
            if (ActiveArc != null) ActiveArc.Cancel();
            if (ActiveSparkAttack != null) ActiveSparkAttack.Cancel();
            if (ActiveSpiralSurge != null) ActiveSpiralSurge.Cancel();
            ClearSparks();
            ClearSpiralBolts();
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
        }

        private void OnDestroy()
        {
            foreach (var spark in _activeSparks)
                if (spark != null) Destroy(spark.gameObject);
            _sparkPool?.Clear();
            foreach (var bolt in _activeSpiralBolts)
                if (bolt != null) Destroy(bolt.gameObject);
            _spiralPool?.Clear();
        }
    }
}
