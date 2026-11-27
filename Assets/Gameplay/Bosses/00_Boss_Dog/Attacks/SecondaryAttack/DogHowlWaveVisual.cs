using UnityEngine;

namespace Game50033
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator), typeof(SpriteRenderer))]
    public sealed class DogHowlWaveVisual : MonoBehaviour
    {
        [SerializeField] private AnimationClip animationClip;

        private void Start()
        {
            // The howl's short damage window must not cut off its sound-wave animation.
            transform.SetParent(null, true);
            Destroy(gameObject, animationClip.length);
        }
    }
}
