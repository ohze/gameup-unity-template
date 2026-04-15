using UnityEngine;
using System.Collections;
using DG.Tweening;
using GameUp.Core.Helpers;
using GameUp.Core.UI;

namespace GameUp.Core.Tutorial
{
    public class TutorialController : MonoSingleton<TutorialController>
    {
        [Header("Talk")]
        [SerializeField] private TalkTutorial talkTutorial;
        [Header("Focus")]
        [SerializeField] private FocusItem focusItem;
        [SerializeField] private MultiFocusItem multiFocusItem;
        [Header("Arrow")]
        [SerializeField] private Transform arrowContainer;
        [SerializeField] private TutorialArrow tutorialArrowPrefab;

        [Header("Hand Drag")]
        [SerializeField] private float handDragDuration = 1f;
        [SerializeField] private Transform handDragPrefab;

        private Coroutine _coroutineTutorial;
        private Tween _handDragTween;
        private readonly Vector3[] _targetWorldCorners = new Vector3[4];
        private SOTutorialStep _currentStep;


        #region Run Tutorial

        [Button]
        public void RunTutorial(TutorialType tutorialType)
        {
            var tutorial = SOTutorialController.Tutorials[tutorialType];
            if (tutorial == null) return;
            RunTutorial(tutorial);
        }

        [Button]
        public void RunTutorial(SOTutorialType tutorial)
        {
            if (_coroutineTutorial != null) StopCoroutine(_coroutineTutorial);
            _coroutineTutorial = StartCoroutine(IERunTutorial(tutorial));
        }

        private IEnumerator IERunTutorial(SOTutorialType tutorial)
        {
            if (tutorial == null || tutorial.tutorialSteps == null || tutorial.tutorialSteps.Count == 0)
                yield break;

            yield return new WaitUntil(() => !UIPopup.IsPopupOn);
            foreach (var step in tutorial.tutorialSteps)
            {
                if (step == null || step.Complete)
                    continue;

                CompleteStep();
                _currentStep = step;
                var destination = DestinationPoint.GetFirstDestination(step.destinationPoint);
                var destination2 = DestinationPoint.GetFirstDestination(step.destinationPoint2);
                var addedTutorialClickHandler = false;
                if (step.useTalk)
                {
                    talkTutorial.ShowTalk(step.talkText);
                }
                else
                {
                    talkTutorial.HideTalk();
                }

                if (step.useFocus)
                {
                    if (step.focusType == FocusType.Multi)
                    {
                        focusItem.HideView();
                        multiFocusItem.ShowView(destination, destination2);
                    }
                    else
                    {
                        multiFocusItem.HideView();
                        focusItem.ShowView(destination);
                        var tutorialClickHandler = destination.gameObject.AddComponent<TutorialClickHandler>();
                        tutorialClickHandler.SetClickAction(MarkComplete);
                        addedTutorialClickHandler = true;
                    }
                }
                else
                {
                    focusItem.HideView();
                    multiFocusItem.HideView();
                }

                if (step.useArrow && !step.useHandDrag)
                {
                    var arrow = GUPool.Spawn(tutorialArrowPrefab, arrowContainer);
                    arrow.ShowView(destination, step.arrowDirection);
                    if (!addedTutorialClickHandler)
                    {
                        var tutorialClickHandler = destination.gameObject.AddComponent<TutorialClickHandler>();
                        tutorialClickHandler.SetClickAction(MarkComplete);
                        addedTutorialClickHandler = true;
                    }
                }
                else if (step.useHandDrag)
                {
                    GUPool.DeSpawnAll(tutorialArrowPrefab);
                    _handDragTween?.Kill();
                    GUPool.DeSpawnAll(handDragPrefab);
                    if (destination != null && destination2 != null)
                    {
                        var handDrag = GUPool.Spawn(handDragPrefab, arrowContainer);
                        PlayHandDragTween(handDrag, destination, destination2);
                    }
                }
                else
                {
                    GUPool.DeSpawnAll(tutorialArrowPrefab);
                }

                yield return new WaitUntil(() => step.Complete);
                CompleteStep();
            }

            tutorial.SetComplete();
            CheckAllTutDone();
        }
        #endregion

        #region Complete Tutorial
        [Button]
        public void MarkComplete()
        {
            if (_currentStep == null) return;
            _currentStep.MarkComplete();
        }

        private void CompleteStep()
        {
            _currentStep = null;
            talkTutorial.HideTalk();
            focusItem.HideView();
            multiFocusItem.HideView();
            GUPool.DeSpawnAll(tutorialArrowPrefab);
            _handDragTween?.Kill();
            _handDragTween = null;
            GUPool.DeSpawnAll(handDragPrefab);
        }
        private void CheckAllTutDone()
        {
            if (SOTutorialController.Instance.IsCompleteAll)
            {
                if (_coroutineTutorial != null) StopCoroutine(_coroutineTutorial);
                _coroutineTutorial = null;
                CompleteStep();
            }
        }

        private void PlayHandDragTween(Transform handDrag, Transform startTarget, Transform endTarget)
        {
            if (handDrag == null || startTarget == null || endTarget == null)
                return;

            if (handDrag is RectTransform handRect && handRect.parent is RectTransform parentRect)
            {
                if (TryGetAnchoredPosition(startTarget, parentRect, out var startAnchoredPos)
                    && TryGetAnchoredPosition(endTarget, parentRect, out var endAnchoredPos))
                {
                    handRect.anchoredPosition = startAnchoredPos;
                    _handDragTween = handRect.DOAnchorPos(endAnchoredPos, handDragDuration)
                        .SetEase(Ease.Linear)
                        .SetLoops(-1, LoopType.Restart);
                    return;
                }
            }

            var startWorldPos = GetTargetWorldCenter(startTarget);
            var endWorldPos = GetTargetWorldCenter(endTarget);
            handDrag.position = startWorldPos;
            _handDragTween = handDrag.DOMove(endWorldPos, handDragDuration)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Restart);
        }

        private bool TryGetAnchoredPosition(Transform target, RectTransform parentRect, out Vector2 anchoredPosition)
        {
            anchoredPosition = Vector2.zero;
            if (target == null || parentRect == null)
                return false;

            var worldCenter = GetTargetWorldCenter(target);
            var screenPoint = RectTransformUtility.WorldToScreenPoint(GetTargetCamera(target), worldCenter);
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, GetCanvasCamera(parentRect), out anchoredPosition);
        }

        private Vector3 GetTargetWorldCenter(Transform target)
        {
            if (target is RectTransform targetRect)
            {
                targetRect.GetWorldCorners(_targetWorldCorners);
                return (_targetWorldCorners[0] + _targetWorldCorners[2]) * 0.5f;
            }

            return target.position;
        }

        private Camera GetTargetCamera(Transform target)
        {
            if (target is RectTransform rectTransform)
            {
                var canvasCamera = GetCanvasCamera(rectTransform);
                if (canvasCamera != null)
                    return canvasCamera;
            }

            return Camera.main;
        }

        private Camera GetCanvasCamera(Component component)
        {
            var canvas = component.GetComponentInParent<Canvas>();
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return null;

            return canvas.worldCamera;
        }
        #endregion
    }
}