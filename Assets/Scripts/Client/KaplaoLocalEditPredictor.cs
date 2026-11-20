using System.Collections.Generic;
using KaplaoGames.Network;
using KaplaoGames.Server;
using KaplaoGames.Shared;
using Mirror;
using UnityEngine;

namespace KaplaoGames.Client
{
    /// <summary>
    /// Applies Kaplao editor changes locally before server confirmation (high-ping friendly).
    /// Transform overrides stack on the predicted pose so WASD / GRT stay responsive.
    /// </summary>
    public static class KaplaoLocalEditPredictor
    {
        const float TransformPosEpsilon = 0.001f;
        const float TransformRotEpsilon = 0.01f;
        const float TransformScaleEpsilon = 0.001f;

        struct TileOverride
        {
            public bool HasColor;
            public Color32 Color;

            public bool HasTexture;
            public string TextureId;

            public bool HasGroups;
            public string Groups;

            public bool HasTransparency;
            public float Transparency;

            public bool HasEditorLayer;
            public int EditorLayer;

            public bool HasNoHitbox;
            public bool NoHitbox;

            public bool HasSortingOrder;
            public int SortingOrder;

            public bool HasSpawnPoint;
            public bool IsSpawnPoint;

            public bool HasTransform;
            public Vector2 Position;
            public float Rotation;
            public Vector2 Scale;
        }

        static readonly Dictionary<uint, TileOverride> Overrides = new();

        static bool IsActive =>
            NetworkClient.active && LocalPlayerRole.IsKaplao();

        public static void ClearAll() => Overrides.Clear();

        public static Vector2 GetEffectivePosition(LevelTileServer tile)
        {
            if (tile != null &&
                Overrides.TryGetValue(tile.netId, out TileOverride o) &&
                o.HasTransform)
            {
                return o.Position;
            }

            return tile != null ? tile.Position : Vector2.zero;
        }

        public static float GetEffectiveRotation(LevelTileServer tile)
        {
            if (tile != null &&
                Overrides.TryGetValue(tile.netId, out TileOverride o) &&
                o.HasTransform)
            {
                return o.Rotation;
            }

            return tile != null ? tile.PlacementRotationDegrees : 0f;
        }

        public static Vector2 GetEffectiveScale(LevelTileServer tile)
        {
            if (tile != null &&
                Overrides.TryGetValue(tile.netId, out TileOverride o) &&
                o.HasTransform)
            {
                return o.Scale;
            }

            return tile != null ? tile.Scale : Vector2.one;
        }

        public static bool TryGetPredictedTransform(
            uint netId,
            out Vector2 position,
            out float rotationDegrees,
            out Vector2 scale)
        {
            position = default;
            rotationDegrees = 0f;
            scale = Vector2.one;
            if (!Overrides.TryGetValue(netId, out TileOverride o) || !o.HasTransform)
                return false;

            position = o.Position;
            rotationDegrees = o.Rotation;
            scale = o.Scale;
            return true;
        }

        public static void PredictMoveBy(KaplaoCursorServer server, float dx, float dy)
        {
            if (!IsActive || server == null || (Mathf.Abs(dx) < 0.00001f && Mathf.Abs(dy) < 0.00001f))
                return;

            for (int i = 0; i < server.SelectedTiles.Count; i++)
            {
                uint netId = server.SelectedTiles[i];
                if (!TryGetTile(netId, out LevelTileServer tile) || tile.IsAttachedToScriptPivot)
                    continue;

                PredictSetTransform(
                    netId,
                    GetEffectivePosition(tile) + new Vector2(dx, dy),
                    GetEffectiveRotation(tile),
                    GetEffectiveScale(tile));
            }
        }

        public static void PredictSetTransform(uint netId, Vector2 position, float rotationDegrees, Vector2 scale)
        {
            if (!IsActive || netId == 0)
                return;

            scale = new Vector2(Mathf.Max(0.01f, scale.x), Mathf.Max(0.01f, scale.y));
            SetOverride(netId, o =>
            {
                o.HasTransform = true;
                o.Position = position;
                o.Rotation = rotationDegrees;
                o.Scale = scale;
                return o;
            });
        }

        public static void PredictApplyTransformPatch(KaplaoCursorServer server, TileTransformPatchRecord patch)
        {
            if (!IsActive || server == null || !patch.HasAnyField)
                return;

            for (int i = 0; i < server.SelectedTiles.Count; i++)
            {
                uint netId = server.SelectedTiles[i];
                if (!TryGetTile(netId, out LevelTileServer tile) || tile.IsAttachedToScriptPivot)
                    continue;

                patch.MergeOnto(
                    GetEffectivePosition(tile),
                    GetEffectiveRotation(tile),
                    GetEffectiveScale(tile),
                    tile.SortingOrder,
                    out Vector2 position,
                    out float rotation,
                    out Vector2 scale,
                    out int sortingOrder);

                PredictSetTransform(netId, position, rotation, scale);

                if (patch.hasSortingOrder)
                {
                    SetOverride(netId, o =>
                    {
                        o.HasSortingOrder = true;
                        o.SortingOrder = sortingOrder;
                        return o;
                    });
                }
            }
        }

        public static void PredictSetColor(KaplaoCursorServer server, Color32 color)
        {
            if (!IsActive || server == null)
                return;

            ApplyToSelection(server, o =>
            {
                o.HasColor = true;
                o.Color = color;
                return o;
            });
        }

        public static void PredictSetTexture(KaplaoCursorServer server, string textureId)
        {
            if (!IsActive || server == null)
                return;

            string normalized = string.IsNullOrWhiteSpace(textureId) ? string.Empty : textureId.Trim();
            ApplyToSelection(server, o =>
            {
                o.HasTexture = true;
                o.TextureId = normalized;
                return o;
            });
        }

        public static void PredictSetSortingOrder(KaplaoCursorServer server, int sortingOrder)
        {
            if (!IsActive || server == null)
                return;

            ApplyToSelection(server, o =>
            {
                o.HasSortingOrder = true;
                o.SortingOrder = sortingOrder;
                return o;
            });
        }

        public static void PredictSetTransparency(KaplaoCursorServer server, float transparency)
        {
            if (!IsActive || server == null)
                return;

            float value = Mathf.Clamp01(transparency);
            ApplyToSelection(server, o =>
            {
                o.HasTransparency = true;
                o.Transparency = value;
                return o;
            });
        }

        public static void PredictSetEditorLayer(KaplaoCursorServer server, int editorLayer)
        {
            if (!IsActive || server == null)
                return;

            int value = Mathf.Clamp(editorLayer, 0, 100);
            ApplyToSelection(server, o =>
            {
                o.HasEditorLayer = true;
                o.EditorLayer = value;
                return o;
            });
        }

        public static void PredictSetNoHitbox(KaplaoCursorServer server, bool noHitbox)
        {
            if (!IsActive || server == null)
                return;

            ApplyToSelection(server, o =>
            {
                o.HasNoHitbox = true;
                o.NoHitbox = noHitbox;
                return o;
            });
        }

        public static void PredictSetSpawnPoint(KaplaoCursorServer server, bool isSpawnPoint)
        {
            if (!IsActive || server == null)
                return;

            ApplyToSelection(server, o =>
            {
                o.HasSpawnPoint = true;
                o.IsSpawnPoint = isSpawnPoint;
                return o;
            });
        }

        public static bool GetEffectiveIsSpawnPoint(LevelTileServer tile)
        {
            if (tile != null &&
                Overrides.TryGetValue(tile.netId, out TileOverride o) &&
                o.HasSpawnPoint)
            {
                return o.IsSpawnPoint;
            }

            return tile != null && tile.IsSpawnPoint;
        }

        public static bool HandleServerSpawnPointSync(LevelTileServer tile, bool serverIsSpawnPoint)
        {
            if (!IsActive || tile == null || !Overrides.TryGetValue(tile.netId, out TileOverride o) || !o.HasSpawnPoint)
                return false;

            if (serverIsSpawnPoint == o.IsSpawnPoint)
            {
                ClearSpawnPointOverride(tile.netId);
                return false;
            }

            return true;
        }

        public static void PredictSetGroups(KaplaoCursorServer server, string groups)
        {
            if (!IsActive || server == null)
                return;

            string normalized = TileGroups.Normalize(groups);
            ApplyToSelection(server, o =>
            {
                o.HasGroups = true;
                o.Groups = normalized;
                return o;
            });
        }

        public static void PredictAddGroup(KaplaoCursorServer server, int group)
        {
            if (!IsActive || server == null || group < 0)
                return;

            for (int i = 0; i < server.SelectedTiles.Count; i++)
            {
                uint netId = server.SelectedTiles[i];
                if (!TryGetTile(netId, out LevelTileServer tile))
                    continue;

                var numbers = new HashSet<int>(TileGroups.ParseNumbers(GetEffectiveGroups(tile)));
                if (!numbers.Add(group))
                    continue;

                SetOverride(netId, o =>
                {
                    o.HasGroups = true;
                    o.Groups = TileGroups.Join(numbers);
                    return o;
                });
            }
        }

        public static void PredictRemoveGroup(KaplaoCursorServer server, int group)
        {
            if (!IsActive || server == null || group < 0)
                return;

            for (int i = 0; i < server.SelectedTiles.Count; i++)
            {
                uint netId = server.SelectedTiles[i];
                if (!TryGetTile(netId, out LevelTileServer tile))
                    continue;

                var numbers = new HashSet<int>(TileGroups.ParseNumbers(GetEffectiveGroups(tile)));
                if (!numbers.Remove(group))
                    continue;

                SetOverride(netId, o =>
                {
                    o.HasGroups = true;
                    o.Groups = TileGroups.Join(numbers);
                    return o;
                });
            }
        }

        public static bool HandleServerColorSync(LevelTileServer tile, Color32 serverColor)
        {
            if (!IsActive || tile == null || !Overrides.TryGetValue(tile.netId, out TileOverride o) || !o.HasColor)
                return false;

            if (ColorsMatch(serverColor, o.Color))
            {
                ClearColorOverride(tile.netId);
                return false;
            }

            tile.ClientApplyPredictedColor(o.Color);
            return true;
        }

        public static bool HandleServerTextureSync(LevelTileServer tile, string serverTextureId)
        {
            if (!IsActive || tile == null || !Overrides.TryGetValue(tile.netId, out TileOverride o) || !o.HasTexture)
                return false;

            string normalized = string.IsNullOrWhiteSpace(serverTextureId) ? string.Empty : serverTextureId.Trim();
            if (normalized == o.TextureId)
            {
                ClearTextureOverride(tile.netId);
                return false;
            }

            tile.ClientApplyPredictedTexture(o.TextureId);
            return true;
        }

        public static bool HandleServerGroupsSync(LevelTileServer tile, string serverGroups)
        {
            if (!IsActive || tile == null || !Overrides.TryGetValue(tile.netId, out TileOverride o) || !o.HasGroups)
                return false;

            string normalized = TileGroups.Normalize(serverGroups);
            if (normalized == o.Groups)
            {
                ClearGroupsOverride(tile.netId);
                return false;
            }

            tile.ClientApplyPredictedGroups(o.Groups);
            return true;
        }

        public static bool HandleServerAppearanceSync(LevelTileServer tile)
        {
            if (!IsActive || tile == null || !Overrides.TryGetValue(tile.netId, out TileOverride o))
                return false;

            bool deferred = false;
            if (o.HasTransparency && !Mathf.Approximately(tile.Transparency, o.Transparency))
                deferred = true;
            if (o.HasEditorLayer && tile.EditorLayer != o.EditorLayer)
                deferred = true;
            if (o.HasNoHitbox && tile.HasNoHitbox != o.NoHitbox)
                deferred = true;
            if (o.HasSortingOrder && tile.SortingOrder != o.SortingOrder)
                deferred = true;

            if (!deferred)
            {
                ClearAppearanceOverride(tile.netId);
                return false;
            }

            tile.ClientApplyPredictedAppearance(
                o.HasTransparency ? o.Transparency : tile.Transparency,
                o.HasEditorLayer ? o.EditorLayer : tile.EditorLayer,
                o.HasNoHitbox ? o.NoHitbox : tile.HasNoHitbox,
                o.HasSortingOrder ? o.SortingOrder : tile.SortingOrder);
            return true;
        }

        public static bool HandleServerTransformSync(LevelTileServer tile)
        {
            if (!IsActive || tile == null || !Overrides.TryGetValue(tile.netId, out TileOverride o) || !o.HasTransform)
                return false;

            // Keep local G/R/T drag visuals until the session ends (server mid-drag lags badly).
            if (KaplaoLocalTransformSession.IsActive)
            {
                tile.ClientApplyPredictedTransform(o.Position, o.Rotation, o.Scale);
                return true;
            }

            if (TransformsMatch(tile, o))
            {
                ClearTransformOverride(tile.netId);
                return false;
            }

            tile.ClientApplyPredictedTransform(o.Position, o.Rotation, o.Scale);
            return true;
        }

        static bool TransformsMatch(LevelTileServer tile, TileOverride o)
        {
            Vector2 pos = tile.Position;
            Vector2 scale = tile.Scale;
            return Mathf.Abs(pos.x - o.Position.x) <= TransformPosEpsilon &&
                   Mathf.Abs(pos.y - o.Position.y) <= TransformPosEpsilon &&
                   Mathf.Abs(Mathf.DeltaAngle(tile.PlacementRotationDegrees, o.Rotation)) <= TransformRotEpsilon &&
                   Mathf.Abs(scale.x - o.Scale.x) <= TransformScaleEpsilon &&
                   Mathf.Abs(scale.y - o.Scale.y) <= TransformScaleEpsilon;
        }

        static void ApplyToSelection(KaplaoCursorServer server, System.Func<TileOverride, TileOverride> mutate)
        {
            for (int i = 0; i < server.SelectedTiles.Count; i++)
                SetOverride(server.SelectedTiles[i], mutate);
        }

        static void SetOverride(uint netId, System.Func<TileOverride, TileOverride> mutate)
        {
            Overrides.TryGetValue(netId, out TileOverride current);
            current = mutate(current);
            Overrides[netId] = current;

            if (!TryGetTile(netId, out LevelTileServer tile))
                return;

            ApplyOverrideToTile(tile, current);
        }

        static void ApplyOverrideToTile(LevelTileServer tile, TileOverride o)
        {
            if (o.HasTransform)
                tile.ClientApplyPredictedTransform(o.Position, o.Rotation, o.Scale);
            if (o.HasColor)
                tile.ClientApplyPredictedColor(o.Color);
            if (o.HasTexture)
                tile.ClientApplyPredictedTexture(o.TextureId);
            if (o.HasGroups)
                tile.ClientApplyPredictedGroups(o.Groups);
            if (o.HasTransparency || o.HasEditorLayer || o.HasNoHitbox || o.HasSortingOrder)
            {
                tile.ClientApplyPredictedAppearance(
                    o.HasTransparency ? o.Transparency : tile.Transparency,
                    o.HasEditorLayer ? o.EditorLayer : tile.EditorLayer,
                    o.HasNoHitbox ? o.NoHitbox : tile.HasNoHitbox,
                    o.HasSortingOrder ? o.SortingOrder : tile.SortingOrder);
            }
        }

        static bool TryGetTile(uint netId, out LevelTileServer tile)
        {
            tile = null;
            if (LevelGridServer.Instance != null &&
                LevelGridServer.Instance.TryGetTileByNetId(netId, out tile))
            {
                return tile != null;
            }

            if (NetworkClient.spawned.TryGetValue(netId, out NetworkIdentity identity) &&
                identity != null &&
                identity.TryGetComponent(out tile))
            {
                return tile != null;
            }

            return false;
        }

        static string GetEffectiveGroups(LevelTileServer tile)
        {
            if (Overrides.TryGetValue(tile.netId, out TileOverride o) && o.HasGroups)
                return o.Groups;
            return tile.GroupsDisplay;
        }

        static bool ColorsMatch(Color32 a, Color32 b) =>
            a.r == b.r && a.g == b.g && a.b == b.b;

        static void ClearColorOverride(uint netId)
        {
            if (!Overrides.TryGetValue(netId, out TileOverride o))
                return;

            o.HasColor = false;
            RemoveIfEmpty(netId, o);
        }

        static void ClearTextureOverride(uint netId)
        {
            if (!Overrides.TryGetValue(netId, out TileOverride o))
                return;

            o.HasTexture = false;
            RemoveIfEmpty(netId, o);
        }

        static void ClearSpawnPointOverride(uint netId)
        {
            if (!Overrides.TryGetValue(netId, out TileOverride o))
                return;

            o.HasSpawnPoint = false;
            RemoveIfEmpty(netId, o);
        }

        static void ClearGroupsOverride(uint netId)
        {
            if (!Overrides.TryGetValue(netId, out TileOverride o))
                return;

            o.HasGroups = false;
            RemoveIfEmpty(netId, o);
        }

        static void ClearAppearanceOverride(uint netId)
        {
            if (!Overrides.TryGetValue(netId, out TileOverride o))
                return;

            o.HasTransparency = false;
            o.HasEditorLayer = false;
            o.HasNoHitbox = false;
            o.HasSortingOrder = false;
            RemoveIfEmpty(netId, o);
        }

        static void ClearTransformOverride(uint netId)
        {
            if (!Overrides.TryGetValue(netId, out TileOverride o))
                return;

            o.HasTransform = false;
            RemoveIfEmpty(netId, o);
        }

        static void RemoveIfEmpty(uint netId, TileOverride o)
        {
            if (!o.HasColor &&
                !o.HasTexture &&
                !o.HasGroups &&
                !o.HasTransparency &&
                !o.HasEditorLayer &&
                !o.HasNoHitbox &&
                !o.HasSortingOrder &&
                !o.HasSpawnPoint &&
                !o.HasTransform)
            {
                Overrides.Remove(netId);
            }
            else
            {
                Overrides[netId] = o;
            }
        }
    }
}
