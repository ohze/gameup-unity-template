using UnityEngine;
using UnityEngine.UI;

namespace GameUp.Core.UI
{
    public class ChangeImageSpriteView : BaseSelectView
    {
        private const string LogTag = nameof(ChangeImageSpriteView);

        [SerializeField] private bool setNativeSize = true;
        [SerializeField] private Image image;
        [SerializeField] private Sprite selectSprite, disableSprite;

        private bool _hasLoggedMissingRefs;

        public override void ChangeSelect(bool isSelected)
        {
            IsSelected = isSelected;
            if (image == null)
            {
                if (!_hasLoggedMissingRefs)
                {
                    _hasLoggedMissingRefs = true;
                    GULogger.Error(LogTag, $"{name}: Missing reference `image`.");
                }

                return;
            }

            image.sprite = isSelected ? selectSprite : disableSprite;
            
            if (setNativeSize && image.sprite != null)
                image.SetNativeSize();  
        }
    }
}