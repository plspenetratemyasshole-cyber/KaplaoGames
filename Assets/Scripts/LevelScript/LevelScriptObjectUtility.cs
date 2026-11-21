using KaplaoGames.Client;
using System;
using System.Collections.Generic;
using KaplaoGames.Network;
using KaplaoGames.Server;
using KaplaoGames.Shared;
using Mirror;
using UnityEngine;

namespace KaplaoGames.LevelScript
{
    public enum ScriptObjectKind : byte
    {
        Tile,
        Player,
        SceneObject
    }

    public readonly struct ScriptGameObjectRef : IEquatable<ScriptGameObjectRef>
    {
        public static readonly ScriptGameObjectRef Null = new ScriptGameObjectRef(ScriptObjectKind.Player, 0);

        public ScriptObjectKind Kind { get; }
        public uint NetId { get; }

        public ScriptGameObjectRef(ScriptObjectKind kind, uint netId)
        {
            Kind = kind;
            NetId = netId;
        }

        public bool IsNull => NetId == 0;

        public override string ToString() => IsNull ? "null" : $"{Kind}#{NetId}";

        public bool Equals(ScriptGameObjectRef other) =>
            IsNull && other.IsNull || !IsNull && !other.IsNull && Kind == other.Kind && NetId == other.NetId;

        public override bool Equals(object obj) => obj is ScriptGameObjectRef other && Equals(other);

        public override int GetHashCode() => HashCode.Combine((int)Kind, NetId);
    }

    public readonly struct ScriptComponentRef : IEquatable<ScriptComponentRef>
    {
        public static readonly ScriptComponentRef Null = new ScriptComponentRef(ScriptGameObjectRef.Null, string.Empty);

        public ScriptGameObjectRef GameObject { get; }
        public string ComponentName { get; }

        public ScriptComponentRef(ScriptGameObjectRef gameObject, string componentName)
        {
            GameObject = gameObject;
            ComponentName = componentName ?? string.Empty;
        }

        public bool IsNull => GameObject.IsNull || string.IsNullOrEmpty(ComponentName);

        public override string ToString() => GameObject.IsNull ? "null" : $"{GameObject}.{ComponentName}";

        public bool Equals(ScriptComponentRef other) =>
            GameObject.Equals(other.GameObject) && ComponentName == other.ComponentName;

        public override bool Equals(object obj) => obj is ScriptComponentRef other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(GameObject, ComponentName);
    }

    public static class LevelScriptObjectUtility
    {
        static readonly Dictionary<uint, Transform> sceneObjectsById = new();
        static readonly Dictionary<Transform, uint> sceneIdsByTransform = new();
        static readonly List<PhysicsMaterial2D> createdMaterials = new();
        static uint nextSceneObjectId = 1;

        public static void ClearSceneObjectRegistry()
        {
            sceneObjectsById.Clear();
            sceneIdsByTransform.Clear();
            nextSceneObjectId = 1;

            for (int i = 0; i < createdMaterials.Count; i++)
            {
                if (createdMaterials[i] != null)
                    UnityEngine.Object.Destroy(createdMaterials[i]);
            }
            createdMaterials.Clear();
        }

        public static uint RegisterSceneObject(Transform transform)
        {
            if (transform == null)
                return 0;

            if (sceneIdsByTransform.TryGetValue(transform, out uint existingId))
                return existingId;

            uint id = nextSceneObjectId++;
            sceneObjectsById[id] = transform;
            sceneIdsByTransform[transform] = id;
            return id;
        }

        public static bool TryResolveTransform(ScriptGameObjectRef reference, out Transform transform)
        {
            transform = null;
            if (reference.IsNull)
                return false;

            if (reference.Kind == ScriptObjectKind.Tile)
            {
                if (!TryResolveTile(reference.NetId, out LevelTileServer tile))
                    return false;

                transform = tile.transform;
                return true;
            }

            if (reference.Kind == ScriptObjectKind.SceneObject)
            {
                if (sceneObjectsById.TryGetValue(reference.NetId, out Transform sceneTr) && sceneTr != null)
                {
                    transform = sceneTr;
                    return true;
                }

                if (NetworkServer.active && NetworkServer.spawned.TryGetValue(reference.NetId, out NetworkIdentity identity) && identity != null)
                {
                    transform = identity.transform;
                    return true;
                }
                if (NetworkClient.active && NetworkClient.spawned.TryGetValue(reference.NetId, out NetworkIdentity clientIdentity) && clientIdentity != null)
                {
                    transform = clientIdentity.transform;
                    return true;
                }

                return false;
            }

            if (!TryResolvePlayer(reference.NetId, out NetworkPlayer player))
                return false;

            transform = player.transform;
            return true;
        }

        public static bool TryResolveTile(uint netId, out LevelTileServer tile)
        {
            LevelGridServer grid = LevelGridServer.Instance;
            if (grid != null && grid.TryGetTileByNetId(netId, out tile))
                return true;

            if (NetworkClient.active &&
                NetworkClient.spawned.TryGetValue(netId, out NetworkIdentity identity) &&
                identity != null &&
                identity.TryGetComponent(out tile))
            {
                return true;
            }

            tile = null;
            return false;
        }

        public static bool TryResolvePlayer(uint netId, out NetworkPlayer player)
        {
            if (NetworkServer.active &&
                NetworkServer.spawned.TryGetValue(netId, out NetworkIdentity serverIdentity) &&
                serverIdentity != null &&
                serverIdentity.TryGetComponent(out player))
            {
                return true;
            }

            if (NetworkClient.active &&
                NetworkClient.spawned.TryGetValue(netId, out NetworkIdentity clientIdentity) &&
                clientIdentity != null &&
                clientIdentity.TryGetComponent(out player))
            {
                return true;
            }

            player = null;
            return false;
        }

        public static List<ScriptGameObjectRef> CollectTilesInGroup(int group)
        {
            var result = new List<ScriptGameObjectRef>();
            var tiles = new List<LevelTileServer>();
            LevelGridServer grid = LevelGridServer.Instance;
            if (grid != null && grid.TileCount > 0)
            {
                grid.CollectTilesWithGroup(group, tiles);
            }
            else
            {
                LevelTileServer[] found = UnityEngine.Object.FindObjectsByType<LevelTileServer>(FindObjectsSortMode.None);
                for (int i = 0; i < found.Length; i++)
                {
                    if (found[i] != null && found[i].HasGroup(group))
                        tiles.Add(found[i]);
                }
            }

            for (int i = 0; i < tiles.Count; i++)
                result.Add(new ScriptGameObjectRef(ScriptObjectKind.Tile, tiles[i].netId));

            return result;
        }

        public static List<ScriptGameObjectRef> CollectAllRunners()
        {
            var result = new List<ScriptGameObjectRef>();
            CollectAllRunnersNonAlloc(result);
            return result;
        }

        public static void CollectAllRunnersNonAlloc(List<ScriptGameObjectRef> result)
        {
            if (result == null)
                return;

            result.Clear();
            ForEachSpawnedPlayer(player =>
            {
                if (player != null && player.role == PlayerRole.Runner)
                    result.Add(new ScriptGameObjectRef(ScriptObjectKind.Player, player.netId));
            });
        }

        public static ScriptGameObjectRef FindNearestPlayer(float x, float y)
        {
            NetworkPlayer nearest = null;
            float bestDistanceSqr = float.MaxValue;

            ForEachSpawnedPlayer(player =>
            {
                if (player == null || player.role != PlayerRole.Runner)
                    return;

                Vector3 position = player.transform.position;
                float dx = position.x - x;
                float dy = position.y - y;
                float distanceSqr = dx * dx + dy * dy;
                if (distanceSqr >= bestDistanceSqr)
                    return;

                bestDistanceSqr = distanceSqr;
                nearest = player;
            });

            if (nearest == null)
                return ScriptGameObjectRef.Null;

            return new ScriptGameObjectRef(ScriptObjectKind.Player, nearest.netId);
        }

        static void ForEachSpawnedPlayer(System.Action<NetworkPlayer> callback)
        {
            if (callback == null)
                return;

            Dictionary<uint, NetworkIdentity> spawned = null;
            if (NetworkServer.active)
                spawned = NetworkServer.spawned;
            else if (NetworkClient.active)
                spawned = NetworkClient.spawned;

            if (spawned != null)
            {
                foreach (KeyValuePair<uint, NetworkIdentity> pair in spawned)
                {
                    if (pair.Value != null && pair.Value.TryGetComponent(out NetworkPlayer player))
                        callback(player);
                }

                return;
            }

            NetworkPlayer[] players = UnityEngine.Object.FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
                callback(players[i]);
        }

        public static ScriptGameObjectRef SpawnTile(string typeName, float x, float y)
        {
            LevelGridServer grid = LevelGridServer.Instance;
            if (grid == null)
                throw new InvalidOperationException("Level grid is not available.");

            TileType type = ParseTileType(typeName);
            uint netId = grid.TryPlaceTile(type, new Vector2(x, y), 0f, new Color32(255, 255, 255, 255));
            if (netId == 0)
                throw new InvalidOperationException($"Failed to spawn tile '{typeName}' at ({x}, {y}).");

            ScriptGameObjectRef reference = new ScriptGameObjectRef(ScriptObjectKind.Tile, netId);
            
            // Регистрируем созданный объект для удаления при Stop
            LevelScriptRuntime runtime = LevelScriptRuntime.Instance;
            if (runtime != null)
                runtime.RegisterCreatedObject(reference);

            return reference;
        }

        public static TileType ParseTileType(string typeName)
        {
            return typeName?.ToLowerInvariant() switch
            {
                "block" => TileType.Block,
                "spike" => TileType.Spike,
                "trigger" => TileType.Trigger,
                "particle" => TileType.Particle,
                "keyframe" => TileType.Keyframe,
                _ => throw new InvalidOperationException(
                    $"Unknown tile type '{typeName}'. Use \"block\", \"spike\", \"trigger\", \"particle\", or \"keyframe\".")
            };
        }

        public static bool TryGetPosition(ScriptGameObjectRef reference, out float x, out float y)
        {
            x = 0f;
            y = 0f;
            if (!TryResolveTransform(reference, out Transform transform) || transform == null)
                return false;

            Vector3 position = transform.position;
            x = position.x;
            y = position.y;
            return true;
        }

        public static bool TryGetCenter(IReadOnlyList<ScriptGameObjectRef> objects, out float x, out float y)
        {
            x = 0f;
            y = 0f;
            if (objects == null || objects.Count == 0)
                return false;

            Vector2 sum = Vector2.zero;
            int count = 0;
            for (int i = 0; i < objects.Count; i++)
            {
                if (!TryGetPosition(objects[i], out float px, out float py))
                    continue;

                sum.x += px;
                sum.y += py;
                count++;
            }

            if (count == 0)
                return false;

            sum /= count;
            x = sum.x;
            y = sum.y;
            return true;
        }

        public static List<uint> CollectTileNetIds(IReadOnlyList<ScriptGameObjectRef> objects)
        {
            var netIds = new List<uint>();
            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i].Kind == ScriptObjectKind.Tile)
                    netIds.Add(objects[i].NetId);
            }

            return netIds;
        }

        public static void AddComponent(ScriptGameObjectRef reference, string componentName)
        {
            if (!TryResolveTransform(reference, out Transform transform) || transform == null)
                return;

            switch (componentName)
            {
                case "Rigidbody2D":
                {
                    if (!TryResolveTile(reference.NetId, out LevelTileServer tile) || tile == null)
                        return;

                    tile.ServerEnablePhysics();
                    break;
                }
                default:
                    throw new InvalidOperationException($"Unknown component '{componentName}'.");
            }
        }

        public static void RemoveComponent(ScriptGameObjectRef reference, string componentName)
        {
            if (!TryResolveTransform(reference, out Transform transform) || transform == null)
                return;

            switch (componentName)
            {
                case "Rigidbody2D":
                {
                    if (!TryResolveTile(reference.NetId, out LevelTileServer tile) || tile == null)
                        return;

                    tile.ServerDisablePhysics();
                    break;
                }
                default:
                    throw new InvalidOperationException($"Unknown component '{componentName}'.");
            }
        }

        public static void SetComponentFloat(ScriptGameObjectRef reference, string componentName, string propertyName, float value)
        {
            if (!TryResolveTransform(reference, out Transform transform) || transform == null)
                return;

            if (componentName == "Player")
            {
                if (reference.Kind != ScriptObjectKind.Player)
                    return;

                if (!TryResolvePlayer(reference.NetId, out NetworkPlayer player) || player == null)
                    return;

                if (!player.TryGetComponent(out RunnerMovementServer runner) || runner == null)
                    return;

                Vector2 spawn = runner.SpawnPosition;
                switch (propertyName)
                {
                    case "spawnPointX":
                        runner.ServerSetSpawnPoint(value, spawn.y);
                        return;
                    case "spawnPointY":
                        runner.ServerSetSpawnPoint(spawn.x, value);
                        return;
                    default:
                        throw new InvalidOperationException($"Unknown Player property '{propertyName}'.");
                }
            }

            if (componentName == "Particle")
            {
                if (!TryResolveTile(reference.NetId, out LevelTileServer tile) || tile == null)
                    return;

                if (!tile.TryGetComponent(out ParticleEmitterTile emitter) || emitter == null)
                    return;

                emitter.ServerSetFloat(propertyName, value);
                return;
            }

            if (componentName != "Rigidbody2D")
                throw new InvalidOperationException($"Unknown component '{componentName}'.");

            Rigidbody2D body = transform.GetComponent<Rigidbody2D>();
            if (body == null)
                throw new InvalidOperationException("Rigidbody2D is not attached to this GameObject.");

            switch (propertyName)
            {
                case "Mass":
                    body.mass = value;
                    break;
                case "GravityScale":
                    body.gravityScale = value;
                    break;
                case "Drag":
                    body.linearDamping = value;
                    break;
                case "AngularDrag":
                    body.angularDamping = value;
                    break;
                default:
                    throw new InvalidOperationException($"Unknown Rigidbody2D property '{propertyName}'.");
            }
        }

        public static void SetComponentInt(ScriptGameObjectRef reference, string componentName, string propertyName, int value)
        {
            if (!TryResolveTransform(reference, out Transform transform) || transform == null)
                return;

            if (componentName == "Particle")
            {
                if (!TryResolveTile(reference.NetId, out LevelTileServer tile) || tile == null)
                    return;

                if (!tile.TryGetComponent(out ParticleEmitterTile emitter) || emitter == null)
                    return;

                emitter.ServerSetInt(propertyName, value);
                return;
            }

            throw new InvalidOperationException($"Unknown component '{componentName}'.");
        }

        public static void SetComponentBool(ScriptGameObjectRef reference, string componentName, string propertyName, bool value)
        {
            if (!TryResolveTransform(reference, out Transform transform) || transform == null)
                return;

            if (componentName == "Particle")
            {
                if (!TryResolveTile(reference.NetId, out LevelTileServer tile) || tile == null)
                    return;

                if (!tile.TryGetComponent(out ParticleEmitterTile emitter) || emitter == null)
                    return;

                emitter.ServerSetBool(propertyName, value);
                return;
            }

            throw new InvalidOperationException($"Unknown component '{componentName}'.");
        }

        public static void CallComponentMethod(
            ScriptGameObjectRef reference,
            string componentName,
            string methodName,
            IReadOnlyList<float> arguments)
        {
            if (!TryResolveTransform(reference, out Transform transform) || transform == null)
                return;

            if (componentName != "Rigidbody2D")
                throw new InvalidOperationException($"Unknown component '{componentName}'.");

            Rigidbody2D body = transform.GetComponent<Rigidbody2D>();
            if (body == null)
                throw new InvalidOperationException("Rigidbody2D is not attached to this GameObject.");

            switch (methodName)
            {
                case "AddForce":
                    if (arguments == null || arguments.Count != 2)
                        throw new InvalidOperationException("Rigidbody2D.AddForce expects 2 float arguments: x, y.");

                    body.AddForce(new Vector2(arguments[0], arguments[1]), ForceMode2D.Force);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown Rigidbody2D method '{methodName}'.");
            }
        }

        public static Color32 ParseHexColor(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
                throw new InvalidOperationException("Color hex string is empty.");

            string value = hex.Trim();
            if (!value.StartsWith("#", StringComparison.Ordinal))
                value = "#" + value;

            if (!ColorUtility.TryParseHtmlString(value, out Color color))
                throw new InvalidOperationException($"Invalid color hex '{hex}'.");

            return color;
        }

        public static Collider2D TryGetCollider(ScriptGameObjectRef reference)
        {
            if (!TryResolveTransform(reference, out Transform transform) || transform == null)
                return null;

            return transform.GetComponent<Collider2D>();
        }

        public static ScriptGameObjectRef ResolveSceneObject(string path)
        {
            if (string.IsNullOrEmpty(path))
                return ScriptGameObjectRef.Null;

            Transform found = FindSceneTransform(path);
            if (found == null)
                throw new InvalidOperationException($"Scene object '{path}' not found.");

            // Check if it's already a tile or player with NetworkIdentity
            if (found.TryGetComponent(out NetworkIdentity identity))
            {
                if (identity.TryGetComponent<LevelTileServer>(out _))
                    return new ScriptGameObjectRef(ScriptObjectKind.Tile, identity.netId);

                if (identity.TryGetComponent<NetworkPlayer>(out _))
                    return new ScriptGameObjectRef(ScriptObjectKind.Player, identity.netId);
            }

            // Register in static registry without forcing NetworkIdentity or NetworkServer.Spawn
            uint id = RegisterSceneObject(found);
            return new ScriptGameObjectRef(ScriptObjectKind.SceneObject, id);
        }

        static Transform FindSceneTransform(string path)
        {
            string unityPath = path.Replace('.', '/');
            GameObject direct = GameObject.Find(unityPath);
            if (direct != null) return direct.transform;

            direct = GameObject.Find(path);
            if (direct != null) return direct.transform;

            string[] parts = path.Split('.');
            Transform current = null;
            GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();

            // Find first part in root objects
            foreach (GameObject rootObj in rootObjects)
            {
                if (rootObj.name.Equals(parts[0], StringComparison.OrdinalIgnoreCase))
                {
                    current = rootObj.transform;
                    break;
                }
            }

            // If not in root objects, check if it's under a "World" or "Objects" container
            if (current == null)
            {
                GameObject worldObj = GameObject.Find("World");
                if (worldObj != null)
                {
                    for (int j = 0; j < worldObj.transform.childCount; j++)
                    {
                        Transform c = worldObj.transform.GetChild(j);
                        if (c.name.Equals(parts[0], StringComparison.OrdinalIgnoreCase))
                        {
                            current = c;
                            break;
                        }
                    }
                }
            }

            if (current == null)
                return null;

            // Navigate through children
            for (int i = 1; i < parts.Length; i++)
            {
                Transform child = null;
                for (int j = 0; j < current.childCount; j++)
                {
                    Transform c = current.GetChild(j);
                    if (c.name.Equals(parts[i], StringComparison.OrdinalIgnoreCase))
                    {
                        child = c;
                        break;
                    }
                }

                if (child == null)
                    return null;

                current = child;
            }

            return current;
        }

        public static void ApplyBackgroundAction(BackgroundActionKind kind, int layerIndex, string hexColor, float value)
        {
            ParallaxBackground bg = ParallaxBackground.Instance ?? UnityEngine.Object.FindFirstObjectByType<ParallaxBackground>();
            if (bg == null)
                return;

            switch (kind)
            {
                case BackgroundActionKind.Clear:
                    bg.ClearBackground();
                    break;
                case BackgroundActionKind.HideLayer:
                    bg.HideLayer(layerIndex);
                    break;
                case BackgroundActionKind.ShowLayer:
                    bg.ShowLayer(layerIndex);
                    break;
                case BackgroundActionKind.SetColor:
                    if (!string.IsNullOrEmpty(hexColor))
                        bg.SetLayerColor(layerIndex, ParseHexColor(hexColor));
                    break;
                case BackgroundActionKind.SetSpeed:
                    bg.SetLayerSpeed(layerIndex, value);
                    break;
                case BackgroundActionKind.SetOffset:
                    bg.SetLayerOffset(layerIndex, value);
                    break;
                case BackgroundActionKind.SetAlpha:
                    bg.SetLayerAlpha(layerIndex, value);
                    break;
                case BackgroundActionKind.Reset:
                    bg.ResetBackground();
                    break;
            }
        }

        public static void ApplyHitboxConfig(
            HitboxConfigKind kind,
            IReadOnlyList<ScriptGameObjectRef> targets,
            float x,
            float y,
            bool boolValue)
        {
            if (targets == null || targets.Count == 0)
                return;

            for (int i = 0; i < targets.Count; i++)
            {
                if (!TryResolveTransform(targets[i], out Transform transform) || transform == null)
                    continue;

                Collider2D col = transform.GetComponent<Collider2D>();
                if (col == null)
                    continue;

                switch (kind)
                {
                    case HitboxConfigKind.Size:
                        if (col is BoxCollider2D box)
                            box.size = new Vector2(x, y);
                        else if (col is CapsuleCollider2D cap)
                            cap.size = new Vector2(x, y);
                        else if (col is CircleCollider2D circle)
                            circle.radius = Mathf.Max(x, y) * 0.5f;
                        break;

                    case HitboxConfigKind.Offset:
                        col.offset = new Vector2(x, y);
                        break;

                    case HitboxConfigKind.Trigger:
                        col.isTrigger = boolValue;
                        break;

                    case HitboxConfigKind.Bounce:
                    {
                        PhysicsMaterial2D mat = col.sharedMaterial != null
                            ? new PhysicsMaterial2D(col.sharedMaterial.name) { bounciness = x, friction = col.sharedMaterial.friction }
                            : new PhysicsMaterial2D { bounciness = x };
                        createdMaterials.Add(mat);
                        col.sharedMaterial = mat;
                        break;
                    }

                    case HitboxConfigKind.Friction:
                    {
                        PhysicsMaterial2D mat = col.sharedMaterial != null
                            ? new PhysicsMaterial2D(col.sharedMaterial.name) { bounciness = col.sharedMaterial.bounciness, friction = x }
                            : new PhysicsMaterial2D { friction = x };
                        createdMaterials.Add(mat);
                        col.sharedMaterial = mat;
                        break;
                    }
                }
            }

            LevelScriptTriggerBridge.Instance?.InvalidateWatchCache();
        }

        public static void ApplySpriteAction(
            SpriteActionKind kind,
            IReadOnlyList<ScriptGameObjectRef> targets,
            bool flipX,
            bool flipY,
            int sortingOrder,
            float alpha,
            string hexColor)
        {
            if (targets == null || targets.Count == 0)
                return;

            for (int i = 0; i < targets.Count; i++)
            {
                if (!TryResolveTransform(targets[i], out Transform transform) || transform == null)
                    continue;

                SpriteRenderer sr = transform.GetComponent<SpriteRenderer>();

                switch (kind)
                {
                    case SpriteActionKind.Destroy:
                        if (sr != null)
                        {
                            sr.enabled = false;
                            sr.sprite = null;
                        }
                        break;

                    case SpriteActionKind.Flip:
                        if (sr != null)
                        {
                            sr.flipX = flipX;
                            sr.flipY = flipY;
                        }
                        break;

                    case SpriteActionKind.SortingOrder:
                        if (sr != null)
                            sr.sortingOrder = sortingOrder;
                        break;

                    case SpriteActionKind.Alpha:
                        if (sr != null)
                        {
                            Color c = sr.color;
                            c.a = Mathf.Clamp01(alpha);
                            sr.color = c;
                        }
                        break;

                    case SpriteActionKind.Color:
                        if (sr != null && !string.IsNullOrEmpty(hexColor))
                            sr.color = ParseHexColor(hexColor);
                        break;
                }
            }

            if (LevelGridServer.Instance != null)
            {
                List<uint> tileNetIds = CollectTileNetIds(targets);
                if (tileNetIds.Count > 0)
                {
                    if (kind == SpriteActionKind.SortingOrder)
                        LevelGridServer.Instance.TrySetTilesSortingOrderByNetIds(tileNetIds, sortingOrder);
                    else if (kind == SpriteActionKind.Alpha)
                        LevelGridServer.Instance.TrySetTilesTransparencyByNetIds(tileNetIds, Mathf.Clamp01(alpha), updateBaseState: false);
                    else if (kind == SpriteActionKind.Color && !string.IsNullOrEmpty(hexColor))
                        LevelGridServer.Instance.TrySetTilesColorByNetIds(tileNetIds, ParseHexColor(hexColor), updateBaseState: false);
                }
            }
        }

        public static void ApplyWorldAction(
            WorldActionKind kind,
            float gx,
            float gy,
            int tileX,
            int tileY,
            int tileId)
        {
            switch (kind)
            {
                case WorldActionKind.SetGravity:
                    Physics2D.gravity = new Vector2(gx, gy);
                    break;

                case WorldActionKind.SetTimeScale:
                    Time.timeScale = Mathf.Max(0f, gx);
                    break;

                case WorldActionKind.SetTile:
                    if (LevelGridServer.Instance != null)
                    {
                        Vector2Int cell = new Vector2Int(tileX, tileY);
                        if (LevelGridServer.Instance.TryGetTile(cell, out LevelTileServer existingTile) && existingTile != null)
                        {
                            LevelTileRecord rec = LevelGridServer.CreateRecordFromTile(existingTile);
                            LevelScriptRuntime.Instance?.RegisterDestroyedTile(rec);
                            LevelGridServer.Instance.TryRemoveTile(cell);
                        }
                        uint newNetId = LevelGridServer.Instance.TryPlaceTile((TileType)tileId, cell, new Color32(255, 255, 255, 255));
                        if (newNetId != 0)
                        {
                            LevelScriptRuntime.Instance?.RegisterCreatedObject(new ScriptGameObjectRef(ScriptObjectKind.Tile, newNetId));
                        }
                        LevelScriptTriggerBridge.Instance?.InvalidateWatchCache();
                    }
                    break;

                case WorldActionKind.RemoveTile:
                    if (LevelGridServer.Instance != null)
                    {
                        Vector2Int cell = new Vector2Int(tileX, tileY);
                        if (LevelGridServer.Instance.TryGetTile(cell, out LevelTileServer existingTile) && existingTile != null)
                        {
                            LevelTileRecord rec = LevelGridServer.CreateRecordFromTile(existingTile);
                            LevelScriptRuntime.Instance?.RegisterDestroyedTile(rec);
                        }
                        LevelGridServer.Instance.TryRemoveTile(cell);
                        LevelScriptTriggerBridge.Instance?.InvalidateWatchCache();
                    }
                    break;
            }
        }
    }
}
