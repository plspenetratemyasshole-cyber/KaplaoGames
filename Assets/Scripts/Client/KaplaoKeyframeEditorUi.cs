using System.Collections.Generic;

using KaplaoGames.LevelScript;

using KaplaoGames.Network;

using KaplaoGames.Server;

using KaplaoGames.Shared;

using Mirror;

using TMPro;

using UnityEngine;

using UnityEngine.UI;



namespace KaplaoGames.Client

{

    [DisallowMultipleComponent]

    public class KaplaoKeyframeEditorUi : MonoBehaviour

    {

        [SerializeField] GameObject kaplaoOnlyRoot;

        [SerializeField] RectTransform panelRoot;

        [SerializeField] TMP_InputField durationInput;

        [SerializeField] TMP_Dropdown animDropdown;

        [SerializeField] TMP_InputField spawnIdInput;

        [SerializeField] Toggle curveToggle;

        [SerializeField] Toggle closeLoopToggle;

        [SerializeField] Toggle distanceToggle;

        [SerializeField] Button applyButton;

        [SerializeField] Button closeButton;



        KaplaoCursorClient cursorClient;

        bool suppress;

        bool dirtyDuration;

        bool dirtySpawnId;

        bool dirtyAnim;

        bool dirtyCurve;

        bool dirtyCloseLoop;

        bool dirtyDistance;



        public bool IsOpen => panelRoot != null && panelRoot.gameObject.activeSelf;



        void Awake()

        {

            if (panelRoot != null) panelRoot.gameObject.SetActive(false);

            if (applyButton != null) applyButton.gameObject.SetActive(false);

        }



        void OnEnable()

        {

            if (closeButton != null) closeButton.onClick.AddListener(Hide);



            // Use "dirtiness" tracking so multi-selection patch does not overwrite unchanged fields.

            if (durationInput != null)

                durationInput.onEndEdit.AddListener(MarkDirtyDuration);

            if (spawnIdInput != null)

                spawnIdInput.onEndEdit.AddListener(MarkDirtySpawnId);

            if (curveToggle != null)

                curveToggle.onValueChanged.AddListener(MarkDirtyCurve);

            if (closeLoopToggle != null)

                closeLoopToggle.onValueChanged.AddListener(MarkDirtyCloseLoop);

            if (distanceToggle != null)

                distanceToggle.onValueChanged.AddListener(MarkDirtyDistance);

            if (animDropdown != null)

                animDropdown.onValueChanged.AddListener(MarkDirtyAnim);

        }



        void OnDisable()

        {

            if (applyButton != null) applyButton.onClick.RemoveListener(Apply);

            if (closeButton != null) closeButton.onClick.RemoveListener(Hide);



            if (durationInput != null)

                durationInput.onEndEdit.RemoveListener(MarkDirtyDuration);

            if (spawnIdInput != null)

                spawnIdInput.onEndEdit.RemoveListener(MarkDirtySpawnId);

            if (curveToggle != null)

                curveToggle.onValueChanged.RemoveListener(MarkDirtyCurve);

            if (closeLoopToggle != null)

                closeLoopToggle.onValueChanged.RemoveListener(MarkDirtyCloseLoop);

            if (distanceToggle != null)

                distanceToggle.onValueChanged.RemoveListener(MarkDirtyDistance);

            if (animDropdown != null)

                animDropdown.onValueChanged.RemoveListener(MarkDirtyAnim);

        }



        void MarkDirtyDuration(string _) { if (!suppress) { dirtyDuration = true; Apply(); } }

        void MarkDirtySpawnId(string _) { if (!suppress) { dirtySpawnId = true; Apply(); } }

        void MarkDirtyAnim(int _) { if (!suppress) { dirtyAnim = true; Apply(); } }

        void MarkDirtyCurve(bool _) { if (!suppress) { dirtyCurve = true; Apply(); } }

        void MarkDirtyCloseLoop(bool _) { if (!suppress) { dirtyCloseLoop = true; Apply(); } }

        void MarkDirtyDistance(bool _) { if (!suppress) { dirtyDistance = true; Apply(); } }



        public void ShowForSelection()

        {

            if (panelRoot == null) return;

            panelRoot.gameObject.SetActive(true);

            RefreshFromSelection();

        }



        public void Hide()

        {

            CommitFocusedInputFields();

            Apply();

            if (panelRoot != null) panelRoot.gameObject.SetActive(false);

        }



        void CommitFocusedInputFields()

        {

            if (durationInput != null && durationInput.isFocused)

                durationInput.DeactivateInputField(true);

            if (spawnIdInput != null && spawnIdInput.isFocused)

                spawnIdInput.DeactivateInputField(true);

        }



        void RefreshFromSelection()

        {

            KaplaoCursorServer server = NetworkClient.localPlayer != null

                ? NetworkClient.localPlayer.GetComponent<KaplaoCursorServer>()

                : null;

            if (server == null || server.SelectedCount <= 0) return;



            suppress = true;
            try
            {
                dirtyDuration = false;
                dirtySpawnId = false;
                dirtyAnim = false;
                dirtyCurve = false;
                dirtyCloseLoop = false;
                dirtyDistance = false;

                // When multiple keyframes are selected, keep all fields "unknown" by default.
                // We only apply fields which user actually changes (dirty flags).
                if (server.SelectedCount == 1)
                {
                    if (!LevelScriptObjectUtility.TryResolveTile(server.SelectedTiles[0], out LevelTileServer tile) ||
                        tile == null || !tile.TryGetComponent(out KeyframeTile keyframe))
                        return;

                    if (durationInput != null) durationInput.text = keyframe.Duration.ToString("G");
                    if (spawnIdInput != null) spawnIdInput.text = keyframe.SpawnId.ToString();
                    if (curveToggle != null) curveToggle.isOn = keyframe.Curve;
                    IReadOnlyList<int> groups = tile.CachedGroupIds;
                    if (closeLoopToggle != null)
                    {
                        closeLoopToggle.isOn = groups.Count > 0
                            ? KeyframeAnimator.GetCloseLoopForGroup(groups[0])
                            : keyframe.CloseLoop;
                    }

                    if (distanceToggle != null)
                    {
                        distanceToggle.isOn = groups.Count > 0
                            ? KeyframeAnimator.GetDistanceForGroup(groups[0])
                            : keyframe.Distance;
                    }

                    if (animDropdown != null)
                    {
                        int index = keyframe.Anim switch
                        {
                            "easein" => 1,
                            "easeout" => 2,
                            "easeinout" => 3,
                            _ => 0
                        };
                        animDropdown.value = index;
                    }
                }
                else
                {
                    if (durationInput != null) durationInput.text = string.Empty;
                    if (spawnIdInput != null) spawnIdInput.text = string.Empty;
                    if (curveToggle != null) curveToggle.isOn = false;
                    if (closeLoopToggle != null) closeLoopToggle.isOn = false;
                    if (distanceToggle != null) distanceToggle.isOn = false;
                    if (animDropdown != null) animDropdown.value = 0;
                }
            }
            finally
            {
                suppress = false;
            }

        }



        void Apply()

        {

            cursorClient = KaplaoLocalBindingUtility.ResolveLocalCursorClient();

            if (cursorClient == null) return;



            if (!dirtyDuration && !dirtySpawnId && !dirtyAnim && !dirtyCurve && !dirtyCloseLoop && !dirtyDistance)

                return;



            var patch = new KeyframePatchRecord();

            if (dirtyDuration && durationInput != null && KaplaoNumericInputUtility.TryParseFloat(durationInput.text, out float d))

            {

                patch.hasDuration = true;

                patch.duration = d;

            }



            if (dirtySpawnId && spawnIdInput != null && int.TryParse(spawnIdInput.text, out int sid))

            {

                patch.hasSpawnId = true;

                patch.spawnId = sid;

            }



            if (dirtyAnim && animDropdown != null)

            {

                patch.hasAnim = true;

                patch.anim = animDropdown.options[animDropdown.value].text;

            }



            if (dirtyCurve && curveToggle != null)

            {

                patch.hasCurve = true;

                patch.curve = curveToggle.isOn;

            }



            if (dirtyCloseLoop && closeLoopToggle != null)

            {

                patch.hasCloseLoop = true;

                patch.closeLoop = closeLoopToggle.isOn;

            }



            if (dirtyDistance && distanceToggle != null)

            {

                patch.hasDistance = true;

                patch.distance = distanceToggle.isOn;

            }



            if (!patch.hasDuration && !patch.hasAnim && !patch.hasSpawnId && !patch.hasCurve &&

                !patch.hasCloseLoop && !patch.hasDistance)

                return;



            KaplaoCursorClient.EnsureClientReadyForCommands();

            cursorClient.CmdApplyKeyframeSettingsPatchToSelection(patch);

        }

    }

}


