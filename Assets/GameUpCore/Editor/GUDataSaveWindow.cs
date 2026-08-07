#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FullSerializer;
using GameUp.Core.Serializer;
using UnityEditor;
using UnityEngine;

namespace GameUp.Core.Editor
{
    /// <summary>
    /// Cửa sổ xem/sửa mọi dữ liệu kế thừa <see cref="BaseDataSave{T}"/>.
    ///
    /// Dữ liệu nằm trong PlayerPrefs và đã bị mã hoá nên không xem được bằng công cụ có sẵn của Unity.
    /// Window này quét toàn bộ class kế thừa BaseDataSave, tìm mọi bản save của từng class
    /// (xem <see cref="GUDataSaveScanner"/> — một class có thể có nhiều key nếu <c>Key</c> phụ thuộc dữ liệu),
    /// đọc qua <see cref="LocalStorageUtils"/> (tự giải mã) rồi vẽ ra để sửa — theo field hoặc theo JSON thô.
    ///
    /// Ghi lại đi đúng đường <c>Save()</c> của chính data class nên format luôn khớp với runtime.
    /// </summary>
    public class GUDataSaveWindow : EditorWindow
    {
        private const string MenuPath = "GameUp/Data/Data Save Viewer";
        private const string WindowTitle = "Data Save";
        private const float ListWidth = 220f;

        private enum ViewMode
        {
            Fields,
            Json
        }

        /// <summary>Một bản save cụ thể: data class + key thật trong PlayerPrefs.</summary>
        private class SaveEntry
        {
            public Type Type;
            public string Key;
            public int CodeVersion;
            public string ProbeError;

            /// <summary>Field quyết định key (rỗng nếu key cố định) — dùng khi tạo lại dữ liệu mặc định.</summary>
            public List<FieldInfo> KeyFields;

            /// <summary>Giá trị của các field trên, nếu key này tìm được bằng cách dò id.</summary>
            public Dictionary<FieldInfo, object> KeyValues;
        }

        private readonly List<SaveEntry> _entries = new List<SaveEntry>();
        private readonly Dictionary<string, bool> _foldouts = new Dictionary<string, bool>();

        private SaveEntry _selected;
        private ViewMode _mode = ViewMode.Fields;

        private object _instance;
        private string _json = string.Empty;
        private string _loadError;
        private string _storeDiagnostic;
        private bool _hasKey;
        private bool _dirty;

        private string _search = string.Empty;
        private Vector2 _listScroll;
        private Vector2 _detailScroll;

        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            var window = GetWindow<GUDataSaveWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(620f, 380f);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshEntries();
        }

        private void OnFocus()
        {
            // Game đang chạy có thể vừa ghi đè dữ liệu — đọc lại cho khớp.
            if (_selected != null && !_dirty) Load(_selected);
            Repaint();
        }

        #region Quét dữ liệu

        private void RefreshEntries()
        {
            var previousType = _selected?.Type;
            var previousKey = _selected?.Key;

            _entries.Clear();

            var stored = GUDataSaveScanner.FindSavedKeys(out _storeDiagnostic);

            foreach (var type in FindDataSaveTypes())
            {
                _entries.AddRange(BuildEntries(type, stored));
            }

            if (previousType == null) return;

            var again = _entries.FirstOrDefault(e => e.Type == previousType && e.Key == previousKey)
                        ?? _entries.FirstOrDefault(e => e.Type == previousType);

            if (again != null) Load(again);
            else ClearSelection();
        }

        private static IEnumerable<Type> FindDataSaveTypes()
        {
            var types = new List<Type>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] assemblyTypes;
                try
                {
                    assemblyTypes = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    assemblyTypes = e.Types.Where(t => t != null).ToArray();
                }
                catch (Exception)
                {
                    continue;
                }

                types.AddRange(assemblyTypes.Where(GUDataSaveScanner.IsDataSave));
            }

            types.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return types;
        }

        /// <summary>
        /// Gộp key từ ba nguồn: key của instance mặc định, key dò được theo id, và key đọc từ store.
        /// Một class có key cố định thì chỉ ra đúng một dòng.
        /// </summary>
        private static IEnumerable<SaveEntry> BuildEntries(Type type, Dictionary<string, Type> stored)
        {
            var defaultKey = type.Name;
            var codeVersion = 1;
            string probeError = null;
            var keyFields = new List<FieldInfo>();

            try
            {
                var probe = Activator.CreateInstance(type);

                var key = GUDataSaveScanner.ReadKey(probe);
                if (!string.IsNullOrEmpty(key)) defaultKey = key;

                var versionProperty = GUDataSaveScanner.FindProperty(type, "Version");
                if (versionProperty != null) codeVersion = (int)versionProperty.GetValue(probe, null);

                keyFields = GUDataSaveScanner.FindKeyFields(type);
            }
            catch (Exception e)
            {
                probeError = (e.InnerException ?? e).Message;
            }

            var keys = new Dictionary<string, Dictionary<FieldInfo, object>> { { defaultKey, null } };

            if (keyFields.Count > 0)
            {
                foreach (var scanned in GUDataSaveScanner.ScanIntKeys(type, keyFields))
                {
                    keys[scanned.Key] = scanned.Value;
                }
            }

            foreach (var pair in stored.Where(p => p.Value == type))
            {
                if (!keys.ContainsKey(pair.Key)) keys[pair.Key] = null;
            }

            return keys
                .OrderBy(pair => pair.Key.Length)
                .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new SaveEntry
                {
                    Type = type,
                    Key = pair.Key,
                    CodeVersion = codeVersion,
                    ProbeError = probeError,
                    KeyFields = keyFields,
                    KeyValues = pair.Value
                });
        }

        #endregion

        #region Đọc / ghi dữ liệu

        private void ClearSelection()
        {
            _selected = null;
            _instance = null;
            _json = string.Empty;
            _loadError = null;
            _hasKey = false;
            _dirty = false;
            _foldouts.Clear();
        }

        private void Load(SaveEntry entry)
        {
            _selected = entry;
            _instance = null;
            _loadError = null;
            _dirty = false;

            _hasKey = LocalStorageUtils.HasKey(entry.Key);
            var raw = LocalStorageUtils.GetString(entry.Key);
            _json = string.IsNullOrEmpty(raw) ? string.Empty : PrettyPrint(raw);

            if (string.IsNullOrEmpty(raw)) return;

            _instance = Deserialize(entry.Type, raw, out _loadError);
        }

        private static object Deserialize(Type type, string raw, out string error)
        {
            error = null;

            try
            {
                var deserialize = typeof(JsonExtension)
                    .GetMethod(nameof(JsonExtension.Deserialize), BindingFlags.Public | BindingFlags.Static)
                    ?.MakeGenericMethod(type);

                var result = deserialize?.Invoke(null, new object[] { raw });
                if (result == null) error = "Không dựng lại được object từ dữ liệu đã lưu.";
                return result;
            }
            catch (Exception e)
            {
                error = (e.InnerException ?? e).Message;
                return null;
            }
        }

        /// <summary>
        /// Đổi tab mà không mất chỉnh sửa dở: chuyển nội dung đang sửa sang dạng của tab kia
        /// (field → JSON dùng đúng serializer của runtime, JSON → field bằng deserialize).
        /// </summary>
        private void SwitchMode(ViewMode mode)
        {
            GUI.FocusControl(null);

            if (!_dirty)
            {
                _mode = mode;
                return;
            }

            if (mode == ViewMode.Json && _instance != null)
            {
                _json = PrettyPrint(_instance.Serialize());
            }
            else if (mode == ViewMode.Fields && !string.IsNullOrEmpty(_json))
            {
                var rebuilt = Deserialize(_selected.Type, _json, out var error);
                if (rebuilt != null)
                {
                    _instance = rebuilt;
                    _loadError = null;
                    _foldouts.Clear();
                }
                else
                {
                    _loadError = error;
                }
            }

            _mode = mode;
        }

        /// <summary>Ghi bằng chính <c>Save()</c> của data class để format khớp tuyệt đối với runtime.</summary>
        private void SaveInstance()
        {
            if (_instance == null) return;

            try
            {
                if (!InvokeSave(_instance)) return;

                PlayerPrefs.Save();
                _dirty = false;
                Load(_selected);
            }
            catch (Exception e)
            {
                GULogger.Exception(e.InnerException ?? e, "DataSaveWindow");
            }
        }

        private static bool InvokeSave(object instance)
        {
            var save = instance.GetType().GetMethod("Save", BindingFlags.Public | BindingFlags.Instance);
            if (save == null)
            {
                GULogger.Error("DataSaveWindow", $"Không tìm thấy Save() trên {instance.GetType().Name}.");
                return false;
            }

            save.Invoke(instance, null);
            return true;
        }

        /// <summary>
        /// Ghi thẳng JSON người dùng nhập (chỉ kiểm tra cú pháp, không ép về schema hiện tại)
        /// để có thể dựng lại save cũ mà test <c>Migrate</c>.
        /// </summary>
        private void SaveJson()
        {
            if (!TryParse(_json, out var error))
            {
                EditorUtility.DisplayDialog("JSON không hợp lệ", error, "OK");
                return;
            }

            LocalStorageUtils.SetString(_selected.Key, _json);
            PlayerPrefs.Save();
            _dirty = false;
            Load(_selected);
        }

        private void DeleteKey()
        {
            if (!EditorUtility.DisplayDialog("Xoá dữ liệu",
                    $"Xoá key '{_selected.Key}' khỏi PlayerPrefs?", "Xoá", "Huỷ")) return;

            PlayerPrefs.DeleteKey(_selected.Key);
            PlayerPrefs.Save();
            Load(_selected);
        }

        /// <summary>
        /// Dựng lại dữ liệu mặc định đúng như nhánh "chưa có key" của <c>Create()</c>:
        /// InitDefault + gán dataVersion. Có thêm bước khôi phục field quyết định key
        /// để bản save nhiều key (hero_3 chẳng hạn) không bị ghi nhầm về key mặc định.
        /// </summary>
        private void ResetToDefault()
        {
            if (!EditorUtility.DisplayDialog("Tạo lại mặc định",
                    $"Xoá dữ liệu hiện tại của '{_selected.Key}' và tạo lại từ InitDefault()?", "Tạo lại", "Huỷ")) return;

            try
            {
                var instance = Activator.CreateInstance(_selected.Type);
                RestoreKeyFields(instance);

                var key = GUDataSaveScanner.ReadKey(instance);
                if (key != _selected.Key)
                {
                    GULogger.Warning("DataSaveWindow",
                        $"Không dựng lại được key '{_selected.Key}' (instance mới cho ra '{key}'). Huỷ thao tác.");
                    return;
                }

                InvokeVoid(instance, "InitDefault");

                var versionProperty = GUDataSaveScanner.FindProperty(_selected.Type, "Version");
                var versionField = _selected.Type.GetField("dataVersion", BindingFlags.Public | BindingFlags.Instance);
                if (versionProperty != null && versionField != null)
                    versionField.SetValue(instance, (int)versionProperty.GetValue(instance, null));

                PlayerPrefs.DeleteKey(_selected.Key);
                InvokeSave(instance);
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                GULogger.Exception(e.InnerException ?? e, "DataSaveWindow");
            }

            Load(_selected);
        }

        /// <summary>Gán lại field quyết định key: ưu tiên giá trị dò được, không có thì lấy từ dữ liệu đang mở.</summary>
        private void RestoreKeyFields(object instance)
        {
            if (_selected.KeyValues != null)
            {
                foreach (var pair in _selected.KeyValues) pair.Key.SetValue(instance, pair.Value);
                return;
            }

            if (_instance == null || _selected.KeyFields == null) return;

            foreach (var field in _selected.KeyFields) field.SetValue(instance, field.GetValue(_instance));
        }

        private static void InvokeVoid(object instance, string methodName)
        {
            var method = GUDataSaveScanner.FindMethod(instance.GetType(), methodName);
            method?.Invoke(instance, null);
        }

        private static string PrettyPrint(string json)
        {
            try
            {
                var result = fsJsonParser.Parse(json, out var data);
                return result.Failed ? json : fsJsonPrinter.PrettyJson(data);
            }
            catch (Exception)
            {
                return json;
            }
        }

        private static bool TryParse(string json, out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "JSON đang trống.";
                return false;
            }

            try
            {
                var result = fsJsonParser.Parse(json, out _);
                if (result.Succeeded) return true;

                error = result.FormattedMessages;
                return false;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }

        #endregion

        #region GUI

        private void OnGUI()
        {
            DrawToolbar();

            EditorGUILayout.BeginHorizontal();
            DrawList();
            DrawDetail();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Quét lại", EditorStyles.toolbarButton, GUILayout.Width(70))) RefreshEntries();

            GUILayout.Space(4);
            _search = GUILayout.TextField(_search, EditorStyles.toolbarSearchField, GUILayout.Width(160));

            GUILayout.FlexibleSpace();

            if (Application.isPlaying)
            {
                GUILayout.Label("Đang Play — dữ liệu trong RAM của game có thể ghi đè thay đổi ở đây",
                    EditorStyles.miniLabel);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(ListWidth));
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.Width(ListWidth));

            var filtered = _entries.Where(e => string.IsNullOrEmpty(_search) ||
                                               e.Type.Name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                               e.Key.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            if (filtered.Count == 0)
            {
                EditorGUILayout.HelpBox(_entries.Count == 0
                    ? "Chưa có class nào kế thừa BaseDataSave<T>."
                    : "Không khớp từ khoá tìm kiếm.", MessageType.Info);
            }

            foreach (var group in filtered.GroupBy(e => e.Type))
            {
                var entries = group.ToList();

                // Class một key thì hiện thẳng tên class; nhiều key thì gom dưới một tiêu đề.
                if (entries.Count == 1)
                {
                    DrawListItem(entries[0], entries[0].Type.Name);
                    continue;
                }

                GUILayout.Label(group.Key.Name, EditorStyles.miniBoldLabel);
                foreach (var entry in entries) DrawListItem(entry, "   " + entry.Key);
            }

            if (!string.IsNullOrEmpty(_storeDiagnostic))
            {
                EditorGUILayout.HelpBox(
                    $"Không đọc được danh sách key của PlayerPrefs ({_storeDiagnostic}).\n" +
                    $"Class có key động chỉ dò được id 0..{GUDataSaveScanner.ScanRange - 1}.",
                    MessageType.None);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawListItem(SaveEntry entry, string label)
        {
            var isSelected = _selected != null && _selected.Type == entry.Type && _selected.Key == entry.Key;
            var hasData = LocalStorageUtils.HasKey(entry.Key);

            var content = new GUIContent(
                hasData ? label : label + "  (trống)",
                $"{entry.Type.FullName}\nKey: {entry.Key}\nVersion trong code: {entry.CodeVersion}");

            var style = new GUIStyle(EditorStyles.miniButton)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal
            };

            var previousColor = GUI.backgroundColor;
            if (isSelected) GUI.backgroundColor = new Color(0.45f, 0.65f, 1f);
            if (!hasData) GUI.contentColor = new Color(1f, 1f, 1f, 0.55f);

            if (GUILayout.Button(content, style) && !isSelected) TrySelect(entry);

            GUI.backgroundColor = previousColor;
            GUI.contentColor = Color.white;
        }

        private void TrySelect(SaveEntry entry)
        {
            if (_dirty && !EditorUtility.DisplayDialog("Chưa lưu",
                    "Thay đổi hiện tại chưa được lưu. Bỏ thay đổi và chuyển sang data khác?", "Bỏ", "Ở lại")) return;

            _foldouts.Clear();
            Load(entry);
            GUI.FocusControl(null);
        }

        private void DrawDetail()
        {
            EditorGUILayout.BeginVertical();

            if (_selected == null)
            {
                EditorGUILayout.HelpBox("Chọn một data class ở danh sách bên trái.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            DrawHeader();

            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);

            if (_mode == ViewMode.Fields) DrawFieldsMode();
            else DrawJsonMode();

            EditorGUILayout.EndScrollView();

            DrawActions();
            EditorGUILayout.EndVertical();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField(_selected.Type.Name, EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("PlayerPrefs key", _selected.Key);
            if (GUILayout.Button("Copy key", EditorStyles.miniButton, GUILayout.Width(70)))
                EditorGUIUtility.systemCopyBuffer = _selected.Key;
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_selected.ProbeError))
                EditorGUILayout.HelpBox($"Không đọc được Key/Version: {_selected.ProbeError}", MessageType.Warning);

            EditorGUILayout.LabelField("Trạng thái", _hasKey ? "Đã có dữ liệu lưu" : "Chưa có dữ liệu");

            DrawVersionRow();

            EditorGUI.BeginChangeCheck();
            var mode = (ViewMode)GUILayout.Toolbar((int)_mode, new[] { "Field", "JSON" });
            if (EditorGUI.EndChangeCheck() && mode != _mode) SwitchMode(mode);

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// dataVersion tách riêng khỏi danh sách field: hạ số này rồi Lưu là cách nhanh nhất
        /// để ép <c>Migrate()</c> chạy lại mà thử.
        /// </summary>
        private void DrawVersionRow()
        {
            EditorGUILayout.BeginHorizontal();

            var versionField = _mode == ViewMode.Fields && _instance != null
                ? _instance.GetType().GetField("dataVersion", BindingFlags.Public | BindingFlags.Instance)
                : null;

            if (versionField != null)
            {
                EditorGUI.BeginChangeCheck();
                var version = EditorGUILayout.IntField("dataVersion (save)", (int)versionField.GetValue(_instance));
                if (EditorGUI.EndChangeCheck())
                {
                    versionField.SetValue(_instance, version);
                    _dirty = true;
                }
            }
            else
            {
                EditorGUILayout.LabelField("dataVersion (save)", "—");
            }

            EditorGUILayout.LabelField($"Version trong code: {_selected.CodeVersion}", GUILayout.Width(160));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawFieldsMode()
        {
            if (_instance != null)
            {
                if (GUDataSaveDrawer.DrawFields(_instance, _selected.Type.Name, _foldouts,
                        new HashSet<string> { "dataVersion" })) _dirty = true;
                return;
            }

            if (!_hasKey)
            {
                EditorGUILayout.HelpBox(
                    "Chưa có dữ liệu cho key này. Bấm \"Tạo lại mặc định\" để sinh dữ liệu từ InitDefault().",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                $"Không đọc được dữ liệu: {_loadError ?? "không rõ nguyên nhân"}.\nMở tab JSON để xem/sửa dữ liệu thô.",
                MessageType.Error);
        }

        private void DrawJsonMode()
        {
            EditorGUILayout.LabelField("JSON thô (đã giải mã)", EditorStyles.miniBoldLabel);

            EditorGUI.BeginChangeCheck();
            _json = EditorGUILayout.TextArea(_json, GUILayout.ExpandHeight(true), GUILayout.MinHeight(200f));
            if (EditorGUI.EndChangeCheck()) _dirty = true;

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Format lại", EditorStyles.miniButton, GUILayout.Width(90)))
                _json = PrettyPrint(_json);

            if (GUILayout.Button("Copy", EditorStyles.miniButton, GUILayout.Width(60)))
                EditorGUIUtility.systemCopyBuffer = _json;

            if (GUILayout.Button("Paste", EditorStyles.miniButton, GUILayout.Width(60)))
            {
                _json = EditorGUIUtility.systemCopyBuffer;
                _dirty = true;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                "JSON được ghi nguyên trạng (chỉ kiểm tra cú pháp) — có thể dán lại save của bản cũ để test Migrate().",
                MessageType.None);
        }

        private void DrawActions()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            var canSave = _dirty && (_mode == ViewMode.Json ? !string.IsNullOrEmpty(_json) : _instance != null);
            using (new EditorGUI.DisabledScope(!canSave))
            {
                if (GUILayout.Button(_dirty ? "Lưu *" : "Lưu", GUILayout.Height(24)))
                {
                    if (_mode == ViewMode.Json) SaveJson();
                    else SaveInstance();
                }
            }

            if (GUILayout.Button("Tải lại", GUILayout.Height(24))) Load(_selected);
            if (GUILayout.Button("Tạo lại mặc định", GUILayout.Height(24))) ResetToDefault();

            using (new EditorGUI.DisabledScope(!_hasKey))
            {
                if (GUILayout.Button("Xoá key", GUILayout.Height(24))) DeleteKey();
            }

            EditorGUILayout.EndHorizontal();
        }

        #endregion
    }
}
#endif
