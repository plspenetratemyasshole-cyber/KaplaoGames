using KaplaoGames.Client;
using KaplaoGames.Network;
using KaplaoGames.Shared;
using Mirror;
using UnityEngine;
using UnityEngine.Serialization;

namespace KaplaoGames.Server
{
    /// <summary>
    /// Runner movement driven by the owning client. Position syncs via client-authoritative NetworkTransform.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(NetworkIdentity))]
    [RequireComponent(typeof(NetworkTransformReliable))]
    public class RunnerMovementServer : NetworkBehaviour
    {
        [Header("Movement")]
        [FormerlySerializedAs("moveSpeed")]
        [SerializeField] float maxRunSpeed = 8f;
        [SerializeField] float groundAcceleration = 28f;
        [SerializeField] float airAcceleration = 16f;
        [FormerlySerializedAs("noInputDeceleration")]
        [SerializeField] float groundDeceleration = 22f;
        [SerializeField] float airDeceleration = 10f;
        [FormerlySerializedAs("jumpForce")]
        [SerializeField] float jumpSpeed = 10f;
        [SerializeField] float doubleJumpSpeed = 9f;
        [SerializeField] int maxAirJumps = 1;
        [SerializeField] float doubleJumpDebounceSeconds = 0.05f;
        [SerializeField] float jumpReleaseDebounceSeconds = 0.05f;
        [Tooltip("Max horizontal speed that can be reached when jumping (separate from ground walk max).")]
        [SerializeField] float maxJumpHorizontalSpeed = 10.8f;
        [SerializeField] float jumpHorizontalBoost = 3.5f;
        [SerializeField] float jumpMomentumCapMultiplier = 1.35f;
        [SerializeField] float fastFallGravityMultiplier = 2.5f;
        [SerializeField] float fastFallJumpCutMultiplier = 0.5f;

        [Header("Ground Check")]
        [SerializeField] float groundCheckDistance = 0.08f;
        [SerializeField] float groundNormalMinY = 0.55f;
        [SerializeField] float groundRayInset = 0.06f;
        [SerializeField] LayerMask groundMask = ~0;
        [Tooltip("Kill residual upward velocity from physics contacts while grounded (prevents landing bounce).")]
        [SerializeField] float groundedBounceSuppressSpeed = 2.5f;

        [Header("Landing FX")]
        [SerializeField] float landingFxMinFallSpeed = 1.5f;
        [SerializeField] int landingParticleCount = 10;

        [Header("Walk FX")]
        [SerializeField] float walkFxMinSpeed = 2.5f;
        [SerializeField] float walkFxInterval = 0.09f;
        [SerializeField] int walkParticleCount = 2;

        [Header("Walls")]
        [SerializeField] float wallNormalMinX = 0.55f;

        [Header("Death")]
        [SerializeField] GameObject deathParticlePrefab;
        [SerializeField] float deathParticleLifetime = 4f;
        [SerializeField] float deathCorpseLieDuration = 1f;
        [SerializeField] float deathCorpseFadeDuration = 1f;
        [SerializeField] TrailRenderer[] trailsToResetOnRespawn;
        [SerializeField] float killBelowY = -100f;
        [SerializeField] Collider2D crushKillCollider;

        [SyncVar(hook = nameof(OnGrabbedChanged))]
        bool syncedGrabbed;

        [SyncVar]
        Vector2 syncedSpawnPosition = new Vector2(0f, 5f);

        Rigidbody2D rb;
        Collider2D col;
        ContactFilter2D groundFilter;
        readonly Collider2D[] spikeHits = new Collider2D[16];
        readonly Collider2D[] crushHits = new Collider2D[16];
        readonly ContactPoint2D[] contactPoints = new ContactPoint2D[8];
        readonly RaycastHit2D[] groundRayHits = new RaycastHit2D[8];
        readonly RaycastHit2D[] groundSampleHits = new RaycastHit2D[8];

        float horizontalInput;
        bool fastFallInput;
        bool jumpRequested;
        bool jumpInputHeld;
        float baseGravityScale;
        int airJumpsRemaining;
        float respawnInvulnerableUntil;
        float localSpikeDeathCooldownUntil;
        bool localSpikeDeathPredicted;
        bool wasGrounded;
        float previousVelocityY;
        float nextWalkFxTime;
        float doubleJumpLockedUntil;
        bool requireJumpReleaseForAirJump;
        float lastJumpReleaseTime;
        float lastJumpExecutedTime;
        PhysicsMaterial2D noBounceMaterial;
        Collider2D groundCarrierCollider;
        Vector2 groundCarrierLocalPoint;
        Vector2 groundCarrierWorldPoint;

        public bool IsGrabbed => syncedGrabbed;
        public Vector2 SpawnPosition => syncedSpawnPosition;

        public bool IsGroundedNow() => IsGrounded();

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            col = GetComponent<Collider2D>();
            ResolveCrushKillCollider();

            groundFilter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = groundMask,
                useTriggers = false
            };

            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            baseGravityScale = rb.gravityScale;
            ApplyNoBounceMaterial();
            enabled = false;
        }

        void ApplyNoBounceMaterial()
        {
            if (noBounceMaterial == null)
            {
                noBounceMaterial = new PhysicsMaterial2D("RunnerNoBounce")
                {
                    bounciness = 0f,
                    friction = 0f
                };
            }

            if (col != null)
                col.sharedMaterial = noBounceMaterial;
            if (rb != null)
                rb.sharedMaterial = noBounceMaterial;
        }

        void ResolveCrushKillCollider()
        {
            if (crushKillCollider != null)
                return;

            RunnerKillHitbox marker = GetComponentInChildren<RunnerKillHitbox>(true);
            if (marker != null)
                crushKillCollider = marker.GetComponent<Collider2D>();
        }

        public override void OnStartServer()
        {
            syncedSpawnPosition = RunnerSpawnPoint.GetPosition();
            if (Mathf.Approximately(syncedSpawnPosition.x, 0f) && Mathf.Approximately(syncedSpawnPosition.y, 0f))
                syncedSpawnPosition = new Vector2(0f, 5f);
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.simulated = true;
            RunnerCollisionSettings.SetRunnerLayer(gameObject);
        }

        public override void OnStartClient()
        {
            if (isOwned)
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
                enabled = true;
            }
            else
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                enabled = false;
            }

            RunnerCollisionSettings.SetRunnerLayer(gameObject);
        }

        public void SetInput(RunnerInput input)
        {
            if (!isOwned)
                return;

            bool wasJumpHeld = jumpInputHeld;

            horizontalInput = input.horizontal;
            fastFallInput = input.fastFall;

            if (wasJumpHeld && !input.jump)
            {
                lastJumpReleaseTime = Time.time;
                requireJumpReleaseForAirJump = false; // keep for backward-compat behavior; release-timing is primary now.
            }

            if (input.jump && !jumpInputHeld)
                jumpRequested = true;

            jumpInputHeld = input.jump;
        }

        [Server]
        public void SetGrabbed(bool grabbed)
        {
            syncedGrabbed = grabbed;
        }

        [Server]
        public void ServerSetSpawnPoint(float x, float y)
        {
            syncedSpawnPosition = new Vector2(x, y);
        }

        [Server]
        public void ServerSetPosition(Vector2 position)
        {
            ApplyPosition(position);

            if (connectionToClient != null)
                TargetForcePosition(connectionToClient, position);
        }

        [Server]
        public void RespawnAtSpawn(Vector2? deathPosition = null)
        {
            if (Time.time < respawnInvulnerableUntil)
                return;

            Vector2 effectPosition = deathPosition ?? rb.position;
            PerformRespawn(effectPosition, ownerAlreadyPredicted: false);
        }

        public void RequestSuicide()
        {
            if (!isOwned)
                return;

            CmdSuicide();
        }

        [Command]
        void CmdSuicide()
        {
            RespawnAtSpawn(rb != null ? rb.position : (Vector2)transform.position);
        }

        [Command]
        void CmdReportSpikeDeath(Vector2 deathPosition)
        {
            if (Time.time < respawnInvulnerableUntil)
            {
                TargetConfirmSpikeDeath(connectionToClient, syncedSpawnPosition, playEffects: false);
                return;
            }

            // Re-check near the client's reported contact (client-auth movement lags on server).
            if (!ServerHasSpikeNear(deathPosition, 0.85f))
            {
                TargetRejectSpikeDeath(connectionToClient, deathPosition);
                return;
            }

            PerformRespawn(deathPosition, ownerAlreadyPredicted: true);
        }

        [Server]
        void PerformRespawn(Vector2 deathPosition, bool ownerAlreadyPredicted)
        {
            foreach (KaplaoCursorServer cursor in FindObjectsByType<KaplaoCursorServer>(FindObjectsSortMode.None))
                cursor.ReleaseGrabbedRunner(this);

            SetGrabbed(false);

            int characterIndex = 0;
            bool facingRight = true;
            if (TryGetComponent(out RunnerAppearanceClient appearance))
                characterIndex = appearance.CharacterIndex;
            if (TryGetComponent(out RunnerAnimationClient animationClient))
                facingRight = animationClient.FacingRight;

            RpcPlayDeathEffect(deathPosition, characterIndex, facingRight, ownerAlreadyPredicted);
            ServerSetPosition(syncedSpawnPosition);
            RpcOnRespawned(ownerAlreadyPredicted);
            respawnInvulnerableUntil = Time.time + 0.5f;
        }

        [ClientRpc]
        void RpcPlayDeathEffect(Vector2 position, int characterIndex, bool facingRight, bool skipOwner)
        {
            if (skipOwner && isOwned)
                return;

            SpawnDeathCorpseAt(position, characterIndex, facingRight);
        }

        [ClientRpc]
        void RpcOnRespawned(bool skipOwner)
        {
            if (skipOwner && isOwned)
            {
                localSpikeDeathPredicted = false;
                return;
            }

            GetComponent<RunnerAnimationClient>()?.OnRespawned();
            localSpikeDeathPredicted = false;
        }

        [TargetRpc]
        void TargetConfirmSpikeDeath(NetworkConnectionToClient conn, Vector2 spawn, bool playEffects)
        {
            ApplyPosition(spawn);
            if (playEffects)
                GetComponent<RunnerAnimationClient>()?.OnRespawned();
            localSpikeDeathPredicted = false;
            localSpikeDeathCooldownUntil = Time.time + 0.5f;
        }

        [TargetRpc]
        void TargetRejectSpikeDeath(NetworkConnectionToClient conn, Vector2 restorePosition)
        {
            // Desync: keep runner at spawn rather than putting them back onto the hazard.
            localSpikeDeathPredicted = false;
            ApplyPosition(syncedSpawnPosition);
            GetComponent<RunnerAnimationClient>()?.OnRespawned();
            localSpikeDeathCooldownUntil = Time.time + 0.15f;
        }

        void SpawnDeathCorpseAt(Vector2 position, int characterIndex, bool facingRight)
        {
            RunnerDeathCorpseClient.Spawn(
                position,
                characterIndex,
                facingRight,
                ResolveCorpseSortingOrder(),
                deathCorpseLieDuration,
                deathCorpseFadeDuration);

            if (deathParticlePrefab == null)
                return;

            GameObject effect = Instantiate(
                deathParticlePrefab,
                new Vector3(position.x, position.y, 0f),
                Quaternion.Euler(-90f, 0f, 0f));

            Destroy(effect, deathParticleLifetime);
        }

        int ResolveCorpseSortingOrder()
        {
            Transform visual = transform.Find("Visual");
            if (visual != null && visual.TryGetComponent(out SpriteRenderer spriteRenderer))
                return spriteRenderer.sortingOrder;

            return 0;
        }

        void PlayDeathEffectLocal(Vector2 position)
        {
            int characterIndex = 0;
            bool facingRight = true;

            if (TryGetComponent(out RunnerAppearanceClient appearance))
                characterIndex = appearance.CharacterIndex;

            if (TryGetComponent(out RunnerAnimationClient animationClient))
                facingRight = animationClient.FacingRight;

            SpawnDeathCorpseAt(position, characterIndex, facingRight);
        }

        [Server]
        public void ServerApplyThrowVelocity(Vector2 velocity)
        {
            if (connectionToClient != null)
                TargetApplyThrowVelocity(connectionToClient, velocity);

            ApplyThrowVelocity(velocity);
        }

        [TargetRpc]
        void TargetForcePosition(NetworkConnectionToClient conn, Vector2 position)
        {
            ApplyPosition(position);
        }

        [TargetRpc]
        void TargetApplyThrowVelocity(NetworkConnectionToClient conn, Vector2 velocity)
        {
            ApplyThrowVelocity(velocity);
        }

        void ApplyPosition(Vector2 position)
        {
            SetTrailsEmitting(false);
            rb.position = position;
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = baseGravityScale;
            airJumpsRemaining = maxAirJumps;
            wasGrounded = false;
            previousVelocityY = 0f;
            jumpRequested = false;
            jumpInputHeld = false;
            doubleJumpLockedUntil = 0f;
            requireJumpReleaseForAirJump = false;
            lastJumpReleaseTime = -Mathf.Infinity;
            lastJumpExecutedTime = -Mathf.Infinity;
            ClearGroundCarrier();
            SetTrailsEmitting(true);
        }

        void SetTrailsEmitting(bool enabledState)
        {
            if (trailsToResetOnRespawn == null || trailsToResetOnRespawn.Length == 0)
                return;

            for (int i = 0; i < trailsToResetOnRespawn.Length; i++)
            {
                TrailRenderer trail = trailsToResetOnRespawn[i];
                if (trail == null)
                    continue;

                trail.emitting = enabledState;
                if (!enabledState)
                    trail.Clear();
            }
        }

        void ApplyThrowVelocity(Vector2 velocity)
        {
            if (!isOwned)
                return;

            rb.linearVelocity = velocity;
            jumpRequested = false;
            airJumpsRemaining = maxAirJumps;
            requireJumpReleaseForAirJump = false;
        }

        void OnGrabbedChanged(bool oldGrabbed, bool grabbed)
        {
            if (!isOwned)
                return;

            if (grabbed)
                rb.linearVelocity = Vector2.zero;
        }

        void FixedUpdate()
        {
            if (isServer)
            {
                TryKillIfCrushedBySolidTile();
                TryKillIfBelowVoid();
            }

            if (!isOwned)
                return;

            if (RunnerCheatsState.IsKaplaoFlyActive)
                return;

            TryReportSpikeDeath();
            TryReportVoidDeath();

            if (localSpikeDeathPredicted)
                return;

            if (syncedGrabbed)
            {
                ClearGroundCarrier();
                return;
            }

            ApplyGroundCarrierMotion();

            bool grounded = IsGrounded();
            Vector2 velocity = rb.linearVelocity;

            // Landing: fall → ground. Own FX immediately; others via Rpc (exclude owner).
            if (!wasGrounded && grounded && previousVelocityY <= -landingFxMinFallSpeed)
                TriggerLandingFx();

            // Physics contacts often leave a tiny upward rebound — kill it while grounded
            // (before jump so a real jump can still set positive Y this frame).
            if (grounded && velocity.y <= groundedBounceSuppressSpeed)
                velocity.y = 0f;

            if (grounded && velocity.y <= 0.05f)
                airJumpsRemaining = maxAirJumps;

            ApplyHorizontalMovement(ref velocity, grounded);
            ApplyJump(ref velocity, grounded);
            ApplyFastFall(ref velocity, grounded);
            rb.linearVelocity = velocity;

            if (grounded)
                TryEmitWalkFx(velocity);

            UpdateGroundCarrier(grounded);
            wasGrounded = grounded;
            previousVelocityY = rb.linearVelocity.y;
        }

        void ApplyGroundCarrierMotion()
        {
            if (groundCarrierCollider == null)
                return;

            if (!TryGetCarrierWorldPoint(groundCarrierCollider, groundCarrierLocalPoint, out Vector2 nextWorldPoint))
            {
                ClearGroundCarrier();
                return;
            }

            Vector2 delta = nextWorldPoint - groundCarrierWorldPoint;
            if (delta.sqrMagnitude > 0.0000001f)
                rb.position += delta;

            groundCarrierWorldPoint = nextWorldPoint;
        }

        void UpdateGroundCarrier(bool grounded)
        {
            if (!grounded || !TryGetGroundSupport(out Collider2D support, out Vector2 supportPoint))
            {
                ClearGroundCarrier();
                return;
            }

            groundCarrierCollider = support;
            groundCarrierLocalPoint = support.transform.InverseTransformPoint(supportPoint);
            groundCarrierWorldPoint = supportPoint;
        }

        void ClearGroundCarrier()
        {
            groundCarrierCollider = null;
            groundCarrierLocalPoint = Vector2.zero;
            groundCarrierWorldPoint = Vector2.zero;
        }

        static bool TryGetCarrierWorldPoint(Collider2D carrier, Vector2 localPoint, out Vector2 worldPoint)
        {
            worldPoint = Vector2.zero;
            if (carrier == null || !carrier.enabled)
                return false;

            worldPoint = carrier.transform.TransformPoint(localPoint);
            return true;
        }

        void TryEmitWalkFx(Vector2 velocity)
        {
            if (Mathf.Abs(velocity.x) < walkFxMinSpeed || Mathf.Abs(horizontalInput) < 0.01f)
                return;

            if (Time.time < nextWalkFxTime)
                return;

            nextWalkFxTime = Time.time + walkFxInterval;
            Vector2 feet = GetFeetWorldPosition();
            Color32 color = SampleGroundColor(feet);
            float moveSign = Mathf.Sign(velocity.x);
            RunnerDustBurst.SpawnWalk(feet, color, moveSign, walkParticleCount);
            CmdPlayWalkFx(feet, color, moveSign);
        }

        void TriggerLandingFx()
        {
            Vector2 feet = GetFeetWorldPosition();
            Color32 color = SampleGroundColor(feet);
            RunnerDustBurst.SpawnLanding(feet, color, landingParticleCount);
            CmdPlayLandingFx(feet, color);
        }

        [Command]
        void CmdPlayLandingFx(Vector2 position, Color32 color)
        {
            RpcPlayLandingFx(position, color);
        }

        [ClientRpc(includeOwner = false)]
        void RpcPlayLandingFx(Vector2 position, Color32 color)
        {
            RunnerDustBurst.SpawnLanding(position, color, landingParticleCount);
        }

        [Command]
        void CmdPlayWalkFx(Vector2 position, Color32 color, float moveSign)
        {
            RpcPlayWalkFx(position, color, moveSign);
        }

        [ClientRpc(includeOwner = false)]
        void RpcPlayWalkFx(Vector2 position, Color32 color, float moveSign)
        {
            RunnerDustBurst.SpawnWalk(position, color, moveSign, walkParticleCount);
        }

        Vector2 GetFeetWorldPosition()
        {
            if (col != null)
                return new Vector2(col.bounds.center.x, col.bounds.min.y);
            return rb.position;
        }

        Color32 SampleGroundColor(Vector2 feet)
        {
            float halfWidth = col != null ? col.bounds.extents.x * 0.8f : 0.2f;
            float distance = groundCheckDistance + 0.55f;

            // Cast multiple rays across the runner's width to handle edge landings.
            for (int r = 0; r < 3; r++)
            {
                float offsetX = (r - 1) * halfWidth; // -halfWidth, 0, +halfWidth
                Vector2 origin = feet + new Vector2(offsetX, 0.02f);
                int hitCount = Physics2D.Raycast(origin, Vector2.down, groundFilter, groundSampleHits, distance);

                for (int i = 0; i < hitCount; i++)
                {
                    Collider2D hitCol = groundSampleHits[i].collider;
                    if (hitCol == null || hitCol == col || hitCol.transform.IsChildOf(transform))
                        continue;

                    LevelTileServer tile = hitCol.GetComponentInParent<LevelTileServer>();
                    if (tile == null || tile.IsTrigger || tile.IsParticle)
                        continue;

                    return ResolveTileDustColor(tile);
                }
            }

            // Fallback: overlap multiple points under the feet.
            for (int p = 0; p < 3; p++)
            {
                float offsetX = (p - 1) * halfWidth;
                Collider2D[] overlaps = Physics2D.OverlapPointAll(feet + new Vector2(offsetX, -0.04f), groundMask);
                for (int i = 0; i < overlaps.Length; i++)
                {
                    Collider2D hitCol = overlaps[i];
                    if (hitCol == null || hitCol == col || hitCol.isTrigger || hitCol.transform.IsChildOf(transform))
                        continue;

                    LevelTileServer tile = hitCol.GetComponentInParent<LevelTileServer>();
                    if (tile == null || tile.IsTrigger || tile.IsParticle)
                        continue;

                    return ResolveTileDustColor(tile);
                }
            }

            return new Color32(180, 180, 180, 255);
        }

        static Color32 ResolveTileDustColor(LevelTileServer tile)
        {
            Color32 tint = tile.TileColor;
            tint.a = 255;

            SpriteRenderer sr = tile.GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null)
                return tint;

            if (!TryAverageSpriteColor(sr.sprite, out Color32 spriteAvg))
                return tint;

            // placeColor is often white — multiply so textured blocks get their real look.
            return new Color32(
                (byte)(tint.r * spriteAvg.r / 255),
                (byte)(tint.g * spriteAvg.g / 255),
                (byte)(tint.b * spriteAvg.b / 255),
                255);
        }

        static bool TryAverageSpriteColor(Sprite sprite, out Color32 average)
        {
            average = default;
            if (sprite == null)
                return false;

            Texture2D texture = sprite.texture;
            if (texture == null || !texture.isReadable)
                return false;

            Rect rect = sprite.textureRect;
            int x = Mathf.FloorToInt(rect.x);
            int y = Mathf.FloorToInt(rect.y);
            int w = Mathf.Max(1, Mathf.FloorToInt(rect.width));
            int h = Mathf.Max(1, Mathf.FloorToInt(rect.height));

            // Prefer lower half of the sprite (ground contact surface).
            int sampleY = y + Mathf.Clamp(Mathf.FloorToInt(h * 0.15f), 0, h - 1);
            int sampleH = Mathf.Max(1, h / 3);
            Color[] pixels;
            try
            {
                pixels = texture.GetPixels(x, sampleY, w, sampleH);
            }
            catch
            {
                return false;
            }

            float r = 0f, g = 0f, b = 0f;
            int count = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                Color p = pixels[i];
                if (p.a < 0.15f)
                    continue;

                r += p.r;
                g += p.g;
                b += p.b;
                count++;
            }

            if (count == 0)
                return false;

            float inv = 1f / count;
            average = new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(r * inv * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(g * inv * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(b * inv * 255f), 0, 255),
                255);
            return true;
        }

        void ApplyHorizontalMovement(ref Vector2 velocity, bool grounded)
        {
            bool hasInput = Mathf.Abs(horizontalInput) > 0.01f;
            float targetSpeedX = hasInput ? horizontalInput * maxRunSpeed : 0f;
            float rate = hasInput
                ? (grounded ? groundAcceleration : airAcceleration)
                : (grounded ? groundDeceleration : airDeceleration);

            // If we're grounded and already faster than ground max (e.g. from a jump),
            // don't snap back to maxRunSpeed while holding input in the same direction.
            // With no input, fall through to targetSpeedX = 0 so ground deceleration applies.
            if (grounded && Mathf.Abs(velocity.x) > maxRunSpeed + 0.001f)
            {
                if (hasInput && Mathf.Sign(targetSpeedX) == Mathf.Sign(velocity.x))
                    targetSpeedX = velocity.x;
            }

            // Same air rule: preserve excess horizontal speed only while steering the same way.
            if (!grounded && Mathf.Abs(velocity.x) > maxRunSpeed + 0.001f)
            {
                if (hasInput && Mathf.Sign(targetSpeedX) == Mathf.Sign(velocity.x))
                    targetSpeedX = velocity.x;
            }

            velocity.x = Mathf.MoveTowards(velocity.x, targetSpeedX, rate * Time.fixedDeltaTime);
            BlockMovementIntoWalls(ref velocity.x);
        }

        void ApplyFastFall(ref Vector2 velocity, bool grounded)
        {
            if (grounded || !fastFallInput)
            {
                rb.gravityScale = baseGravityScale;
                return;
            }

            if (velocity.y > 0f)
                velocity.y *= fastFallJumpCutMultiplier;

            rb.gravityScale = baseGravityScale * fastFallGravityMultiplier;
        }

        void ApplyJump(ref Vector2 velocity, bool grounded)
        {
            if (!jumpRequested)
                return;

            if (grounded)
            {
                // If contacts briefly report grounded right after a jump (edge jitter),
                // block any repeat jump for a short time.
                if (Time.time < doubleJumpLockedUntil)
                {
                    jumpRequested = false;
                    return;
                }

                velocity.y = jumpSpeed;
                ApplyJumpHorizontalBoost(ref velocity);
                airJumpsRemaining = maxAirJumps;
                doubleJumpLockedUntil = Time.time + Mathf.Max(0f, doubleJumpDebounceSeconds);
                requireJumpReleaseForAirJump = true;
                lastJumpExecutedTime = Time.time;
                jumpRequested = false;
                return;
            }

            if (Time.time < doubleJumpLockedUntil)
            {
                jumpRequested = false;
                return;
            }

            // Allow air-jump only if the player really released the button
            // after the previous jump, and release lasted long enough.
            if (lastJumpReleaseTime < lastJumpExecutedTime ||
                Time.time - lastJumpReleaseTime < jumpReleaseDebounceSeconds)
            {
                jumpRequested = false;
                return;
            }

            if (airJumpsRemaining <= 0)
                return;

            velocity.y = doubleJumpSpeed;
            ApplyJumpHorizontalBoost(ref velocity, 0.65f);
            airJumpsRemaining--;
            jumpRequested = false;
            requireJumpReleaseForAirJump = true;
            lastJumpExecutedTime = Time.time;
            GetComponent<RunnerAnimationClient>()?.PlayJump();
        }

        void ApplyJumpHorizontalBoost(ref Vector2 velocity, float multiplier = 1f)
        {
            if (Mathf.Abs(horizontalInput) < 0.01f)
                return;

            float boost = jumpHorizontalBoost * multiplier;
            float cap = maxJumpHorizontalSpeed;
            float inputSign = Mathf.Sign(horizontalInput);
            float curSign = Mathf.Sign(velocity.x);
            float curAbs = Mathf.Abs(velocity.x);

            // Old behavior: if input asks for opposite direction during a jump boost,
            // flip direction instantly without losing speed magnitude.
            if (curAbs > 0.05f && curSign != 0f && inputSign != 0f && curSign != inputSign)
            {
                // Flip to the direction the player is currently asking for.
                velocity.x = inputSign * curAbs;
                return;
            }

            // Don't clamp/snap down if we're already above cap and still steering the same way.
            if (curAbs > cap + 0.001f && (curSign == inputSign || curAbs < 0.05f))
                return;

            // Normal case: steer velocity toward the jump-cap in the chosen direction.
            float targetSpeed = inputSign * cap;
            velocity.x = Mathf.MoveTowards(velocity.x, targetSpeed, boost);

            // Enforce cap so jump acceleration can't exceed it.
            velocity.x = Mathf.Clamp(velocity.x, -cap, cap);
        }

        void BlockMovementIntoWalls(ref float velocityX)
        {
            int count = col.GetContacts(groundFilter, contactPoints);
            for (int i = 0; i < count; i++)
            {
                Vector2 normal = contactPoints[i].normal;
                if (Mathf.Abs(normal.x) < wallNormalMinX)
                    continue;

                if (velocityX * normal.x < -0.01f)
                    velocityX = 0f;
            }
        }

        void TryReportSpikeDeath()
        {
            if (Time.time < localSpikeDeathCooldownUntil || localSpikeDeathPredicted)
                return;

            if (!IsTouchingSpike())
                return;

            Vector2 deathPosition = rb.position;
            localSpikeDeathCooldownUntil = Time.time + 0.5f;
            PredictSpikeDeathLocally(deathPosition);
            CmdReportSpikeDeath(deathPosition);
        }

        void TryReportVoidDeath()
        {
            if (Time.time < localSpikeDeathCooldownUntil || localSpikeDeathPredicted)
                return;

            if (rb.position.y >= killBelowY)
                return;

            Vector2 deathPosition = rb.position;
            localSpikeDeathCooldownUntil = Time.time + 0.5f;
            PredictSpikeDeathLocally(deathPosition);
            CmdReportVoidDeath(deathPosition);
        }

        [Server]
        void TryKillIfCrushedBySolidTile()
        {
            if (Time.time < respawnInvulnerableUntil)
                return;

            ResolveCrushKillCollider();
            if (crushKillCollider == null || !crushKillCollider.enabled)
                return;

            var filter = new ContactFilter2D
            {
                useTriggers = false
            };
            filter.NoFilter();

            int count = crushKillCollider.Overlap(filter, crushHits);
            for (int i = 0; i < count; i++)
            {
                Collider2D hit = crushHits[i];
                if (IsBlockingTileCollider(hit))
                {
                    RespawnAtSpawn(crushKillCollider.bounds.center);
                    return;
                }
            }
        }

        [Server]
        bool IsBlockingTileCollider(Collider2D hit)
        {
            if (hit == null || hit == col || hit == crushKillCollider)
                return false;

            if (hit.transform.IsChildOf(transform) || IsRunnerCollider(hit))
                return false;

            LevelTileServer tile = hit.GetComponentInParent<LevelTileServer>();
            if (tile == null)
                return false;

            return !tile.IsTrigger &&
                   !tile.IsParticle &&
                   !tile.IsSpike &&
                   !tile.IsKeyframe &&
                   !tile.IsExecutor &&
                   !tile.HasNoHitbox;
        }

        [Server]
        void TryKillIfBelowVoid()
        {
            if (Time.time < respawnInvulnerableUntil)
                return;

            float y = rb != null ? rb.position.y : transform.position.y;
            if (y >= killBelowY)
                return;

            RespawnAtSpawn(rb != null ? rb.position : (Vector2)transform.position);
        }

        [Command]
        void CmdReportVoidDeath(Vector2 deathPosition)
        {
            if (Time.time < respawnInvulnerableUntil)
            {
                TargetConfirmSpikeDeath(connectionToClient, syncedSpawnPosition, playEffects: false);
                return;
            }

            float serverY = rb != null ? rb.position.y : transform.position.y;
            if (deathPosition.y >= killBelowY && serverY >= killBelowY)
                return;

            PerformRespawn(deathPosition, ownerAlreadyPredicted: true);
        }

        void PredictSpikeDeathLocally(Vector2 deathPosition)
        {
            localSpikeDeathPredicted = true;
            jumpRequested = false;
            requireJumpReleaseForAirJump = false;
            horizontalInput = 0f;
            fastFallInput = false;
            PlayDeathEffectLocal(deathPosition);
            ApplyPosition(syncedSpawnPosition);
            GetComponent<RunnerAnimationClient>()?.OnRespawned();
        }

        [Server]
        bool ServerHasSpikeNear(Vector2 position, float radius)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(position, radius);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null || hit == col)
                    continue;

                if (hit.TryGetComponent(out LevelTileServer tile) && tile.IsSpike)
                    return true;

                LevelTileServer parentTile = hit.GetComponentInParent<LevelTileServer>();
                if (parentTile != null && parentTile.IsSpike)
                    return true;
            }

            return false;
        }

        bool IsTouchingSpike()
        {
            // Include both trigger and solid spike colliders (legacy solid spikes / race frames).
            var filter = new ContactFilter2D { useTriggers = true };
            filter.NoFilter();
            int count = col.Overlap(filter, spikeHits);
            for (int i = 0; i < count; i++)
            {
                if (IsSpikeCollider(spikeHits[i]))
                    return true;
            }

            // Catch near-miss tip grazes that ground raycasts used to treat as jumpable floor.
            Bounds bounds = col.bounds;
            float pad = 0.06f;
            Vector2 point = bounds.center;
            Vector2 size = (Vector2)bounds.size + new Vector2(pad * 2f, pad * 2f);
            count = Physics2D.OverlapBox(point, size, 0f, filter, spikeHits);
            for (int i = 0; i < count; i++)
            {
                if (IsSpikeCollider(spikeHits[i]))
                    return true;
            }

            return false;
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

        bool TryGetGroundSupport(out Collider2D support, out Vector2 supportPoint)
        {
            if (TryGetGroundSupportByContacts(out support, out supportPoint))
                return true;

            return TryGetGroundSupportByRaycasts(out support, out supportPoint);
        }

        bool IsGrounded()
        {
            if (IsGroundedByContacts())
                return true;

            return IsGroundedByRaycasts();
        }

        bool IsGroundedByContacts()
        {
            return TryGetGroundSupportByContacts(out _, out _);
        }

        bool TryGetGroundSupportByContacts(out Collider2D support, out Vector2 supportPoint)
        {
            support = null;
            supportPoint = Vector2.zero;

            int count = col.GetContacts(groundFilter, contactPoints);
            for (int i = 0; i < count; i++)
            {
                Collider2D other = contactPoints[i].collider;
                if (!IsValidGroundCollider(other))
                    continue;

                if (contactPoints[i].normal.y < groundNormalMinY)
                    continue;

                support = other;
                supportPoint = contactPoints[i].point;
                return true;
            }

            return false;
        }

        bool IsGroundedByRaycasts()
        {
            return TryGetGroundSupportByRaycasts(out _, out _);
        }

        bool TryGetGroundSupportByRaycasts(out Collider2D support, out Vector2 supportPoint)
        {
            support = null;
            supportPoint = Vector2.zero;

            Bounds bounds = col.bounds;
            float originY = bounds.min.y + 0.01f;
            float castDistance = groundCheckDistance + 0.02f;
            float leftX = bounds.min.x + groundRayInset;
            float rightX = bounds.max.x - groundRayInset;
            float centerX = bounds.center.x;

            return GroundRaycastHit(centerX, originY, castDistance, out support, out supportPoint) ||
                   GroundRaycastHit(leftX, originY, castDistance, out support, out supportPoint) ||
                   GroundRaycastHit(rightX, originY, castDistance, out support, out supportPoint);
        }

        bool GroundRaycastHit(float originX, float originY, float castDistance, out Collider2D support, out Vector2 supportPoint)
        {
            support = null;
            supportPoint = Vector2.zero;

            int hitCount = Physics2D.Raycast(
                new Vector2(originX, originY),
                Vector2.down,
                groundFilter,
                groundRayHits,
                castDistance);

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit2D hit = groundRayHits[i];
                Collider2D hitCol = hit.collider;
                if (hitCol == null || hitCol.isTrigger)
                    continue;

                if (!IsValidGroundCollider(hitCol))
                    continue;

                if (hit.normal.y >= groundNormalMinY)
                {
                    support = hitCol;
                    supportPoint = hit.point;
                    return true;
                }
            }

            return false;
        }

        bool IsValidGroundCollider(Collider2D other)
        {
            if (other == null || other == col)
                return false;

            if (other.transform.IsChildOf(transform))
                return false;

            // Other runners are never walkable ground.
            if (IsRunnerCollider(other))
                return false;

            LevelTileServer tile = other.GetComponentInParent<LevelTileServer>();
            if (tile != null && (tile.IsTrigger || tile.IsParticle || tile.IsSpike))
                return false;

            return true;
        }

        static bool IsRunnerCollider(Collider2D other)
        {
            if (other == null)
                return false;

            if (other.GetComponentInParent<RunnerMovementServer>() != null)
                return true;

            return other.GetComponentInParent<NetworkPlayer>() is NetworkPlayer player &&
                   player.role == PlayerRole.Runner;
        }
    }
}
