using UnityEngine;

namespace GameUp.Core.UI
{
    public abstract class BaseChangeActiveView : MonoBehaviour
    {
        protected bool isActive;
        public abstract bool IsActive { get; set; }
        public abstract void ChangeView(bool isActive);
    }
}