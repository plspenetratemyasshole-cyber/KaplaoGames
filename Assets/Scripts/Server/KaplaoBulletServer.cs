using KaplaoGames.Network;
using KaplaoGames.Shared;
using Mirror;
using UnityEngine;

namespace KaplaoGames.Server
{
    /// <summary>
    /// Server-authoritative Kaplao bullet. Spawned by KaplaoCursorServer in Gun mode.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    [RequireComponent(typeof(NetworkTransformReliable))]
    public class KaplaoBulletServer : NetworkBehaviour
    {
        [SerializeField] float speed = 48f;
        [SerializeField] float lifetime = 3f;
        [SerializeField] float ownerIgnoreSeconds = 0.15f;
        [SerializeField] float spriteRotationOffset = -90f;

        Vector2 velocity;
        float spawnTime;
        float ignoreOwnerUntil;
        Transform shooterRoot;

        [Server]
        public void ServerInit(Vector2 direction, KaplaoCursorServer owner)
        {
            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector2.right;

            velocity = direction.normalized * speed;
            spawnTime = Time.time;
            ignoreOwnerUntil = spawnTime + ownerIgnoreSeconds;
            shooterRoot = owner != null ? owner.transform : null;

            ApplyRotation();
        }

        [ServerCallback]
        void FixedUpdate()
        {
            if (!isServer)
                return;

            transform.position = (Vector2)transform.position + velocity * Time.fixedDeltaTime;
            ApplyRotation();

            if (Time.time - spawnTime >= lifetime)
                NetworkServer.Destroy(gameObject);
        }

        [ServerCallback]
        void OnTriggerEnter2D(Collider2D other)
        {
            if (!isServer || other == null || ShouldIgnoreCollider(other))
                return;

            HandleHit(other);
            NetworkServer.Destroy(gameObject);
        }

        [Server]
        void ApplyRotation()
        {
            float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg + spriteRotationOffset;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        [Server]
        bool ShouldIgnoreCollider(Collider2D collider)
        {
            if (collider.transform == transform || collider.transform.IsChildOf(transform))
                return true;

            if (Time.time >= ignoreOwnerUntil || shooterRoot == null)
                return false;

            return collider.transform == shooterRoot || collider.transform.IsChildOf(shooterRoot);
        }

        [Server]
        void HandleHit(Collider2D other)
        {
            if (other.GetComponentInParent<NetworkPlayer>() is not NetworkPlayer networkPlayer ||
                networkPlayer.role != PlayerRole.Runner ||
                other.GetComponentInParent<RunnerMovementServer>() is not RunnerMovementServer runner)
            {
                return;
            }

            runner.RespawnAtSpawn(transform.position);
        }
    }
}
