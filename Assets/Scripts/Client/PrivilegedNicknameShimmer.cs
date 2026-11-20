using System.Collections.Generic;
using UnityEngine;

namespace KaplaoGames.Client
{
    /// <summary>
    /// Legacy shimmer — disabled. Nickname effects now use Easy Text Effects via KaplaoNicknameEasyEffectsApplier.
    /// Kept so old scenes/prefabs that still reference the component do not break.
    /// </summary>
    [DisallowMultipleComponent]
    public class PrivilegedNicknameShimmer : MonoBehaviour
    {
        public void Activate(string email, string preset = "none")
        {
            enabled = false;
        }

        public void Deactivate()
        {
            enabled = false;
        }

        void OnEnable()
        {
            enabled = false;
        }
    }
}
