namespace Game50033
{
    using UnityEngine;

    public abstract class FloatingWeaponVisual : MonoBehaviour
    {
        // motion stays instrument-specific; cancellation is the shared presentation contract.
        public abstract void Cancel();
    }
}
