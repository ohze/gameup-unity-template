using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GameUp.Core.UI
{
    public class UIBaseView : MonoBehaviour, IView, IAnimate
    {
        [SerializeField] private UIAnimationMode animationMode = UIAnimationMode.Custom;
        [SerializeField] private string animationTypeName = typeof(UIFadeAnimation).AssemblyQualifiedName;

        protected IAnimation _anim;

        private void Reset()
        {
            animationMode = UIAnimationMode.Custom;
            animationTypeName = typeof(UIFadeAnimation).AssemblyQualifiedName;
            _anim = ResolveAnimation(animationMode, animationTypeName);
        }

        protected virtual void Awake()
        {
            _anim = ResolveAnimation(animationMode, animationTypeName);
        }

#if UNITY_EDITOR
        private bool _animationSyncScheduled;

        /// <summary>
        /// Lớp con PHẢI override (không khai báo lại OnValidate) — Unity chỉ gọi bản của class dẫn xuất,
        /// khai báo trùng tên sẽ che mất bản này và animation không còn được đồng bộ.
        /// </summary>
        protected virtual void OnValidate()
        {
            if (animationMode == UIAnimationMode.Default)
            {
                animationTypeName = null;
            }
            else if (string.IsNullOrWhiteSpace(animationTypeName))
            {
                animationTypeName = typeof(UIFadeAnimation).AssemblyQualifiedName;
            }

            ScheduleAnimationSync();
        }

        /// <summary>
        /// Unity cấm AddComponent/DestroyImmediate ngay trong OnValidate (spam warning, dễ hỏng prefab),
        /// nên hoãn phần thêm/bớt component animation sang delayCall.
        /// </summary>
        private void ScheduleAnimationSync()
        {
            if (_animationSyncScheduled) return;
            _animationSyncScheduled = true;

            EditorApplication.delayCall += () =>
            {
                _animationSyncScheduled = false;

                if (this == null) return;
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;

                var previousMode = animationMode;
                var previousType = animationTypeName;

                _anim = ResolveAnimation(animationMode, animationTypeName);

                // ResolveAnimation có thể tự fallback về Default khi type không hợp lệ.
                if (previousMode != animationMode || previousType != animationTypeName)
                {
                    EditorUtility.SetDirty(this);
                }
            };
        }
#endif

        private IAnimation ResolveAnimation(UIAnimationMode mode, string typeName)
        {
            if (mode == UIAnimationMode.Default)
            {
                RemoveAllCustomAnimations();
                var defaultAnim = EnsureDefaultAnimation();
                return defaultAnim;
            }

            if (TryResolveAndEnsureComponent(typeName, out var resolved))
            {
                RemoveDefaultOnlyAnimations();
                RemoveOtherCustomAnimations(resolved);
                return resolved;
            }

            animationMode = UIAnimationMode.Default;
            animationTypeName = null;
            RemoveAllCustomAnimations();
            return EnsureDefaultAnimation();
        }

        private UIDefaultAnimation EnsureDefaultAnimation()
        {
            var existingDefaults = GetComponents<UIDefaultAnimation>();
            for (int i = 0; i < existingDefaults.Length; i++)
            {
                var d = existingDefaults[i];
                if (d == null) continue;
                if (d is UIBaseAnimation) continue;
                return d;
            }

            return gameObject.AddComponent<UIDefaultAnimation>();
        }

        private void RemoveDefaultOnlyAnimations()
        {
            var defaults = GetComponents<UIDefaultAnimation>();
            for (int i = defaults.Length - 1; i >= 0; i--)
            {
                var d = defaults[i];
                if (d == null) continue;
                if (d is UIBaseAnimation) continue;
                DestroyAnimationComponent(d);
            }
        }

        private void RemoveAllCustomAnimations()
        {
            var all = GetComponents<UIBaseAnimation>();
            for (int i = all.Length - 1; i >= 0; i--)
            {
                var anim = all[i];
                if (anim == null) continue;
                DestroyAnimationComponent(anim);
            }
        }

        private void RemoveOtherCustomAnimations(object keep)
        {
            var keepComponent = keep as Component;
            var all = GetComponents<UIBaseAnimation>();
            for (int i = all.Length - 1; i >= 0; i--)
            {
                var anim = all[i];
                if (anim == null) continue;
                if (keepComponent != null && anim == keepComponent) continue;
                DestroyAnimationComponent(anim);
            }
        }

        private void DestroyAnimationComponent(Component c)
        {
            if (Application.isPlaying)
            {
                Destroy(c);
                return;
            }

            DestroyImmediate(c);
        }

        private bool TryResolveAndEnsureComponent(string typeName, out IAnimation anim)
        {
            anim = null;
            if (string.IsNullOrWhiteSpace(typeName)) return false;

            var type = Type.GetType(typeName);
            if (type == null) return false;
            if (!typeof(UIBaseAnimation).IsAssignableFrom(type)) return false;

            var existing = GetComponent(type) as IAnimation;
            if (existing != null)
            {
                anim = existing;
                return true;
            }

            if (gameObject.AddComponent(type) is IAnimation added)
            {
                anim = added;
                return true;
            }

            return false;
        }

        public void Open()
        {
            OnOpen();
        }

        public void Close()
        {
            OnClose();
        }

        #region IAnimate

        public virtual void OnOpen()
        {
            _anim.OnStop();
            _anim.OnStart();
        }

        public void OnStop()
        {
            _anim.OnStop();
        }

        public virtual void OnClose(Action onComplete = null)
        {
            _anim.OnStop();

            // Phải set callback TRƯỚC khi OnReverse: khi không có DOTween, OnReverse gọi callback ngay lập tức.
            _anim.SetReverseCompleteCallback(() =>
            {
                if (gameObject.activeSelf) gameObject.Hide();
                onComplete?.Invoke();
            }).OnReverse();
        }

        #endregion
    }
}