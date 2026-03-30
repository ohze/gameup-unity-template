using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameUp.Core.UI
{
    public class CustomButtonGroup : MonoBehaviour
    {
        [SerializeField] private List<CustomSelectButton> buttons;
        private readonly Signal<int> _onChangeSelectButton = new();
        [SerializeField] private int defaultSelectedIndex = -1;

        private int _currentSelectedIndex = -1;

        private void Awake()
        {
            if (defaultSelectedIndex >= 0)
                SetSelected(defaultSelectedIndex, notify: false);
        }

        private void OnEnable()
        {
            if (buttons == null) return;

            for (var i = 0; i < buttons.Count; i++)
            {
                var button = buttons[i];
                if (button == null) continue;

                var index = i;
                button.onClick.AddListener(() => { SetSelected(index, notify: true); });
            }
        }

        private void OnDisable()
        {
            if (buttons == null) return;

            for (var i = 0; i < buttons.Count; i++)
            {
                var button = buttons[i];
                if (button == null) continue;

                var index = i;
                button.onClick.RemoveListener(() => { SetSelected(index, notify: true); });
            }
        }

        private void OnValidate()
        {
            buttons = GetComponentsInChildren<CustomSelectButton>().ToList();
        }

        public void AddListener(Action<int> callback)
        {
            _onChangeSelectButton.AddListener(callback);
        }

        public void RemoveListener(Action<int> callback)
        {
            _onChangeSelectButton.RemoveListener(callback);
        }

        public void SetSelected(int index, bool notify = true)
        {
            if (buttons == null || buttons.Count == 0) return;
            if (index < 0 || index >= buttons.Count) return;
            if (_currentSelectedIndex == index) return;

            _currentSelectedIndex = index;

            for (var i = 0; i < buttons.Count; i++)
            {
                var button = buttons[i];
                if (button == null) continue;
                button.IsSelect = i == index;
            }

            if (notify)
                _onChangeSelectButton.Dispatch(index);
        }
    }
}