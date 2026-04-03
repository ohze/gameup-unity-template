#if DOTween__DEPENDENCIES_INSTALLED
using DG.Tweening;
#endif
using UnityEngine;
using UnityEngine.UI;

namespace GameUp.Core.UI
{
    public class ChangeImageScaleView : BaseSelectView
    {
        private const string LogTag = nameof(ChangeImageScaleView);

        [SerializeField] private float duration = 0.2f;
        [SerializeField] private float scale = 0.8f;
#if DOTween__DEPENDENCIES_INSTALLED
        [SerializeField] private Ease ease = Ease.OutBack;
#endif
        [SerializeField] private Image image;
        [SerializeField] private Sprite[] sprites;

#if DOTween__DEPENDENCIES_INSTALLED
        private Sequence _sequence;
#endif
        private bool _hasLoggedMissingRefs;

        public override void ChangeSelect(bool isSelected)
        {
            IsSelected = isSelected;
#if DOTween__DEPENDENCIES_INSTALLED
            _sequence?.Kill(true);
#endif

            if (image == null)
            {
                if (!_hasLoggedMissingRefs)
                {
                    _hasLoggedMissingRefs = true;
                    GULogger.Error(LogTag, $"{name}: Missing reference `image`.");
                }

                return;
            }
            if (sprites == null || sprites.Length < 2)
            {
                if (!_hasLoggedMissingRefs)
                {
                    _hasLoggedMissingRefs = true;
                    var len = sprites == null ? 0 : sprites.Length;
                    GULogger.Error(LogTag, $"{name}: `sprites` must have at least 2 entries (len={len}).");
                }

                return;
            }

            var targetSprite = isSelected ? sprites[0] : sprites[1];
            var trs = image.transform;

#if DOTween__DEPENDENCIES_INSTALLED
            _sequence = DOTween.Sequence();
            _sequence
                .Append(trs.DOScale(scale, duration))
                .AppendCallback(() => image.sprite = targetSprite)
                .Append(trs.DOScale(1f, duration).SetEase(ease));
#else
            trs.localScale = Vector3.one * scale;
            image.sprite = targetSprite;
            trs.localScale = Vector3.one;
#endif
        }
    }
}