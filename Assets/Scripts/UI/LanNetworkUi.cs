using System.Collections.Generic;
using KaplaoGames.Shared;
using TMPro;
using UnityEngine;

namespace KaplaoGames.UI
{
    /// <summary>
    /// Shared LAN adapter dropdown helpers for Settings.
    /// </summary>
    public static class LanNetworkUi
    {
        /// <summary>
        /// If the saved interface IP is no longer present, fall back to Auto.
        /// Returns true when the saved value was cleared.
        /// </summary>
        public static bool EnsureSelectedInterfaceStillExists()
        {
            string current = PlayerSession.LanInterfaceIPv4;
            if (string.IsNullOrWhiteSpace(current))
                return false;

            List<NetworkAdapterChoice> choices = NetworkAdapterUtility.GetIPv4Choices();
            for (int i = 0; i < choices.Count; i++)
            {
                if (choices[i].IPv4 == current)
                    return false;
            }

            PlayerSession.SetLanInterfaceIPv4(string.Empty);
            Debug.Log("[LanNetworkUi] Selected LAN interface disappeared. Switched to Auto.");
            return true;
        }

        public static List<NetworkAdapterChoice> RefreshDropdown(TMP_Dropdown dropdown)
        {
            EnsureSelectedInterfaceStillExists();

            var choices = NetworkAdapterUtility.GetIPv4Choices();
            if (dropdown == null)
                return choices;

            dropdown.ClearOptions();
            var options = new List<string> { "Auto" };
            for (int i = 0; i < choices.Count; i++)
                options.Add(choices[i].DisplayName);

            dropdown.AddOptions(options);

            string current = PlayerSession.LanInterfaceIPv4;
            int selected = 0;
            if (!string.IsNullOrWhiteSpace(current))
            {
                for (int i = 0; i < choices.Count; i++)
                {
                    if (choices[i].IPv4 == current)
                    {
                        selected = i + 1;
                        break;
                    }
                }
            }

            dropdown.SetValueWithoutNotify(selected);
            dropdown.RefreshShownValue();
            return choices;
        }

        public static void ApplyDropdown(TMP_Dropdown dropdown, List<NetworkAdapterChoice> choices)
        {
            if (dropdown == null)
            {
                return;
            }

            int selected = dropdown.value;
            if (selected <= 0)
            {
                PlayerSession.SetLanInterfaceIPv4(string.Empty);
                return;
            }

            if (choices == null)
                choices = NetworkAdapterUtility.GetIPv4Choices();

            int index = selected - 1;
            if (index < 0 || index >= choices.Count)
            {
                PlayerSession.SetLanInterfaceIPv4(string.Empty);
                return;
            }

            PlayerSession.SetLanInterfaceIPv4(choices[index].IPv4);
        }
    }
}
