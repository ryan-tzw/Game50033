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
        // public float startWidth;
        // public float endWidth;
        public float width;
        public Material material;

        private LineRenderer _lr;

        protected override void AttackTowardPoint(Vector3 pointOnCircumference)
        {
            GameObject lineRendererObject = new GameObject("Line Attack");
            _lr = lineRendererObject.AddComponent<LineRenderer>();
            MeshCollider _meshCollider = lineRendererObject.AddComponent<MeshCollider>();
            MeshRenderer _meshRenderer = lineRendererObject.AddComponent<MeshRenderer>();
            MeshFilter _meshFilter = lineRendererObject.AddComponent<MeshFilter>();
            Mesh _mesh = new Mesh();
            lineRendererObject.transform.SetParent(transform);

            _lr.positionCount = pointsOnLine;
            _lr.SetPosition(0, transform.position);
            _lr.SetPosition(pointsOnLine - 1, pointOnCircumference);
            _lr.startWidth = width;
            _lr.endWidth = width;
            _lr.alignment = LineAlignment.TransformZ;
            _lr.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            CalculateSpirals(pointOnCircumference);

            _lr.BakeMesh(_mesh, true);
            _meshCollider.sharedMesh = _mesh;
            _meshFilter.mesh = _mesh;
            _meshRenderer.material = material;
            lineRendererObject.transform.position += new Vector3(0f, 0.01f, 0f);
            lineRendererObject.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            Destroy(_lr);
            
            _meshCollider.convex = true;
            _meshCollider.isTrigger = true;
            
            _matColor = material.color;
            _matColorTranslucent = material.color;
            _matColorTranslucent.a = 0.5f;
        }

        void OnTriggerEnter(Collider other)
        {
            if (rotating && other.TryGetComponent(out Player player)
                && player.Health.TryTakeDamage(1, transform.position - player.transform.position))
                Debug.Log("Player damaged");
        }

        private void CalculateSpirals(Vector3 pointOnCircumference)
        {
            float t;
            for (int i = 1; i < pointsOnLine - 1; i++)
            {
                t = (float)i / pointsOnLine;
                Vector3 point = Vector3.Lerp(transform.position, pointOnCircumference, t);
                float wave = Mathf.Sin(t * Mathf.PI * frequency) * amplitude;
                Vector3 newPoint = point + (new Vector3(1f, 0f, 1f).normalized * wave);

                _lr.SetPosition(i, newPoint);
            }
        }
    }
}
