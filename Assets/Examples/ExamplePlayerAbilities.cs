namespace Game50033.Examples
{
    using UnityEngine;

    public sealed class ExamplePlayerAbilities : PlayerAbilities
    {
        [SerializeField, Min(0.01f)] private float primaryCooldown = 0.5f;
        [SerializeField, Min(0.01f)] private float secondaryCooldown = 1f;
        [SerializeField, Min(0.01f)] private float defensiveCooldown = 2f;
        [SerializeField, Min(0.01f)] private float specialCooldown = 3f;
        [SerializeField, Min(0.01f)] private float actionDuration = 0.25f;

        private float _primaryReadyAt;
        private float _secondaryReadyAt;
        private float _defensiveReadyAt;
        private float _specialReadyAt;
        private float _activeUntil;

        private void Update()
        {
            if (_activeUntil > 0f && Time.time >= _activeUntil) Cancel();
        }

        public override bool TryUsePrimary() =>
            TryActivate("Primary", ref _primaryReadyAt, primaryCooldown, blockMovement: true, blockDodge: false);

        public override bool TryUseSecondary() =>
            TryActivate("Secondary", ref _secondaryReadyAt, secondaryCooldown, blockMovement: false, blockDodge: false);

        public override bool TryUseDefensive() =>
            TryActivate("Defensive", ref _defensiveReadyAt, defensiveCooldown, blockMovement: false, blockDodge: true);

        public override bool TryUseSpecial() =>
            TryActivate("Special", ref _specialReadyAt, specialCooldown, blockMovement: true, blockDodge: true);

        private bool TryActivate(string ability, ref float readyAt, float cooldown, bool blockMovement, bool blockDodge)
        {
            if (!isActiveAndEnabled || Player.Health.IsDowned || Player.Movement.IsDodging
                || BlocksAbilities || Time.time < readyAt) return false;

            readyAt = Time.time + cooldown;
            _activeUntil = Time.time + actionDuration;
            BlocksMovement = blockMovement;
            BlocksDodge = blockDodge;
            BlocksAbilities = true;
            Debug.Log($"{Player.name}: example {ability} activated.", this);
            return true;
        }

        public override void Cancel()
        {
            _activeUntil = 0f;
            BlocksMovement = false;
            BlocksDodge = false;
            BlocksAbilities = false;
        }
    }
}
