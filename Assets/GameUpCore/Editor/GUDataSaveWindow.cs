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
    /// Window này quét toàn bộ class kế thừa BaseDataSave, đọc key tương ứng qua
    /// <see cref="LocalStorageUtils"/> (tự giải mã), rồi vẽ ra để sửa — theo field hoặc theo JSON thô.
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

        /// <summary>Một data class tìm được, kèm key và version đọc từ code.</summary>
        private class SaveInfo
        {
            public Type Type;
            public string Key;
            public int CodeVersion;
            public string ProbeError;
        }

        private readonly List<SaveInfo> _saves = new List<SaveInfo>();
        private readonly Dictionary<string, bool> _foldouts = new Dictionary<string, bool>();

        private SaveInfo _selected;
        private ViewMode _mode = ViewMode.Fields;

        private object _instance;
        private string _json = string.Empty;
        private string _loadError;
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
            RefreshTypes();
        }

        private void OnFocus()
        {
            // Game đang chạy có thể vừa ghi đè dữ liệu — đọc lại cho khớp.
            if (_selected != null && !_dirty) Load(_selected);
            Repaint();
        }

        #region Quét type

        private void RefreshTypes()
        {
            _saves.Clear();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
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
                    if (type == null || type.IsAbstract || type.IsGenericTypeDefinition) continue;
                    if (!IsDataSave(type)) continue;

                    _saves.Add(BuildInfo(type));
                }
            }

            _saves.Sort((a, b) => string.CompareOrdinal(a.Type.Name, b.Type.Name));

            if (_selected != null)
            {
                var stillThere = _saves.FirstOrDefault(s => s.Type == _selected.Type);
                if (stillThere != null) Load(stillThere);
                else ClearSelection();
            }
        }

        private static bool IsDataSave(Type type)
        {
            for (var t = type.BaseType; t != null; t = t.BaseType)
            {
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(BaseDataSave<>)) return true;
            }

            return false;
        }

        /// <summary>
        /// Key và Version là property protected nên phải tạo instance rỗng rồi đọc bằng reflection.
        /// Instance này chỉ dùng để dò thông tin, không đụng tới dữ liệu đã lưu.
        /// </summary>
        private static SaveInfo BuildInfo(Type type)
        {
            var info = new SaveInfo { Type = type, Key = type.Name, CodeVersion = 1 };

            try
            {
                var probe = Activator.CreateInstance(type);
                var keyProperty = FindProperty(type, "Key");
                var versionProperty = FindProperty(type, "Version");

                if (keyProperty != null)
                {
                    var key = (string)keyProperty.GetValue(probe, null);
                    if (!string.IsNullOrEmpty(key)) info.Key = key;
                }

                if (versionProperty != null) info.CodeVersion = (int)versionProperty.GetValue(probe, null);
            }
            catch (Exception e)
            {
                info.ProbeError = e.InnerException?.Message ?? e.Message;
            }

            return info;
        }

        private static PropertyInfo FindProperty(Type type, string name)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                                       BindingFlags.Instance | BindingFlags.DeclaredOnly;

            for (var t = type; t != null; t = t.BaseType)
            {
                var property = t.GetProperty(name, flags);
                if (property != null) return property;
            }

            return null;
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

        private void Load(SaveInfo info)
        {
            _selected = info;
            _instance = null;
            _loadError = null;
            _dirty = false;

            _hasKey = LocalStorageUtils.HasKey(info.Key);
            var raw = LocalStorageUtils.GetString(info.Key);
            _json = string.IsNullOrEmpty(raw) ? string.Empty : PrettyPrint(raw);

            if (string.IsNullOrEmpty(raw)) return;

            _instance = Deserialize(info.Type, raw, out _loadError);
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
                var save = _selected.Type.GetMethod("Save", BindingFlags.Public | BindingFlags.Instance);
                if (save == null)
                {
                    GULogger.Error("DataSaveWindow", $"Không tìm thấy Save() trên {_selected.Type.Name}.");
                    return;
                }

                save.Invoke(_instance, null);
                PlayerPrefs.Save();
                _dirty = false;
                Load(_selected);
            }
            catch (Exception e)
            {
                GULogger.Exception(e.InnerException ?? e, "DataSaveWindow");
            }
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

        /// <summary>Xoá key rồi gọi Create() để data class tự chạy InitDefault và ghi lại.</summary>
        private void ResetToDefault()
        {
            if (!EditorUtility.DisplayDialog("Tạo lại mặc định",
                    $"Xoá dữ liệu hiện tại của '{_selected.Key}' và tạo lại từ InitDefault()?", "Tạo lại", "Huỷ")) return;

            try
            {
                PlayerPrefs.DeleteKey(_selected.Key);

                var create = _selected.Type.GetMethod("Create",
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                create?.Invoke(null, null);

                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                GULogger.Exception(e.InnerException ?? e, "DataSaveWindow");
            }

            Load(_selected);
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

            if (GUILayout.Button("Quét lại", EditorStyles.toolbarButton, GUILayout.Width(70))) RefreshTypes();

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

            var filtered = _saves.Where(s => string.IsNullOrEmpty(_search) ||
                                             s.Type.Name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                             s.Key.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0);

            var any = false;
            foreach (var info in filtered)
            {
                any = true;
                DrawListItem(info);
            }

            if (!any)
            {
                EditorGUILayout.HelpBox(_saves.Count == 0
                    ? "Chưa có class nào kế thừa BaseDataSave<T>."
                    : "Không khớp từ khoá tìm kiếm.", MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawListItem(SaveInfo info)
        {
            var isSelected = _selected != null && _selected.Type == info.Type;
            var hasData = LocalStorageUtils.HasKey(info.Key);

            var label = new GUIContent(
                hasData ? info.Type.Name : info.Type.Name + "  (trống)",
                $"{info.Type.FullName}\nKey: {info.Key}\nVersion trong code: {info.CodeVersion}");

            var style = new GUIStyle(EditorStyles.miniButton)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal
            };

            var previousColor = GUI.backgroundColor;
            if (isSelected) GUI.backgroundColor = new Color(0.45f, 0.65f, 1f);
            if (!hasData) GUI.contentColor = new Color(1f, 1f, 1f, 0.55f);

            if (GUILayout.Button(label, style) && !isSelected) TrySelect(info);

            GUI.backgroundColor = previousColor;
            GUI.contentColor = Color.white;
        }

        private void TrySelect(SaveInfo info)
        {
            if (_dirty && !EditorUtility.DisplayDialog("Chưa lưu",
                    "Thay đổi hiện tại chưa được lưu. Bỏ thay đổi và chuyển sang data khác?", "Bỏ", "Ở lại")) return;

            _foldouts.Clear();
            Load(info);
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
