namespace Game50033
{
    using UnityEngine;

    [CreateAssetMenu(menuName = "Game50033/Resonance/Reprise Effect", fileName = "RepriseEffect")]
    public sealed class RepriseEffect : ReverberationEffect
    {
        [SerializeField, Range(0f, 1f)] private float damageMultiplier = 0.5f;

        internal override void Execute(in ResonanceActivation activation)
        {
            if (damageMultiplier > 0f) activation.Reverberation.OriginalAttack?.Replay(damageMultiplier);
        }
    }
}
