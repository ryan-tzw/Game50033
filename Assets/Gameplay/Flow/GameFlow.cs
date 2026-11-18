namespace Game50033
{
    using System;
    using UnityEngine;
    using UnityEngine.Serialization;

    /// <summary>
    /// Authored order of the game: the lobby, then the scenes of one run (cutscenes and encounters)
    /// in the order they are played. Read by each scene's GameManager; holds no runtime state.
    /// Scene names must match the scenes listed in Build Profiles / Build Settings.
    /// </summary>
    [CreateAssetMenu(menuName = "Game50033/Tuning/Game Flow", fileName = "GameFlow")]
    public sealed class GameFlow : ScriptableObject
    {
        [Tooltip("Scene where players join and pick characters. Runs start here and return here.")]
        public string lobbyScene = SceneNames.Lobby;

        [Tooltip("Scenes played in order after the lobby. After the last one the game returns to the lobby.")]
        public string[] runScenes = { SceneNames.Cutscene1, SceneNames.Game };

        public ResultSettings results = new();

        [Serializable]
        public sealed class ResultSettings
        {
            [Tooltip("Defeat: seconds after the fight ends before A / E ready presses count, so a press meant as an attack doesn't skip the results.")]
            [FormerlySerializedAs("readyInputDelay")]
            [Min(0f)] public float defeatReadyDelay = 0.5f;

            [Tooltip("Victory: seconds every player must stand in the ready circle before the next scene loads. Leaving cancels it.")]
            [Min(0f)] public float victoryCountdownSeconds = 3f;
        }

        public bool IsLobby(string sceneName) => sceneName == lobbyScene;

        public string FirstRunScene => runScenes != null && runScenes.Length > 0 ? runScenes[0] : lobbyScene;

        /// <summary>The scene after <paramref name="sceneName"/> in the run; the lobby after the last one or for unknown scenes.</summary>
        public string NextAfter(string sceneName)
        {
            var index = runScenes == null ? -1 : Array.IndexOf(runScenes, sceneName);
            return index >= 0 && index + 1 < runScenes.Length ? runScenes[index + 1] : lobbyScene;
        }
    }
}
