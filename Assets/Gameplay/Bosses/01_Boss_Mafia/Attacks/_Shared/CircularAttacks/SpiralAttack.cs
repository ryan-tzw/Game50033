namespace Game50033
{
    using Unity.Collections;
    using Unity.VisualScripting;
    using UnityEngine;
    using UnityEngine.AI;

    public class SpiralAttack : CircularBossAttack
    {
        [Header("Line Attack")]
        public int pointsOnLine = 8;
        public float startWidth;
        public float endWidth;
        // public float width;
        public Material material;
        public float a;
        public float b;
        public int revolutions;

        private LineRenderer _lr;

        protected override void AttackTowardPoint(Vector3 pointOnCircumference)
        {
            GameObject lineRendererObject = new GameObject("Mesh");
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

            CalculateSpirals();

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
            if (_rotating && !_blinking && other.TryGetComponent(out Player player))
            {
                player.Health.TryTakeDamage(1, transform.position - player.transform.position);
                Debug.Log("Player damaged");
            }

        }

        private void CalculateSpirals()
        {
            float theta = 0;
            // float a = 0; // So that the spiral starts from the center
            float k = b;
            float step = revolutions * 2 * Mathf.PI / pointsOnLine;

            for (int i = 1; i < pointsOnLine; i++)
            {
                // Archimedean Spiral Cartesian Coordinates
                // float x = (a + (b * theta)) * Mathf.Cos(theta);
                // float z = (a + (b * theta)) * Mathf.Sin(theta);
                float x = a * Mathf.Exp(k * theta) * Mathf.Cos(theta);
                float z = a * Mathf.Exp(k * theta) * Mathf.Sin(theta);
                Vector3 newPoint = new Vector3(x, transform.position.y, z);
                theta += step;

                _lr.SetPosition(i, newPoint);
            }
        }

        private void CreateCollisions()
        {
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
