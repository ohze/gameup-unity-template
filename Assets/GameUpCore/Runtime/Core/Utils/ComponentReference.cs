using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace GameUp.Core
{
    [Serializable]
    public class ComponentReference<TComponent> : AssetReference where TComponent : Component
    {
        public ComponentReference(string guid) : base(guid) { }

        // Sử dụng 'new' để che dấu phương thức của lớp cha, trả về đúng kiểu TComponent
        public new AsyncOperationHandle<TComponent> InstantiateAsync(Vector3 position, Quaternion rotation, Transform parent = null)
        {
            return Addressables.ResourceManager.CreateChainOperation(
                base.InstantiateAsync(position, rotation, parent),
                GetRequiredComponent);
        }

        public new AsyncOperationHandle<TComponent> InstantiateAsync(Transform parent = null, bool instantiateInWorldSpace = false)
        {
            return Addressables.ResourceManager.CreateChainOperation(
                base.InstantiateAsync(parent, instantiateInWorldSpace),
                GetRequiredComponent);
        }

        public AsyncOperationHandle<TComponent> LoadAssetAsync()
        {
            return Addressables.ResourceManager.CreateChainOperation(
                base.LoadAssetAsync<GameObject>(),
                GetRequiredComponent);
        }

        private AsyncOperationHandle<TComponent> GetRequiredComponent(AsyncOperationHandle<GameObject> handle)
        {
            if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
            {
                var component = handle.Result.GetComponent<TComponent>();
                if (component != null)
                {
                    return Addressables.ResourceManager.CreateCompletedOperation(component, string.Empty);
                }

                return Addressables.ResourceManager.CreateCompletedOperation<TComponent>(null,
                    $"Object {handle.Result.name} does not have component {typeof(TComponent).Name}");
            }

            return Addressables.ResourceManager.CreateCompletedOperation<TComponent>(null, "Async operation failed or result is null");
        }

        public override bool ValidateAsset(Object obj)
        {
            var go = obj as GameObject;
            return go != null && go.GetComponent<TComponent>() != null;
        }

        public override bool ValidateAsset(string path)
        {
#if UNITY_EDITOR
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return go != null && go.GetComponent<TComponent>() != null;
#else
            return false;
#endif
        }

        // Tiện ích giải phóng nhanh
        public void ReleaseInstance(AsyncOperationHandle<TComponent> handle)
        {
            if (handle.Result != null)
            {
                Addressables.ReleaseInstance(handle.Result.gameObject);
            }
            Addressables.Release(handle);
        }
    }
}