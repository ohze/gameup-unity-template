using UnityEngine;
using GameUp.Core;
using GameUp.UI;

namespace GameUp.Samples
{
    /// <summary>
    /// Sample bootstrap demonstrating UI systems: ScreenNavigator, PopupStack, Toast, Dialog, Banner.
    /// Attach to a GameObject in UIDemoScene. Requires UIManager in scene.
    /// </summary>
    public sealed class UIDemoBootstrap : MonoBehaviour
    {
        void Start()
        {
            GLogger.Log("UIDemo", "=== UI Demo Started ===");
            GLogger.Log("UIDemo", "UIManager ready. Use buttons in scene to test navigation.");
        }

        /// <summary>Called by UI buttons to show a toast.</summary>
        public void ShowToast()
        {
            Toast.Show("This is a toast notification!");
        }

        /// <summary>Called by UI buttons to show a confirm dialog.</summary>
        public void ShowDialog()
        {
            Dialog.ShowConfirm(
                "Confirm Action",
                "Are you sure you want to proceed?",
                result => GLogger.Log("Dialog", $"User chose: {(result ? "Confirm" : "Cancel")}"),
                "Yes", "No"
            );
        }

        /// <summary>Called by UI buttons to show an alert.</summary>
        public void ShowAlert()
        {
            Dialog.ShowAlert("Notice", "This is an alert dialog.", () =>
                GLogger.Log("Dialog", "Alert dismissed."));
        }

        /// <summary>Called by UI buttons to show a banner notification.</summary>
        public void ShowBanner()
        {
            BannerNotification.Show("Achievement unlocked: First Steps!");
        }
    }
}
