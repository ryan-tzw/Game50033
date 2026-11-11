namespace Game50033
{
    using UnityEngine;

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

        public bool BlocksMovement { get; protected set; }
        public bool BlocksDodge { get; protected set; }
        public bool BlocksAbilities { get; protected set; }

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
