#if DOTween__DEPENDENCIES_INSTALLED
using DG.Tweening;
#endif

namespace GameUp.Core.UI
{
    public class UIMoveAnimation : UIBaseAnimation
    {
        public MoveType moveType = MoveType.MoveY;
        public float startPos = 300, middlePos = -10, endPos;
        public float firstTime = 0.2f, secondTime = 0.02f;

        public override IAnimation OnStart()
        {
#if DOTween__DEPENDENCIES_INSTALLED
            mainSequence?.Kill();
            canvasGroup.alpha = 0;
            if (moveType == MoveType.MoveX)
            {
                content.ChangeAnchorX(startPos);
                mainSequence = DOTween.Sequence()
                    .Append(content.DOAnchorPosX(middlePos, firstTime))
                    .Join(canvasGroup.DOFade(1, firstTime))
                    .Append(content.DOAnchorPosX(endPos, secondTime))
                    .OnComplete(() =>
                    {
                        OnStartCompleteCallback?.Invoke();
                        OnStartCompleteCallback = null;
                    });
                ;
                mainSequence.Restart();
            }
            else
            {
                content.ChangeAnchorY(startPos);
                mainSequence = DOTween.Sequence()
                    .Append(content.DOAnchorPosY(middlePos, firstTime))
                    .Join(canvasGroup.DOFade(1, firstTime))
                    .Append(content.DOAnchorPosY(endPos, secondTime))
                    .OnComplete(() =>
                    {
                        OnStartCompleteCallback?.Invoke();
                        OnStartCompleteCallback = null;
                    });
                mainSequence.Restart();
            }

#else
            canvasGroup.alpha = 1f;
            if (moveType == MoveType.MoveX)
                content.ChangeAnchorX(endPos);
            else
                content.ChangeAnchorY(endPos);
            OnStartCompleteCallback?.Invoke();
            OnStartCompleteCallback = null;
#endif
            return this;
        }

        public override IAnimation OnReverse()
        {
#if DOTween__DEPENDENCIES_INSTALLED
            mainSequence?.Kill();
            canvasGroup.alpha = 1;
            if (moveType == MoveType.MoveX)
            {
                content.ChangeAnchorX(endPos);
                mainSequence = DOTween.Sequence()
                    .Append(canvasGroup.DOFade(0, firstTime))
                    .Join(content.DOAnchorPosX(startPos, firstTime))
                    .OnComplete(() =>
                    {
                        OnReverseCompleteCallback?.Invoke();
                        OnReverseCompleteCallback = null;
                    });
                mainSequence.Restart();
            }
            else
            {
                content.ChangeAnchorY(endPos);
                mainSequence = DOTween.Sequence()
                    .Append(canvasGroup.DOFade(0, firstTime))
                    .Join(content.DOAnchorPosY(startPos, firstTime))
                    .OnComplete(() =>
                    {
                        OnReverseCompleteCallback?.Invoke();
                        OnReverseCompleteCallback = null;
                    });
                ;
                mainSequence.Restart();
            }

#else
            canvasGroup.alpha = 0f;
            if (moveType == MoveType.MoveX)
                content.ChangeAnchorX(startPos);
            else
                content.ChangeAnchorY(startPos);
            OnReverseCompleteCallback?.Invoke();
            OnReverseCompleteCallback = null;
#endif
            return this;
        }
    }

    public enum MoveType
    {
        MoveX,
        MoveY
    }
}