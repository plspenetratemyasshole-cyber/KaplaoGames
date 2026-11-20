using System;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace KaplaoGames.Shared
{
    /// <summary>
    /// Device-code sign-in. While busy, clicking Sign in again forces a poll
    /// (does not create a new device code — that was a common reason login never linked).
    /// </summary>
    public class KaplaoAccountClient : MonoBehaviour
    {
        const string PendingCodeKey = "kaplao.account.pendingDeviceCode";
        const string PendingUrlKey = "kaplao.account.pendingWebsiteUrl";

        public static KaplaoAccountClient Instance { get; private set; }

        public bool IsBusy { get; private set; }
        public string StatusMessage { get; private set; } = string.Empty;
        public string PendingDeviceCode => pollDeviceCode;

        public event Action SignedIn;
        public event Action SignInFailed;
        public event Action SignedOut;
        public event Action StatusChanged;

        int signInGeneration;
        bool previousRunInBackground;

        enum Phase
        {
            Idle,
            Starting,
            Polling
        }

        Phase phase = Phase.Idle;
        string pollBaseUrl;
        string pollDeviceCode;
        float pollNextAt;
        float pollDeadline;
        float pollInterval = 1f;
        UnityWebRequest activeRequest;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            TearDownEditorHook();
            DisposeRequest();
            if (Instance == this)
                Instance = null;
        }

        void Update() => Tick();

        void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && IsBusy)
                ForcePollNow();
        }

        void OnApplicationPause(bool pauseStatus)
        {
            if (!pauseStatus && IsBusy)
                ForcePollNow();
        }

#if UNITY_EDITOR
        void EditorTick()
        {
            if (!EditorApplication.isPlaying)
                return;
            Tick();
        }
#endif

        void EnsureEditorHook()
        {
#if UNITY_EDITOR
            EditorApplication.update -= EditorTick;
            EditorApplication.update += EditorTick;
#endif
        }

        void TearDownEditorHook()
        {
#if UNITY_EDITOR
            EditorApplication.update -= EditorTick;
#endif
        }

        string ResolveBaseUrl(string overrideUrl = null)
        {
            if (!string.IsNullOrWhiteSpace(overrideUrl))
                return overrideUrl.Trim().TrimEnd('/');

            var localConfig = GetComponent<KaplaoAccountConfig>();
            if (localConfig != null)
                return localConfig.WebsiteUrl;

            if (KaplaoAccountConfig.Instance != null)
                return KaplaoAccountConfig.Instance.WebsiteUrl;

            return "http://localhost:3000";
        }

        /// <summary>
        /// Start sign-in, or if already waiting, poll again (do not mint a new device code).
        /// </summary>
        public void BeginSignIn(string websiteUrlOverride = null)
        {
            if (IsBusy && phase == Phase.Polling && !string.IsNullOrEmpty(pollDeviceCode))
            {
                Debug.Log("[KaplaoAccount] Already signing in — forcing poll now (same device code).");
                SetStatus($"Checking login… ({pollDeviceCode})");
                ForcePollNow();
                return;
            }

            string baseUrl = ResolveBaseUrl(websiteUrlOverride);
            Debug.Log($"[KaplaoAccount] BeginSignIn baseUrl={baseUrl}");

            CancelActiveFlow(clearStatus: false);
            signInGeneration++;

            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            EnsureEditorHook();

            IsBusy = true;
            phase = Phase.Starting;
            pollBaseUrl = baseUrl;
            SetStatus("Connecting to account site…");

            string startUrl = baseUrl + "/api/device/start";
            activeRequest = new UnityWebRequest(startUrl, UnityWebRequest.kHttpVerbPOST);
            activeRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes("{}"));
            activeRequest.downloadHandler = new DownloadHandlerBuffer();
            activeRequest.SetRequestHeader("Content-Type", "application/json");
            activeRequest.timeout = 25;
            activeRequest.SendWebRequest();
        }

        public void ForcePollNow()
        {
            if (IsBusy && phase == Phase.Polling && !string.IsNullOrEmpty(pollDeviceCode))
            {
                DisposeRequest();
                pollNextAt = 0f;
                return;
            }

            string savedCode = PlayerPrefs.GetString(PendingCodeKey, string.Empty);
            string savedUrl = PlayerPrefs.GetString(PendingUrlKey, string.Empty);
            if (string.IsNullOrEmpty(savedCode) || string.IsNullOrEmpty(savedUrl) || PlayerSession.IsLoggedIn)
                return;

            Debug.Log($"[KaplaoAccount] Resuming poll for saved device {savedCode}");
            CancelActiveFlow(clearStatus: false);
            signInGeneration++;
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            EnsureEditorHook();
            IsBusy = true;
            phase = Phase.Polling;
            pollBaseUrl = savedUrl.TrimEnd('/');
            pollDeviceCode = savedCode;
            pollInterval = 1f;
            pollDeadline = Time.realtimeSinceStartup + 5f * 60f;
            pollNextAt = 0f;
            SetStatus($"Checking login… ({pollDeviceCode})");
        }

        public void SignOut()
        {
            if (PlayerSession.IsLoggedIn)
            {
                KaplaoPlayerActionLogClient.Log(
                    "account_logout",
                    metadataJson: "{\"source\":\"KaplaoAccountClient.SignOut\"}");
            }

            CancelActiveFlow(clearStatus: true);
            signInGeneration++;
            IsBusy = false;
            PlayerPrefs.DeleteKey(PendingCodeKey);
            PlayerPrefs.DeleteKey(PendingUrlKey);
            PlayerPrefs.Save();
            PlayerSession.SignOutKeepLocalNickname();
            SignedOut?.Invoke();
        }

        /// <summary>
        /// Cancel active sign-in flow (e.g., if user closed the browser window).
        /// Allows starting a new sign-in attempt without restarting the game.
        /// </summary>
        public void CancelSignIn()
        {
            if (!IsBusy)
                return;

            Debug.Log("[KaplaoAccount] Sign-in cancelled by user");
            CancelActiveFlow(clearStatus: true);
            signInGeneration++;
            IsBusy = false;
            PlayerPrefs.DeleteKey(PendingCodeKey);
            PlayerPrefs.DeleteKey(PendingUrlKey);
            PlayerPrefs.Save();
            SetStatus("Sign-in cancelled. Click Sign in to try again.");
        }

        void CancelActiveFlow(bool clearStatus)
        {
            DisposeRequest();
            phase = Phase.Idle;
            pollDeviceCode = null;
            pollBaseUrl = null;
            TearDownEditorHook();
            Application.runInBackground = previousRunInBackground;
            if (clearStatus)
                SetStatus(string.Empty);
        }

        void DisposeRequest()
        {
            if (activeRequest == null)
                return;

            try
            {
                activeRequest.Abort();
            }
            catch
            {
                // ignored
            }

            activeRequest.Dispose();
            activeRequest = null;
        }

        void Tick()
        {
            if (!IsBusy)
                return;

            if (phase == Phase.Starting)
            {
                TickStarting();
                return;
            }

            if (phase == Phase.Polling)
                TickPolling();
        }

        void TickStarting()
        {
            if (activeRequest == null || !activeRequest.isDone)
                return;

            int generation = signInGeneration;
            UnityWebRequest req = activeRequest;
            activeRequest = null;

            try
            {
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning(
                        $"[KaplaoAccount] device/start failed: {req.error} ({req.responseCode}) body={req.downloadHandler?.text}");
                    SetStatus("Sign in failed: cannot reach website. Check Website Url.");
                    FailSignIn();
                    return;
                }

                string startBody = req.downloadHandler != null ? req.downloadHandler.text : string.Empty;
                string deviceCode = ExtractJsonString(startBody, "deviceCode");
                int pollIntervalMs = ExtractJsonInt(startBody, "pollIntervalMs", 1000);

                if (string.IsNullOrWhiteSpace(deviceCode))
                {
                    Debug.LogWarning($"[KaplaoAccount] Bad start JSON: {startBody}");
                    SetStatus("Sign in failed: bad response from website.");
                    FailSignIn();
                    return;
                }

                if (generation != signInGeneration)
                    return;

                string loginUrl = $"{pollBaseUrl}/device-signin?device={UnityWebRequest.EscapeURL(deviceCode)}";
                Debug.Log($"[KaplaoAccount] Opening sign-in: {loginUrl}");
                SetStatus($"Browser opened. After login click Sign in again. Code: {deviceCode}");
                Application.OpenURL(loginUrl);

                pollDeviceCode = deviceCode;
                pollInterval = Mathf.Clamp(pollIntervalMs / 1000f, 0.75f, 2f);
                pollDeadline = Time.realtimeSinceStartup + 10f * 60f;
                pollNextAt = Time.realtimeSinceStartup;
                phase = Phase.Polling;

                PlayerPrefs.SetString(PendingCodeKey, deviceCode);
                PlayerPrefs.SetString(PendingUrlKey, pollBaseUrl);
                PlayerPrefs.Save();
            }
            finally
            {
                req.Dispose();
            }
        }

        void TickPolling()
        {
            if (Time.realtimeSinceStartup > pollDeadline)
            {
                SetStatus("Sign in timed out. Click Sign in again.");
                FailSignIn();
                return;
            }

            if (activeRequest != null)
            {
                if (!activeRequest.isDone)
                    return;

                UnityWebRequest req = activeRequest;
                activeRequest = null;
                pollNextAt = Time.realtimeSinceStartup + pollInterval;

                try
                {
                    HandlePollResponse(req);
                }
                finally
                {
                    req.Dispose();
                }

                return;
            }

            if (Time.realtimeSinceStartup < pollNextAt)
                return;

            if (string.IsNullOrEmpty(pollBaseUrl) || string.IsNullOrEmpty(pollDeviceCode))
                return;

            // Cache-buster: some hosts cache identical GET poll URLs.
            string pollUrl =
                $"{pollBaseUrl}/api/device/poll?code={UnityWebRequest.EscapeURL(pollDeviceCode)}&_={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            activeRequest = UnityWebRequest.Get(pollUrl);
            activeRequest.SetRequestHeader("Cache-Control", "no-cache");
            activeRequest.SetRequestHeader("Pragma", "no-cache");
            activeRequest.timeout = 15;
            activeRequest.SendWebRequest();
        }

        void HandlePollResponse(UnityWebRequest pollRequest)
        {
            string pollBody = pollRequest.downloadHandler != null
                ? pollRequest.downloadHandler.text
                : string.Empty;

            Debug.Log(
                $"[KaplaoAccount] poll HTTP {(long)pollRequest.responseCode} result={pollRequest.result} body={Truncate(pollBody, 500)}");

            DevicePollResponse parsed = null;
            try
            {
                parsed = JsonUtility.FromJson<DevicePollResponse>(pollBody);
            }
            catch
            {
                // ignored
            }

            string status = parsed != null && !string.IsNullOrEmpty(parsed.status)
                ? parsed.status
                : ExtractJsonString(pollBody, "status");

            if (string.IsNullOrWhiteSpace(status))
            {
                if (pollRequest.result != UnityWebRequest.Result.Success)
                    Debug.LogWarning(
                        $"[KaplaoAccount] poll error: {pollRequest.error} ({pollRequest.responseCode})");
                return;
            }

            if (status == "expired" || status == "unknown")
            {
                Debug.LogWarning($"[KaplaoAccount] Device code {status}. body={pollBody}");
                SetStatus("Sign in expired / unknown. Click Sign in again from the game.");
                PlayerPrefs.DeleteKey(PendingCodeKey);
                PlayerPrefs.DeleteKey(PendingUrlKey);
                PlayerPrefs.Save();
                FailSignIn();
                return;
            }

            if (status == "pending")
            {
                SetStatus($"Waiting for browser… then click Sign in again ({pollDeviceCode})");
                return;
            }

            if (status != "ready")
                return;

            string token = !string.IsNullOrEmpty(parsed?.token)
                ? parsed.token
                : ExtractJsonString(pollBody, "token");
            string nickname = !string.IsNullOrEmpty(parsed?.nickname)
                ? parsed.nickname
                : ExtractJsonString(pollBody, "nickname");
            string email = !string.IsNullOrEmpty(parsed?.email)
                ? parsed.email
                : ExtractJsonString(pollBody, "email");

            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(nickname))
            {
                Debug.LogWarning($"[KaplaoAccount] ready but missing fields: {pollBody}");
                return;
            }

            PlayerSession.SetAccount(nickname, email, token);
            PlayerPrefs.DeleteKey(PendingCodeKey);
            PlayerPrefs.DeleteKey(PendingUrlKey);
            PlayerPrefs.Save();
            IsBusy = false;
            CancelActiveFlow(clearStatus: true);
            Debug.Log($"[KaplaoAccount] Signed in as {nickname} / {PlayerSession.MaskedAccountEmail}");
            SignedIn?.Invoke();
        }

        void FailSignIn()
        {
            IsBusy = false;
            CancelActiveFlow(clearStatus: false);
            SignInFailed?.Invoke();
        }

        void SetStatus(string message)
        {
            StatusMessage = message ?? string.Empty;
            StatusChanged?.Invoke();
        }

        static string Truncate(string value, int max)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= max)
                return value;
            return value.Substring(0, max) + "…";
        }

        static string ExtractJsonString(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
                return null;

            var match = Regex.Match(
                json,
                $"\"{Regex.Escape(key)}\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\"",
                RegexOptions.CultureInvariant);
            if (!match.Success)
                return null;

            return match.Groups[1].Value
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\");
        }

        static int ExtractJsonInt(string json, string key, int fallback)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
                return fallback;

            var match = Regex.Match(json, $"\"{Regex.Escape(key)}\"\\s*:\\s*(-?\\d+)");
            if (!match.Success)
                return fallback;

            return int.TryParse(match.Groups[1].Value, out int value) ? value : fallback;
        }

        [Serializable]
        class DevicePollResponse
        {
            public string status;
            public string token;
            public string nickname;
            public string email;
        }
    }
}
