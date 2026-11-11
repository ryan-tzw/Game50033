namespace Game50033
{
    using UnityEngine;

    public abstract class ReverberationEffect : ScriptableObject
    {
        internal abstract void Execute(in ResonanceActivation activation);
    }
}
