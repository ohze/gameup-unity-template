using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace GameUp.Core.UI
{
    public class UIShowMoveItemAnimation : UIBaseAnimation
    {
        [Header("Item List")]
        [SerializeField] private List<RectTransform> itemList = new();
        [SerializeField] private List<CanvasGroup> canvasGroups = new();
        [SerializeField] private List<Vector2> originalPositions = new();
        [Header("Config")]
        [SerializeField] private float startYOffset = 150f;
        [SerializeField] private float duration = 0.4f;
        [SerializeField] private float delayBetweenItems = 0.1f;

        private Sequence _startSequence;
        private Sequence _reverseSequence;
        private int _cachedItemCount = -1;

        private void Awake()
        {
            SyncCache();
            RebuildSequences();
        }

        private void OnValidate()
        {
            SyncCache();
        }

        public override IAnimation OnStart()
        {
            EnsureReady();
            _reverseSequence?.Pause();

            for (int i = 0; i < itemList.Count; i++)
            {
                var item = itemList[i];
                if (item == null) continue;

                var originPos = originalPositions[i];
                item.anchoredPosition = originPos + Vector2.up * startYOffset;

                var group = canvasGroups[i];
                if (group != null) group.alpha = 0f;
            }

            _startSequence.Restart();
            return this;
        }

        public override IAnimation OnReverse()
        {
            EnsureReady();
            _startSequence?.Pause();

            for (int i = 0; i < itemList.Count; i++)
            {
                var item = itemList[i];
                if (item == null) continue;

                item.anchoredPosition = originalPositions[i];

                var group = canvasGroups[i];
                if (group != null) group.alpha = 1f;
            }

            _reverseSequence.Restart();
            return this;
        }

        private void EnsureReady()
        {
            if (_cachedItemCount != itemList.Count)
            {
                SyncCache();
                RebuildSequences();
                return;
            }

            for (int i = 0; i < itemList.Count; i++)
            {
                if (itemList[i] == null) continue;

                if (canvasGroups.Count != itemList.Count || canvasGroups[i] == null)
                {
                    SyncCache();
                    RebuildSequences();
                    return;
                }
            }
        }

        private void SyncCache()
        {
            _cachedItemCount = itemList.Count;

            if (canvasGroups.Count != itemList.Count)
            {
                canvasGroups.Clear();
                for (int i = 0; i < itemList.Count; i++) canvasGroups.Add(null);
            }

            if (originalPositions.Count != itemList.Count)
            {
                originalPositions.Clear();
                for (int i = 0; i < itemList.Count; i++) originalPositions.Add(default);
            }

            for (int i = 0; i < itemList.Count; i++)
            {
                var item = itemList[i];
                if (item == null)
                {
                    canvasGroups[i] = null;
                    originalPositions[i] = default;
                    continue;
                }

                if (canvasGroups[i] == null)
                {
                    if (!item.TryGetComponent(out CanvasGroup group))
                    {
                        group = item.gameObject.AddComponent<CanvasGroup>();
                    }

                    canvasGroups[i] = group;
                }

                if (!Application.isPlaying)
                {
                    originalPositions[i] = item.anchoredPosition;
                }
                else if (originalPositions[i] == default && item.anchoredPosition != default)
                {
                    originalPositions[i] = item.anchoredPosition;
                }
            }
        }

        private void RebuildSequences()
        {
            _startSequence?.Kill();
            _reverseSequence?.Kill();

            _startSequence = DOTween.Sequence()
                .SetUpdate(true)
                .SetAutoKill(false)
                .Pause();

            _reverseSequence = DOTween.Sequence()
                .SetUpdate(true)
                .SetAutoKill(false)
                .Pause();

            for (int i = 0; i < itemList.Count; i++)
            {
                var item = itemList[i];
                var group = canvasGroups.Count == itemList.Count ? canvasGroups[i] : null;
                var originPos = originalPositions.Count == itemList.Count ? originalPositions[i] : default;

                if (item == null || group == null) continue;

                _startSequence.Insert(
                    i * delayBetweenItems,
                    item.DOAnchorPosY(originPos.y, duration).SetEase(Ease.OutBack)
                );
                _startSequence.Insert(i * delayBetweenItems, group.DOFade(1f, duration));

                var reverseIndex = itemList.Count - i - 1;
                var reverseItem = itemList[reverseIndex];
                var reverseGroup = canvasGroups[reverseIndex];
                var reverseOriginPos = originalPositions[reverseIndex];

                if (reverseItem == null || reverseGroup == null) continue;

                _reverseSequence.Insert(
                    i * delayBetweenItems,
                    reverseItem.DOAnchorPosY(reverseOriginPos.y - startYOffset, duration).SetEase(Ease.InCubic)
                );
                _reverseSequence.Insert(i * delayBetweenItems, reverseGroup.DOFade(0f, duration));
            }

            _startSequence.OnComplete(() =>
            {
                OnStartCompleteCallback?.Invoke();
                OnStartCompleteCallback = null;
            });

            _reverseSequence.OnComplete(() =>
            {
                OnReverseCompleteCallback?.Invoke();
                OnReverseCompleteCallback = null;
            });
        }
    }
}