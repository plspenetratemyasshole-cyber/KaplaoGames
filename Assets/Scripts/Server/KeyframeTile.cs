using KaplaoGames.Shared;
using Mirror;
using UnityEngine;

namespace KaplaoGames.Server
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    public class KeyframeTile : NetworkBehaviour
    {
        [SyncVar] float syncedDuration = 1f;
        [SyncVar] string syncedAnim = "linear";
        [SyncVar] int syncedSpawnId;
        [SyncVar] bool syncedCurve;
        [SyncVar] bool syncedCloseLoop;
        [SyncVar] bool syncedDistance;

        public float Duration => syncedDuration;
        public string Anim => syncedAnim;
        public int SpawnId => syncedSpawnId;
        public bool Curve => syncedCurve;
        public bool CloseLoop => syncedCloseLoop;
        public bool Distance => syncedDistance;

        [Server]
        public void ServerApply(KeyframeRecord record)
        {
            syncedDuration = Mathf.Max(0f, record.duration);
            syncedAnim = string.IsNullOrWhiteSpace(record.anim) ? "linear" : record.anim.Trim().ToLowerInvariant();
            syncedSpawnId = record.spawnId;
            syncedCurve = record.curve;
            syncedCloseLoop = record.closeLoop;
            syncedDistance = record.distance;
        }

        [Server]
        public void ServerApplyPatch(KeyframePatchRecord patch)
        {
            if (patch.hasDuration)
                syncedDuration = Mathf.Max(0f, patch.duration);

            if (patch.hasAnim)
                syncedAnim = string.IsNullOrWhiteSpace(patch.anim) ? "linear" : patch.anim.Trim().ToLowerInvariant();

            if (patch.hasSpawnId)
                syncedSpawnId = patch.spawnId;

            if (patch.hasCurve)
                syncedCurve = patch.curve;

            if (patch.hasCloseLoop)
                syncedCloseLoop = patch.closeLoop;

            if (patch.hasDistance)
                syncedDistance = patch.distance;
        }

        public KeyframeRecord ExportRecord() => new KeyframeRecord
        {
            duration = syncedDuration,
            anim = syncedAnim,
            spawnId = syncedSpawnId,
            curve = syncedCurve,
            closeLoop = syncedCloseLoop,
            distance = syncedDistance
        };
    }
}
