#if DOTween__DEPENDENCIES_INSTALLED
using DG.Tweening;
#endif
using UnityEngine;

namespace GameUp.Core.UI
{
    public class ChangeAndMoveTransform : BaseSelectView
    {
        private const string LogTag = nameof(ChangeAndMoveTransform);

        [SerializeField] private MoveType moveType = MoveType.MoveX;
        [SerializeField] private float duration = 0.2f;
        [SerializeField] private RectTransform moveItemTrs;
        [SerializeField] private float disablePos, enablePos;

#if DOTween__DEPENDENCIES_INSTALLED
        private Tween _changePosTween;
#endif
        private bool _hasLoggedMissingRefs;

        public override void ChangeSelect(bool isSelected)
        {
            IsSelected = isSelected;

#if DOTween__DEPENDENCIES_INSTALLED
            _changePosTween?.Kill();
#endif
            if (moveItemTrs == null)
            {
                if (!_hasLoggedMissingRefs)
                {
                    _hasLoggedMissingRefs = true;
                    GULogger.Error(LogTag, $"{name}: Missing reference `moveItemTrs`.");
                }

                return;
            }

            if (moveType == MoveType.MoveX)
            {
#if DOTween__DEPENDENCIES_INSTALLED
                _changePosTween = moveItemTrs.DOAnchorPosX(isSelected ? enablePos : disablePos, duration);
#else
                moveItemTrs.ChangeAnchorX(isSelected ? enablePos : disablePos);
#endif
            }
            else
            {
#if DOTween__DEPENDENCIES_INSTALLED
                _changePosTween = moveItemTrs.DOAnchorPosY(isSelected ? enablePos : disablePos, duration);
#else
                moveItemTrs.ChangeAnchorY(isSelected ? enablePos : disablePos);
#endif
            }
        }
    }
}