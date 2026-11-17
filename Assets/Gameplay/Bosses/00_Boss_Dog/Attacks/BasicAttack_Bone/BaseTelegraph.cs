using UnityEngine;

namespace Game50033
{
    public abstract class BaseTelegraph : MonoBehaviour
    {
        public abstract void Begin(Vector3 from, Vector3 to, float duration);
    }
}
