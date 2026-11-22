namespace Game50033
{
    using System;
    using UnityEngine;

    /// <summary>
    /// One selectable character. Each player in the lobby must pick a different one.
    /// </summary>
    [CreateAssetMenu(menuName = "Game50033/Character Definition", fileName = "Character")]
    public sealed class CharacterDefinition : ScriptableObject
    {
        [SerializeField] private string displayName;

        [Tooltip("Used as the in-world character sprite when set.")]
        [SerializeField] private Sprite sprite;

        [Tooltip("Tint applied to the character's sprite (via the _PlayerTint shader property).")]
        [SerializeField] private Color tint = Color.white;

        [Tooltip("Optional playable prefab spawned when this character is confirmed in the Lobby.")]
        [SerializeField] private Player playerPrefab;

        [SerializeField] private Material abilityPalette;
        public Material AbilityPalette => abilityPalette;
        [SerializeField] private Texture2D abilityBackground;
        [SerializeField] private Sprite instrumentIcon;
        public Texture2D AbilityBackground => abilityBackground;
        public Sprite InstrumentIcon => instrumentIcon;

        [Serializable]
        public struct AbilityPreview
        {
            public string name;
            public Texture2D icon;
            [TextArea(2, 5)] public string description;
        }

        [Tooltip("Primary, Secondary, Defensive, Special. Presentation only; does not define gameplay values.")]
        [SerializeField] private AbilityPreview[] abilityPreviews = new AbilityPreview[4];

        public int PreviewRevision { get; private set; }
        private void OnValidate() { PreviewRevision++; }

        public AbilityPreview GetAbilityPreview(PlayerAbility ability) =>
            abilityPreviews != null && (int)ability >= 0 && (int)ability < abilityPreviews.Length
                ? abilityPreviews[(int)ability] : default;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public Sprite Sprite => sprite;
        public Color Tint => tint;
        public Player PlayerPrefab => playerPrefab;
    }
}
