#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace GameUp.Core.Editor
{
    /// <summary>
    /// Vẽ UI chỉnh sửa cho một object bất kỳ bằng reflection — dùng cho
    /// <see cref="GUDataSaveWindow"/> vì dữ liệu của <c>BaseDataSave</c> nằm trong PlayerPrefs
    /// (không phải asset) nên không có SerializedObject để tận dụng Inspector mặc định.
    ///
    /// Hỗ trợ: kiểu cơ bản, enum, kiểu Unity thường dùng, List/mảng, Dictionary và class/struct lồng nhau.
    /// Kiểu không nhận diện được thì hiện read-only để tránh ghi hỏng dữ liệu.
    /// </summary>
    internal static class GUDataSaveDrawer
    {
        /// <summary>Chặn đệ quy vô hạn khi dữ liệu tự tham chiếu.</summary>
        private const int MaxDepth = 8;

        private const BindingFlags FieldFlags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        private static readonly HashSet<Type> ScalarTypes = new HashSet<Type>
        {
            typeof(string), typeof(bool), typeof(char),
            typeof(byte), typeof(sbyte), typeof(short), typeof(ushort),
            typeof(int), typeof(uint), typeof(long), typeof(ulong),
            typeof(float), typeof(double), typeof(decimal),
            typeof(DateTime), typeof(TimeSpan),
            typeof(Vector2), typeof(Vector3), typeof(Vector4),
            typeof(Vector2Int), typeof(Vector3Int),
            typeof(Color), typeof(Color32), typeof(Quaternion), typeof(Rect), typeof(Bounds)
        };

        public static bool IsScalar(Type type)
        {
            return type != null && (type.IsEnum || ScalarTypes.Contains(type));
        }

        /// <summary>
        /// Vẽ toàn bộ field của <paramref name="target"/>. Trả về true nếu người dùng vừa sửa gì đó.
        /// <paramref name="path"/> là khoá duy nhất để nhớ trạng thái foldout giữa các lần vẽ.
        /// </summary>
        public static bool DrawFields(object target, string path, Dictionary<string, bool> foldouts,
            HashSet<string> skipFields = null, int depth = 0)
        {
            if (target == null)
            {
                EditorGUILayout.LabelField("null");
                return false;
            }

            var changed = false;

            foreach (var field in GetSerializedFields(target.GetType()))
            {
                if (skipFields != null && skipFields.Contains(field.Name)) continue;

                var value = field.GetValue(target);
                var fieldChanged = false;
                var newValue = DrawValue(ObjectNames.NicifyVariableName(field.Name), field.FieldType, value,
                    path + "." + field.Name, foldouts, depth, ref fieldChanged);

                if (!fieldChanged) continue;

                if (field.IsInitOnly)
                {
                    // readonly: không ghi lại được, nhưng object tham chiếu có thể đã bị sửa tại chỗ.
                    changed = true;
                    continue;
                }

                field.SetValue(target, newValue);
                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// Lấy field theo đúng luật serialize của project: field public, hoặc private có [SerializeField].
        /// Duyệt từ base xuống derived để field kế thừa hiện trước, giống thứ tự Inspector.
        /// </summary>
        public static IEnumerable<FieldInfo> GetSerializedFields(Type type)
        {
            var chain = new List<Type>();
            for (var t = type; t != null && t != typeof(object); t = t.BaseType) chain.Add(t);
            chain.Reverse();

            foreach (var t in chain)
            {
                foreach (var field in t.GetFields(FieldFlags))
                {
                    if (field.IsStatic) continue;
                    if (field.IsNotSerialized) continue;
                    if (field.Name.IndexOf("k__BackingField", StringComparison.Ordinal) >= 0) continue;
                    if (!field.IsPublic && !field.IsDefined(typeof(SerializeField), true)) continue;

                    yield return field;
                }
            }
        }

        private static object DrawValue(string label, Type type, object value, string path,
            Dictionary<string, bool> foldouts, int depth, ref bool changed)
        {
            if (IsScalar(type))
            {
                EditorGUI.BeginChangeCheck();
                var scalar = DrawScalar(label, type, value);
                if (!EditorGUI.EndChangeCheck()) return value;

                changed = true;
                return scalar;
            }

            if (depth >= MaxDepth)
            {
                EditorGUILayout.LabelField(label, $"({type.Name} — lồng quá sâu, sửa bằng tab JSON)");
                return value;
            }

            if (typeof(IDictionary).IsAssignableFrom(type))
                return DrawDictionary(label, type, value, path, foldouts, depth, ref changed);

            if (type.IsArray || typeof(IList).IsAssignableFrom(type))
                return DrawList(label, type, value, path, foldouts, depth, ref changed);

            if (type.IsClass || (type.IsValueType && !type.IsPrimitive))
                return DrawNested(label, type, value, path, foldouts, depth, ref changed);

            EditorGUILayout.LabelField(label, value != null ? value.ToString() : "null");
            return value;
        }

        #region Scalar

        private static object DrawScalar(string label, Type type, object value)
        {
            if (type.IsEnum)
            {
                var current = value as Enum ?? (Enum)Activator.CreateInstance(type);
                return type.IsDefined(typeof(FlagsAttribute), false)
                    ? EditorGUILayout.EnumFlagsField(label, current)
                    : EditorGUILayout.EnumPopup(label, current);
            }

            if (type == typeof(string)) return EditorGUILayout.TextField(label, (string)value ?? string.Empty);
            if (type == typeof(bool)) return EditorGUILayout.Toggle(label, ToBool(value));
            if (type == typeof(int)) return EditorGUILayout.IntField(label, Convert.ToInt32(value ?? 0));
            if (type == typeof(float)) return EditorGUILayout.FloatField(label, Convert.ToSingle(value ?? 0f));
            if (type == typeof(double)) return EditorGUILayout.DoubleField(label, Convert.ToDouble(value ?? 0d));
            if (type == typeof(long)) return EditorGUILayout.LongField(label, Convert.ToInt64(value ?? 0L));

            if (type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort) ||
                type == typeof(uint) || type == typeof(ulong) || type == typeof(decimal))
            {
                // Các kiểu số ít gặp: nhập bằng long/double rồi ép về, kẹp trong khoảng hợp lệ để không overflow.
                if (type == typeof(decimal))
                    return (decimal)EditorGUILayout.DoubleField(label, (double)Convert.ToDecimal(value ?? 0m));

                var entered = EditorGUILayout.LongField(label, Convert.ToInt64(value ?? 0));
                return ClampToIntegerType(entered, type);
            }

            if (type == typeof(char))
            {
                var text = EditorGUILayout.TextField(label, value != null ? value.ToString() : string.Empty);
                return string.IsNullOrEmpty(text) ? '\0' : text[0];
            }

            if (type == typeof(DateTime))
            {
                var current = value is DateTime dt ? dt : default;
                var text = EditorGUILayout.DelayedTextField(label, current.ToString("O", CultureInfo.InvariantCulture));
                return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
                    ? parsed
                    : current;
            }

            if (type == typeof(TimeSpan))
            {
                var current = value is TimeSpan ts ? ts : default;
                var text = EditorGUILayout.DelayedTextField(label, current.ToString());
                return TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var parsed) ? parsed : current;
            }

            if (type == typeof(Vector2)) return EditorGUILayout.Vector2Field(label, value is Vector2 v2 ? v2 : default);
            if (type == typeof(Vector3)) return EditorGUILayout.Vector3Field(label, value is Vector3 v3 ? v3 : default);
            if (type == typeof(Vector4)) return EditorGUILayout.Vector4Field(label, value is Vector4 v4 ? v4 : default);
            if (type == typeof(Vector2Int)) return EditorGUILayout.Vector2IntField(label, value is Vector2Int v2i ? v2i : default);
            if (type == typeof(Vector3Int)) return EditorGUILayout.Vector3IntField(label, value is Vector3Int v3i ? v3i : default);
            if (type == typeof(Color)) return EditorGUILayout.ColorField(label, value is Color c ? c : Color.white);
            if (type == typeof(Color32)) return (Color32)EditorGUILayout.ColorField(label, value is Color32 c32 ? c32 : new Color32(255, 255, 255, 255));
            if (type == typeof(Rect)) return EditorGUILayout.RectField(label, value is Rect r ? r : default);
            if (type == typeof(Bounds)) return EditorGUILayout.BoundsField(label, value is Bounds b ? b : default);

            if (type == typeof(Quaternion))
            {
                var q = value is Quaternion quat ? quat : Quaternion.identity;
                var edited = EditorGUILayout.Vector4Field(label, new Vector4(q.x, q.y, q.z, q.w));
                return new Quaternion(edited.x, edited.y, edited.z, edited.w);
            }

            EditorGUILayout.LabelField(label, value != null ? value.ToString() : "null");
            return value;
        }

        private static bool ToBool(object value)
        {
            return value is bool b && b;
        }

        private static object ClampToIntegerType(long entered, Type type)
        {
            if (type == typeof(byte)) return (byte)Math.Min(byte.MaxValue, Math.Max(byte.MinValue, entered));
            if (type == typeof(sbyte)) return (sbyte)Math.Min(sbyte.MaxValue, Math.Max(sbyte.MinValue, entered));
            if (type == typeof(short)) return (short)Math.Min(short.MaxValue, Math.Max(short.MinValue, entered));
            if (type == typeof(ushort)) return (ushort)Math.Min(ushort.MaxValue, Math.Max(ushort.MinValue, entered));
            if (type == typeof(uint)) return (uint)Math.Min(uint.MaxValue, Math.Max(uint.MinValue, entered));
            if (type == typeof(ulong)) return (ulong)Math.Max(0, entered);
            return entered;
        }

        #endregion

        #region Nested / List / Dictionary

        private static object DrawNested(string label, Type type, object value, string path,
            Dictionary<string, bool> foldouts, int depth, ref bool changed)
        {
            var expanded = Foldout(path, $"{label}  ({type.Name})", foldouts);
            if (!expanded) return value;

            EditorGUI.indentLevel++;
            try
            {
                if (value == null)
                {
                    if (DrawNullRow(type, out var created))
                    {
                        changed = true;
                        return created;
                    }

                    return null;
                }

                if (DrawFields(value, path, foldouts, null, depth + 1)) changed = true;
                return value;
            }
            finally
            {
                EditorGUI.indentLevel--;
            }
        }

        private static object DrawList(string label, Type type, object value, string path,
            Dictionary<string, bool> foldouts, int depth, ref bool changed)
        {
            var elementType = GetElementType(type);
            var list = value as IList;
            var count = list?.Count ?? 0;

            var expanded = Foldout(path, $"{label}  ({count})", foldouts);
            if (!expanded) return value;

            EditorGUI.indentLevel++;
            try
            {
                if (list == null)
                {
                    if (DrawNullRow(type, out var created))
                    {
                        changed = true;
                        return created;
                    }

                    return null;
                }

                var removeIndex = -1;

                for (var i = 0; i < list.Count; i++)
                {
                    var itemChanged = false;
                    var elementLabel = $"[{i}]";
                    object newItem;

                    if (IsScalar(elementType))
                    {
                        EditorGUILayout.BeginHorizontal();
                        newItem = DrawValue(elementLabel, elementType, list[i], $"{path}[{i}]", foldouts, depth, ref itemChanged);
                        if (MiniButton("−", "Xoá phần tử")) removeIndex = i;
                        EditorGUILayout.EndHorizontal();
                    }
                    else
                    {
                        newItem = DrawValue(elementLabel, elementType, list[i], $"{path}[{i}]", foldouts, depth + 1, ref itemChanged);
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        if (MiniButton($"− Xoá [{i}]", "Xoá phần tử")) removeIndex = i;
                        EditorGUILayout.EndHorizontal();
                    }

                    if (!itemChanged) continue;

                    list[i] = newItem;
                    changed = true;
                }

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                var addRequested = MiniButton("+ Thêm phần tử", "Thêm phần tử vào cuối");
                EditorGUILayout.EndHorizontal();

                // Sửa cấu trúc sau khi vẽ xong để không đụng vào list đang duyệt.
                if (removeIndex >= 0)
                {
                    changed = true;
                    return RemoveElement(type, list, removeIndex);
                }

                if (addRequested)
                {
                    changed = true;
                    return AppendElement(type, list, elementType);
                }

                return list;
            }
            finally
            {
                EditorGUI.indentLevel--;
            }
        }

        private static object DrawDictionary(string label, Type type, object value, string path,
            Dictionary<string, bool> foldouts, int depth, ref bool changed)
        {
            var dict = value as IDictionary;
            var count = dict?.Count ?? 0;

            var expanded = Foldout(path, $"{label}  ({count})", foldouts);
            if (!expanded) return value;

            EditorGUI.indentLevel++;
            try
            {
                if (dict == null)
                {
                    if (DrawNullRow(type, out var created))
                    {
                        changed = true;
                        return created;
                    }

                    return null;
                }

                var args = GetDictionaryArgs(type);
                var keyType = args.Item1;
                var valueType = args.Item2;

                // Chụp key ra list trước: sửa dictionary trong lúc duyệt sẽ ném exception.
                var keys = new List<object>();
                foreach (var key in dict.Keys) keys.Add(key);

                object removeKey = null;
                var hasRemove = false;
                object renameFrom = null;
                object renameTo = null;
                var hasRename = false;

                for (var i = 0; i < keys.Count; i++)
                {
                    var key = keys[i];
                    var entryPath = $"{path}[{i}]";

                    EditorGUILayout.BeginHorizontal();

                    var keyChanged = false;
                    object newKey = key;
                    if (IsScalar(keyType))
                    {
                        EditorGUI.BeginChangeCheck();
                        newKey = DrawScalar("Key", keyType, key);
                        if (EditorGUI.EndChangeCheck()) keyChanged = true;
                    }
                    else
                    {
                        EditorGUILayout.LabelField(key != null ? key.ToString() : "null");
                    }

                    EditorGUILayout.EndHorizontal();

                    EditorGUI.indentLevel++;
                    var valueChanged = false;
                    var newValue = DrawValue("→", valueType, dict[key], entryPath + ".value", foldouts, depth + 1, ref valueChanged);
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    if (MiniButton("− Xoá", "Xoá entry"))
                    {
                        removeKey = key;
                        hasRemove = true;
                    }

                    EditorGUILayout.EndHorizontal();
                    EditorGUI.indentLevel--;

                    if (valueChanged)
                    {
                        dict[key] = newValue;
                        changed = true;
                    }

                    if (!keyChanged || Equals(newKey, key)) continue;

                    if (dict.Contains(newKey))
                    {
                        GULogger.Warning("DataSaveWindow", $"Key '{newKey}' đã tồn tại, bỏ qua đổi tên.");
                        continue;
                    }

                    renameFrom = key;
                    renameTo = newKey;
                    hasRename = true;
                }

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (MiniButton("+ Thêm entry", "Thêm entry mới"))
                {
                    var freeKey = FindFreeKey(dict, keyType);
                    if (freeKey != null || !keyType.IsValueType)
                    {
                        dict[freeKey] = CreateDefault(valueType);
                        changed = true;
                    }
                    else
                    {
                        GULogger.Warning("DataSaveWindow", "Không tìm được key trống để thêm entry mới.");
                    }
                }

                EditorGUILayout.EndHorizontal();

                if (hasRemove)
                {
                    dict.Remove(removeKey);
                    changed = true;
                }
                else if (hasRename)
                {
                    var moved = dict[renameFrom];
                    dict.Remove(renameFrom);
                    dict[renameTo] = moved;
                    changed = true;
                }

                return dict;
            }
            finally
            {
                EditorGUI.indentLevel--;
            }
        }

        #endregion

        #region Helpers

        private static bool Foldout(string path, string label, Dictionary<string, bool> foldouts)
        {
            foldouts.TryGetValue(path, out var expanded);
            var newValue = EditorGUILayout.Foldout(expanded, label, true);
            if (newValue != expanded) foldouts[path] = newValue;
            return newValue;
        }

        /// <summary>Hàng cho giá trị null: cho phép tạo instance rỗng để bắt đầu nhập liệu.</summary>
        private static bool DrawNullRow(Type type, out object created)
        {
            created = null;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("null");
            var pressed = MiniButton("Tạo mới", $"Khởi tạo {type.Name} rỗng");
            EditorGUILayout.EndHorizontal();

            if (!pressed) return false;

            created = CreateDefault(type);
            return created != null;
        }

        private static bool MiniButton(string label, string tooltip)
        {
            return GUILayout.Button(new GUIContent(label, tooltip), EditorStyles.miniButton,
                GUILayout.Width(Mathf.Max(24f, EditorStyles.miniButton.CalcSize(new GUIContent(label)).x + 8f)));
        }

        public static object CreateDefault(Type type)
        {
            if (type == typeof(string)) return string.Empty;

            try
            {
                if (type.IsArray) return Array.CreateInstance(type.GetElementType() ?? typeof(object), 0);
                return Activator.CreateInstance(type);
            }
            catch (Exception)
            {
                // Không có constructor rỗng — để null, người dùng vẫn sửa được qua tab JSON.
                return null;
            }
        }

        private static Type GetElementType(Type type)
        {
            if (type.IsArray) return type.GetElementType() ?? typeof(object);

            var listInterface = type.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>));
            return listInterface != null ? listInterface.GetGenericArguments()[0] : typeof(object);
        }

        private static Tuple<Type, Type> GetDictionaryArgs(Type type)
        {
            var dictInterface = type.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));

            if (dictInterface == null) return Tuple.Create(typeof(object), typeof(object));

            var args = dictInterface.GetGenericArguments();
            return Tuple.Create(args[0], args[1]);
        }

        private static object AppendElement(Type listType, IList list, Type elementType)
        {
            var item = CreateDefault(elementType);

            if (!listType.IsArray)
            {
                list.Add(item);
                return list;
            }

            var array = Array.CreateInstance(elementType, list.Count + 1);
            for (var i = 0; i < list.Count; i++) array.SetValue(list[i], i);
            array.SetValue(item, list.Count);
            return array;
        }

        private static object RemoveElement(Type listType, IList list, int index)
        {
            if (!listType.IsArray)
            {
                list.RemoveAt(index);
                return list;
            }

            var elementType = listType.GetElementType() ?? typeof(object);
            var array = Array.CreateInstance(elementType, list.Count - 1);
            var cursor = 0;
            for (var i = 0; i < list.Count; i++)
            {
                if (i == index) continue;
                array.SetValue(list[i], cursor++);
            }

            return array;
        }

        /// <summary>Tìm key chưa dùng để thêm entry mới (int thì tăng dần, string thì thêm hậu tố).</summary>
        private static object FindFreeKey(IDictionary dict, Type keyType)
        {
            if (keyType == typeof(string))
            {
                var candidate = "key";
                var index = 1;
                while (dict.Contains(candidate)) candidate = "key_" + index++;
                return candidate;
            }

            if (keyType == typeof(int) || keyType == typeof(long))
            {
                long candidate = 0;
                while (dict.Contains(keyType == typeof(int) ? (object)(int)candidate : candidate)) candidate++;
                return keyType == typeof(int) ? (object)(int)candidate : candidate;
            }

            var fallback = CreateDefault(keyType);
            return fallback != null && !dict.Contains(fallback) ? fallback : null;
        }

        #endregion
    }
}
#endif
