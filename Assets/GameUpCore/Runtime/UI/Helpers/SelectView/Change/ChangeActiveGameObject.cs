using System.Collections.Generic;
using UnityEngine;

namespace GameUp.Core.UI
{
    public class ChangeActiveGameObject : BaseSelectView
    {
        private const string LogTag = nameof(ChangeActiveGameObject);

        [SerializeField] private List<GameObject> activeObjects = new List<GameObject>();
        [SerializeField] private List<GameObject> disableObjects = new List<GameObject>();

        private bool _hasLoggedMissingRefs;

        public override void ChangeSelect(bool isSelected)
        {
            IsSelected = isSelected;

            if (!_hasLoggedMissingRefs)
            {
                var nullActive = CountNull(activeObjects);
                var nullDisable = CountNull(disableObjects);
                if (nullActive > 0 || nullDisable > 0)
                {
                    _hasLoggedMissingRefs = true;
                    GULogger.Warning(LogTag,
                        $"{name}: List contains null entries (activeObjects={nullActive}, disableObjects={nullDisable}).");
                }
            }

            for (var i = 0; i < activeObjects.Count; i++)
            {
                var obj = activeObjects[i];
                obj?.SetActive(isSelected);
            }

            for (var i = 0; i < disableObjects.Count; i++)
            {
                var obj = disableObjects[i];
                obj?.SetActive(!isSelected);
            }
        }

        private static int CountNull(List<GameObject> list)
        {
            if (list == null) return 0;

            var count = 0;
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] == null)
                    count++;
            }

            return count;
        }
    }
}