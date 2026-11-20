using UnityEngine;

namespace KaplaoGames.Server
{
    /// <summary>
    /// Marks a smaller secondary runner collider that kills the runner
    /// when a solid block overlaps it.
    /// </summary>
    [AddComponentMenu("Kaplao/Runner Kill Hitbox")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public class RunnerKillHitbox : MonoBehaviour
    {
    }
}
