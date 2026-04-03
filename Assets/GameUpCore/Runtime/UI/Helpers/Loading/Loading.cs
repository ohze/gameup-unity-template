using System;
using UnityEngine;

namespace GameUp.Core.UI
{
    /// <summary>
    /// Điều phối một overlay loading duy nhất: mỗi lần Open mới đóng instance trước (nếu có).
    /// Callback đóng chỉ cập nhật <see cref="_currentItem"/> khi đúng instance — tránh race khi đóng/mở chồng lấn.
    /// </summary>
    public class Loading : MonoSingleton<Loading>
    {
        [SerializeField] private LoadingOverlayBase prefabItem;
        [SerializeField] private LoadingOverlayBase prefabItemCutoutMask;
        [SerializeField] private RectTransform contentHolder;

        private LoadingOverlayBase _currentItem;

        [Button]
        public static void Open(
            bool autoClose = true,
            float autoCloseTime = 1,
            Action onOpened = null,
            Action onClosed = null)
        {
            Instance.Present(
                itemPrefab: Instance.prefabItem,
                autoClose,
                autoCloseTime,
                onOpened,
                onClosed);
        }

        /// <summary>
        /// Cutout: ưu tiên <paramref name="cutoutStartLocalPosition"/> (local trong parent của mask);
        /// không thì dùng <paramref name="cutoutStartReference"/> — <see cref="RectTransform"/> lấy tâm rect;
        /// Transform 3D chiếu qua <paramref name="cutoutWorldSpaceCamera"/> (mặc định Canvas / Main).
        /// </summary>
        [Button]
        public static void OpenCutoutMask(
            bool autoClose = true,
            float autoCloseTime = 1,
            Action onOpened = null,
            Action onClosed = null,
            Vector3? cutoutStartLocalPosition = null,
            Transform cutoutStartReference = null,
            Camera cutoutWorldSpaceCamera = null)
        {
            Instance.Present(
                itemPrefab: Instance.prefabItemCutoutMask,
                autoClose,
                autoCloseTime,
                onOpened,
                onClosed,
                cutoutStartLocalPosition,
                cutoutStartReference,
                cutoutWorldSpaceCamera);
        }

        public static void Close()
        {
            Instance.InternalClose();
        }

        private void Present(
            LoadingOverlayBase itemPrefab,
            bool autoClose,
            float autoCloseTime,
            Action onOpened,
            Action onClosed,
            Vector3? cutoutStartLocalPosition = null,
            Transform cutoutStartReference = null,
            Camera cutoutWorldSpaceCamera = null)
        {
            if (itemPrefab == null || contentHolder == null)
            {
                GULogger.Warning("Loading", "itemPrefab hoặc contentHolder chưa gán — bỏ qua Present.");
                return;
            }

            InternalClose();

            var item = GUPool.Spawn(itemPrefab, contentHolder);
            _currentItem = item;

            if (item is LoadingItemCutoutMask cutout)
            {
                cutout.ApplyCutoutStartForOpen(
                    cutoutStartLocalPosition,
                    cutoutStartReference,
                    cutoutWorldSpaceCamera);
            }

            item.Open(autoClose, autoCloseTime, onOpened, () =>
            {
                if (_currentItem == item)
                    _currentItem = null;
                onClosed?.Invoke();
            });
        }

        private void InternalClose()
        {
            if (_currentItem == null)
                return;

            _currentItem.Close();
            _currentItem = null;
        }
    }
}
