namespace Game50033
{
    using UnityEngine;

    public class TromboneAbilities : PlayerAbilities
    {
        [SerializeField, Min(0.01f)] private float trombonePrimaryCooldown = 0.5f;
        [SerializeField, Min(0.01f)] private float tromboneSecondaryCooldown = 1f;
        [SerializeField, Min(0.01f)] private float tromboneDefensiveCooldown = 2f;
        [SerializeField, Min(0.01f)] private float tromboneSpecialCooldown = 3f;
        [SerializeField, Min(0.01f)] private float tromboneActionDuration = 0.25f;

        private float _trombonePrimaryReadyAt;
        private float _tromboneSecondaryReadyAt;
        private float _tromboneDefensiveReadyAt;
        private float _tromboneSpecialReadyAt;
        private float _tromboneActiveUntil;
        private TromboneCommandZone _activeCommandZone;
        public GameObject tromboneCommandZone;

        public bool IsInCommandZone => _activeCommandZone != null && _activeCommandZone.IsOwnerInside;
        public float PressureAccumulationMultiplier => _activeCommandZone != null
            ? _activeCommandZone.PressureAccumulationMultiplier : 1f;
        public float PressureDecayMultiplier => _activeCommandZone != null
            ? _activeCommandZone.PressureDecayMultiplier : 1f;

        enum TromboneAbilitiesType
        {
            TrombonePrimary,
            TromboneSecondary,
            TromboneDefensive,
            TromboneSpecial
        }
        void Update()
        {
            if (_tromboneActiveUntil > 0f && Time.time >= _tromboneActiveUntil) Cancel();
        }

        public override bool TryUsePrimary() =>
            TryActivate(TromboneAbilitiesType.TrombonePrimary, ref _trombonePrimaryReadyAt, trombonePrimaryCooldown, blockMovement: true, blockDodge: false);

        public override bool TryUseSecondary()
        {
            if (!CanCreateCommandZone())
            {
                return false;
            }

            return TryActivate(
                TromboneAbilitiesType.TromboneSecondary,
                ref _tromboneSecondaryReadyAt,
                tromboneSecondaryCooldown,
                blockMovement: false,
                blockDodge: false);
        }

        public override bool TryUseDefensive() =>
            TryActivate(TromboneAbilitiesType.TromboneDefensive, ref _tromboneDefensiveReadyAt, tromboneDefensiveCooldown, blockMovement: false, blockDodge: true);

        public override bool TryUseSpecial() =>
            TryActivate(TromboneAbilitiesType.TromboneSpecial, ref _tromboneSpecialReadyAt, tromboneSpecialCooldown, blockMovement: true, blockDodge: true);

        private bool TryActivate(TromboneAbilitiesType ability, ref float readyAt, float cooldown, bool blockMovement, bool blockDodge)
        {
            if (!isActiveAndEnabled || Player.Health.IsDowned || Player.Movement.IsDodging
                || BlocksAbilities || Time.time < readyAt) return false;

            readyAt = Time.time + cooldown;
            _tromboneActiveUntil = Time.time + tromboneActionDuration;
            BlocksMovement = blockMovement;
            BlocksDodge = blockDodge;
            BlocksAbilities = true;

            switch (ability)
            {
                case TromboneAbilitiesType.TrombonePrimary:
                    TrombonePrimaryAbility();
                    break;
                case TromboneAbilitiesType.TromboneSecondary:
                    CreateCommandZone();
                    break;
                case TromboneAbilitiesType.TromboneDefensive:
                    TromboneDefensiveAbility();
                    break;
                case TromboneAbilitiesType.TromboneSpecial:
                    TromboneSpecialAbility();
                    break;
            }

            Debug.Log($"{Player.name}: example {ability} activated.", this);
            return true;
        }
        public void TrombonePrimaryAbility()
        {

        }
        public void TromboneSecondaryAbility()
        {
            // Make a command zone at player location, if player leave command zone, zone destroyed, Reverberation zone created
            if (!CanCreateCommandZone())
            {
                return;
            }

            CreateCommandZone();
        }

        private void CreateCommandZone()
        {
            var commandZone = Instantiate(tromboneCommandZone, transform.position, Quaternion.identity);
            _activeCommandZone = commandZone.GetComponent<TromboneCommandZone>();
            _activeCommandZone.Initialize(Player);
        }

        private bool CanCreateCommandZone()
        {
            if (_activeCommandZone != null && _activeCommandZone.IsActive)
            {
                return false;
            }

            if (tromboneCommandZone == null || !tromboneCommandZone.activeSelf)
            {
                return false;
            }

            var zone = tromboneCommandZone.GetComponent<TromboneCommandZone>();
            if (zone == null || !zone.enabled)
            {
                return false;
            }

            // Reject an incomplete Sustain setup before starting the cooldown.
            var reverberation = zone.SustainReverberationPrefab;
            if (reverberation == null || !reverberation.enabled || !reverberation.gameObject.activeSelf)
            {
                return false;
            }

            if (!(reverberation.Effect is SustainEffect sustain) || !sustain.IsConfigured)
            {
                return false;
            }

            return zone.GetComponentInChildren<Reverberation>(true) == null;
        }

        protected override void OnDisable()
        {
            if (_activeCommandZone != null)
            {
                _activeCommandZone.CancelZone();
            }

            _activeCommandZone = null;
            base.OnDisable();
        }

        public void TromboneDefensiveAbility()
        {
            
        }
        public void TromboneSpecialAbility()
        {

        }
        public override void Cancel()
        {
            _tromboneActiveUntil = 0f;
            BlocksMovement = false;
            BlocksDodge = false;
            BlocksAbilities = false;
        }
    }
}
