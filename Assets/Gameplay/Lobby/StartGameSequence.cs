namespace Game50033
{
    using UnityEngine;

    /// <summary>
    /// Lobby -> run handoff: asks the scene's GameManager to start the run. The GameManager locks
    /// joining, freezes the players, keeps them across scene loads and loads the first run scene
    /// (set in the GameFlow asset).
    /// </summary>
    public sealed class StartGameSequence : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;

        public bool IsRunning { get; private set; }

        public void Begin()
        {
            if (IsRunning) return;

            if (gameManager == null)
            {
                Debug.LogError($"{nameof(StartGameSequence)} requires the scene's {nameof(GameManager)}.", this);
                return;
            }

            IsRunning = true;
            gameManager.StartRun();
        }
    }
}
