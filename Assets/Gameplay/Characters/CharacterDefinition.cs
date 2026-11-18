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

        [Tooltip("Shown in the character roster/ready header and used as the in-world sprite when set.")]
        [SerializeField] private Sprite sprite;

        [Tooltip("Tint applied to the character's sprite (via the _PlayerTint shader property).")]
        [SerializeField] private Color tint = Color.white;

        [Tooltip("Optional playable prefab spawned when this character is confirmed in the Lobby.")]
        [SerializeField] private Player playerPrefab;

        [Serializable]
        public struct AbilityPreview
        {
            public string name;
            [TextArea(2, 5)] public string description;
        }

        [Tooltip("Primary, Secondary, Defensive, Special. Presentation text only; does not define gameplay values.")]
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
