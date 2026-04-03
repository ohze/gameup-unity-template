using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GameUp.Core.UI
{
    [RequireComponent(typeof(CustomSelectViews))]
    public class CustomSelectButton : MonoBehaviour
    {
        [SerializeField] private Button btn;
        [SerializeField] private CustomSelectViews view;
        public UnityEvent onClick;

        private bool _isSelect;
        private UnityAction _clickHandler;

        public bool IsSelect
        {
            get => _isSelect;
            set
            {
                if (_isSelect == value) return;
                _isSelect = value;
                if (view != null)
                    view.IsSelect = value;
            }
        }

        private void Awake()
        {
            _clickHandler = () => { onClick?.Invoke(); };
        }

        private void OnEnable()
        {
            btn?.onClick.AddListener(_clickHandler);
        }

        private void OnDisable()
        {
            btn?.onClick.RemoveListener(_clickHandler);
        }

        private void OnValidate()
        {
            if (view == null)
                view = GetComponent<CustomSelectViews>() ?? GetComponentInChildren<CustomSelectViews>(true);

            if (btn == null)
                btn = GetComponent<Button>() ?? GetComponentInChildren<Button>(true);
        }
    }
}