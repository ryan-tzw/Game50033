namespace Game50033
{
    using System.Collections;
    using UnityEngine;

    // Keeps music playing between scenes
    [DefaultExecutionOrder(-950)]
    [DisallowMultipleComponent]
    public sealed class MusicManager : MonoBehaviour
    {
        [SerializeField] private AudioSource firstSource;
        [SerializeField] private AudioSource secondSource;
        [SerializeField, Min(0f)] private float crossfadeSeconds = 0.5f;
        private static MusicManager _current;
        private AudioSource _target;
        private Coroutine _fade;

        public static MusicManager Current => _current;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic() => _current = null;

        private void Awake()
        {
            if (_current != null && _current != this)
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }
            _current = this;
            // Keep music playing when the scene changes.
            var session = FindFirstObjectByType<GameSession>();
            if (session != null) transform.SetParent(session.transform, false);
            else { transform.SetParent(null); DontDestroyOnLoad(gameObject); }
            if (firstSource != null) firstSource.ignoreListenerPause = true;
            if (secondSource != null) secondSource.ignoreListenerPause = true;
        }

        public void PlayMusic(AudioClip clip)
        {
            if (clip == null) { StopMusic(); return; }
            if (firstSource == null || secondSource == null) return;
            if (_target != null && _target.clip == clip && _target.isPlaying) return;
            var next = firstSource.clip == clip && firstSource.isPlaying ? firstSource
                : secondSource.clip == clip && secondSource.isPlaying ? secondSource
                : firstSource.volume <= secondSource.volume ? firstSource : secondSource;
            if (next.clip != clip || !next.isPlaying)
            {
                next.Stop();
                next.clip = clip;
                next.volume = 0f;
                next.Play();
            }
            _target = next;
            BeginFade();
        }

        public void StopMusic()
        {
            if (_target == null) return;
            _target = null;
            BeginFade();
        }

        private void BeginFade()
        {
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(Fade());
        }

        private IEnumerator Fade()
        {
            var firstVolume = firstSource.volume;
            var secondVolume = secondSource.volume;
            var elapsed = 0f;
            do
            {
                elapsed += Time.unscaledDeltaTime;
                var t = crossfadeSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / crossfadeSeconds);
                firstSource.volume = Mathf.Lerp(firstVolume, _target == firstSource ? 1f : 0f, t);
                secondSource.volume = Mathf.Lerp(secondVolume, _target == secondSource ? 1f : 0f, t);
                if (t >= 1f) break;
                yield return null;
            } while (true);
            if (_target != firstSource) { firstSource.Stop(); firstSource.clip = null; }
            if (_target != secondSource) { secondSource.Stop(); secondSource.clip = null; }
            _fade = null;
        }

        private void OnDestroy() { if (_current == this) _current = null; }
    }
}
