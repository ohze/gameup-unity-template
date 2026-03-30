using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using GameUp.Core;

namespace GameUp.Core.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class ToastItem : MonoBehaviour
    {
        private static readonly List<ToastItem> _Instances = new();

        [SerializeField] private CanvasGroup group;
        [SerializeField] private TextMeshProUGUI toastTxt;
        [SerializeField] private RectTransform rectTrs;

        private Sequence _moveSeq;
        private float _showPosY;
        private float _timeShow;

        private void OnEnable()
        {
            _Instances.Add(this);
        }

        private void OnDisable()
        {
            _Instances.Remove(this);
        }

        public ToastItem SetTimeShow(float t)
        {
            _timeShow = t;
            return this;
        }

        public ToastItem SetStartPosY(float pos)
        {
            _showPosY = pos;
            return this;
        }

        public ToastItem SetText(string str)
        {
            toastTxt.text = str;
            return this;
        }

        public void ShowToast()
        {
            rectTrs.ChangeAnchorY(_showPosY - 100);
            group.alpha = 0;
            PlayAnim();
        }

        [Button]
        private void PlayAnim()
        {
            gameObject.Show();
            _moveSeq?.Kill();
            group.alpha = 0;
            _moveSeq = DOTween.Sequence().Append(group.DOFade(1, 0.2f))
                .Join(rectTrs.DOAnchorPosY(_showPosY, 0.2f))
                .AppendInterval(_timeShow)
                .Append(rectTrs.DOAnchorPosY(_showPosY + 50, 0.1f))
                .Join(group.DOFade(0, 0.1f))
                .OnComplete(() => { GUPool.DeSpawn(this); });
        }

        public static void RemoveOtherToast()
        {
            for (var i = _Instances.Count - 1; i >= 0; i--) GUPool.DeSpawn(_Instances[i]);
        }
    }
}