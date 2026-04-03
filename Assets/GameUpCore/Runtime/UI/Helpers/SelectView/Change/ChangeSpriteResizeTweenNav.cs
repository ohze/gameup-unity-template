#if DOTween__DEPENDENCIES_INSTALLED
using DG.Tweening;
#endif
using GameUp.Core;
using UnityEngine;
using UnityEngine.UI;

namespace GameUp.Core.UI
{
    public class ChangeSpriteResizeTweenNav : BaseSelectView
    {
        private const string LogTag = nameof(ChangeSpriteResizeTweenNav);

        [Header("UI")]
        [SerializeField] private Image targetImage;
        [SerializeField] private Image targetIconImage;
        [SerializeField] private Sprite selectSprite, disableSprite;
        [SerializeField] private GameObject obj;

        [Header("Config")]
        [SerializeField] private RectTransform posSelect;
        [SerializeField] private RectTransform posNotSelect;

        [Header("Size")]
        [SerializeField] private Vector2 sizeNotSelectBg;
        [SerializeField] private Vector2 sizeSelectBg;
        [SerializeField] private Vector2 sizeNotSelectIcon;
        [SerializeField] private Vector2 sizeSelectIcon;

#if DOTween__DEPENDENCIES_INSTALLED
        private Sequence _sequence;
#endif
        private bool _hasLoggedMissingRefs;

        private void OnValidate()
        {
            if (targetIconImage != null && targetIconImage.sprite != null)
                sizeSelectIcon = new Vector2(targetIconImage.sprite.rect.width, targetIconImage.sprite.rect.height);

            if (disableSprite != null)
                sizeNotSelectBg = new Vector2(disableSprite.rect.width, disableSprite.rect.height);

            if (selectSprite != null)
                sizeSelectBg = new Vector2(selectSprite.rect.width, selectSprite.rect.height);
        }

        public override void ChangeSelect(bool isSelected)
        {
            IsSelected = isSelected;

            if (targetImage == null || targetIconImage == null)
            {
                if (!_hasLoggedMissingRefs)
                {
                    _hasLoggedMissingRefs = true;
                    GULogger.Error(LogTag, $"{name}: Missing reference `targetImage` or `targetIconImage`.");
                }

                return;
            }

            targetImage.sprite = isSelected ? selectSprite : disableSprite;
            if (obj != null) obj.SetActive(isSelected);

            PlayTween(isSelected);
        }

        private void PlayTween(bool isSelected)
        {
#if DOTween__DEPENDENCIES_INSTALLED
            _sequence?.Kill(true);
#endif

            if (targetImage == null || targetIconImage == null || posSelect == null || posNotSelect == null)
            {
                if (!_hasLoggedMissingRefs)
                {
                    _hasLoggedMissingRefs = true;
                    GULogger.Error(LogTag, $"{name}: Missing reference (targetImage/targetIconImage/posSelect/posNotSelect).");
                }

                return;
            }

            var targetBgSize = isSelected ? sizeSelectBg : sizeNotSelectBg;
            var targetIconSize = isSelected ? sizeSelectIcon : sizeNotSelectIcon;
            var targetY = isSelected ? posSelect.anchoredPosition.y : posNotSelect.anchoredPosition.y;
            
            var iconPunchScale = isSelected ? 1.1f : 1f;
            
#if DOTween__DEPENDENCIES_INSTALLED
            _sequence = DOTween.Sequence();

            _sequence
                .Append(targetImage.rectTransform.DOSizeDelta(targetBgSize, 0.25f).SetEase(Ease.OutQuad))
                .Join(targetIconImage.rectTransform.DOSizeDelta(targetIconSize, 0.25f).SetEase(Ease.OutQuad))
                .Join(targetIconImage.rectTransform.DOAnchorPosY(targetY, 0.25f).SetEase(Ease.OutCubic))
                .Join(targetIconImage.rectTransform.DOScale(iconPunchScale, 0.2f).SetEase(Ease.OutBack))
                .OnComplete(() =>
                {
                    if (!isSelected)
                        targetIconImage.rectTransform.localScale = Vector3.one;
                });
#else
            targetImage.rectTransform.sizeDelta = targetBgSize;
            targetIconImage.rectTransform.sizeDelta = targetIconSize;
            targetIconImage.rectTransform.ChangeAnchorY(targetY);
            targetIconImage.rectTransform.localScale = Vector3.one * iconPunchScale;
            if (!isSelected)
                targetIconImage.rectTransform.localScale = Vector3.one;
#endif
        }
    }
}