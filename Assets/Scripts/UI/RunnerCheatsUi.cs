using KaplaoGames.Client;
using KaplaoGames.Network;
using KaplaoGames.Shared;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KaplaoGames.UI
{
    /// <summary>
    /// Corner cheat panel for local runner when Cheats is enabled on MobileControlsConfig.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Kaplao/UI/Runner Cheats UI")]
    public class RunnerCheatsUi : MonoBehaviour
    {
        [SerializeField] GameObject panelRoot;
        [SerializeField] Button kaplaoFlyButton;
        [SerializeField] TextMeshProUGUI kaplaoFlyButtonText;
        [SerializeField] TMP_InputField flySpeedField;
        [SerializeField] Button immortalityButton;
        [SerializeField] TextMeshProUGUI immortalityButtonText;
        [SerializeField] TMP_InputField spikePushField;
        [SerializeField] TMP_InputField spikePulseSizeField;

        void OnEnable()
        {
            if (kaplaoFlyButton != null)
                kaplaoFlyButton.onClick.AddListener(OnKaplaoFlyClicked);
            if (immortalityButton != null)
                immortalityButton.onClick.AddListener(OnImmortalityClicked);
            if (flySpeedField != null)
                flySpeedField.onEndEdit.AddListener(OnFlySpeedEdited);
            if (spikePushField != null)
                spikePushField.onEndEdit.AddListener(OnSpikePushEdited);
            if (spikePulseSizeField != null)
                spikePulseSizeField.onEndEdit.AddListener(OnSpikePulseSizeEdited);

            SyncFromState();
        }

        void OnDisable()
        {
            if (kaplaoFlyButton != null)
                kaplaoFlyButton.onClick.RemoveListener(OnKaplaoFlyClicked);
            if (immortalityButton != null)
                immortalityButton.onClick.RemoveListener(OnImmortalityClicked);
            if (flySpeedField != null)
                flySpeedField.onEndEdit.RemoveListener(OnFlySpeedEdited);
            if (spikePushField != null)
                spikePushField.onEndEdit.RemoveListener(OnSpikePushEdited);
            if (spikePulseSizeField != null)
                spikePulseSizeField.onEndEdit.RemoveListener(OnSpikePulseSizeEdited);
        }

        void Update()
        {
            bool show = ShouldShow();
            if (panelRoot != null && panelRoot.activeSelf != show)
            {
                panelRoot.SetActive(show);
                if (show)
                    RebuildPanelLayout();
            }

            if (!show)
            {
                if (RunnerCheatsState.KaplaoFly || RunnerCheatsState.Immortality)
                    RunnerCheatsState.Reset();
                return;
            }

            EnsureCheatsClientOnLocalRunner();
        }

        void RebuildPanelLayout()
        {
            if (panelRoot == null)
                return;

            RectTransform rect = panelRoot.GetComponent<RectTransform>();
            if (rect == null)
                return;

            // ContentSizeFitter often leaves height 0 until forced rebuild.
            if (rect.sizeDelta.y < 1f)
                rect.sizeDelta = new Vector2(Mathf.Max(220f, rect.sizeDelta.x), 220f);

            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            Canvas.ForceUpdateCanvases();
        }

        static bool ShouldShow()
        {
            if (!MobileControlsConfig.IsCheatsEnabled)
                return false;

            if (!NetworkClient.active || NetworkClient.localPlayer == null)
                return false;

            return LocalPlayerRole.IsRunner();
        }

        static void EnsureCheatsClientOnLocalRunner()
        {
            GameObject local = NetworkClient.localPlayer != null
                ? NetworkClient.localPlayer.gameObject
                : null;
            if (local == null)
                return;

            if (local.GetComponent<RunnerCheatsClient>() == null)
                local.AddComponent<RunnerCheatsClient>();
        }

        void OnKaplaoFlyClicked()
        {
            RunnerCheatsState.KaplaoFly = !RunnerCheatsState.KaplaoFly;
            SyncFromState();
        }

        void OnImmortalityClicked()
        {
            RunnerCheatsState.Immortality = !RunnerCheatsState.Immortality;
            SyncFromState();
        }

        void OnFlySpeedEdited(string text)
        {
            if (float.TryParse(text, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float speed) &&
                speed > 0f)
            {
                RunnerCheatsState.FlySpeed = speed;
            }
            else
            {
                RunnerCheatsState.FlySpeed = RunnerCheatsState.DefaultFlySpeed;
            }

            SyncFromState();
        }

        void OnSpikePushEdited(string text)
        {
            if (float.TryParse(text, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float pushForce) &&
                pushForce > 0f)
            {
                RunnerCheatsState.SpikePushForce = pushForce;
            }
            else
            {
                RunnerCheatsState.SpikePushForce = RunnerCheatsState.DefaultSpikePushForce;
            }

            SyncFromState();
        }

        void OnSpikePulseSizeEdited(string text)
        {
            if (float.TryParse(text, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float size) &&
                size > 0f)
            {
                RunnerCheatsState.SpikePulseSize = size;
            }
            else
            {
                RunnerCheatsState.SpikePulseSize = RunnerCheatsState.DefaultSpikePulseSize;
            }

            SyncFromState();
        }

        void SyncFromState()
        {
            if (kaplaoFlyButtonText != null)
                kaplaoFlyButtonText.text = $"Kaplao fly: {(RunnerCheatsState.KaplaoFly ? "ON" : "OFF")}";

            if (immortalityButtonText != null)
                immortalityButtonText.text = $"Immortality: {(RunnerCheatsState.Immortality ? "ON" : "OFF")}";

            if (flySpeedField != null && !flySpeedField.isFocused)
                flySpeedField.text = RunnerCheatsState.FlySpeed.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

            if (spikePushField != null && !spikePushField.isFocused)
                spikePushField.text = RunnerCheatsState.SpikePushForce.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

            if (spikePulseSizeField != null && !spikePulseSizeField.isFocused)
                spikePulseSizeField.text = RunnerCheatsState.SpikePulseSize.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
