using GameUp.Core;
using UnityEngine;

namespace GameUp.Core.UI
{
    public class Toast : MonoSingleton<Toast>
    {
        [SerializeField] private ToastItem prefabItem;
        [SerializeField] private RectTransform contentHolder;

        public static void Show(string str, float timeShow = 1.3f, float showPosY = 0)
        {
            ToastItem.RemoveOtherToast();
            var item = GUPool.Spawn(Instance.prefabItem, Instance.contentHolder);
            item.transform.localPosition = Vector3.zero;
            item.SetTimeShow(timeShow).SetStartPosY(showPosY).SetText(str).ShowToast();
        }

        public static void Close()
        {
            ToastItem.RemoveOtherToast();
        }
    }
}