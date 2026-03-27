using System.Collections.Generic;
using UnityEngine;
using System;

namespace GameUp.Core.UI
{
    public class ObjectFinder : MonoSingleton<ObjectFinder>
    {
        [SerializeField] private List<ObjectType> objects = new();
        private readonly Dictionary<ObjectID, Transform> _cachedObjects = new();

        protected override void Awake()
        {
            base.Awake();
            RebuildCache();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            RebuildCache();
        }
#endif

        public static Transform GetObject(ObjectID t)
        {
            if (Instance._cachedObjects.TryGetValue(t, out var cachedTransform))
                return cachedTransform;

            Instance.RebuildCache();
            if (Instance._cachedObjects.TryGetValue(t, out cachedTransform))
                return cachedTransform;

            return null;
        }

        private void RebuildCache()
        {
            _cachedObjects.Clear();
            foreach (var obj in objects)
            {
                if (obj == null || obj.transform == null)
                    continue;

                _cachedObjects[obj.type] = obj.transform;
            }
        }
    }

    [Serializable]
    public class ObjectType
    {
        public ObjectID type;
        public Transform transform;
    }

    public enum ObjectID
    {
        PopupHolder,
        ScreenHolder,
    }
}