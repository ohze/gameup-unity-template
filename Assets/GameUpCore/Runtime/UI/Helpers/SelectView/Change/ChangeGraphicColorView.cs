using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GameUp.Core.UI
{
    public class ChangeGraphicColorView : BaseSelectView
    {
        private const string LogTag = nameof(ChangeGraphicColorView);

        [SerializeField] private List<Graphic> graphics = new List<Graphic>();
        [SerializeField] private Color[] imColors;
        [SerializeField] private Color selectedColor = Color.white;
        [SerializeField] private Color deselectedColor = Color.white;

        private bool _hasLoggedMissingRefs;

        public override void ChangeSelect(bool isSelected)
        {
            IsSelected = isSelected;

            if (!_hasLoggedMissingRefs)
            {
                var nullGraphics = CountNull(graphics);
                if (nullGraphics > 0)
                {
                    _hasLoggedMissingRefs = true;
                    GULogger.Warning(LogTag, $"{name}: `graphics` contains null entries (count={nullGraphics}).");
                }
            }

            var color = GetColor(isSelected);
            for (var i = 0; i < graphics.Count; i++)
            {
                var g = graphics[i];
                if (g != null) g.color = color;
            }
        }

        private Color GetColor(bool isSelected)
        {
            if (imColors != null && imColors.Length >= 2)
                return isSelected ? imColors[0] : imColors[1];

            if (!_hasLoggedMissingRefs)
            {
                _hasLoggedMissingRefs = true;
                var len = imColors == null ? 0 : imColors.Length;
                GULogger.Warning(LogTag,
                    $"{name}: `imColors` is missing/invalid (len={len}). Falling back to selected/deselectedColor.");
            }

            return isSelected ? selectedColor : deselectedColor;
        }

        private static int CountNull(List<Graphic> list)
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