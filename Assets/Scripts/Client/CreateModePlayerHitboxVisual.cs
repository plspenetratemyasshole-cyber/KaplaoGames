using System.Collections.Generic;
using KaplaoGames.Network;
using KaplaoGames.Server;
using KaplaoGames.Shared;
using Mirror;
using UnityEngine;

namespace KaplaoGames.Client
{
    /// <summary>
    /// Draws runner collider outlines for local Kaplao in Create mode.
    /// Optional Hitbox Trail records a snapshot each frame only while the setting is on.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkPlayer))]
    public class CreateModePlayerHitboxVisual : MonoBehaviour
    {
        static readonly Color LiveColor = new(0.15f, 0.95f, 0.3f, 0.95f);
        static readonly Color KillLiveColor = new(0.25f, 0.6f, 1f, 0.95f);
        static readonly Color TrailColor = new(0.15f, 0.95f, 0.3f, 0.35f);
        const int MaxTrailSamples = 48;

        Collider2D bodyCollider;
        Collider2D killCollider;
        LineRenderer liveOutline;
        LineRenderer killLiveOutline;
        Transform trailRoot;
        readonly List<LineRenderer> trailLines = new();
        int trailWriteIndex;
        bool lastTrailEnabled;
        Material sharedLineMaterial;

        void Awake()
        {
            ResolveColliders();
            EnsureLiveOutlines();
        }

        void OnEnable()
        {
            CreateModeEditorSettings.Changed += OnSettingsChanged;
            RunnerHitboxCheat.Changed += OnSettingsChanged;
            lastTrailEnabled = CreateModeEditorSettings.HitboxTrail;
            RefreshVisibility();
        }

        void OnDisable()
        {
            CreateModeEditorSettings.Changed -= OnSettingsChanged;
            RunnerHitboxCheat.Changed -= OnSettingsChanged;
            ClearTrail();
            if (liveOutline != null)
                liveOutline.enabled = false;
            if (killLiveOutline != null)
                killLiveOutline.enabled = false;
        }

        void LateUpdate()
        {
            bool showLive = ShouldShowLiveHitboxes();
            bool kaplaoCreate = IsLocalKaplaoCreateMode();

            if (!showLive && !kaplaoCreate)
            {
                if (liveOutline != null)
                    liveOutline.enabled = false;
                if (killLiveOutline != null)
                    killLiveOutline.enabled = false;
                return;
            }

            if (showLive)
                UpdateLiveOutlines();
            else
            {
                if (liveOutline != null)
                    liveOutline.enabled = false;
                if (killLiveOutline != null)
                    killLiveOutline.enabled = false;
            }

            // Trail is Kaplao Create-mode only — never for the runner Hitbox cheat.
            bool trailEnabled = kaplaoCreate && CreateModeEditorSettings.HitboxTrail;
            if (trailEnabled != lastTrailEnabled)
            {
                if (!trailEnabled)
                    ClearTrail();
                lastTrailEnabled = trailEnabled;
            }

            if (trailEnabled)
                RecordTrailSample();
        }

        void OnSettingsChanged()
        {
            bool trailEnabled = IsLocalKaplaoCreateMode() && CreateModeEditorSettings.HitboxTrail;
            if (!trailEnabled && lastTrailEnabled)
                ClearTrail();
            lastTrailEnabled = trailEnabled;
            RefreshVisibility();
        }

        void RefreshVisibility()
        {
            if (!ShouldShowLiveHitboxes())
            {
                if (liveOutline != null)
                    liveOutline.enabled = false;
                if (killLiveOutline != null)
                    killLiveOutline.enabled = false;
            }

            bool showTrail = IsLocalKaplaoCreateMode() && CreateModeEditorSettings.HitboxTrail;
            for (int i = 0; i < trailLines.Count; i++)
            {
                if (trailLines[i] != null)
                    trailLines[i].enabled = showTrail;
            }
        }

        void UpdateLiveOutlines()
        {
            EnsureLiveOutlines();
            if (liveOutline == null)
                return;

            if (!TryBuildWorldOutline(bodyCollider, out Vector3[] points))
            {
                liveOutline.enabled = false;
            }
            else
            {
                liveOutline.enabled = true;
                liveOutline.startColor = LiveColor;
                liveOutline.endColor = LiveColor;
                liveOutline.positionCount = points.Length;
                liveOutline.SetPositions(points);
            }

            if (killLiveOutline == null)
                return;

            if (!TryBuildWorldOutline(killCollider, out Vector3[] killPoints))
            {
                killLiveOutline.enabled = false;
                return;
            }

            killLiveOutline.enabled = true;
            killLiveOutline.startColor = KillLiveColor;
            killLiveOutline.endColor = KillLiveColor;
            killLiveOutline.positionCount = killPoints.Length;
            killLiveOutline.SetPositions(killPoints);
        }

        void RecordTrailSample()
        {
            if (!TryBuildWorldOutline(bodyCollider, out Vector3[] points))
                return;

            var sample = new Vector3[points.Length];
            System.Array.Copy(points, sample, points.Length);

            LineRenderer line;
            if (trailLines.Count < MaxTrailSamples)
            {
                line = CreateTrailLineRenderer();
                trailLines.Add(line);
                trailWriteIndex = trailLines.Count % MaxTrailSamples;
            }
            else
            {
                line = trailLines[trailWriteIndex];
                trailWriteIndex = (trailWriteIndex + 1) % MaxTrailSamples;
            }

            ApplySampleToLine(line, sample);
        }

        void ClearTrail()
        {
            for (int i = 0; i < trailLines.Count; i++)
            {
                if (trailLines[i] != null)
                    Destroy(trailLines[i].gameObject);
            }

            trailLines.Clear();
            trailWriteIndex = 0;
        }

        void ApplySampleToLine(LineRenderer line, Vector3[] points)
        {
            if (line == null || points == null)
                return;

            line.enabled = true;
            line.startColor = TrailColor;
            line.endColor = TrailColor;
            line.positionCount = points.Length;
            line.SetPositions(points);
        }

        bool TryBuildWorldOutline(Collider2D sourceCollider, out Vector3[] worldPoints)
        {
            worldPoints = null;
            if (sourceCollider == null || !sourceCollider.enabled)
                return false;

            if (sourceCollider is BoxCollider2D box)
            {
                Vector2 half = box.size * 0.5f;
                Vector2 offset = box.offset;
                Transform t = box.transform;
                worldPoints = new[]
                {
                    t.TransformPoint(offset.x - half.x, offset.y - half.y, 0f),
                    t.TransformPoint(offset.x - half.x, offset.y + half.y, 0f),
                    t.TransformPoint(offset.x + half.x, offset.y + half.y, 0f),
                    t.TransformPoint(offset.x + half.x, offset.y - half.y, 0f),
                    t.TransformPoint(offset.x - half.x, offset.y - half.y, 0f)
                };
                return true;
            }

            if (sourceCollider is CapsuleCollider2D capsule)
            {
                worldPoints = BuildCapsuleOutline(capsule);
                return worldPoints != null && worldPoints.Length >= 3;
            }

            if (sourceCollider is PolygonCollider2D polygon && polygon.pathCount > 0)
            {
                Vector2[] path = polygon.GetPath(0);
                if (path == null || path.Length == 0)
                    return false;

                Vector2 offset = polygon.offset;
                Transform t = polygon.transform;
                worldPoints = new Vector3[path.Length + 1];
                for (int i = 0; i < path.Length; i++)
                    worldPoints[i] = t.TransformPoint(path[i] + offset);
                worldPoints[path.Length] = worldPoints[0];
                return true;
            }

            Bounds bounds = sourceCollider.bounds;
            worldPoints = new[]
            {
                new Vector3(bounds.min.x, bounds.min.y, 0f),
                new Vector3(bounds.min.x, bounds.max.y, 0f),
                new Vector3(bounds.max.x, bounds.max.y, 0f),
                new Vector3(bounds.max.x, bounds.min.y, 0f),
                new Vector3(bounds.min.x, bounds.min.y, 0f)
            };
            return true;
        }

        static Vector3[] BuildCapsuleOutline(CapsuleCollider2D capsule)
        {
            if (capsule == null)
                return null;

            const int arcSegments = 16;
            Vector2 size = capsule.size;
            Vector2 offset = capsule.offset;
            Transform t = capsule.transform;
            bool vertical = capsule.direction == CapsuleDirection2D.Vertical;

            float diameter = vertical ? Mathf.Abs(size.x) : Mathf.Abs(size.y);
            float length = vertical ? Mathf.Abs(size.y) : Mathf.Abs(size.x);
            float radius = Mathf.Max(0.0001f, diameter * 0.5f);
            float straight = Mathf.Max(0f, length - diameter);
            float halfStraight = straight * 0.5f;

            // Two inclusive arcs + closing point back to start.
            int pointCount = (arcSegments + 1) * 2 + 1;
            var points = new Vector3[pointCount];
            int index = 0;

            if (vertical)
            {
                Vector2 topCenter = offset + new Vector2(0f, halfStraight);
                Vector2 bottomCenter = offset - new Vector2(0f, halfStraight);

                // Top semicircle: right → left through the top.
                for (int i = 0; i <= arcSegments; i++)
                {
                    float angle = Mathf.PI * i / arcSegments;
                    points[index++] = t.TransformPoint(
                        topCenter.x + Mathf.Cos(angle) * radius,
                        topCenter.y + Mathf.Sin(angle) * radius,
                        0f);
                }

                // Bottom semicircle: left → right through the bottom.
                // Side edges are formed by connecting the arc endpoints.
                for (int i = 0; i <= arcSegments; i++)
                {
                    float angle = Mathf.PI + Mathf.PI * i / arcSegments;
                    points[index++] = t.TransformPoint(
                        bottomCenter.x + Mathf.Cos(angle) * radius,
                        bottomCenter.y + Mathf.Sin(angle) * radius,
                        0f);
                }
            }
            else
            {
                Vector2 rightCenter = offset + new Vector2(halfStraight, 0f);
                Vector2 leftCenter = offset - new Vector2(halfStraight, 0f);

                // Right semicircle: top → bottom through the right.
                for (int i = 0; i <= arcSegments; i++)
                {
                    float angle = -Mathf.PI * 0.5f + Mathf.PI * i / arcSegments;
                    points[index++] = t.TransformPoint(
                        rightCenter.x + Mathf.Cos(angle) * radius,
                        rightCenter.y + Mathf.Sin(angle) * radius,
                        0f);
                }

                // Left semicircle: bottom → top through the left.
                for (int i = 0; i <= arcSegments; i++)
                {
                    float angle = Mathf.PI * 0.5f + Mathf.PI * i / arcSegments;
                    points[index++] = t.TransformPoint(
                        leftCenter.x + Mathf.Cos(angle) * radius,
                        leftCenter.y + Mathf.Sin(angle) * radius,
                        0f);
                }
            }

            points[index++] = points[0];
            if (index == pointCount)
                return points;

            var trimmed = new Vector3[index];
            System.Array.Copy(points, trimmed, index);
            return trimmed;
        }

        void ResolveColliders()
        {
            if (bodyCollider != null)
            {
                if (killCollider == null)
                    ResolveKillCollider();
                return;
            }

            Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D col = colliders[i];
                if (col == null || IsPalmOrToolCursorCollider(col) || col.GetComponent<RunnerKillHitbox>() != null)
                    continue;

                bodyCollider = col;
                break;
            }

            ResolveKillCollider();
        }

        void ResolveKillCollider()
        {
            if (killCollider != null)
                return;

            RunnerKillHitbox marker = GetComponentInChildren<RunnerKillHitbox>(true);
            if (marker != null)
                killCollider = marker.GetComponent<Collider2D>();
        }

        void EnsureLiveOutlines()
        {
            if (liveOutline != null)
            {
                if (killLiveOutline == null)
                    EnsureKillOutline();
                return;
            }

            GameObject outlineObject = new GameObject("PlayerHitboxOutline");
            outlineObject.transform.SetParent(transform, false);
            liveOutline = ConfigureLineRenderer(outlineObject.AddComponent<LineRenderer>(), LiveColor, 0.045f);
            liveOutline.enabled = false;
            EnsureKillOutline();
        }

        void EnsureKillOutline()
        {
            if (killLiveOutline != null)
                return;

            GameObject outlineObject = new GameObject("PlayerKillHitboxOutline");
            outlineObject.transform.SetParent(transform, false);
            killLiveOutline = ConfigureLineRenderer(outlineObject.AddComponent<LineRenderer>(), KillLiveColor, 0.04f);
            killLiveOutline.enabled = false;
        }

        LineRenderer CreateTrailLineRenderer()
        {
            if (trailRoot == null)
            {
                GameObject root = new GameObject("PlayerHitboxTrail");
                root.transform.SetParent(transform, false);
                trailRoot = root.transform;
            }

            GameObject sampleObject = new GameObject($"Trail_{trailLines.Count}");
            sampleObject.transform.SetParent(trailRoot, false);
            return ConfigureLineRenderer(sampleObject.AddComponent<LineRenderer>(), TrailColor, 0.03f);
        }

        LineRenderer ConfigureLineRenderer(LineRenderer line, Color color, float width)
        {
            if (sharedLineMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                sharedLineMaterial = shader != null ? new Material(shader) : null;
            }

            line.sharedMaterial = sharedLineMaterial;
            line.useWorldSpace = true;
            line.loop = false;
            line.widthMultiplier = width;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.sortingOrder = 32000;
            line.startColor = color;
            line.endColor = color;
            line.positionCount = 0;
            return line;
        }

        static bool IsPalmOrToolCursorCollider(Collider2D collider)
        {
            Transform root = collider.transform;
            while (root != null)
            {
                string objectName = root.name;
                if (objectName == "PalmCursor" ||
                    objectName == "GrabCursor" ||
                    objectName == "Crosshair" ||
                    objectName == "TilePreview")
                    return true;
                root = root.parent;
            }

            return false;
        }

        static bool IsLocalKaplaoCreateMode()
        {
            if (NetworkClient.localPlayer == null)
                return false;

            if (!NetworkClient.localPlayer.TryGetComponent(out KaplaoCursorServer localCursor))
                return false;

            return KaplaoLocalGameModePredictor.IsCreate(localCursor);
        }

        static bool ShouldShowLiveHitboxes()
        {
            if (IsLocalKaplaoCreateMode() && CreateModeEditorSettings.ShowHitboxes)
                return true;

            return RunnerHitboxCheat.ShowHitboxes && IsLocalRunner();
        }

        static bool IsLocalRunner()
        {
            if (NetworkClient.localPlayer == null)
                return false;

            return NetworkClient.localPlayer.TryGetComponent(out NetworkPlayer player) &&
                   player.role == PlayerRole.Runner;
        }
    }
}
