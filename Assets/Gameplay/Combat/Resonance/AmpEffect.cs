namespace Game50033
{
    using UnityEngine;

    [CreateAssetMenu(menuName = "Game50033/Resonance/Amp Effect", fileName = "AmpEffect")]
    public sealed class AmpEffect : ReverberationEffect
    {
        [SerializeField, Min(1f)] private float damageMultiplier = 1.5f;

        internal override void Execute(in ResonanceActivation activation)
        {
            activation.Attack.ApplyDamageMultiplier(damageMultiplier);
        }
    }
}
