namespace Game50033
{
    using UnityEngine;

    public sealed class ExterminatorBotBoss : BaseBoss
    {
        [SerializeField] private Boss3Tuning tuning;
        [SerializeField] private BoxCollider arenaFloor;

        internal Boss3Tuning Tuning => tuning;
        internal BoxCollider ArenaFloor => arenaFloor;
        internal BouncingArcAttack ActiveArc { get; set; }
        protected override bool PlayAttackAnimationOnStart => false;

        protected override void InitializeHealth() =>
            Health.Initialize(BossTotalPhases, tuning.maxHealth, Mathf.Clamp(tuning.phaseTwoHealth, 0f, tuning.maxHealth));

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
            Attacks.SetPhase(phaseNumber);
        }
    }
}
