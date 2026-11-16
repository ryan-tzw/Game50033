namespace Game50033
{
    using UnityEngine;
    using UnityEngine.UIElements;

    public sealed class HitboxDebugOverlay : VisualElement
    {
        private readonly DebugState _state;
        private readonly DebugVisualizationTuning _tuning;

        public HitboxDebugOverlay(DebugState state, DebugVisualizationTuning tuning)
        {
            _state = state;
            _tuning = tuning;
            name = "hitbox-debug-overlay";
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = style.right = style.top = style.bottom = 0;
            style.display = DisplayStyle.None;
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext context)
        {
            if (!_state.ShowHitboxes || !_state.ShowEnemyHurtboxes || _tuning == null || panel == null) return;
            var camera = Camera.main;
            if (camera == null || !camera.orthographic) return;

            var painter = context.painter2D;
            var color = _tuning.enemyHurtboxColor;
            painter.fillColor = new Color(color.r, color.g, color.b, color.a * Mathf.Clamp01(_tuning.fillOpacity));
            painter.strokeColor = new Color(color.r, color.g, color.b, color.a * Mathf.Clamp01(_tuning.outlineOpacity));
            painter.lineWidth = Mathf.Max(0f, _tuning.outlineWidth);

            var hurtboxes = CombatHurtbox.Active;
            for (var i = 0; i < hurtboxes.Count; i++)
            {
                var hurtbox = hurtboxes[i];
                if (hurtbox == null || !hurtbox.isActiveAndEnabled || hurtbox.GetComponentInParent<Player>() != null) continue;
                if (hurtbox.Target == null || hurtbox.Target is Behaviour target && !target.isActiveAndEnabled) continue;

                // use the query's world capsule, including its authored anchor, rotation and nonuniform scale.
                // orthographic projection preserves the round cross-section; only the capsule axis foreshortens.
                hurtbox.GetWorldCapsule(out var worldBottom, out var worldTop, out var worldRadius);
                var bottomDepth = Vector3.Dot(worldBottom - camera.transform.position, camera.transform.forward);
                var topDepth = Vector3.Dot(worldTop - camera.transform.position, camera.transform.forward);
                if (Mathf.Max(bottomDepth, topDepth) + worldRadius < camera.nearClipPlane
                    || Mathf.Min(bottomDepth, topDepth) - worldRadius > camera.farClipPlane) continue;

                var bottom = this.WorldToLocal(RuntimePanelUtils.CameraTransformWorldToPanel(panel, worldBottom, camera));
                var top = this.WorldToLocal(RuntimePanelUtils.CameraTransformWorldToPanel(panel, worldTop, camera));
                var edge = this.WorldToLocal(RuntimePanelUtils.CameraTransformWorldToPanel(panel,
                    worldBottom + camera.transform.right * worldRadius, camera));
                var radius = Vector2.Distance(bottom, edge);
                var bounds = Rect.MinMaxRect(Mathf.Min(bottom.x, top.x) - radius, Mathf.Min(bottom.y, top.y) - radius,
                    Mathf.Max(bottom.x, top.x) + radius, Mathf.Max(bottom.y, top.y) + radius);
                if (radius <= 0f || !contentRect.Overlaps(bounds)) continue;

                var axis = top - bottom;
                axis = axis.sqrMagnitude > 0.000001f ? axis.normalized : Vector2.up;
                var perpendicular = new Vector2(axis.y, -axis.x);
                var angle = Mathf.Atan2(perpendicular.y, perpendicular.x) * Mathf.Rad2Deg;
                painter.BeginPath();
                painter.MoveTo(top + perpendicular * radius);
                painter.Arc(top, radius, angle, angle + 180f);
                painter.LineTo(bottom - perpendicular * radius);
                painter.Arc(bottom, radius, angle + 180f, angle + 360f);
                painter.ClosePath();
                painter.Fill();
                if (painter.lineWidth > 0f) painter.Stroke();
            }
        }
    }
}
