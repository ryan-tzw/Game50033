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

        internal Boss3Tuning Tuning => tuning;
        internal BoxCollider ArenaFloor => arenaFloor;
        internal BouncingArcAttack ActiveArc { get; set; }
        internal HomingSparkAttack ActiveSparkAttack { get; set; }
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
            ClearSparks();
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

        private void HandleDefeat(IEncounterBoss boss) => ClearSparks();

        private void OnDestroy()
        {
            foreach (var spark in _activeSparks)
                if (spark != null) Destroy(spark.gameObject);
            _sparkPool?.Clear();
        }
    }
}
