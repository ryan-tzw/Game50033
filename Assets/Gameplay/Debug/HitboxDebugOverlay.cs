namespace Game50033
{
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.UIElements;

    public sealed class HitboxDebugOverlay : VisualElement
    {
        private readonly DebugState _state;
        private readonly DebugVisualizationTuning _tuning;
        private readonly IReadOnlyList<Player> _players;
        private readonly List<Vector2> _contour = new(192);
        private readonly List<int> _contourEnds = new(128);
        private Vector3[] _worldPoints = new Vector3[8];
        private Vector2[] _projectedPoints = new Vector2[8];
        private Vector2[] _hull = new Vector2[16];

        public HitboxDebugOverlay(DebugState state, DebugVisualizationTuning tuning, IReadOnlyList<Player> players = null)
        {
            _state = state;
            _tuning = tuning;
            _players = players;
            name = "hitbox-debug-overlay";
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = style.right = style.top = style.bottom = 0;
            style.display = DisplayStyle.None;
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext context)
        {
            if (!_state.ShowHitboxes || _tuning == null || panel == null) return;
            var camera = Camera.main;
            if (camera == null || !camera.orthographic) return;

            var painter = context.painter2D;
            painter.lineWidth = Mathf.Max(0f, _tuning.outlineWidth);

            var hurtboxes = CombatHurtbox.Active;
            for (var i = 0; _state.ShowEnemyHurtboxes && i < hurtboxes.Count; i++)
            {
                var hurtbox = hurtboxes[i];
                if (hurtbox == null || !hurtbox.isActiveAndEnabled || hurtbox.GetComponentInParent<Player>() != null) continue;
                if (hurtbox.Target == null || hurtbox.Target is Behaviour target && !target.isActiveAndEnabled) continue;

                hurtbox.GetWorldCapsule(out var worldBottom, out var worldTop, out var worldRadius);
                DrawCapsule(painter, camera, worldBottom, worldTop, worldRadius, _tuning.enemyHurtboxColor);
            }

            var snapshots = _state.AttackSnapshots;
            for (var i = 0; i < snapshots.Count; i++)
            {
                var snapshot = snapshots[i];
                if (!snapshot.Scene.isLoaded || snapshot.LiveFrame >= 0 && snapshot.LiveFrame != Time.frameCount) continue;
                if (snapshot.Shape == DebugAttackSnapshot.Geometry.BossLine)
                {
                    if (_state.ShowBossAttacks) DrawBossLine(painter, camera, snapshot);
                    continue;
                }
                if (snapshot.IsReverberation && !_state.ShowReverberationAreas) continue;
                var attackVisible = snapshot.IsReverberation || _state.ShowPlayerAttacks;
                var attackColor = snapshot.IsReverberation ? _tuning.reverberationAreaColor : _tuning.playerAttackColor;
                if (snapshot.Shape != DebugAttackSnapshot.Geometry.Sector)
                {
                    if (!attackVisible) continue;
                    if (snapshot.Shape == DebugAttackSnapshot.Geometry.Capsule)
                        DrawCapsule(painter, camera, snapshot.Origin, snapshot.Direction, snapshot.OuterRadius,
                            attackColor, dashed: snapshot.LiveFrame < 0);
                    else
                        DrawPrism(painter, camera, snapshot, attackColor);
                    continue;
                }

                var boundary = Mathf.Clamp(snapshot.SweetSpotRadius, snapshot.InnerRadius, snapshot.OuterRadius);
                if (attackVisible)
                {
                    // when both categories are visible, replace the sweet band rather than blending two fills there.
                    var inner = _state.ShowSpecialZones && snapshot.HasSweetSpot && snapshot.SweetSpotInside
                        ? boundary : snapshot.InnerRadius;
                    var outer = _state.ShowSpecialZones && snapshot.HasSweetSpot && !snapshot.SweetSpotInside
                        ? boundary : snapshot.OuterRadius;
                    DrawSector(painter, camera, snapshot, inner, outer, attackColor);
                }
                if (_state.ShowSpecialZones && snapshot.HasSweetSpot)
                {
                    DrawSector(painter, camera, snapshot,
                        snapshot.SweetSpotInside ? snapshot.InnerRadius : boundary,
                        snapshot.SweetSpotInside ? boundary : snapshot.OuterRadius, _tuning.specialZoneColor);
                }
            }

            var traces = _state.ProjectileTraces;
            for (var i = 0; _state.ShowPlayerAttacks && i < traces.Count; i++)
            {
                var trace = traces[i];
                if (!trace.Scene.isLoaded || !trace.IsFinished && !trace.KeepLastStep && trace.Frame != Time.frameCount) continue;
                DrawProjectile(painter, camera, trace);
            }

            if (_state.ShowReverberationAreas)
            {
                var fields = Reverberation.Active;
                for (var i = 0; i < fields.Count; i++)
                {
                    var field = fields[i];
                    if (field != null && field.IsAvailable) DrawReverberation(painter, camera, field);
                }
            }

            var bossColliders = _state.BossColliders;
            for (var i = 0; _state.ShowBossAttacks && i < bossColliders.Count; i++)
            {
                var shape = bossColliders[i];
                if (shape.Owner == null || !shape.Owner.isActiveAndEnabled || shape.Collider == null
                    || !shape.Collider.enabled || !shape.Collider.gameObject.activeInHierarchy) continue;
                DrawBossCollider(painter, camera, shape);
            }

            // draw the small player shapes last so larger attack previews cannot cover their outlines.
            for (var i = 0; _state.ShowPlayerHurtboxes && _players != null && i < _players.Count; i++)
            {
                var player = _players[i];
                if (player == null || !player.isActiveAndEnabled || player.Hurtbox == null || !player.Hurtbox.IsAvailable
                    || player.Health == null || player.Health.IsDowned || player.Health.IsInvulnerable) continue;
                player.Hurtbox.GetWorldCapsule(out var bottom, out var top, out var radius);
                DrawCapsule(painter, camera, bottom, top, radius, _tuning.playerHurtboxColor);
            }
        }

        private void DrawCapsule(Painter2D painter, Camera camera, Vector3 worldBottom, Vector3 worldTop,
            float radiusWorld, Color color, bool dashed = false)
        {
            painter.fillColor = new Color(color.r, color.g, color.b, color.a * Mathf.Clamp01(_tuning.fillOpacity));
            painter.strokeColor = new Color(color.r, color.g, color.b, color.a * Mathf.Clamp01(_tuning.outlineOpacity));
            var bottomDepth = Vector3.Dot(worldBottom - camera.transform.position, camera.transform.forward);
            var topDepth = Vector3.Dot(worldTop - camera.transform.position, camera.transform.forward);
            if (Mathf.Max(bottomDepth, topDepth) + radiusWorld < camera.nearClipPlane
                || Mathf.Min(bottomDepth, topDepth) - radiusWorld > camera.farClipPlane) return;

            var bottom = this.WorldToLocal(RuntimePanelUtils.CameraTransformWorldToPanel(panel, worldBottom, camera));
            var top = this.WorldToLocal(RuntimePanelUtils.CameraTransformWorldToPanel(panel, worldTop, camera));
            var edge = this.WorldToLocal(RuntimePanelUtils.CameraTransformWorldToPanel(panel,
                worldBottom + camera.transform.right * radiusWorld, camera));
            var radius = Vector2.Distance(bottom, edge);
            var bounds = Rect.MinMaxRect(Mathf.Min(bottom.x, top.x) - radius, Mathf.Min(bottom.y, top.y) - radius,
                Mathf.Max(bottom.x, top.x) + radius, Mathf.Max(bottom.y, top.y) + radius);
            if (radius <= 0f || !contentRect.Overlaps(bounds)) return;

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
            if (painter.lineWidth <= 0f) return;
            if (dashed)
            {
                _contour.Clear();
                for (var cap = 0; cap < 2; cap++)
                {
                    var centre = cap == 0 ? top : bottom;
                    for (var i = 0; i <= 24; i++)
                    {
                        var radians = (angle + cap * 180f + i * 7.5f) * Mathf.Deg2Rad;
                        _contour.Add(centre + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius);
                    }
                }
                painter.BeginPath();
                StrokeDashed(painter, 0, _contour.Count, true);
            }
            painter.Stroke();
        }

        private void DrawSector(Painter2D painter, Camera camera, in DebugAttackSnapshot snapshot,
            float innerRadius, float outerRadius, Color color)
        {
            if (outerRadius <= innerRadius || snapshot.Angle <= 0f) return;
            var extent = outerRadius * Mathf.Max(1f, snapshot.Stretch);
            var depth = Vector3.Dot(snapshot.Origin - camera.transform.position, camera.transform.forward);
            if (depth + extent < camera.nearClipPlane || depth - extent > camera.farClipPlane) return;

            // use the same four-degree polygon and world-XZ stretch as CombatHurtbox.TryOverlapSector.
            // two contours preserve the hole in a full circle without drawing a false radial seam.
            var segments = Mathf.Max(2, Mathf.CeilToInt(snapshot.Angle / 4f));
            var fullCircle = snapshot.Angle >= 360f;
            _contour.Clear();
            painter.BeginPath();
            for (var band = 0; band < 2; band++)
            {
                for (var i = 0; i <= segments; i++)
                {
                    var fraction = (float)i / segments;
                    var angle = Mathf.Lerp(-snapshot.Angle * 0.5f, snapshot.Angle * 0.5f,
                        band == 0 ? fraction : 1f - fraction);
                    var radial = Quaternion.AngleAxis(angle, Vector3.up) * snapshot.Direction;
                    radial += snapshot.StretchAxis * (Vector3.Dot(radial, snapshot.StretchAxis) * (snapshot.Stretch - 1f));
                    var world = snapshot.Origin + radial * (band == 0 ? outerRadius : innerRadius);
                    var point = this.WorldToLocal(RuntimePanelUtils.CameraTransformWorldToPanel(panel, world, camera));
                    if (i == 0 && (band == 0 || fullCircle)) painter.MoveTo(point);
                    else painter.LineTo(point);
                    _contour.Add(point);
                }
                if (fullCircle) painter.ClosePath();
            }
            if (!fullCircle) painter.ClosePath();
            painter.fillColor = new Color(color.r, color.g, color.b, color.a * Mathf.Clamp01(_tuning.fillOpacity));
            painter.Fill(FillRule.OddEven);

            if (snapshot.LiveFrame >= 0)
            {
                painter.strokeColor = new Color(color.r, color.g, color.b, color.a * Mathf.Clamp01(_tuning.outlineOpacity));
                if (painter.lineWidth > 0f) painter.Stroke();
                return;
            }

            // dash lengths are measured in panel pixels, independent of range, depth stretch and camera zoom.
            // these are historical impact snapshots; their expiry never extends a damage window.
            if (painter.lineWidth <= 0f) return;
            painter.strokeColor = new Color(color.r, color.g, color.b, color.a * Mathf.Clamp01(_tuning.outlineOpacity));
            painter.BeginPath();
            if (fullCircle)
            {
                StrokeDashed(painter, 0, segments + 1, false);
                StrokeDashed(painter, segments + 1, segments + 1, false);
            }
            else StrokeDashed(painter, 0, _contour.Count, true);
            painter.Stroke();
        }

        private void DrawProjectile(Painter2D painter, Camera camera, DebugProjectileTrace trace)
        {
            var color = _tuning.playerAttackColor;
            painter.fillColor = new Color(color.r, color.g, color.b, color.a * Mathf.Clamp01(_tuning.fillOpacity));
            painter.strokeColor = new Color(color.r, color.g, color.b, color.a * Mathf.Clamp01(_tuning.outlineOpacity));
            _contour.Clear();
            _contourEnds.Clear();
            painter.BeginPath();

            // -----------------------------
            // project the actual swept volumes
            // -----------------------------
            // a translated capsule projects to the convex hull of its four endpoints, expanded by a circle.
            // use the accepted wall fraction so the preview stops with the actual damage checks.
            var sweeps = trace.Sweeps;
            for (var sweepIndex = 0; sweepIndex < sweeps.Count; sweepIndex++)
            {
                var sweep = sweeps[sweepIndex];
                // coincident contours can produce degenerate triangles in the vector tessellator.
                // repeated queries describe the same volume, so emit that volume only once.
                var duplicate = false;
                for (var i = 0; i < sweepIndex; i++)
                {
                    var previous = sweeps[i];
                    if (previous.Radius == sweep.Radius && previous.Motion == sweep.Motion
                        && (previous.Inside == sweep.Inside && previous.Outside == sweep.Outside
                            || previous.Inside == sweep.Outside && previous.Outside == sweep.Inside))
                    {
                        duplicate = true;
                        break;
                    }
                }
                if (duplicate) continue;
                var motion = sweep.Motion * trace.AllowedFraction;
                var minimumDepth = float.PositiveInfinity;
                var maximumDepth = float.NegativeInfinity;
                var minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
                var maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
                for (var i = 0; i < 4; i++)
                {
                    var world = (i % 2 == 0 ? sweep.Inside : sweep.Outside) + (i >= 2 ? motion : Vector3.zero);
                    var depth = Vector3.Dot(world - camera.transform.position, camera.transform.forward);
                    minimumDepth = Mathf.Min(minimumDepth, depth);
                    maximumDepth = Mathf.Max(maximumDepth, depth);
                    var point = this.WorldToLocal(RuntimePanelUtils.CameraTransformWorldToPanel(panel, world, camera));
                    _projectedPoints[i] = point;
                    minimum = Vector2.Min(minimum, point);
                    maximum = Vector2.Max(maximum, point);
                }
                if (maximumDepth + sweep.Radius < camera.nearClipPlane || minimumDepth - sweep.Radius > camera.farClipPlane) continue;
                var edge = this.WorldToLocal(RuntimePanelUtils.CameraTransformWorldToPanel(panel,
                    sweep.Inside + camera.transform.right * sweep.Radius, camera));
                var radius = Vector2.Distance(edge, _projectedPoints[0]);
                if (radius <= 0f || !contentRect.Overlaps(Rect.MinMaxRect(minimum.x - radius, minimum.y - radius,
                    maximum.x + radius, maximum.y + radius))) continue;

                var hullCount = BuildHull(4);

                // round the hull by the query radius, targeting 0.2 panel pixels of arc chord error.
                var first = _contour.Count;
                var arcStep = Mathf.Clamp(2f * Mathf.Acos(Mathf.Clamp(1f - 0.2f / radius, -1f, 1f)),
                    Mathf.Deg2Rad, Mathf.PI * 0.5f);
                for (var i = 0; i < hullCount; i++)
                {
                    var start = 0f;
                    var turn = Mathf.PI * 2f;
                    if (hullCount > 1)
                    {
                        var incoming = (_hull[i] - _hull[(i + hullCount - 1) % hullCount]).normalized;
                        var outgoing = (_hull[(i + 1) % hullCount] - _hull[i]).normalized;
                        start = Mathf.Atan2(-incoming.x, incoming.y);
                        turn = Mathf.Repeat(Mathf.Atan2(-outgoing.x, outgoing.y) - start, Mathf.PI * 2f);
                    }
                    var segments = Mathf.Max(1, Mathf.CeilToInt(turn / arcStep));
                    for (var j = 0; j <= segments; j++)
                    {
                        var angle = start + turn * j / segments;
                        var point = _hull[i] + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                        if (_contour.Count == first) painter.MoveTo(point);
                        else painter.LineTo(point);
                        _contour.Add(point);
                    }
                }
                painter.ClosePath();
                _contourEnds.Add(_contour.Count);
            }

            // one nonzero fill combines overlapping query shapes without stacking their translucent fills.
            // solid live outlines expose the query primitives; the final frame uses the existing snapshot dashes.
            if (_contourEnds.Count == 0) return;
            painter.Fill(FillRule.NonZero);
            if (painter.lineWidth <= 0f) return;
            if (trace.IsFinished)
            {
                painter.BeginPath();
                var start = 0;
                for (var i = 0; i < _contourEnds.Count; i++)
                {
                    StrokeDashed(painter, start, _contourEnds[i] - start, true);
                    start = _contourEnds[i];
                }
            }
            painter.Stroke();
        }

        private void DrawBossLine(Painter2D painter, Camera camera, in DebugAttackSnapshot snapshot)
        {
            if (painter.lineWidth <= 0f) return;
            var start = camera.WorldToViewportPoint(snapshot.Origin);
            var end = camera.WorldToViewportPoint(snapshot.Direction);
            if (Mathf.Max(start.z, end.z) < camera.nearClipPlane || Mathf.Min(start.z, end.z) > camera.farClipPlane) return;

            var color = _tuning.bossAttackColor;
            painter.strokeColor = new Color(color.r, color.g, color.b, color.a * Mathf.Clamp01(_tuning.outlineOpacity));
            _contour.Clear();
            _contour.Add(this.WorldToLocal(RuntimePanelUtils.CameraTransformWorldToPanel(panel, snapshot.Origin, camera)));
            _contour.Add(this.WorldToLocal(RuntimePanelUtils.CameraTransformWorldToPanel(panel, snapshot.Direction, camera)));
            painter.BeginPath();
            StrokeDashed(painter, 0, _contour.Count, false);
            painter.Stroke();
        }

        private void DrawBossCollider(Painter2D painter, Camera camera, in DebugState.BossCollider shape)
        {
            var collider = shape.Collider;
            var volume = collider.transform;
            var color = _tuning.bossAttackColor;
            if (collider is SphereCollider sphere)
            {
                var scale = volume.lossyScale;
                var radius = sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                var centre = volume.TransformPoint(sphere.center);
                DrawCapsule(painter, camera, centre, centre, radius, color);
            }
            else if (collider is BoxCollider box)
            {
                var half = box.size * 0.5f;
                for (var i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? -half.x : half.x, (i & 2) == 0 ? -half.y : half.y,
                        (i & 4) == 0 ? -half.z : half.z);
                    _worldPoints[i] = volume.TransformPoint(box.center + corner);
                }
                DrawProjectedHull(painter, camera, 8, color, dashed: false);
            }
            else if (shape.MeshVertices is { Length: > 0 } vertices)
            {
                if (_worldPoints.Length < vertices.Length)
                {
                    var capacity = Mathf.NextPowerOfTwo(vertices.Length);
                    _worldPoints = new Vector3[capacity];
                    _projectedPoints = new Vector2[capacity];
                    _hull = new Vector2[capacity * 2];
                }
                for (var i = 0; i < vertices.Length; i++) _worldPoints[i] = volume.TransformPoint(vertices[i]);
                DrawProjectedHull(painter, camera, vertices.Length, color, dashed: false);
            }
        }

        private void DrawReverberation(Painter2D painter, Camera camera, Reverberation field)
        {
            var strip = new ReverberationOverlap.FootprintStrip(field);
            var count = strip.SegmentCount;
            if (count == 0) return;

            var origin = field.GroundOrigin;
            var scale = field.FootprintScale;
            var extent = field.FootprintRadius * scale;
            var depth = Vector3.Dot(origin - camera.transform.position, camera.transform.forward);
            if (depth + extent < camera.nearClipPlane || depth - extent > camera.farClipPlane) return;

            strip.GetEdge(0, out var innerStart, out var outerStart);
            strip.GetEdge(count, out _, out var outerEnd);
            var closedLoop = field.FootprintReveal >= 1f && count > 1
                && (outerStart - outerEnd).sqrMagnitude < 0.00000001f;
            var lastEdge = closedLoop ? count - 1 : count;
            var hasHole = innerStart.sqrMagnitude > 0f;

            painter.BeginPath();
            for (var i = 0; i <= lastEdge; i++)
            {
                strip.GetEdge(i, out _, out var outer);
                var point = this.WorldToLocal(RuntimePanelUtils.CameraTransformWorldToPanel(panel, origin + outer * scale, camera));
                if (i == 0) painter.MoveTo(point);
                else painter.LineTo(point);
            }
            if (closedLoop) painter.ClosePath();
            if (hasHole)
            {
                for (var i = lastEdge; i >= 0; i--)
                {
                    strip.GetEdge(i, out var inner, out _);
                    var point = this.WorldToLocal(RuntimePanelUtils.CameraTransformWorldToPanel(panel, origin + inner * scale, camera));
                    if (closedLoop && i == lastEdge) painter.MoveTo(point);
                    else painter.LineTo(point);
                }
                painter.ClosePath();
            }
            else if (!closedLoop)
            {
                painter.LineTo(this.WorldToLocal(RuntimePanelUtils.CameraTransformWorldToPanel(panel, origin, camera)));
                painter.ClosePath();
            }

            var color = _tuning.reverberationAreaColor;
            painter.fillColor = new Color(color.r, color.g, color.b, color.a * Mathf.Clamp01(_tuning.fillOpacity));
            painter.strokeColor = new Color(color.r, color.g, color.b, color.a * Mathf.Clamp01(_tuning.outlineOpacity));
            painter.Fill(FillRule.OddEven);
            if (painter.lineWidth > 0f) painter.Stroke();
        }

        private void DrawPrism(Painter2D painter, Camera camera, in DebugAttackSnapshot snapshot, Color color)
        {
            _worldPoints[0] = snapshot.Origin;
            _worldPoints[1] = snapshot.Direction;
            _worldPoints[2] = snapshot.StretchAxis;
            for (var i = 0; i < 3; i++) _worldPoints[i + 3] = _worldPoints[i] + snapshot.Extrusion;
            DrawProjectedHull(painter, camera, 6, color, dashed: true);
        }

        private void DrawProjectedHull(Painter2D painter, Camera camera, int pointCount, Color color, bool dashed)
        {
            var minimumDepth = float.PositiveInfinity;
            var maximumDepth = float.NegativeInfinity;
            var minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (var i = 0; i < pointCount; i++)
            {
                var world = _worldPoints[i];
                var depth = Vector3.Dot(world - camera.transform.position, camera.transform.forward);
                minimumDepth = Mathf.Min(minimumDepth, depth);
                maximumDepth = Mathf.Max(maximumDepth, depth);
                var point = this.WorldToLocal(RuntimePanelUtils.CameraTransformWorldToPanel(panel, world, camera));
                _projectedPoints[i] = point;
                minimum = Vector2.Min(minimum, point);
                maximum = Vector2.Max(maximum, point);
            }
            if (maximumDepth < camera.nearClipPlane || minimumDepth > camera.farClipPlane
                || !contentRect.Overlaps(Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y))) return;

            var hullCount = BuildHull(pointCount);
            if (hullCount < 3) return;
            painter.fillColor = new Color(color.r, color.g, color.b, color.a * Mathf.Clamp01(_tuning.fillOpacity));
            painter.strokeColor = new Color(color.r, color.g, color.b, color.a * Mathf.Clamp01(_tuning.outlineOpacity));
            painter.BeginPath();
            painter.MoveTo(_hull[0]);
            for (var i = 1; i < hullCount; i++) painter.LineTo(_hull[i]);
            painter.ClosePath();
            painter.Fill();
            if (painter.lineWidth <= 0f) return;
            if (dashed)
            {
                _contour.Clear();
                for (var i = 0; i < hullCount; i++) _contour.Add(_hull[i]);
                painter.BeginPath();
                StrokeDashed(painter, 0, hullCount, true);
            }
            painter.Stroke();
        }

        private int BuildHull(int pointCount)
        {
            // sort projected points and remove duplicates before building a counterclockwise convex hull.
            // collinear motion becomes a capsule; coincident endpoints become a sphere.
            for (var i = 1; i < pointCount; i++)
            {
                var point = _projectedPoints[i];
                var j = i - 1;
                while (j >= 0 && (_projectedPoints[j].x > point.x || _projectedPoints[j].x == point.x && _projectedPoints[j].y > point.y))
                {
                    _projectedPoints[j + 1] = _projectedPoints[j];
                    j--;
                }
                _projectedPoints[j + 1] = point;
            }
            var unique = 0;
            for (var i = 0; i < pointCount; i++)
                if (unique == 0 || (_projectedPoints[i] - _projectedPoints[unique - 1]).sqrMagnitude > 0.00000001f)
                    _projectedPoints[unique++] = _projectedPoints[i];
            var hullCount = 0;
            if (unique == 1) _hull[hullCount++] = _projectedPoints[0];
            else
            {
                for (var pass = 0; pass < 2; pass++)
                {
                    var limit = pass == 0 ? 2 : hullCount + 1;
                    for (var i = pass == 0 ? 0 : unique - 2; pass == 0 ? i < unique : i >= 0; i += pass == 0 ? 1 : -1)
                    {
                        var point = _projectedPoints[i];
                        while (hullCount >= limit && Cross(_hull[hullCount - 2], _hull[hullCount - 1], point) <= 0f) hullCount--;
                        _hull[hullCount++] = point;
                    }
                }
                hullCount--;
            }
            return hullCount;
        }

        private static float Cross(Vector2 a, Vector2 b, Vector2 c)
        {
            return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        }

        private void StrokeDashed(Painter2D painter, int startIndex, int count, bool closed)
        {
            var dash = Mathf.Max(0.5f, _tuning.snapshotDashLength);
            var pattern = dash + Mathf.Max(0.5f, _tuning.snapshotDashGap);
            var travelled = 0f;
            for (var i = 0; i < (closed ? count : count - 1); i++)
            {
                var from = _contour[startIndex + i];
                var to = _contour[startIndex + (i + 1) % count];
                var length = Vector2.Distance(from, to);
                if (length < 0.0001f) continue;
                for (var start = Mathf.Floor(travelled / pattern) * pattern; start < travelled + length; start += pattern)
                {
                    var begin = Mathf.Max(start, travelled);
                    var end = Mathf.Min(start + dash, travelled + length);
                    if (end <= begin) continue;
                    painter.MoveTo(Vector2.Lerp(from, to, (begin - travelled) / length));
                    painter.LineTo(Vector2.Lerp(from, to, (end - travelled) / length));
                }
                travelled += length;
            }
        }
    }
}
