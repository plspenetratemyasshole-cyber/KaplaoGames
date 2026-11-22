using System.Collections;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KaplaoGames.Network;
using KaplaoGames.UI.Theme;

namespace KaplaoGames.UI
{
    /// <summary>
    /// Sleek floating HUD indicator at top-center showing background account & role verification status.
    /// Fades in smoothly when verification begins, shows animated spinner, and transitions to
    /// "Synced" checkmark briefly before fading away completely.
    /// </summary>
    [DisallowMultipleComponent]
    public class KaplaoDataFetchHud : MonoBehaviour
    {
        enum State
        {
            Hidden,
            Verifying,
            Success,
            FadingOut
        }

        [SerializeField] RectTransform rootRect;
        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField] Image bgImage;
        [SerializeField] Image borderImage;
        [SerializeField] RectTransform spinnerRect;
        [SerializeField] Image spinnerImage;
        [SerializeField] TextMeshProUGUI labelText;

        State currentState = State.Hidden;
        float successTimer = 0f;
        float verifyingTimeout = 12f;
        NetworkPlayer boundPlayer;

        static readonly Color TextNormal = new Color(0.973f, 0.980f, 0.988f, 1f); // #F8FAFC
        static readonly Color AccentCyan = new Color(0.220f, 0.741f, 0.973f, 1f); // #38BDF8
        static readonly Color BorderCyan = new Color(0.220f, 0.741f, 0.973f, 0.55f);
        static readonly Color AccentSuccess = new Color(0.204f, 0.827f, 0.600f, 1f); // #34D399
        static readonly Color BorderSuccess = new Color(0.204f, 0.827f, 0.600f, 0.65f);
        static readonly Color BgPill = new Color(0.059f, 0.090f, 0.165f, 0.92f); // #0F172A 92%

        void Awake()
        {
            EnsureVisualHierarchy();
        }

        void Start()
        {
            EnsureVisualHierarchy();
            TryBindLocalPlayer();

            if (currentState == State.Hidden)
            {
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 0f;
                    canvasGroup.interactable = false;
                    canvasGroup.blocksRaycasts = false;
                }
            }
        }

        void OnDestroy()
        {
            UnbindPlayer();
        }

        void OnDisable()
        {
            UnbindPlayer();
        }

        void Update()
        {
            if (boundPlayer == null)
            {
                TryBindLocalPlayer();
            }

            switch (currentState)
            {
                case State.Verifying:
                    if (canvasGroup != null)
                        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 1f, Time.unscaledDeltaTime * 5f);

                    if (spinnerRect != null)
                        spinnerRect.Rotate(0f, 0f, -360f * Time.unscaledDeltaTime);

                    verifyingTimeout -= Time.unscaledDeltaTime;
                    if (verifyingTimeout <= 0f)
                    {
                        TransitionToSuccess();
                    }
                    break;

                case State.Success:
                    if (canvasGroup != null)
                        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 1f, Time.unscaledDeltaTime * 5f);

                    successTimer -= Time.unscaledDeltaTime;
                    if (successTimer <= 0f)
                    {
                        currentState = State.FadingOut;
                    }
                    break;

                case State.FadingOut:
                    if (canvasGroup != null)
                    {
                        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 0f, Time.unscaledDeltaTime * 4f);
                        if (canvasGroup.alpha <= 0.001f)
                        {
                            currentState = State.Hidden;
                            gameObject.SetActive(false);
                        }
                    }
                    else
                    {
                        currentState = State.Hidden;
                        gameObject.SetActive(false);
                    }
                    break;
            }
        }

        void TryBindLocalPlayer()
        {
            if (!NetworkClient.active)
                return;

            NetworkIdentity localIdentity = NetworkClient.localPlayer;
            if (localIdentity == null)
                return;

            NetworkPlayer np = localIdentity.GetComponent<NetworkPlayer>();
            if (np == null)
                return;

            if (boundPlayer != np)
            {
                UnbindPlayer();
                boundPlayer = np;
                boundPlayer.IsVerifyingAccountChanged += OnVerifyingStateChanged;

                if (boundPlayer.isVerifyingAccount)
                {
                    ShowVerifying();
                }
            }
        }

        void UnbindPlayer()
        {
            if (boundPlayer != null)
            {
                boundPlayer.IsVerifyingAccountChanged -= OnVerifyingStateChanged;
                boundPlayer = null;
            }
        }

        void OnVerifyingStateChanged(bool verifying)
        {
            if (verifying)
            {
                ShowVerifying();
            }
            else
            {
                if (currentState == State.Verifying)
                {
                    TransitionToSuccess();
                }
            }
        }

        void ShowVerifying()
        {
            gameObject.SetActive(true);
            currentState = State.Verifying;
            verifyingTimeout = 12f;

            if (labelText != null)
            {
                labelText.text = "Fetching data...";
                labelText.color = TextNormal;
            }

            if (spinnerImage != null)
            {
                spinnerImage.sprite = KaplaoUiSprites.Spinner();
                spinnerImage.color = AccentCyan;
            }

            if (borderImage != null)
            {
                borderImage.color = BorderCyan;
            }
        }

        void TransitionToSuccess()
        {
            currentState = State.Success;
            successTimer = 0.65f;

            if (labelText != null)
            {
                labelText.text = "Synced";
                labelText.color = AccentSuccess;
            }

            if (spinnerImage != null)
            {
                spinnerImage.sprite = KaplaoUiSprites.Checkmark();
                spinnerImage.color = AccentSuccess;
            }

            if (spinnerRect != null)
            {
                spinnerRect.localRotation = Quaternion.identity;
            }

            if (borderImage != null)
            {
                borderImage.color = BorderSuccess;
            }
        }

        public void EnsureVisualHierarchy()
        {
            rootRect = GetComponent<RectTransform>();
            if (rootRect == null)
                rootRect = gameObject.AddComponent<RectTransform>();

            rootRect.anchorMin = new Vector2(0.5f, 1f);
            rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.anchoredPosition = new Vector2(0f, -74f);
            rootRect.sizeDelta = new Vector2(185f, 34f);

            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            bgImage = GetComponent<Image>();
            if (bgImage == null)
                bgImage = gameObject.AddComponent<Image>();

            bgImage.sprite = KaplaoUiSprites.RoundedRect(16);
            bgImage.type = Image.Type.Sliced;
            bgImage.color = BgPill;
            bgImage.raycastTarget = false;

            // Border
            Transform borderTrans = transform.Find("UI_Border");
            GameObject borderObj;
            if (borderTrans != null)
            {
                borderObj = borderTrans.gameObject;
            }
            else
            {
                borderObj = new GameObject("UI_Border", typeof(RectTransform), typeof(Image));
                borderObj.transform.SetParent(transform, false);
            }

            RectTransform borderRt = borderObj.GetComponent<RectTransform>();
            borderRt.anchorMin = Vector2.zero;
            borderRt.anchorMax = Vector2.one;
            borderRt.offsetMin = Vector2.zero;
            borderRt.offsetMax = Vector2.zero;

            borderImage = borderObj.GetComponent<Image>();
            borderImage.sprite = KaplaoUiSprites.RoundedBorder(16, 1);
            borderImage.type = Image.Type.Sliced;
            borderImage.color = BorderCyan;
            borderImage.raycastTarget = false;

            // Spinner
            Transform spinnerTrans = transform.Find("Spinner");
            GameObject spinnerObj;
            if (spinnerTrans != null)
            {
                spinnerObj = spinnerTrans.gameObject;
            }
            else
            {
                spinnerObj = new GameObject("Spinner", typeof(RectTransform), typeof(Image));
                spinnerObj.transform.SetParent(transform, false);
            }

            spinnerRect = spinnerObj.GetComponent<RectTransform>();
            spinnerRect.anchorMin = new Vector2(0f, 0.5f);
            spinnerRect.anchorMax = new Vector2(0f, 0.5f);
            spinnerRect.pivot = new Vector2(0.5f, 0.5f);
            spinnerRect.anchoredPosition = new Vector2(20f, 0f);
            spinnerRect.sizeDelta = new Vector2(18f, 18f);

            spinnerImage = spinnerObj.GetComponent<Image>();
            spinnerImage.sprite = KaplaoUiSprites.Spinner();
            spinnerImage.type = Image.Type.Simple;
            spinnerImage.color = AccentCyan;
            spinnerImage.raycastTarget = false;

            // Label
            Transform labelTrans = transform.Find("Label");
            GameObject labelObj;
            if (labelTrans != null)
            {
                labelObj = labelTrans.gameObject;
            }
            else
            {
                labelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                labelObj.transform.SetParent(transform, false);
            }

            RectTransform labelRt = labelObj.GetComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0f, 0f);
            labelRt.anchorMax = new Vector2(1f, 1f);
            labelRt.offsetMin = new Vector2(36f, 0f);
            labelRt.offsetMax = new Vector2(-10f, 0f);

            labelText = labelObj.GetComponent<TextMeshProUGUI>();
            labelText.text = "Fetching data...";
            labelText.fontSize = 13.5f;
            labelText.fontStyle = FontStyles.Bold;
            labelText.color = TextNormal;
            labelText.alignment = TextAlignmentOptions.MidlineLeft;
            labelText.raycastTarget = false;
        }
    }
}
