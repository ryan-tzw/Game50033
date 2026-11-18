namespace Game50033
{
    using System;
    using UnityEngine;

    [CreateAssetMenu(menuName = "Game50033/Tuning/Lobby", fileName = "LobbyTuning")]
    public sealed class LobbyTuning : ScriptableObject
    {
        public UnreadySettings unready = new();

        [Serializable]
        public sealed class UnreadySettings
        {
            [Tooltip("Seconds a ready player must stay in the unready ring before returning to character select.")]
            [Min(0f)] public float countdownSeconds = 2f;
            [Tooltip("World height above the player's feet where the countdown number is shown.")]
            public float labelHeight = 1.1f;
            public Color labelColor = new(0.45f, 0.9f, 1f);
        }
    }
}
