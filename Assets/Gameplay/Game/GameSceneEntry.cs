namespace Game50033
{
    using UnityEngine;
    using UnityEngine.InputSystem;

    /// <summary>
    /// Placed in the game scene: moves the players carried over from the lobby onto spawn
    /// points and gives them back control.
    /// </summary>
    public sealed class GameSceneEntry : MonoBehaviour
    {
        [SerializeField] private Transform[] spawnPoints = System.Array.Empty<Transform>();

        private void Start()
        {
            if (PlayerInput.all.Count == 0)
            {
                Debug.LogWarning($"No players were carried over. Start from the {SceneNames.Lobby} scene to join and pick characters first.");
                return;
            }

            for (var i = 0; i < PlayerInput.all.Count; i++)
            {
                var playerInput = PlayerInput.all[i];

                if (spawnPoints.Length > 0)
                {
                    var spawn = spawnPoints[i % spawnPoints.Length];
                    var characterController = playerInput.GetComponent<CharacterController>();
                    if (characterController) characterController.enabled = false;
                    playerInput.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
                    if (characterController) characterController.enabled = true;
                }

                playerInput.ActivateInput();
            }
        }
    }
}
