namespace Game50033
{
    using UnityEngine;

    [CreateAssetMenu(menuName = "Game50033/Resonance/Encore Effect", fileName = "EncoreEffect")]
    public sealed class EncoreEffect : ReverberationEffect
    {
        [SerializeField, Range(0f, 1f)] private float damageMultiplier = 0.5f;

        internal override void Execute(in ResonanceActivation activation)
        {
            if (damageMultiplier > 0f)
                activation.Attack.CaptureReplay(activation.AttackContact)?.Replay(damageMultiplier);
        }
    }
}
