using KaplaoGames.Server;
using Mirror;
using UnityEngine;

namespace KaplaoGames.Client
{
    /// <summary>
    /// Drives runner sprite animations on the owning client.
    /// Parameters replicate to other peers via NetworkAnimator.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RunnerMovementServer))]
    [RequireComponent(typeof(NetworkAnimator))]
    public class RunnerAnimationClient : NetworkBehaviour
    {
        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int GroundedId = Animator.StringToHash("Grounded");
        static readonly int GrabbedId = Animator.StringToHash("Grabbed");
        static readonly int DeathId = Animator.StringToHash("Death");
        static readonly int JumpId = Animator.StringToHash("Jump");

        [SerializeField] Transform visualRoot;
        [SerializeField] Animator animator;
        [SerializeField] float flipThreshold = 0.05f;

        [SyncVar(hook = nameof(OnFacingChanged))]
        bool syncedFacingRight = true;

        RunnerMovementServer movement;
        Rigidbody2D rb;
        NetworkAnimator networkAnimator;
        bool deathPlaying;
        bool localFacingRight = true;

        public bool FacingRight => isOwned ? localFacingRight : syncedFacingRight;

        void Awake()
        {
            movement = GetComponent<RunnerMovementServer>();
            rb = GetComponent<Rigidbody2D>();
            networkAnimator = GetComponent<NetworkAnimator>();
            ResolveVisualReferences();
        }

        void ResolveVisualReferences()
        {
            if (visualRoot == null)
            {
                Transform child = transform.Find("Visual");
                if (child != null)
                    visualRoot = child;
            }

            if (animator == null && visualRoot != null)
                animator = visualRoot.GetComponent<Animator>();
        }

        void LateUpdate()
        {
            if (animator == null || rb == null || movement == null)
                return;

            if (deathPlaying)
                return;

            // Remote clients receive animator parameters from NetworkAnimator.
            if (!isOwned)
                return;

            bool grounded = movement.IsGroundedNow();
            bool grabbed = movement.IsGrabbed;
            float horizontalVelocity = rb.linearVelocity.x;
            float speed = Mathf.Abs(horizontalVelocity);

            animator.SetBool(GrabbedId, grabbed);
            animator.SetBool(GroundedId, grounded);
            animator.SetFloat(SpeedId, grabbed ? 0f : speed);

            if (visualRoot != null && !grabbed && speed > flipThreshold)
            {
                bool facingRight = horizontalVelocity >= 0f;
                if (facingRight != localFacingRight)
                {
                    localFacingRight = facingRight;
                    CmdSetFacing(facingRight);
                }

                ApplyFacing(facingRight);
            }
        }

        [Command]
        void CmdSetFacing(bool facingRight)
        {
            syncedFacingRight = facingRight;
        }

        void OnFacingChanged(bool oldFacing, bool facingRight)
        {
            if (isOwned)
                return;

            ApplyFacing(facingRight);
        }

        void ApplyFacing(bool facingRight)
        {
            if (visualRoot == null)
                return;

            Vector3 scale = visualRoot.localScale;
            float magnitude = Mathf.Abs(scale.x);
            if (magnitude < 0.001f)
                magnitude = 1f;

            scale.x = facingRight ? magnitude : -magnitude;
            visualRoot.localScale = scale;
        }

        public void PlayDeath()
        {
            if (animator == null)
                return;

            deathPlaying = true;
            animator.ResetTrigger(DeathId);
            animator.SetTrigger(DeathId);
        }

        public void PlayJump()
        {
            if (animator == null || deathPlaying)
                return;

            if (networkAnimator != null)
            {
                networkAnimator.ResetTrigger(JumpId);
                networkAnimator.SetTrigger(JumpId);
                return;
            }

            animator.ResetTrigger(JumpId);
            animator.SetTrigger(JumpId);
        }

        public void OnRespawned()
        {
            if (animator == null)
                return;

            deathPlaying = false;
            localFacingRight = true;
            animator.ResetTrigger(DeathId);
            animator.Play("Idle", 0, 0f);
            animator.Update(0f);
            ApplyFacing(syncedFacingRight);
        }

#if UNITY_EDITOR
        protected new void OnValidate()
        {
            ResolveVisualReferences();
        }
#endif
    }
}
