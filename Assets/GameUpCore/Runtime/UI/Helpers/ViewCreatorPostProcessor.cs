using UnityEditor;
using UnityEngine;

namespace GameUp.Core.UI
{
    public class ViewCreatorPostProcessor : AssetPostprocessor
    {
        private enum UpdateFlags
        {
            None = 0,
            Popup = 1 << 0,
            Screen = 1 << 1
        }

        private const string LoggerTag = "ViewCreatorPostProcessor";
        private const string PopupPrefabFolderPath = "Assets/_MainProject/Prefabs/UI/Popups/";
        private const string ScreenPrefabFolderPath = "Assets/_MainProject/Prefabs/UI/Screens/";
        private const string PrefabExtension = ".prefab";
        private const string PopupDataResourcePath = "Data/PopupData";
        private const string ScreenDataResourcePath = "Data/ScreenData";

        private static bool _popupDataDirty;
        private static bool _screenDataDirty;
        private static bool _isRefreshScheduled;

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            UpdateFlags updateFlags = CollectUpdateFlags(importedAssets, deletedAssets, movedAssets, movedFromAssetPaths);
            if (updateFlags == UpdateFlags.None)
            {
                return;
            }

            if ((updateFlags & UpdateFlags.Popup) != 0)
            {
                _popupDataDirty = true;
            }

            if ((updateFlags & UpdateFlags.Screen) != 0)
            {
                _screenDataDirty = true;
            }

            ScheduleRefreshIfNeeded();
        }

        private static UpdateFlags CollectUpdateFlags(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            UpdateFlags flags = UpdateFlags.None;
            flags |= GetUpdateFlagsFromPaths(importedAssets);
            if (flags == (UpdateFlags.Popup | UpdateFlags.Screen))
            {
                return flags;
            }

            flags |= GetUpdateFlagsFromPaths(deletedAssets);
            if (flags == (UpdateFlags.Popup | UpdateFlags.Screen))
            {
                return flags;
            }

            flags |= GetUpdateFlagsFromPaths(movedAssets);
            if (flags == (UpdateFlags.Popup | UpdateFlags.Screen))
            {
                return flags;
            }

            flags |= GetUpdateFlagsFromPaths(movedFromAssetPaths);
            return flags;
        }

        private static UpdateFlags GetUpdateFlagsFromPaths(string[] assetPaths)
        {
            if (assetPaths == null || assetPaths.Length == 0)
            {
                return UpdateFlags.None;
            }

            UpdateFlags flags = UpdateFlags.None;
            for (int i = 0; i < assetPaths.Length; i++)
            {
                string path = assetPaths[i];
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                if (!path.EndsWith(PrefabExtension))
                {
                    continue;
                }

                if (path.StartsWith(PopupPrefabFolderPath))
                {
                    flags |= UpdateFlags.Popup;
                }

                if (path.StartsWith(ScreenPrefabFolderPath))
                {
                    flags |= UpdateFlags.Screen;
                }

                if (flags == (UpdateFlags.Popup | UpdateFlags.Screen))
                {
                    return flags;
                }
            }

            return flags;
        }

        private static void ScheduleRefreshIfNeeded()
        {
            if (_isRefreshScheduled)
            {
                return;
            }

            _isRefreshScheduled = true;
            EditorApplication.delayCall += RunScheduledRefresh;
        }

        private static void RunScheduledRefresh()
        {
            _isRefreshScheduled = false;

            if (_popupDataDirty)
            {
                _popupDataDirty = false;
                TrySetupPopupData();
            }

            if (_screenDataDirty)
            {
                _screenDataDirty = false;
                TrySetupScreenData();
            }
        }

        private static void TrySetupPopupData()
        {
            PopupData popupData = Resources.Load<PopupData>(PopupDataResourcePath);
            if (popupData == null)
            {
                GULogger.Warning(LoggerTag,
                    $"Khong tim thay PopupData tai Resources path: '{PopupDataResourcePath}'.");
                return;
            }

            popupData.SetUp();
        }

        private static void TrySetupScreenData()
        {
            ScreenData screenData = Resources.Load<ScreenData>(ScreenDataResourcePath);
            if (screenData == null)
            {
                GULogger.Warning(LoggerTag,
                    $"Khong tim thay ScreenData tai Resources path: '{ScreenDataResourcePath}'.");
                return;
            }

            screenData.SetUp();
        }
    }
}