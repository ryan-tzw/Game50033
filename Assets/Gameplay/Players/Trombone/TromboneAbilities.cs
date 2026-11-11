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
        public GameObject tromboneCommandZone;
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

        public override bool TryUseSecondary() =>
            TryActivate(TromboneAbilitiesType.TromboneSecondary, ref _tromboneSecondaryReadyAt, tromboneSecondaryCooldown, blockMovement: false, blockDodge: false);

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
                    TromboneSecondaryAbility();
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
            GameObject commandZone = Instantiate(tromboneCommandZone, transform.position, transform.rotation, transform);
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
