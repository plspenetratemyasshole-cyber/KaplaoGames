using System;
using UnityEngine;

namespace KaplaoGames.Shared
{
    /// <summary>
    /// Serializable level format for future file save/load.
    /// </summary>
    [Serializable]
    public class LevelData
    {
        public int version = 10;
        public string script = string.Empty;
        public LevelTileRecord[] tiles = Array.Empty<LevelTileRecord>();
        public TileLinkGroupRecord[] linkGroups = Array.Empty<TileLinkGroupRecord>();
        public float worldSpawnX = 0f;
        public float worldSpawnY = 5f;

        // Banned nicknames. Moderators are session-only and are not restored on join.
        public string[] bannedIds = Array.Empty<string>();
        // Banned IPs (normalized).
        public string[] bannedIps = Array.Empty<string>();
        // Kept for old level.json compatibility; always ignored on load / empty on save.
        public string[] moderatorIds = Array.Empty<string>();
    }

    [Serializable]
    public struct LevelTileRecord
    {
        public float x;
        public float y;
        public float rotationDegrees;
        public float scaleX;
        public float scaleY;
        public TileType type;
        public byte r;
        public byte g;
        public byte b;
        public string textureId;
        public int sortingOrder;
        public string groups;
        public byte quarterTurns;
        public float transparency;
        public bool noHitbox;
        public int editorLayer;

        // Legacy field for importing levels saved before version 7.
        public bool invisible;

        // Particle emitter settings (used only when type == TileType.Particle).
        public ParticleEmitterRecord particle;

        // Unix ms (UTC) when the tile was placed on the host machine.
        public long createdAt;

        // Trigger-only: acts like spawnPoints() for this tile's groups.
        public bool isSpawnPoint;

        // Keyframe marker settings (used only when type == TileType.Keyframe).
        public KeyframeRecord keyframe;

        // Executor settings (used only when type == TileType.Executor).
        public ExecutorRecord executor;
    }

    [Serializable]
    public struct ExecutorGroupState
    {
        public bool hasValue;
        public float pivotX;
        public float pivotY;
        public float rotation;
        public float scale;

        public static ExecutorGroupState Identity(Vector2 pivot)
        {
            return new ExecutorGroupState
            {
                hasValue = true,
                pivotX = pivot.x,
                pivotY = pivot.y,
                rotation = 0f,
                scale = 1f
            };
        }

        public Vector2 Pivot => new Vector2(pivotX, pivotY);
    }

    [Serializable]
    public struct ExecutorRecord
    {
        public int targetGroup;
        public float duration;
        public string anim;
        public int x360;
        public bool spawnedExecutor;
        public bool spawnExecutorAtEnd;
        public int spawnAtEndGroup;
        public bool loop;
        public float loopEverySeconds;
        public bool useKeyframes;
        public int keyframesGroup;
        public bool closeLoop;
        public bool returnEnabled;
        public float returnDelaySeconds;
        public ExecutorGroupState startState;
        public ExecutorGroupState endState;
    }

    [Serializable]
    public struct KeyframeRecord
    {
        public float duration;
        public string anim;
        public int spawnId;
        public bool curve;
        public bool closeLoop;
        public bool distance;
    }

    /// <summary>
    /// Patch-like record: server applies only fields with corresponding flags.
    /// This fixes the "multi edit overwrites unrelated fields" bug.
    /// </summary>
    [Serializable]
    public struct KeyframePatchRecord
    {
        public bool hasDuration;
        public float duration;

        public bool hasAnim;
        public string anim;

        public bool hasSpawnId;
        public int spawnId;

        public bool hasCurve;
        public bool curve;

        public bool hasCloseLoop;
        public bool closeLoop;

        public bool hasDistance;
        public bool distance;
    }

    /// <summary>
    /// Patch-like transform record: server applies only fields with corresponding flags.
    /// Works for every tile type (blocks, particles, executors, keyframes, etc.).
    /// </summary>
    [Serializable]
    public struct TileTransformPatchRecord
    {
        public bool hasPositionX;
        public float positionX;

        public bool hasPositionY;
        public float positionY;

        public bool hasRotation;
        public float rotationDegrees;

        public bool hasScaleX;
        public float scaleX;

        public bool hasScaleY;
        public float scaleY;

        public bool hasSortingOrder;
        public int sortingOrder;

        public bool HasAnyField =>
            hasPositionX || hasPositionY || hasRotation ||
            hasScaleX || hasScaleY || hasSortingOrder;

        public void MergeOnto(
            Vector2 position,
            float rotation,
            Vector2 scale,
            int sortingOrder,
            out Vector2 mergedPosition,
            out float mergedRotation,
            out Vector2 mergedScale,
            out int mergedSortingOrder)
        {
            mergedPosition = position;
            mergedRotation = rotation;
            mergedScale = scale;
            mergedSortingOrder = sortingOrder;

            if (hasPositionX) mergedPosition.x = positionX;
            if (hasPositionY) mergedPosition.y = positionY;
            if (hasRotation) mergedRotation = rotationDegrees;
            if (hasScaleX) mergedScale.x = scaleX;
            if (hasScaleY) mergedScale.y = scaleY;
            if (hasSortingOrder) mergedSortingOrder = sortingOrder;
        }

        public static TileTransformPatchRecord Full(
            float x,
            float y,
            float rotationDegrees,
            float scaleX,
            float scaleY,
            int sortingOrder)
        {
            return new TileTransformPatchRecord
            {
                hasPositionX = true,
                positionX = x,
                hasPositionY = true,
                positionY = y,
                hasRotation = true,
                rotationDegrees = rotationDegrees,
                hasScaleX = true,
                scaleX = scaleX,
                hasScaleY = true,
                scaleY = scaleY,
                hasSortingOrder = true,
                sortingOrder = sortingOrder
            };
        }
    }

    [Serializable]
    public struct ParticleEmitterRecord
    {
        public bool enabled;
        public float duration;
        public int emission;

        public ParticleFloatRange lifetime;
        public ParticleFloatRange angle;
        public ParticleFloatRange speed;
        public ParticleFloatRange accelRad;
        public ParticleFloatRange accelTan;

        public float posVarX;
        public float posVarY;
        public float gravityX;
        public float gravityY;

        public ParticleFloatRange startSize;
        public ParticleFloatRange endSize;
        public ParticleFloatRange startSpin;
        public ParticleFloatRange endSpin;

        public ParticleFloatRange startR;
        public ParticleFloatRange startG;
        public ParticleFloatRange startB;
        public ParticleFloatRange startA;
        public ParticleFloatRange endR;
        public ParticleFloatRange endG;
        public ParticleFloatRange endB;
        public ParticleFloatRange endA;

        public string textureId;
    }

    [Serializable]
    public struct ParticleFloatRange
    {
        public float value;
        public float range;
    }

    [Serializable]
    public struct TileLinkGroupRecord
    {
        public TilePositionRecord[] tiles;
    }

    [Serializable]
    public struct TilePositionRecord
    {
        public float x;
        public float y;
    }
}
