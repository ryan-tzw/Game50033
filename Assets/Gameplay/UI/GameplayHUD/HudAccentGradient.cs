namespace Game50033
{
    using UnityEngine;
    using UnityEngine.UIElements;

    internal sealed class HudAccentGradient : VisualElement
    {
        private GameplayHudTuning _tuning;
        private Color _accent;

        public HudAccentGradient()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("hud-symbol");
            generateVisualContent += Paint;
        }

        public void SetAppearance(GameplayHudTuning tuning, Color accent)
        {
            _tuning = tuning;
            _accent = accent;
            MarkDirtyRepaint();
        }

        private void Paint(MeshGenerationContext context)
        {
            if (_tuning == null) return;
            var size = contentRect.size;
            if (size.x <= 0f || size.y <= 0f) return;

            var dark = new Color(0.035f, 0.025f, 0.05f);
            var lightCorner = Color.Lerp(dark, _accent, _tuning.abilityGradientTopLeftStrength);
            var darkCorner = Color.Lerp(dark, _accent, _tuning.abilityGradientBottomRightStrength);
            // the other two corners sit halfway along the diagonal, so both triangles share the same gradient.
            var middle = Color.Lerp(lightCorner, darkCorner, 0.5f);
            var mesh = context.Allocate(4, 6);
            mesh.SetNextVertex(new Vertex { position = new Vector3(0f, 0f, Vertex.nearZ), tint = lightCorner });
            mesh.SetNextVertex(new Vertex { position = new Vector3(size.x, 0f, Vertex.nearZ), tint = middle });
            mesh.SetNextVertex(new Vertex { position = new Vector3(size.x, size.y, Vertex.nearZ), tint = darkCorner });
            mesh.SetNextVertex(new Vertex { position = new Vector3(0f, size.y, Vertex.nearZ), tint = middle });
            mesh.SetNextIndex(0); mesh.SetNextIndex(1); mesh.SetNextIndex(2);
            mesh.SetNextIndex(2); mesh.SetNextIndex(3); mesh.SetNextIndex(0);
        }
    }
}
