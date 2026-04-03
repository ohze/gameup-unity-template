using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace GameUp.Core.UI
{
    public class ChangeImageScaleView : BaseSelectView
    {
        private const string LogTag = nameof(ChangeImageScaleView);

        [SerializeField] private float duration = 0.2f;
        [SerializeField] private float scale = 0.8f;
        [SerializeField] private Ease ease = Ease.OutBack;
        [SerializeField] private Image image;
        [SerializeField] private Sprite[] sprites;

        private Sequence _sequence;
        private bool _hasLoggedMissingRefs;

        public override void ChangeSelect(bool isSelected)
        {
            IsSelected = isSelected;
            _sequence?.Kill(true);

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

            _sequence = DOTween.Sequence();
            _sequence
                .Append(trs.DOScale(scale, duration))
                .AppendCallback(() => image.sprite = targetSprite)
                .Append(trs.DOScale(1f, duration).SetEase(ease));
        }
    }
}