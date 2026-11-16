namespace Game50033
{
    using UnityEngine;

    [CreateAssetMenu(menuName = "Game50033/Tuning/Player Shadows", fileName = "PlayerShadows")]
    public sealed class PlayerShadowTuning : ScriptableObject
    {
        [Tooltip("Additional shadow atlas size. Rounded to a power of two between 256 and 8192. Cascades follow the URP asset.")]
        [Min(256)] public int atlasResolution = 2048;
        [Tooltip("Excluded only from the player shadow map. These objects still cast normal world shadows.")]
        public LayerMask excludedCasterLayers;

        [Header("Weapon Receivers")]
        [Tooltip("Environment-only atlas for weapon receivers. Uses the same cascades as the player atlas.")]
        [Min(256)] public int weaponAtlasResolution = 1024;
        [Tooltip("Character and weapon visual layers excluded only from the weapon receiver map. World shadows are unchanged.")]
        public LayerMask weaponExcludedCasterLayers = (1 << 11) | (1 << 13);
    }
}
