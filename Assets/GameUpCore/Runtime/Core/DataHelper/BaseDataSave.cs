using System;

namespace GameUp.Core
{
    /// <summary>
    /// Lớp cơ sở cho dữ liệu lưu cục bộ, có đánh version để nâng cấp save cũ khi đổi cấu trúc dữ liệu.
    ///
    /// <code>
    /// public class PlayerData : BaseDataSave&lt;PlayerData&gt;
    /// {
    ///     public int coin;
    ///     public int gem;
    ///
    ///     protected override int Version => 2;               // tăng mỗi lần đổi schema
    ///     protected override void InitDefault() => coin = 100;
    ///     protected override void InitHasKey() { }
    ///     protected override void Migrate(int fromVersion)
    ///     {
    ///         if (fromVersion &lt; 2) gem = 0;                  // field mới thêm ở v2
    ///     }
    /// }
    /// </code>
    /// </summary>
    [Serializable]
    public abstract class BaseDataSave<T> where T : BaseDataSave<T>, new()
    {
        /// <summary>
        /// Version của dữ liệu đang nằm trong bộ nhớ. Để public cho serializer ghi/đọc được;
        /// không sửa tay — <see cref="Create"/> tự cập nhật sau khi migrate.
        /// Save từ bản cũ chưa có field này sẽ đọc ra 0.
        /// </summary>
        public int dataVersion;

        protected virtual string Key => typeof(T).Name;

        /// <summary>Tăng giá trị này mỗi lần đổi cấu trúc dữ liệu, rồi xử lý trong <see cref="Migrate"/>.</summary>
        protected virtual int Version => 1;

        protected abstract void InitDefault();
        protected abstract void InitHasKey();

        /// <summary>
        /// Nâng cấp dữ liệu đọc từ bản cũ. <paramref name="fromVersion"/> = 0 nghĩa là save được tạo
        /// trước khi có versioning. Chạy trước <see cref="InitHasKey"/>.
        /// </summary>
        protected virtual void Migrate(int fromVersion)
        {
        }

        public static T Create()
        {
            return CreateInternal(null);
        }

        public static T CreateWithInit(Action<T> initCallback)
        {
            return CreateInternal(initCallback);
        }

        private static T CreateInternal(Action<T> initCallback)
        {
            var instance = new T();
            var key = instance.Key;

            if (LocalStorageUtils.HasKey(key))
            {
                var data = LocalStorageUtils.GetObject<T>(key);
                if (data != null)
                {
                    data.ApplyMigration();
                    data.InitHasKey();
                    data.Save();
                    return data;
                }

                GULogger.Warning("DataSave", $"Không đọc được dữ liệu '{key}', tạo lại từ mặc định.");
            }

            if (initCallback != null) initCallback(instance);
            else instance.InitDefault();

            instance.dataVersion = instance.Version;
            instance.Save();
            return instance;
        }

        private void ApplyMigration()
        {
            var loadedVersion = dataVersion;
            if (loadedVersion == Version) return;

            if (loadedVersion > Version)
            {
                // Save mới hơn code (thường do cài đè bản build cũ) — giữ nguyên dữ liệu, chỉ cảnh báo.
                GULogger.Warning("DataSave",
                    $"'{Key}' có version {loadedVersion} mới hơn code ({Version}). Bỏ qua migrate.");
                return;
            }

            try
            {
                Migrate(loadedVersion);
            }
            catch (Exception e)
            {
                GULogger.Exception(e, "DataSave");
            }

            dataVersion = Version;
        }

        /// <summary>Ghi dữ liệu hiện tại xuống bộ nhớ cục bộ. Gọi sau khi thay đổi field.</summary>
        public void Save()
        {
            LocalStorageUtils.SetObject(Key, this);
        }
    }
}
