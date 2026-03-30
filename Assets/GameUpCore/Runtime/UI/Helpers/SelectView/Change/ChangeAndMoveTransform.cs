using DG.Tweening;
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

        private Tween _changePosTween;
        private bool _hasLoggedMissingRefs;
    
        public override void ChangeSelect(bool isSelected)
        {
            IsSelected = isSelected;

            _changePosTween?.Kill();
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
                _changePosTween = moveItemTrs.DOAnchorPosX(isSelected ? enablePos : disablePos, duration);
            }
            else
            {
                _changePosTween = moveItemTrs.DOAnchorPosY(isSelected ? enablePos : disablePos, duration);
            }
        }
    }
}