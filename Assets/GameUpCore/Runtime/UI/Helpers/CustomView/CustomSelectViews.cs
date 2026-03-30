using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameUp.Core.UI
{
    public class CustomSelectViews : MonoBehaviour
    {
        [SerializeField] private List<BaseSelectView> views;

        private bool _isSelect;

        public bool IsSelect
        {
            get => _isSelect;
            set
            {
                if (_isSelect == value) return;
                _isSelect = value;
                if (views == null) return;

                for (var i = 0; i < views.Count; i++)
                {
                    var view = views[i];
                    if (view == null) continue;
                    view.ChangeSelect(value);
                }
            }
        }

        private void OnValidate()
        {
            views = GetComponents<BaseSelectView>().ToList();
        }
    }
}