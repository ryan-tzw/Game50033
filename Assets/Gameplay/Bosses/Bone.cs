using System;
using UnityEngine;

namespace Game50033
{
    public class Bone : MonoBehaviour
    {
        [SerializeField] private float speed = 8f;          
        [SerializeField] private bool destroyOnArrival = false;


        private Vector3 _start;
        private Vector3 _target;
        private float _duration;
        private float _elapsed;
        private bool _flying;

        public void FlyTo(Vector3 target)
        {
            _start = transform.position;
            _target = target;

            float distance = Vector3.Distance(_start, _target);
            _duration = Mathf.Max(distance / speed, 0.01f);
            _elapsed = 0f;
            _flying = true;
        }

        private void Update()
        {
            if (!_flying) return;

            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);

            Vector3 position = Vector3.Lerp(_start, _target, t);

            transform.position = position;

            if (t >= 1f)
            {
                _flying = false;
                if (destroyOnArrival) Destroy(gameObject);
            }
        }

    }
}
