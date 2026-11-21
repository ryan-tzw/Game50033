namespace Game50033
{
    using System.Collections;
    using System.Collections.Generic;
    using System.ComponentModel;
    using Unity.Pipeline.Scripting;
    using UnityEngine;
    using UnityEngine.UIElements;
    using UnityPipeline.Microsoft.CodeAnalysis.CSharp.Syntax;

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
        private float _rotationElapsed;

        private Color _nullColor = new Color(0, 0, 0, 0);
        protected Color _matColor;
        protected Color _matColorTranslucent;
        protected bool _blinking;
        protected bool _alreadyBlinked;
        private Quaternion _randomStartRotation;

        protected bool _lingering;
        protected GameObject lingerHolder;
        private float lingeringLifeTime;
        private bool _finishedAttack;

        public override void Setup(BaseBoss owner)
        {
            base.Setup(owner);
            _elapsed = 0f;
            _rotating = false;
            _blinking = false;
            _alreadyBlinked = false;
            _matColor = _nullColor;
            _matColorTranslucent = _nullColor;
            _randomStartRotation = Quaternion.Euler(0f, 60 * Random.Range(0, 6), 0f);
            transform.position = new Vector3(0f, 0f, 0f);

            if (windUpTime < 1.0f) windUpTime = 1.0f;
            _lingering = lingeringEffect && lingeringObject;
            _finishedAttack = false;
        }

        protected virtual void Update()
        {
            _elapsed += Time.deltaTime;
            if (_blinking && !_alreadyBlinked)
            {
                if (bossWindUpAnimationId != 0 & windUpTime > 0f) boss.PlayAnimationTrigger(bossWindUpAnimationId);
                foreach (MeshRenderer child in GetComponentsInChildren<MeshRenderer>())
                {
                    StartCoroutine(BlinkWarning(child));
                }
                _blinking = false;
            }
            if (_elapsed > (windUpTime + activeTime))
            {
                _finishedAttack = true;
                Destroy(gameObject);
            }
            if (_alreadyBlinked && _rotating)
            {
                if (bossAttackAnimationId != 0) boss.PlayAnimationTrigger(bossAttackAnimationId);
                if (_lingering)
                {
                    lingerHolder.SetActive(true);
                    lingerHolder.transform.SetParent(null);
                }
                _rotationElapsed += Time.deltaTime;
                RotateAttack();
            }
        }

        protected override IEnumerator FireAfterWindUp(Player player)
        {
            boss.GetComponentInChildren<Animator>().SetBool("FinishedAttack", _finishedAttack);
            if (lingeringEffect && lingeringObject) lingeringLifeTime = lingeringObject.GetComponent<LingeringEffect>().lifetime;
            yield return new WaitUntil(() => boss.Movement.MoveToCenter() && boss.Movement.canMove == false);
            // transform.SetParent(boss.transform);
            // yield return new WaitUntil(() => (boss.transform.position.x < 0.05f) && (boss.transform.position.z < 0.1f));
            if (_lingering)
            {
                lingerHolder = new GameObject("Linger Holder");
                lingerHolder.SetActive(false);
                BoxCollider _col = lingerHolder.AddComponent<BoxCollider>();
                LingeringEffect _le = lingerHolder.AddComponent<LingeringEffect>();
                Rigidbody _rb = lingerHolder.GetComponent<Rigidbody>();
                _rb.constraints = RigidbodyConstraints.FreezePosition | RigidbodyConstraints.FreezeRotation;
                _col.size = new Vector3(0, 0, 0);
                _le.lifetime = lingeringLifeTime;
                _le.persistOnContact = true;
                lingerHolder.transform.SetParent(transform);
            }

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
                yield return new WaitForSeconds(windUpTime * 0.15f);
                child.material.color = _matColorTranslucent;
                yield return new WaitForSeconds(windUpTime * 0.15f);
                child.material.color = _matColor;
            }
            yield return new WaitForSeconds(windUpTime * 0.1f);
            _alreadyBlinked = true;
        }

        protected virtual void RotateAttack()
        {
            transform.rotation *= Quaternion.Euler(0f, _rotateAmount * Time.deltaTime, 0f);
            if (_lingering && _rotationElapsed > lingeringLifeTime * 0.2f)
            {
                _rotationElapsed = 0f;
                GameObject nextLinger = Instantiate(lingerHolder);
                nextLinger.transform.rotation = transform.rotation;
                nextLinger.GetComponent<LingeringEffect>().lifetime = lingeringLifeTime;
                lingerHolder = nextLinger;
            }
        }

        protected virtual void AttackTowardPoint(Vector3 pointOnCircumference) { }
    }
}
