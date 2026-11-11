namespace Game50033
{
    using UnityEngine;

    /// <summary>
    /// One selectable character. Each player in the lobby must pick a different one.
    /// </summary>
    [CreateAssetMenu(menuName = "Game50033/Character Definition", fileName = "Character")]
    public sealed class CharacterDefinition : ScriptableObject
    {
        [SerializeField] private string displayName;

        [Tooltip("Shown on the character select panel and used as the in-world sprite when set.")]
        [SerializeField] private Sprite sprite;

        [Tooltip("Tint applied to the character's sprite (via the _PlayerTint shader property).")]
        [SerializeField] private Color tint = Color.white;

        [Tooltip("Optional playable prefab spawned when this character is confirmed in the Lobby.")]
        [SerializeField] private Player playerPrefab;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public Sprite Sprite => sprite;
        public Color Tint => tint;
        public Player PlayerPrefab => playerPrefab;
    }
}
