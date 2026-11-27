namespace Game50033
{
    using UnityEngine;

    [CreateAssetMenu(menuName = "Game50033/Tuning/Boss Attack Audio", fileName = "BossAttackAudio")]
    public sealed class BossAttackAudioTuning : ScriptableObject
    {
        public AudioClip windUpSound;
        public AudioClip releaseSound;

        [Range(0f, 1f)] public float volume = 1f;
    }
}
