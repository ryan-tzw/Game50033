namespace Game50033
{
    using UnityEngine;

    // Plays sounds for lobby actions.
    [DisallowMultipleComponent]
    public sealed class LobbyAudio : MonoBehaviour
    {
        [SerializeField] private AudioSource source;
        [SerializeField] private AudioClip playerJoined;
        [SerializeField] private AudioClip playerLeft;
        [SerializeField] private AudioClip characterChanged;
        [SerializeField] private AudioClip confirmed;
        [SerializeField] private AudioClip invalid;
        [SerializeField] private AudioClip back;
        [SerializeField] private AudioClip startGame;

        private void Awake()
        {
            if (source == null)
            {
                var session = FindFirstObjectByType<GameSession>();
                if (session != null) source = session.GetComponent<AudioSource>();
            }
            if (source != null) source.ignoreListenerPause = true;
        }
        public void PlayJoin() => Play(playerJoined);
        public void PlayLeave() => Play(playerLeft);
        public void PlayBrowse() => Play(characterChanged);
        public void PlayConfirm() => Play(confirmed);
        public void PlayInvalid() => Play(invalid);
        public void PlayBack() => Play(back);
        public void PlayStartGame() => Play(startGame);
        private void Play(AudioClip clip)
        {
            if (isActiveAndEnabled && source != null && source.isActiveAndEnabled && clip != null) source.PlayOneShot(clip);
        }
    }
}
