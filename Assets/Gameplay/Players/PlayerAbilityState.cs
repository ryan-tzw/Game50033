namespace Game50033
{
    using UnityEngine;

    public enum PlayerAbility { Primary, Secondary, Defensive, Special }

    // a read-only view of ability-owned deadlines/resources; presentation never advances these values.
    public readonly struct PlayerAbilityState
    {
        public readonly bool Supported;
        public readonly float Remaining;
        public readonly float Duration;
        public readonly int Charges;
        public readonly int MaxCharges;

        public PlayerAbilityState(float remaining, float duration, int charges = 0, int maxCharges = 0)
        {
            Supported = true;
            Remaining = Mathf.Max(0f, remaining);
            Duration = Mathf.Max(0.001f, duration);
            Charges = Mathf.Max(0, charges);
            MaxCharges = Mathf.Max(0, maxCharges);
        }
    }
}
