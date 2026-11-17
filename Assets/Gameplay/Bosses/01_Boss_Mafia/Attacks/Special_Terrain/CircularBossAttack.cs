namespace Game50033
{
    using Unity.Mathematics.Geometry;
    using Unity.VisualScripting;
    using UnityEngine;
    using UnityEngine.Rendering;

    // Actually since this is a phase transition it would make more sense to add it under phase transitions (KIV first)
    public class CircularBossAttack : MonoBehaviour
    {
        [Header("Circle Reference")]
        public int numberOfPoints;
        public float radius;

        public GameObject lingeringEffect;
        private Vector3 _center;

        protected void Awake()
        {
            _center = transform.position;
        }

        private void Start()
        {
            FireAttack();
        }

        // https://www.youtube.com/watch?v=DdAfwHYNFOE
        protected void FireAttack()
        {
            for (int i=0; i<numberOfPoints; i++)
            {
                float circumferenceProgress = (float)i/numberOfPoints;
                float currentRadian = circumferenceProgress * 2 * Mathf.PI;
                
                float xScaled = Mathf.Cos(currentRadian);
                float zScaled = Mathf.Sin(currentRadian);

                float x = xScaled * radius;
                float z = zScaled * radius;

                Instantiate(lingeringEffect, new Vector3(x,_center.y,z), Quaternion.identity);
            }
        }
    }
}
