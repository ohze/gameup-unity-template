using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameUp.Core.UI
{
    public class ChangeGameObjectsActive : BaseChangeActiveView
    {
        private const string LogTag = nameof(ChangeGameObjectsActive);

        [SerializeField] private List<GameObjectChangeActive> items = new List<GameObjectChangeActive>();
        private bool _hasLoggedMissingRefs;

        public override bool IsActive
        {
            get => isActive;
            set
            {
                isActive = value;
                for (var i = 0; i < items.Count; i++)
                    items[i]?.ChangeView(value);
            }
        }

        public override void ChangeView(bool enable)
        {
            if (!_hasLoggedMissingRefs)
            {
                var nullCount = CountNullObj(items);
                if (nullCount > 0)
                {
                    _hasLoggedMissingRefs = true;
                    GULogger.Warning(LogTag, $"{name}: `items` has entries with missing `obj` (count={nullCount}).");
                }
            }

            IsActive = enable;
        }

        private static int CountNullObj(List<GameObjectChangeActive> list)
        {
            if (list == null) return 0;

            var count = 0;
            for (var i = 0; i < list.Count; i++)
            {
                var item = list[i];
                if (item != null && item.obj == null)
                    count++;
            }

            return count;
        }
    }

    [Serializable]
    public class GameObjectChangeActive
    {
        public GameObject obj;
        public bool isActiveOnViewActive;

        public void ChangeView(bool isActive)
        {
            if (obj == null)
                return;

            obj.SetActive(isActiveOnViewActive ? isActive : !isActive);
        }
    }
}