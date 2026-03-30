#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace GameUp.Core.UI
{
    public class ViewCreatorPostProcessor : AssetPostprocessor
    {
        private const string LoggerTag = "ViewCreatorPostProcessor";

        private const string PrefabExtension = ".prefab";
        private const string AssetExtension = ".asset";

        private const string PopupDataResourcePath = "Data/PopupData";
        private const string ScreenDataResourcePath = "Data/ScreenData";

        // Match the reference behavior: detect by folder segments, not strict prefix.
        private const string PopupsSegment = "/Popups/";
        private const string ScreensSegment = "/Screens/";
        private const string SingletonsFolderSegment = "Assets/_MainProject/Data/Singletons/";

        private static bool _popupDataDirty;
        private static bool _screenDataDirty;
        private static bool _addressableHolderDirty;
        private static bool _isRefreshScheduled;

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            CollectFlags(importedAssets);
            CollectFlags(deletedAssets);
            CollectFlags(movedAssets);
            CollectFlags(movedFromAssetPaths);

            if (!_popupDataDirty && !_screenDataDirty && !_addressableHolderDirty)
                return;

            ScheduleRefreshIfNeeded();
        }

        private static void CollectFlags(string[] paths)
        {
            if (paths == null || paths.Length == 0) return;

            for (var i = 0; i < paths.Length; i++)
            {
                var path = paths[i];
                if (string.IsNullOrEmpty(path)) continue;

                // Normalize slashes defensively (Unity usually uses '/').
                if (path.IndexOf('\\') >= 0) path = path.Replace('\\', '/');

                if (!_addressableHolderDirty &&
                    path.EndsWith(AssetExtension, StringComparison.OrdinalIgnoreCase) &&
                    path.IndexOf(SingletonsFolderSegment, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _addressableHolderDirty = true;
                }

                if (path.EndsWith(PrefabExtension, StringComparison.OrdinalIgnoreCase))
                {
                    if (!_popupDataDirty &&
                        path.IndexOf(PopupsSegment, StringComparison.OrdinalIgnoreCase) >= 0)
                        _popupDataDirty = true;

                    if (!_screenDataDirty &&
                        path.IndexOf(ScreensSegment, StringComparison.OrdinalIgnoreCase) >= 0)
                        _screenDataDirty = true;
                }

                if (_popupDataDirty && _screenDataDirty && _addressableHolderDirty)
                    return;
            }
        }

        private static void ScheduleRefreshIfNeeded()
        {
            if (_isRefreshScheduled) return;

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

            if (_addressableHolderDirty)
            {
                _addressableHolderDirty = false;
                TrySetupAddressableDataHolder();
            }
        }

        private static void TrySetupPopupData()
        {
            var popupData = Resources.Load<PopupData>(PopupDataResourcePath);
            if (!popupData)
            {
                GULogger.Warning(LoggerTag, $"Khong tim thay PopupData tai Resources path: '{PopupDataResourcePath}'.");
                return;
            }

            popupData.SetUp();
        }

        private static void TrySetupScreenData()
        {
            var screenData = Resources.Load<ScreenData>(ScreenDataResourcePath);
            if (!screenData)
            {
                GULogger.Warning(LoggerTag, $"Khong tim thay ScreenData tai Resources path: '{ScreenDataResourcePath}'.");
                return;
            }

            screenData.SetUp();
        }

        private static void TrySetupAddressableDataHolder()
        {
            // Prefer the reference snippet behavior (Resources.Load + SetUp).
            var holder = GameUp.Core.AddressableDataHolder.Editor_LoadFromResourcesOrNull();
            if (!holder)
            {
                GULogger.Warning(LoggerTag,
                    "Khong tim thay AddressableHolder trong Resources. Hay chay 'GameUp/Project/Folder Setup' de tao day du.");
                return;
            }

            holder.SetUp();
        }
    }
}
#endif