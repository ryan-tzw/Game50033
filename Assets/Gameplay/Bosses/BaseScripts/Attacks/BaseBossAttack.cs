namespace Game50033
{
    using UnityEngine;
    public abstract class BaseBossAttack : MonoBehaviour
    {
        protected int _attackDamage = 1;
        protected float startTime;
        public float windUpTime;
        public float activeTime;
        public float recoveryTime;
        protected BaseBoss boss;

        public bool IsChanneling { get; protected set; }
        public virtual bool CanStart => true;

        // Prevent boss from starting another attack while Charge is still moving it (for Tutorial boss)

        public virtual bool ControlsBoss => false;
        public virtual float BossControlTimeout => 0f;
        public event System.Action<BaseBossAttack> BossControlReleased;

        protected void NotifyBossControlReleased() => BossControlReleased?.Invoke(this);

        protected virtual void Awake() { startTime = Time.time; }

        protected virtual void OnDisable() { Cancel(); }

        protected virtual void ChannelAttack() { IsChanneling = true; }
        public virtual void Attack() { IsChanneling = false; }
        public abstract void Cancel();

        public virtual void Setup(BaseBoss owner)
        {
            boss = owner;
        }
    }
}
