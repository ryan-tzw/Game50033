namespace Game50033
{
    using UnityEngine;

    /// <summary>Authored main menu settings. The asset lives at Assets/Tuning/UI/MainMenuTuning.asset.</summary>
    [CreateAssetMenu(menuName = "Game50033/Tuning/Main Menu", fileName = "MainMenuTuning")]
    public sealed class MainMenuTuning : ScriptableObject
    {
        [Tooltip("How much one left/right press changes a volume slider (0-1).")]
        [Range(0.01f, 0.5f)] public float volumeStep = 0.1f;
    }
}
