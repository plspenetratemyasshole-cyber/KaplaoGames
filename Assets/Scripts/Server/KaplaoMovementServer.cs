using KaplaoGames.Shared;
using Mirror;
using UnityEngine;

namespace KaplaoGames.Server
{
    /// <summary>
    /// Kaplao free-flight movement driven by the owning client.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    [RequireComponent(typeof(NetworkTransformReliable))]
    public class KaplaoMovementServer : NetworkBehaviour
    {
        [Header("Movement")]
        [SerializeField] float moveSpeed = 8f;
        [SerializeField] float shiftSpeedMultiplier = 10f;

        KaplaoInput currentInput;

        public void SetInput(KaplaoInput input)
        {
            if (!isOwned)
                return;

            currentInput = input;
        }

        void Update()
        {
            if (!isOwned)
                return;

            Vector2 direction = new Vector2(currentInput.horizontal, currentInput.vertical);
            if (direction.sqrMagnitude > 1f)
                direction.Normalize();

            float speed = moveSpeed;
            if (currentInput.shiftHeld)
                speed *= shiftSpeedMultiplier;

            transform.position += (Vector3)(direction * (speed * Time.deltaTime));
        }
    }
}
