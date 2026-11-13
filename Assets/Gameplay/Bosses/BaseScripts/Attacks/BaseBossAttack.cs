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

        public bool IsChanneling { get; protected set; }

        protected virtual void Awake() { startTime = Time.time; }

        protected virtual void OnDisable() { Cancel(); }

        protected virtual void ChannelAttack() { IsChanneling = true; }
        public virtual void Attack() { IsChanneling = false; }
        public abstract void Cancel();
    }
}
