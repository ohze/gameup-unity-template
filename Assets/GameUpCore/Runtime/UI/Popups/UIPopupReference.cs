using System;

namespace GameUp.Core.UI
{
    [Serializable]
    public class UIPopupReference : ComponentReference<UIPopup>
    {
        public UIPopupReference(string guid) : base(guid)
        {
        }
    }
}