using System.Collections.Generic;
using KaplaoGames.Shared;
using UnityEngine;

namespace KaplaoGames.Server
{
    public enum LevelUndoKind : byte
    {
        LevelSnapshot = 0,
        Selection = 1
    }

    public sealed class LevelUndoEntry
    {
        public LevelUndoKind Kind;
        public LevelData LevelSnapshot;
        public uint[] SelectionNetIds;
    }

    public struct ClipboardTileRecord
    {
        public TileType Type;
        public Vector2 OffsetFromCenter;
        public float RotationDegrees;
        public Vector2 Scale;
        public Color32 Color;
        public string Groups;
        public byte QuarterTurns;
        public float Transparency;
        public bool NoHitbox;
        public string TextureId;
        public int SortingOrder;
        public int EditorLayer;
        public ParticleEmitterRecord Particle;
        public bool HasParticle;
    }
}
