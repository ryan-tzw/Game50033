namespace Game50033
{
    using UnityEngine;

    public enum SceneMusicMode { Keep, Play, Silence }

    [DefaultExecutionOrder(-800)]
    [DisallowMultipleComponent]
    public sealed class SceneMusic : MonoBehaviour
    {
        [SerializeField] private MusicManager managerPrefab;
        [SerializeField] private SceneMusicMode mode = SceneMusicMode.Play;
        [SerializeField] private AudioClip music;

        private void Awake()
        {
            if (MusicManager.Current == null && managerPrefab != null) Instantiate(managerPrefab);
        }
        private void Start() => Apply();
        public void Apply()
        {
            var manager = MusicManager.Current;
            if (manager == null || mode == SceneMusicMode.Keep) return;
            if (mode == SceneMusicMode.Silence) manager.StopMusic();
            else manager.PlayMusic(music);
        }
    }
}
