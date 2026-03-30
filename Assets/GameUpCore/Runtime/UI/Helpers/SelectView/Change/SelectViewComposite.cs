using System.Collections.Generic;
using GameUp.Core;
using UnityEngine;

namespace GameUp.Core.UI
{
    public sealed class SelectViewComposite : BaseSelectView
    {
        private const string LogTag = nameof(SelectViewComposite);

        [SerializeField] private List<MonoBehaviour> views = new List<MonoBehaviour>();
        private bool _hasLoggedInvalidItems;

        public override void ChangeSelect(bool isSelected)
        {
            IsSelected = isSelected;

            for (var i = 0; i < views.Count; i++)
            {
                var view = views[i];
                if (view == null)
                    continue;

                if (view is ISelectView selectView)
                {
                    selectView.ChangeSelect(isSelected);
                    continue;
                }

                if (!_hasLoggedInvalidItems)
                {
                    _hasLoggedInvalidItems = true;
                    GULogger.Warning(LogTag, $"{name}: `views` contains a component that does not implement ISelectView ({view.GetType().Name}).");
                }
            }
        }
    }
}

