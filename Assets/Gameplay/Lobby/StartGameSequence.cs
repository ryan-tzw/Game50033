namespace Game50033
{
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.InputSystem;
    using UnityEngine.SceneManagement;

    /// <summary>
    /// Lobby -> cutscene handoff: locks joining, freezes the players, keeps them alive across
    /// scene loads (they're needed again in the game scene), then loads the intro cutscene.
    /// </summary>
    public sealed class StartGameSequence : MonoBehaviour
    {
        [SerializeField] private PlayerInputManager playerInputManager;

        [Tooltip("Scene loaded when everyone is ready (the intro cutscene, which then loads the game).")]
        [SerializeField] private string nextSceneName = SceneNames.Cutscene1;

        public bool IsRunning { get; private set; }

        public void Begin()
        {
            if (IsRunning) return;
            IsRunning = true;

            if (playerInputManager) playerInputManager.DisableJoining();
            KeepPlayersAcrossSceneLoads();
            SceneManager.LoadScene(nextSceneName);
        }

        private static void KeepPlayersAcrossSceneLoads()
        {
            // copy: PlayerInput.all can change while we iterate
            foreach (var playerInput in new List<PlayerInput>(PlayerInput.all))
            {
                playerInput.DeactivateInput(); // frozen until GameSceneEntry re-enables it
                if (playerInput.TryGetComponent(out Player player)) player.enabled = false;
                DontDestroyOnLoad(playerInput.transform.root.gameObject);
            }
        }
    }
}
