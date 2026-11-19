namespace Game50033
{
    using System.Collections;
    using System.ComponentModel;
    using Unity.Pipeline.Scripting;
    using UnityEngine;
    using UnityEngine.UIElements;

    // Actually since this is a phase transition it would make more sense to add it under phase transitions (KIV first)
    public class CircularBossAttack : TargetedBossAttack
    {
        [Header("Circular Attack Reference")]
        public int numberOfPoints;
        public float radius;
        public bool rotating;
        protected bool _rotating;
        public float rotationSpeed;
        private float _rotateAmount;

        protected float _elapsed;

        private Color _nullColor = new Color(0, 0, 0, 0);
        protected Color _matColor;
        protected Color _matColorTranslucent;
        protected bool _blinking;
        private Quaternion _randomStartRotation;

        public override void Setup(BaseBoss owner)
        {
            base.Setup(owner);
            // _attackHolder = new GameObject("Attack Holder");
            // _attackHolder.transform.SetParent(transform);
            _elapsed = 0f;
            _rotating = false;
            _blinking = false;
            _matColor = _nullColor;
            _matColorTranslucent = _nullColor;
            _randomStartRotation = Quaternion.Euler(0f, 60 * Random.Range(0, 6), 0f);
            transform.position = new Vector3(0f, 0f, 0f);
        }

        protected virtual void Update()
        {
            _elapsed += Time.deltaTime;
            if (_blinking)
            {
                foreach (MeshRenderer child in GetComponentsInChildren<MeshRenderer>())
                {
                    StartCoroutine(BlinkWarning(child));
                }
                _blinking = false;
            }
            if (_elapsed > (windUpTime + activeTime)) Destroy(gameObject);
            if ((_elapsed > windUpTime) && _rotating) RotateAttack();
        }

        protected override IEnumerator FireAfterWindUp(Player player)
        {
            yield return new WaitUntil(() => boss.Movement.MoveToCenter() && boss.Movement.canMove == false);
            // transform.SetParent(boss.transform);
            // yield return new WaitUntil(() => (boss.transform.position.x < 0.05f) && (boss.transform.position.z < 0.1f));

            // Code Referenced From: https://www.youtube.com/watch?v=DdAfwHYNFOE
            for (int i = 0; i < numberOfPoints; i++)
            {
                float circumferenceProgress = (float)i / numberOfPoints;
                float currentRadian = circumferenceProgress * 2 * Mathf.PI;

                float xScaled = Mathf.Cos(currentRadian);
                float zScaled = Mathf.Sin(currentRadian);

                float x = xScaled * radius;
                float z = zScaled * radius;

                AttackTowardPoint(new Vector3(x, boss.transform.position.y, z));
            }
            transform.rotation *= _randomStartRotation;

            if (_matColor != _nullColor)
            {
                _blinking = true;
            }

            if (rotating)
            {
                _rotateAmount = activeTime * rotationSpeed;
                _rotating = true;
            }
        }

        IEnumerator BlinkWarning(MeshRenderer child)
        {
            for (int i = 0; i < 3; i++)
            {
                yield return new WaitForSeconds(0.15f);
                child.material.color = _matColorTranslucent;
                yield return new WaitForSeconds(0.15f);
                child.material.color = _matColor;
            }
        }

        protected virtual void RotateAttack()
        {
            transform.rotation *= Quaternion.Euler(0f, _rotateAmount * Time.deltaTime, 0f);
        }

        protected virtual void AttackTowardPoint(Vector3 pointOnCircumference) { }
    }
}
