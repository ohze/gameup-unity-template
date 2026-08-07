#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using FullSerializer;
using UnityEngine;

namespace GameUp.Core.Editor
{
    /// <summary>
    /// Dò các bản save của <see cref="BaseDataSave{T}"/> đang có trong PlayerPrefs.
    ///
    /// Cần thiết vì <c>Key</c> có thể phụ thuộc dữ liệu của chính instance
    /// (ví dụ <c>Key => $"hero_{HeroId}"</c> — một class sinh ra nhiều bản save).
    /// Instance mặc định chỉ cho ra đúng một key nên phải tìm các key còn lại bằng hai đường:
    ///
    /// 1. Đọc thẳng store của PlayerPrefs (file prefs trên Linux, plist trên macOS, registry trên Windows)
    ///    rồi lấy <c>$type</c> trong JSON đã giải mã để biết key đó thuộc class nào — chính xác tuyệt đối.
    /// 2. Nếu không đọc được store: dò field quyết định key rồi quét id 0..N bằng PlayerPrefs.HasKey.
    /// </summary>
    internal static class GUDataSaveScanner
    {
        /// <summary>Giới hạn id khi phải dò bằng HasKey (đường 2).</summary>
        public const int ScanRange = 200;

        private const BindingFlags MemberFlags = BindingFlags.Public | BindingFlags.NonPublic |
                                                 BindingFlags.Instance | BindingFlags.DeclaredOnly;

        #region Key / Version của một data class

        public static bool IsDataSave(Type type)
        {
            if (type == null || type.IsAbstract || type.IsGenericTypeDefinition) return false;

            for (var t = type.BaseType; t != null; t = t.BaseType)
            {
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(BaseDataSave<>)) return true;
            }

            return false;
        }

        public static PropertyInfo FindProperty(Type type, string name)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                var property = t.GetProperty(name, MemberFlags);
                if (property != null) return property;
            }

            return null;
        }

        /// <summary>Tìm method kể cả protected/private, duyệt ngược lên base (GetMethod không thấy non-public của base).</summary>
        public static MethodInfo FindMethod(Type type, string name)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                var method = t.GetMethod(name, MemberFlags);
                if (method != null) return method;
            }

            return null;
        }

        /// <summary>Tìm field kể cả protected/private, duyệt ngược lên base.</summary>
        public static FieldInfo FindField(Type type, string name)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                var field = t.GetField(name, MemberFlags);
                if (field != null) return field;
            }

            return null;
        }

        /// <summary>Đọc property <c>Key</c> (protected) của một instance.</summary>
        public static string ReadKey(object instance)
        {
            if (instance == null) return null;

            try
            {
                var property = FindProperty(instance.GetType(), "Key");
                return property?.GetValue(instance, null) as string;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Tìm field mà đổi giá trị thì <c>Key</c> đổi theo — tức field quyết định key.
        /// Làm bằng cách gán giá trị lạ rồi so key, nên không cần data class khai báo gì thêm.
        /// </summary>
        public static List<FieldInfo> FindKeyFields(Type type)
        {
            var result = new List<FieldInfo>();

            object probe;
            try
            {
                probe = Activator.CreateInstance(type);
            }
            catch (Exception)
            {
                return result;
            }

            var baseKey = ReadKey(probe);
            if (string.IsNullOrEmpty(baseKey)) return result;

            foreach (var field in GUDataSaveDrawer.GetSerializedFields(type))
            {
                var sentinel = MakeSentinel(field.FieldType);
                if (sentinel == null) continue;

                object original;
                try
                {
                    original = field.GetValue(probe);
                    field.SetValue(probe, sentinel);
                }
                catch (Exception)
                {
                    continue;
                }

                var changedKey = ReadKey(probe);
                field.SetValue(probe, original);

                if (changedKey != baseKey) result.Add(field);
            }

            return result;
        }

        /// <summary>Giá trị "lạ" để thử: chỉ những kiểu có thể xuất hiện trong tên key mới cần thử.</summary>
        private static object MakeSentinel(Type type)
        {
            if (type == typeof(int)) return 987654321;
            if (type == typeof(long)) return 987654321L;
            if (type == typeof(short)) return (short)32100;
            if (type == typeof(byte)) return (byte)251;
            if (type == typeof(string)) return "__gu_probe__";
            if (type.IsEnum)
            {
                var values = Enum.GetValues(type);
                return values.Length > 1 ? values.GetValue(values.Length - 1) : null;
            }

            return null;
        }

        /// <summary>
        /// Dò key bằng cách gán id 0..<see cref="ScanRange"/> vào field quyết định key rồi hỏi HasKey.
        /// Trả về map key → giá trị field đã dùng, để sau này tạo lại đúng bản save đó.
        /// </summary>
        public static Dictionary<string, Dictionary<FieldInfo, object>> ScanIntKeys(Type type, List<FieldInfo> keyFields)
        {
            var result = new Dictionary<string, Dictionary<FieldInfo, object>>();

            // Nhiều field cùng quyết định key thì tổ hợp bùng nổ — trường hợp đó để đường đọc store lo.
            if (keyFields.Count != 1) return result;

            var field = keyFields[0];
            var isInt = field.FieldType == typeof(int);
            var isLong = field.FieldType == typeof(long);
            var isEnum = field.FieldType.IsEnum;
            if (!isInt && !isLong && !isEnum) return result;

            object probe;
            try
            {
                probe = Activator.CreateInstance(type);
            }
            catch (Exception)
            {
                return result;
            }

            var candidates = new List<object>();
            if (isEnum)
            {
                foreach (var value in Enum.GetValues(field.FieldType)) candidates.Add(value);
            }
            else
            {
                for (var i = 0; i < ScanRange; i++) candidates.Add(isLong ? (object)(long)i : i);
            }

            foreach (var candidate in candidates)
            {
                try
                {
                    field.SetValue(probe, candidate);
                }
                catch (Exception)
                {
                    continue;
                }

                var key = ReadKey(probe);
                if (string.IsNullOrEmpty(key) || result.ContainsKey(key)) continue;
                if (!LocalStorageUtils.HasKey(key)) continue;

                result[key] = new Dictionary<FieldInfo, object> { { field, candidate } };
            }

            return result;
        }

        #endregion

        #region SettingVar khai báo trong code

        public static bool IsSettingVar(Type type)
        {
            return FindSettingVarBase(type) != null;
        }

        /// <summary>Kiểu giá trị T của <c>SettingVar&lt;T&gt;</c> (bool/int/float/long…), null nếu không phải SettingVar.</summary>
        public static Type GetSettingValueType(Type type)
        {
            return FindSettingVarBase(type)?.GetGenericArguments()[0];
        }

        private static Type FindSettingVarBase(Type type)
        {
            if (type == null) return null;

            for (var t = type; t != null; t = t.BaseType)
            {
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(SettingVar<>)) return t;
            }

            return null;
        }

        /// <summary>
        /// Tìm các <c>SettingVar</c> khai báo dạng field static để biết key kể cả khi chưa lưu lần nào,
        /// và biết luôn kiểu giá trị (BooleanVar hay IntVar) thay vì phải đoán từ chuỗi đã lưu.
        ///
        /// Chỉ đọc field static — không gọi property getter, vì getter hay khởi tạo Singleton
        /// và sẽ đẻ GameObject ngay trong Editor.
        /// </summary>
        public static Dictionary<string, Type> FindDeclaredSettingVars()
        {
            var result = new Dictionary<string, Type>();

            foreach (var type in EnumerateCandidateTypes())
            {
                CollectSettingVars(type, result);
            }

            return result;
        }

        /// <summary>
        /// Chỉ duyệt assembly có tham chiếu tới GameUp.Core — class kế thừa BaseDataSave hay SettingVar
        /// bắt buộc phải tham chiếu, nên bỏ qua vài trăm assembly của Unity cho nhanh.
        /// </summary>
        public static IEnumerable<Type> EnumerateCandidateTypes()
        {
            var coreAssembly = typeof(SettingVar<>).Assembly;
            var coreName = coreAssembly.GetName().Name;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly != coreAssembly && !References(assembly, coreName)) continue;

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types.Where(t => t != null).ToArray();
                }
                catch (Exception)
                {
                    continue;
                }

                foreach (var type in types)
                {
                    if (type != null) yield return type;
                }
            }
        }

        private static bool References(Assembly assembly, string assemblyName)
        {
            try
            {
                foreach (var reference in assembly.GetReferencedAssemblies())
                {
                    if (reference.Name == assemblyName) return true;
                }
            }
            catch (Exception)
            {
                return false;
            }

            return false;
        }

        private static void CollectSettingVars(Type type, Dictionary<string, Type> result)
        {
            FieldInfo[] fields;
            try
            {
                fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static |
                                        BindingFlags.DeclaredOnly);
            }
            catch (Exception)
            {
                return;
            }

            foreach (var field in fields)
            {
                if (!IsSettingVar(field.FieldType)) continue;

                try
                {
                    // Đọc field static sẽ chạy static constructor của class khai báo — bọc try cho chắc.
                    var instance = field.GetValue(null);
                    if (instance == null) continue;

                    var key = FindField(instance.GetType(), "Key")?.GetValue(instance) as string;
                    if (!string.IsNullOrEmpty(key)) result[key] = instance.GetType();
                }
                catch (Exception)
                {
                    // Field chưa khởi tạo được (phụ thuộc runtime) — bỏ qua, đường đọc store vẫn tìm ra key.
                }
            }
        }

        #endregion

        #region Đọc thẳng store của PlayerPrefs

        /// <summary>Kết quả đọc store: tách sẵn dữ liệu dạng object và dạng giá trị đơn.</summary>
        public class StoreScan
        {
            /// <summary>Key → data class, lấy từ <c>$type</c> trong JSON.</summary>
            public readonly Dictionary<string, Type> DataSaves = new Dictionary<string, Type>();

            /// <summary>Key → giá trị đã giải mã của những key không phải JSON (SettingVar, cờ lưu bằng LocalStorageUtils…).</summary>
            public readonly Dictionary<string, string> Scalars = new Dictionary<string, string>();
        }

        /// <summary>
        /// Đọc mọi key trong PlayerPrefs rồi phân loại. Key của SDK khác không giải mã được nên tự bị loại.
        /// <paramref name="diagnostic"/> khác null nghĩa là không đọc được store (sẽ phải dựa vào ScanIntKeys).
        /// </summary>
        public static StoreScan ScanStore(out string diagnostic)
        {
            var result = new StoreScan();
            var store = ReadStore(out diagnostic);
            if (store == null) return result;

            foreach (var pair in store)
            {
                var value = TryDecrypt(pair.Value);
                if (string.IsNullOrEmpty(value)) continue;

                if (value.StartsWith("{", StringComparison.Ordinal))
                {
                    var type = ReadTypeFromJson(value);
                    if (type != null && IsDataSave(type)) result.DataSaves[pair.Key] = type;
                    continue;
                }

                result.Scalars[pair.Key] = value;
            }

            return result;
        }

        private static string TryDecrypt(string stored)
        {
            if (string.IsNullOrEmpty(stored)) return null;

            try
            {
                // Chuỗi lưu xuống được ghi bằng UTF8 kèm BOM, cắt luôn cho chắc (TrimStart không coi BOM là whitespace).
                var value = EncryptUtils.Decrypt(stored)?.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
                return IsPlainText(value) ? value : null;
            }
            catch (Exception)
            {
                // Key của SDK khác / dữ liệu không mã hoá — bỏ qua, không log để khỏi rác console.
                return null;
            }
        }

        /// <summary>
        /// Giải mã nhầm chuỗi base64 của SDK khác vẫn có xác suất lọt qua bước kiểm padding,
        /// nên loại thêm bằng cách đòi kết quả phải là text đọc được.
        /// </summary>
        private static bool IsPlainText(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;

            foreach (var c in value)
            {
                if (char.IsControl(c) && c != '\r' && c != '\n' && c != '\t') return false;
            }

            return true;
        }

        /// <summary>
        /// Lấy type từ khoá <c>$type</c> mà FullSerializer ghi kèm (do serialize theo kiểu tĩnh object).
        /// </summary>
        private static Type ReadTypeFromJson(string json)
        {
            try
            {
                var result = fsJsonParser.Parse(json, out var data);
                if (result.Failed || !data.IsDictionary) return null;

                if (!data.AsDictionary.TryGetValue("$type", out var typeData) || !typeData.IsString) return null;

                var typeName = typeData.AsString;
                var resolved = Type.GetType(typeName, false);
                if (resolved != null) return resolved;

                // "Namespace.Type, Assembly" mà Type.GetType không load được assembly → tìm trong các assembly đã nạp.
                var fullName = typeName.Split(',')[0].Trim();
                return AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType(fullName, false))
                    .FirstOrDefault(type => type != null);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Đọc toàn bộ cặp key → giá trị chuỗi thô của PlayerPrefs, hoặc null nếu không đọc được.</summary>
        private static Dictionary<string, string> ReadStore(out string diagnostic)
        {
            diagnostic = null;

            try
            {
#if UNITY_EDITOR_WIN
                return ReadWindowsStore(out diagnostic);
#elif UNITY_EDITOR_OSX
                return ReadMacStore(out diagnostic);
#else
                return ReadLinuxStore(out diagnostic);
#endif
            }
            catch (Exception e)
            {
                diagnostic = e.Message;
                return null;
            }
        }

#if !UNITY_EDITOR_WIN && !UNITY_EDITOR_OSX
        /// <summary>Linux: ~/.config/unity3d/&lt;company&gt;/&lt;product&gt;/prefs — XML, value bọc thêm một lớp base64.</summary>
        private static Dictionary<string, string> ReadLinuxStore(out string diagnostic)
        {
            diagnostic = null;

            var config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (string.IsNullOrEmpty(config))
                config = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");

            var root = Path.Combine(config, "unity3d");
            var path = FindPrefsFile(root);
            if (path == null)
            {
                diagnostic = $"Không tìm thấy file prefs trong {root}";
                return null;
            }

            var result = new Dictionary<string, string>();
            var document = XDocument.Load(path);

            foreach (var element in document.Descendants("pref"))
            {
                var name = (string)element.Attribute("name");
                if (string.IsNullOrEmpty(name)) continue;
                if ((string)element.Attribute("type") != "string") continue;

                try
                {
                    result[name] = Encoding.UTF8.GetString(Convert.FromBase64String(element.Value));
                }
                catch (FormatException)
                {
                    result[name] = element.Value;
                }
            }

            return result;
        }

        /// <summary>
        /// Unity thay ký tự không hợp lệ trong tên thư mục nên khớp theo tên đã chuẩn hoá,
        /// tránh trượt với những product name kiểu "Packventure: Loot &amp; Slash".
        /// </summary>
        private static string FindPrefsFile(string root)
        {
            if (!Directory.Exists(root)) return null;

            var exact = Path.Combine(root, Application.companyName, Application.productName, "prefs");
            if (File.Exists(exact)) return exact;

            var companyDirectory = Directory.EnumerateDirectories(root)
                .FirstOrDefault(d => Normalize(Path.GetFileName(d)) == Normalize(Application.companyName));
            if (companyDirectory == null) return null;

            var productDirectory = Directory.EnumerateDirectories(companyDirectory)
                .FirstOrDefault(d => Normalize(Path.GetFileName(d)) == Normalize(Application.productName));
            if (productDirectory == null) return null;

            var path = Path.Combine(productDirectory, "prefs");
            return File.Exists(path) ? path : null;
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            var builder = new StringBuilder(value.Length);
            foreach (var c in value)
            {
                if (char.IsLetterOrDigit(c)) builder.Append(char.ToLowerInvariant(c));
            }

            return builder.ToString();
        }
#endif

#if UNITY_EDITOR_OSX
        /// <summary>macOS: đọc qua `defaults export` để lấy giá trị mới nhất từ cfprefsd (file plist có thể còn cũ).</summary>
        private static Dictionary<string, string> ReadMacStore(out string diagnostic)
        {
            diagnostic = null;

            var domain = $"unity.{Application.companyName}.{Application.productName}";
            var process = new System.Diagnostics.Process
            {
                StartInfo =
                {
                    FileName = "/usr/bin/defaults",
                    Arguments = $"export \"{domain}\" -",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);

            if (string.IsNullOrEmpty(output))
            {
                diagnostic = $"Không đọc được plist của domain {domain}";
                return null;
            }

            var result = new Dictionary<string, string>();
            var document = XDocument.Parse(output);
            var dict = document.Descendants("dict").FirstOrDefault();
            if (dict == null) return result;

            var elements = dict.Elements().ToList();
            for (var i = 0; i < elements.Count - 1; i += 2)
            {
                if (elements[i].Name != "key") continue;
                if (elements[i + 1].Name != "string") continue;

                result[elements[i].Value] = elements[i + 1].Value;
            }

            return result;
        }
#endif

#if UNITY_EDITOR_WIN
        /// <summary>
        /// Windows: HKCU\Software\Unity\UnityEditor\&lt;company&gt;\&lt;product&gt;. Tên value có đuôi "_h&lt;hash&gt;",
        /// chuỗi lưu dạng REG_BINARY UTF-8. Gọi qua reflection để không phụ thuộc assembly Registry lúc biên dịch.
        /// </summary>
        private static Dictionary<string, string> ReadWindowsStore(out string diagnostic)
        {
            diagnostic = null;

            var registryType = Type.GetType("Microsoft.Win32.Registry, mscorlib") ??
                               Type.GetType("Microsoft.Win32.Registry, Microsoft.Win32.Registry");
            if (registryType == null)
            {
                diagnostic = "Không truy cập được Registry API.";
                return null;
            }

            var currentUser = registryType
                .GetProperty("CurrentUser", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (currentUser == null)
            {
                diagnostic = "Không mở được HKEY_CURRENT_USER.";
                return null;
            }

            var keyType = currentUser.GetType();
            var path = $@"Software\Unity\UnityEditor\{Application.companyName}\{Application.productName}";
            var subKey = keyType.GetMethod("OpenSubKey", new[] { typeof(string) })
                ?.Invoke(currentUser, new object[] { path });

            if (subKey == null)
            {
                diagnostic = $@"Không tìm thấy registry HKCU\{path}";
                return null;
            }

            var names = (string[])keyType.GetMethod("GetValueNames", Type.EmptyTypes)?.Invoke(subKey, null);
            var getValue = keyType.GetMethod("GetValue", new[] { typeof(string) });
            var result = new Dictionary<string, string>();

            foreach (var name in names ?? Array.Empty<string>())
            {
                var value = getValue?.Invoke(subKey, new object[] { name });
                if (!(value is byte[] bytes)) continue;

                // Unity ghi chuỗi kèm byte 0 kết thúc.
                var length = Array.IndexOf(bytes, (byte)0);
                if (length < 0) length = bytes.Length;

                var hashIndex = name.LastIndexOf("_h", StringComparison.Ordinal);
                var key = hashIndex > 0 ? name.Substring(0, hashIndex) : name;
                result[key] = Encoding.UTF8.GetString(bytes, 0, length);
            }

            (subKey as IDisposable)?.Dispose();
            return result;
        }
#endif

        #endregion
    }
}
#endif
