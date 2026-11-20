using System.Collections;
using KaplaoGames.Server;
using KaplaoGames.UI;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KaplaoGames.Client
{
    /// <summary>
    /// Pure client-side runner cheats. Does not touch server scripts:
    /// fly reads rb position each physics tick and teleports (no collision);
    /// immortality hides the collider for the physics tick and nudges away from spikes.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-200)]
    public class RunnerCheatsClient : MonoBehaviour
    {
        const float SpikeEscapeMarginRatio = 0.12f / RunnerCheatsState.DefaultSpikePushForce;
        const float SpikePulseCooldownSeconds = 0.12f;

        readonly Collider2D[] spikeHits = new Collider2D[16];

        Rigidbody2D rb;
        Collider2D col;
        bool immortalitySuppressedThisTick;
        Vector2 immortalityEscapePosition;
        Coroutine restoreRoutine;
        float nextSpikePulseTime;

        bool flyPhysicsApplied;
        RigidbodyType2D savedBodyType;
        float savedGravityScale;
        bool savedColliderEnabled;
        Vector2 flyTargetPosition;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            col = GetComponent<Collider2D>();
        }

        void OnDisable()
        {
            RestoreFlyPhysicsIfNeeded();
            RestoreColliderIfNeeded();
            if (restoreRoutine != null)
            {
                StopCoroutine(restoreRoutine);
                restoreRoutine = null;
            }
        }

        void FixedUpdate()
        {
            if (!isActiveAndEnabled || rb == null || col == null)
                return;

            if (!NetworkClient.active || !isOwnedLocalRunner())
                return;

            if (!MobileControlsConfig.IsCheatsEnabled)
            {
                RestoreFlyPhysicsIfNeeded();
                RestoreColliderIfNeeded();
                return;
            }

            if (RunnerCheatsState.IsKaplaoFlyActive)
            {
                ApplyFlyPhysicsMode();
                ApplyFlyStep(Time.fixedDeltaTime);
                return;
            }

            RestoreFlyPhysicsIfNeeded();
            ApplyImmortalityForFixedUpdate();
        }

        void LateUpdate()
        {
            if (!isActiveAndEnabled || rb == null)
                return;

            if (!NetworkClient.active || !isOwnedLocalRunner())
                return;

            if (!RunnerCheatsState.IsKaplaoFlyActive)
                return;

            if (KaplaoModeratedChatUi.IsChatInputFocused)
                return;

            // Until FixedUpdate initializes fly mode, keep current place.
            if (!flyPhysicsApplied)
                flyTargetPosition = rb.position;

            // Re-assert teleport after any other client scripts this frame.
            TeleportTo(flyTargetPosition);
        }

        void ApplyImmortalityForFixedUpdate()
        {
            if (!RunnerCheatsState.Immortality)
            {
                RestoreColliderIfNeeded();
                return;
            }

            if (!TryCollectSpikeColliders(out int spikeCount))
                return;

            // Suppress spike overlap for this physics tick so RunnerMovementServer
            // never reports death — then move slightly away from the hazard.
            Vector2 contactPoint = ComputeSpikeContactPoint(rb.position, spikeCount);
            immortalityEscapePosition = ComputeSpikeEscapePosition(rb.position, spikeCount);
            SpawnSpikeSavePulse(contactPoint);
            TeleportTo(immortalityEscapePosition);

            if (col.enabled)
            {
                col.enabled = false;
                immortalitySuppressedThisTick = true;
            }

            if (restoreRoutine == null)
                restoreRoutine = StartCoroutine(RestoreAfterFixedUpdate());
        }

        void ApplyFlyStep(float deltaTime)
        {
            if (KaplaoModeratedChatUi.IsChatInputFocused)
            {
                flyTargetPosition = rb.position;
                TeleportTo(flyTargetPosition);
                return;
            }

            Vector2 current = rb.position;
            Vector2 move = ReadFlyMove();
            float speed = Mathf.Max(0.01f, RunnerCheatsState.FlySpeed);
            flyTargetPosition = current + move * speed * deltaTime;
            TeleportTo(flyTargetPosition);
        }

        void ApplyFlyPhysicsMode()
        {
            if (flyPhysicsApplied)
                return;

            savedBodyType = rb.bodyType;
            savedGravityScale = rb.gravityScale;
            savedColliderEnabled = col.enabled;

            // Capture current position immediately so LateUpdate cannot snap to (0,0)/spawn.
            flyTargetPosition = rb.position;
            TeleportTo(flyTargetPosition);

            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            col.enabled = false;
            flyPhysicsApplied = true;
        }

        void RestoreFlyPhysicsIfNeeded()
        {
            if (!flyPhysicsApplied)
                return;

            flyPhysicsApplied = false;
            rb.bodyType = savedBodyType;
            rb.gravityScale = savedGravityScale;
            col.enabled = savedColliderEnabled;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            Physics2D.SyncTransforms();
        }

        void TeleportTo(Vector2 position)
        {
            rb.position = position;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            Physics2D.SyncTransforms();
        }

        IEnumerator RestoreAfterFixedUpdate()
        {
            yield return new WaitForFixedUpdate();
            restoreRoutine = null;

            if (!immortalitySuppressedThisTick)
                yield break;

            TeleportTo(immortalityEscapePosition);
            RestoreColliderIfNeeded();
        }

        void RestoreColliderIfNeeded()
        {
            if (!immortalitySuppressedThisTick)
                return;

            immortalitySuppressedThisTick = false;
            if (col != null && !flyPhysicsApplied)
                col.enabled = true;
        }

        void SpawnSpikeSavePulse(Vector2 contactPoint)
        {
            if (Time.unscaledTime < nextSpikePulseTime)
                return;

            nextSpikePulseTime = Time.unscaledTime + SpikePulseCooldownSeconds;
            ImmortalitySpikePulse.Spawn(contactPoint);
        }

        Vector2 ComputeSpikeContactPoint(Vector2 current, int spikeCount)
        {
            Vector2 bestPoint = current;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < spikeCount; i++)
            {
                Collider2D spike = spikeHits[i];
                if (spike == null || spike == col)
                    continue;

                ColliderDistance2D distance = Physics2D.Distance(col, spike);
                if (distance.isValid)
                {
                    Vector2 point = (distance.pointA + distance.pointB) * 0.5f;
                    float absDistance = Mathf.Abs(distance.distance);
                    if (absDistance < bestDistance)
                    {
                        bestDistance = absDistance;
                        bestPoint = point;
                    }
                    continue;
                }

                Vector2 fallback = spike.ClosestPoint(current);
                float fallbackDistance = (fallback - current).sqrMagnitude;
                if (fallbackDistance < bestDistance)
                {
                    bestDistance = fallbackDistance;
                    bestPoint = fallback;
                }
            }

            return bestPoint;
        }

        Vector2 ComputeSpikeEscapePosition(Vector2 current, int spikeCount)
        {
            float pushForce = Mathf.Max(0.01f, RunnerCheatsState.SpikePushForce);
            float pushMargin = pushForce * SpikeEscapeMarginRatio;

            Vector2 separation = Vector2.zero;
            for (int i = 0; i < spikeCount; i++)
            {
                Collider2D spike = spikeHits[i];
                if (spike == null || spike == col)
                    continue;

                ColliderDistance2D distance = Physics2D.Distance(col, spike);
                if (distance.isValid)
                {
                    float push = Mathf.Max(0f, -distance.distance) + pushMargin;
                    if (push > 0.001f)
                    {
                        separation += distance.normal * push;
                        continue;
                    }
                }

                Vector2 awayFromSpike = current - (Vector2)spike.bounds.center;
                if (awayFromSpike.sqrMagnitude < 0.0001f)
                    awayFromSpike = Vector2.up;

                separation += awayFromSpike.normalized * pushForce;
            }

            if (separation.sqrMagnitude < 0.0001f)
                separation = Vector2.up * pushForce;

            if (separation.magnitude < pushForce)
                separation = separation.normalized * pushForce;

            return current + separation;
        }

        bool TryCollectSpikeColliders(out int count)
        {
            count = 0;
            if (col == null || !col.enabled)
                return false;

            var filter = new ContactFilter2D { useTriggers = true };
            filter.NoFilter();
            count = col.Overlap(filter, spikeHits);
            count = FilterSpikeColliders(count);

            if (count > 0)
                return true;

            Bounds bounds = col.bounds;
            float pad = 0.06f;
            count = Physics2D.OverlapBox(
                bounds.center,
                (Vector2)bounds.size + new Vector2(pad * 2f, pad * 2f),
                0f,
                filter,
                spikeHits);
            count = FilterSpikeColliders(count);
            return count > 0;
        }

        int FilterSpikeColliders(int count)
        {
            int write = 0;
            for (int i = 0; i < count; i++)
            {
                Collider2D hit = spikeHits[i];
                if (!IsSpikeCollider(hit))
                    continue;

                spikeHits[write++] = hit;
            }

            return write;
        }

        bool isOwnedLocalRunner()
        {
            if (!TryGetComponent(out NetworkIdentity identity) || !identity.isOwned)
                return false;

            return TryGetComponent(out Network.NetworkPlayer player) &&
                   player.role == Shared.PlayerRole.Runner;
        }

        bool IsSpikeCollider(Collider2D hit)
        {
            if (hit == null || hit == col)
                return false;

            if (hit.TryGetComponent(out LevelTileServer tile) && tile.IsSpike)
                return true;

            LevelTileServer parentTile = hit.GetComponentInParent<LevelTileServer>();
            return parentTile != null && parentTile.IsSpike;
        }

        static Vector2 ReadFlyMove()
        {
            float x = 0f;
            float y = 0f;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) y -= 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) y += 1f;
            }

            if (MobileControlsConfig.IsMobileControlsEnabled)
            {
                if (Mathf.Abs(x) <= 0.01f)
                    x = MobileInputState.RunnerHorizontal;
                if (Mathf.Abs(y) <= 0.01f)
                    y = MobileInputState.RunnerVertical;
            }

            Vector2 move = new Vector2(x, y);
            if (move.sqrMagnitude > 1f)
                move.Normalize();
            return move;
        }
    }
}
