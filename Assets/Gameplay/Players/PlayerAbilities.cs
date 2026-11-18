namespace Game50033
{
    using UnityEngine;

    public enum PlayerAttackPhase
    {
        None,
        Windup,
        Active,
        Recovery,
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Player))]
    public abstract class PlayerAbilities : MonoBehaviour
    {
        private Player _player;

        protected Player Player
        {
            get
            {
                if (_player == null) _player = GetComponent<Player>();
                return _player;
            }
        }

        protected bool IgnoreCooldowns => Player.Session != null && Player.Session.Debug.NoCooldowns;

        // reset deadlines only; active actions, resources and combo progression keep their normal owners.
        public virtual void ClearCooldowns() { }

        // unsupported slots default to a disabled icon; concrete instruments expose their own live state.
        public virtual PlayerAbilityState GetAbilityState(PlayerAbility ability) => default;

        public bool BlocksMovement { get; protected set; }
        public bool BlocksDodge { get; protected set; }
        public bool BlocksAbilities { get; protected set; }
        public virtual bool BlocksDefensive => BlocksAbilities;
        public virtual float MovementSpeedMultiplier => 1f;
        public virtual PlayerAttackPhase AnimationPhase => PlayerAttackPhase.None;

        protected virtual void OnDisable()
        {
            Cancel();
            BlocksMovement = false;
            BlocksDodge = false;
            BlocksAbilities = false;
        }

        // called while held; implementations return false until their own cooldown permits activation.
        public abstract bool TryUsePrimary();
        public abstract bool TryUseSecondary();
        public abstract bool TryUseDefensive();
        public abstract bool TryUseSpecial();

        // stop active attacks and release restrictions; already-spent cooldowns can remain.
        public abstract void Cancel();
    }
}
