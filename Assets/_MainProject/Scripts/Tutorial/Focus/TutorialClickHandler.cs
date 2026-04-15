using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GameUp.Core.Tutorial
{
    public class TutorialClickHandler : MonoBehaviour, IPointerClickHandler, IPointerDownHandler
    {
        private Action _onClick;

        public void SetClickAction(Action callBack)
        {
            _onClick = callBack;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _onClick?.Invoke();
            Destroy(this);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _onClick?.Invoke();
            Destroy(this);
        }
    }
}