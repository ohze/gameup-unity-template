using UnityEngine;
using UnityEngine.UI;

namespace GameUp.Core
{
    public static class UIExtension
    {
        public static void SetAnchor(this Image img, Vector2 anchor)
        {
            img.GetComponent<RectTransform>().anchoredPosition = anchor;
        }

        public static void ChangeAlpha(this Graphic graphic, float a)
        {
            var color = graphic.color;
            color.a = a;
            graphic.color = color;
        }

        public static void ChangeAnchorX(this RectTransform rect, float x)
        {
            var pos = rect.anchoredPosition;
            pos.x = x;
            rect.anchoredPosition = pos;
        }

        public static void ChangeAnchorY(this RectTransform rect, float y)
        {
            var pos = rect.anchoredPosition;
            pos.y = y;
            rect.anchoredPosition = pos;
        }

        public static void ChangeSizeX(this RectTransform rect, float x)
        {
            var size = rect.sizeDelta;
            size.x = x;
            rect.sizeDelta = size;
        }

        public static void ChangeSizeY(this RectTransform rect, float y)
        {
            var size = rect.sizeDelta;
            size.y = y;
            rect.sizeDelta = size;
        }

        public static void SetLeft(this RectTransform rt, float left)
        {
            rt.offsetMin = new Vector2(left, rt.offsetMin.y);
        }

        public static void SetRight(this RectTransform rt, float right)
        {
            rt.offsetMax = new Vector2(-right, rt.offsetMax.y);
        }

        public static void SetFullStretch(this RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }

        // Căn giữa Anchor (Center-Middle)
        public static void SetAnchorCenter(this RectTransform rt)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        public static void RotateTarget(this RectTransform a, Transform target, float speed)
        {
            var rotation = Quaternion.LookRotation(target.position - a.position);
            rotation.x = 0;
            rotation.z = 0;
            a.rotation = Quaternion.Slerp(a.rotation, rotation, Time.deltaTime * speed);
        }

        public static void SetScaleByViewSize(this Image im, Sprite sp, float viewSize)
        {
            im.Show();
            im.sprite = sp;
            im.SetNativeSize();
            var tex = sp.texture;
            var scaleSize = viewSize / Mathf.Max(tex.width, tex.height);
            im.transform.localScale = scaleSize * Vector3.one;
        }

        public static void SetRaycastable(this GameObject go, bool enabled)
        {
            var graphics = go.GetComponentsInChildren<Graphic>();
            foreach (var g in graphics)
            {
                g.raycastTarget = enabled;
            }
        }

        public static void ChangeColor(this Graphic graphic, Color newColor, bool keepAlpha = true)
        {
            if (keepAlpha)
            {
                newColor.a = graphic.color.a;
            }
            graphic.color = newColor;
        }

        // Cuộn về đầu/cuối ScrollRect lập tức
        public static void ScrollToTop(this ScrollRect scrollRect) => scrollRect.normalizedPosition = new Vector2(0, 1);
        public static void ScrollToBottom(this ScrollRect scrollRect) => scrollRect.normalizedPosition = new Vector2(0, 0);

        // Thêm Listener nhanh (giảm bớt boilerplate code)
        public static void OnClick(this Button btn, UnityEngine.Events.UnityAction action)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(action);
        }

        public static bool IsPointInside(this RectTransform rect, Vector2 screenPoint, Camera cam = null)
        {
            return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, cam);
        }

        // Tự động căn chỉnh Image theo tỷ lệ khung hình mà không làm méo hình
        public static void SetNativeSizeWithMaxWidth(this Image img, float maxWidth)
        {
            img.SetNativeSize();
            float width = img.rectTransform.sizeDelta.x;
            float height = img.rectTransform.sizeDelta.y;

            if (width > maxWidth)
            {
                float ratio = maxWidth / width;
                img.rectTransform.sizeDelta = new Vector2(maxWidth, height * ratio);
            }
        }
    }
}