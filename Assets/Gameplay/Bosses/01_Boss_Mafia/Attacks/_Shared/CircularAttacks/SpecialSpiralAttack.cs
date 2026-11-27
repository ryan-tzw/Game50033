namespace Game50033
{
    using Unity.VisualScripting;
    using UnityEngine;
    public class SpecialSpiralAttack : CircularBossAttack
    {
        [Header("Line Attack")]
        public int pointsOnLine = 8;
        public float amplitude = 1.5f;
        public float frequency = 2.0f;
        public float startWidth;
        public float endWidth;
        [Min(90)] public float waveAngle;
        public Material material;

        private LineRenderer _lr;

        protected override void AttackTowardPoint(Vector3 pointOnCircumference)
        {
            GameObject lineRendererObject = new GameObject("Line Attack");
            _lr = lineRendererObject.AddComponent<LineRenderer>();
            MeshRenderer _meshRenderer = lineRendererObject.AddComponent<MeshRenderer>();
            MeshFilter _meshFilter = lineRendererObject.AddComponent<MeshFilter>();
            Mesh _mesh = new Mesh();
            lineRendererObject.transform.SetParent(transform);

            _lr.positionCount = pointsOnLine;
            _lr.SetPosition(0, transform.position);
            _lr.SetPosition(pointsOnLine - 1, pointOnCircumference);
            _lr.startWidth = startWidth;
            _lr.endWidth = endWidth;
            _lr.alignment = LineAlignment.TransformZ;
            _lr.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            CalculateSpirals(pointOnCircumference);
            _lr.Simplify(0.05f);
            CreateCollisions();

            _lr.BakeMesh(_mesh, true);

            _meshFilter.mesh = _mesh;
            _meshRenderer.material = material;
            lineRendererObject.transform.position += new Vector3(0f, 0.01f, 0f);
            lineRendererObject.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            Destroy(_lr);

            _matColor = material.color;
            _matColorTranslucent = material.color;
            _matColorTranslucent.a = 0.5f;
        }

        void OnTriggerEnter(Collider other)
        {
            if (_rotating && _alreadyBlinked && other.TryGetComponent(out Player player))
            {
                player.Health.TryTakeDamage(1, transform.position - player.transform.position);
                Debug.Log("Player damaged");
            }
        }

        private void CalculateSpirals(Vector3 pointOnCircumference)
        {
            GameObject linger = new GameObject("Linger Line");
            float t;
            Vector3 direction = (pointOnCircumference - transform.position).normalized;
            Vector3 offsetDir = Quaternion.AngleAxis(waveAngle, Vector3.up) * direction;

            if (_lingering) linger.transform.SetParent(lingerHolder.transform);
            else Destroy(linger);

            for (int i = 1; i < pointsOnLine - 1; i++)
            {
                t = (float)i / (pointsOnLine - 1);
                Vector3 point = Vector3.Lerp(transform.position, pointOnCircumference, t);
                float wave = Mathf.Sin(t * Mathf.PI * frequency) * amplitude;
                float x = offsetDir.x * wave;
                float z = offsetDir.z * wave;
                Vector3 newPoint = new Vector3(point.x + x, point.y, point.z + z);

                _lr.SetPosition(i, newPoint);
                if (_lingering)
                {
                    GameObject lingerObject = Instantiate(lingeringObject, newPoint, Quaternion.identity);
                    lingerObject.transform.SetParent(linger.transform);
                }
            }
        }

        private void CreateCollisions()
        {
            var debug = _rotating ? FindAnyObjectByType<GameSession>()?.Debug : null;
            float cumulativeDistance = 0f;
            float[] distances = new float[_lr.positionCount];
            float totalDistance = 0f;
            for (int i = 0; i < _lr.positionCount - 1; i++)
            {
                float distance = Vector3.Distance(_lr.GetPosition(i), _lr.GetPosition(i + 1));
                distances[i] = distance;
                totalDistance += distance;
            }

            for (int i = 0; i < _lr.positionCount - 1; i++)
            {
                Vector3 startPoint = _lr.GetPosition(i);
                Vector3 endPoint = _lr.GetPosition(i + 1);
                Vector3 direction = endPoint - startPoint;
                Vector3 normalizedDirection = direction.normalized;
                normalizedDirection.y = 0f;
                float distance = distances[i];

                GameObject boxColObject = InstantiateBoxCollider();
                BoxCollider boxCollider = boxColObject.GetComponent<BoxCollider>();

                float excess = 0f;
                float t;
                float xSize;
                Vector3 worldPos;
                Vector3 sectionedCenter = new Vector3();

                if (distance > 1.0f)
                {
                    int count = Mathf.FloorToInt(distance);
                    excess = distance - count;
                    sectionedCenter = startPoint + normalizedDirection / 2;
                    for (int j = 0; j < count; j++)
                    {
                        GameObject sectionedBox = InstantiateBoxCollider();
                        BoxCollider sectionedBoxCol = sectionedBox.GetComponent<BoxCollider>();
                        if (j > 0) sectionedCenter += normalizedDirection;
                        cumulativeDistance += 1.0f;
                        t = cumulativeDistance / totalDistance;
                        xSize = _lr.widthCurve.Evaluate(t) * _lr.widthMultiplier * 0.9f;
                        sectionedBoxCol.size = new Vector3(xSize, 1, 1.0f);
                        sectionedBox.transform.rotation = Quaternion.LookRotation(normalizedDirection);
                        worldPos = new Vector3(sectionedCenter.x, 0f, sectionedCenter.z);
                        sectionedBox.transform.position = worldPos;
                        sectionedBox.transform.SetParent(transform, true);
                        debug?.RegisterBossCollider(this, sectionedBoxCol);
                    }
                }

                if (excess > 0f && excess < 0.2f)
                {
                    Destroy(boxColObject);
                    continue;
                }

                Vector3 centerPoint = (distance > 1.0f) ? sectionedCenter + normalizedDirection * (1 - excess) : (startPoint + direction / 2);

                cumulativeDistance += (excess > 0f) ? excess : distance;
                t = cumulativeDistance / totalDistance;
                xSize = _lr.widthCurve.Evaluate(t) * _lr.widthMultiplier * 0.9f;
                float zSize = (excess > 0f) && (distance > 1.0f) ? excess : distance;
                boxCollider.size = new Vector3(xSize, 1, zSize);
                boxColObject.transform.rotation = Quaternion.LookRotation(normalizedDirection);

                worldPos = new Vector3(centerPoint.x, 0f, centerPoint.z);
                boxColObject.transform.position = worldPos;
                boxColObject.transform.SetParent(transform, true);
                debug?.RegisterBossCollider(this, boxCollider);
            }
        }

        private GameObject InstantiateBoxCollider()
        {
            GameObject boxColObject = new GameObject("Box Collider");
            BoxCollider boxCollider = boxColObject.AddComponent<BoxCollider>();
            boxCollider.isTrigger = true;
            return boxColObject;
        }
    }
}
