namespace Game50033
{
    using System.Globalization;

    // Authored values for ability inspection. Live timers and charges come from PlayerAbilityState.
    public readonly struct PlayerAbilityStats
    {
        public readonly (string Label, string Value) First;
        public readonly (string Label, string Value) Second;
        public readonly (string Label, string Value) Third;

        public PlayerAbilityStats((string Label, string Value) first,
            (string Label, string Value) second = default, (string Label, string Value) third = default)
        {
            First = first;
            Second = second;
            Third = third;
        }

        public static string Number(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
        public static string Seconds(float value) => Number(value) + " s";
        public static (string Label, string Value) Cooldown(float duration) =>
            duration >= 1f ? ("CD", Seconds(duration)) : default;
        public static string Range(float minimum, float maximum) =>
            minimum == maximum ? Number(minimum) : Number(minimum) + "–" + Number(maximum);
    }
}
