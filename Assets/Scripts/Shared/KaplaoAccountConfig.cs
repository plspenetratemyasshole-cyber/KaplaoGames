using UnityEngine;

namespace KaplaoGames.Shared
{
    /// <summary>
    /// Website URL used for account Sign in. Place on the Main Menu object.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Kaplao/Account/Kaplao Account Config")]
    public class KaplaoAccountConfig : MonoBehaviour
    {
        public static KaplaoAccountConfig Instance { get; private set; }

        [Tooltip("Base URL of KaplaoGamesWebsite (no trailing slash). Local default: http://localhost:3000")]
        [SerializeField] string websiteUrl = "http://localhost:3000";

        [Tooltip("Must match GAME_SERVER_SECRET on the website. Used by host to verify admin status.")]
        [SerializeField] string gameServerSecret = "";

        public string WebsiteUrl
        {
            get
            {
                string url = string.IsNullOrWhiteSpace(websiteUrl) ? "http://localhost:3000" : websiteUrl.Trim();
                return url.TrimEnd('/');
            }
        }

        public string GameServerSecret => gameServerSecret ?? string.Empty;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
