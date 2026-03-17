using System;

namespace GameUp.Core
{
    [Serializable]
    public abstract class BaseDataSave<T> where T : BaseDataSave<T>, new()
    {
        protected virtual string Key => typeof(T).Name;
        protected abstract void InitDefault();
        protected abstract void InitHasKey();

        public static T Create()
        {
            var instance = new T();
            var key = instance.Key;

            if (LocalStorageUtils.HasKey(key))
            {
                var data = LocalStorageUtils.GetObject<T>(key);
                if (data != null)
                {
                    data.InitHasKey();
                    data.Save();
                    return data;
                }
            }

            instance.InitDefault();
            instance.Save();
            return instance;
        }

        public static T CreateWithInit(Action<T> initCallback)
        {
            var instance = new T();
            var key = instance.Key;

            if (LocalStorageUtils.HasKey(key))
            {
                var data = LocalStorageUtils.GetObject<T>(key);
                if (data != null)
                {
                    data.InitHasKey();
                    data.Save();
                    return data;
                }
            }

            initCallback?.Invoke(instance);
            instance.Save();
            return instance;
        }

        protected void Save()
        {
            LocalStorageUtils.SetObject(Key, this);
        }
    }
}