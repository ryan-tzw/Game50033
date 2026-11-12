namespace Game50033
{
    using System.Collections;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    /// <summary>
    /// Stand-in for a real cutscene: fades a CanvasGroup (the "Placeholder" text) in, holds,
    /// fades it out, then loads the next scene. Replace the contents of this scene with the
    /// real cutscene (e.g. a Timeline) and keep the load-next-scene step at the end.
    ///
    /// Players carried over from the lobby are still alive in DontDestroyOnLoad with their
    /// input switched off; GameSceneEntry turns it back on in the game scene.
    /// </summary>
    public sealed class PlaceholderCutscene : MonoBehaviour
    {
        [SerializeField] private CanvasGroup content;
        [SerializeField, Min(0f)] private float fadeInSeconds = 1f;
        [SerializeField, Min(0f)] private float holdSeconds = 1.5f;
        [SerializeField, Min(0f)] private float fadeOutSeconds = 1f;
        [SerializeField] private string nextSceneName = SceneNames.Game;

        private IEnumerator Start()
        {
            if (content) content.alpha = 0f;

            yield return Fade(0f, 1f, fadeInSeconds);
            yield return new WaitForSeconds(holdSeconds);
            yield return Fade(1f, 0f, fadeOutSeconds);

            SceneManager.LoadScene(nextSceneName);
        }

        private IEnumerator Fade(float from, float to, float seconds)
        {
            for (var t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (content) content.alpha = Mathf.Lerp(from, to, t / seconds);
                yield return null;
            }

            if (content) content.alpha = to;
        }
    }
}
