using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using System.IO;
using System.Text;
using GameUp.SDK;

namespace GameUp.SDK.Editor.Setup
{
    public class GameUpSetupWindow : EditorWindow
    {
        private List<SetupTabBase> _allTabs;
        private List<SetupTabBase> _visibleTabs;
        private int _activeTabIndex;
        private string _loadErrors;
        private string _saveErrors;
        private Vector2 _scrollPosition;

        [MenuItem("GameUp/SDK/Setup")]
        public static void ShowWindow()
        {
            if (!Installer.GameUpDependenciesWindow.AreAllRequiredPackagesInstalled())
            {
                Installer.GameUpDependenciesWindow.ShowWindow();
                return;
            }
            GetWindow<GameUpSetupWindow>("GameUp SDK Setup").minSize = new Vector2(500, 550);
        }

        private void OnEnable()
        {
            _allTabs = new List<SetupTabBase>
            {
                new FacebookSetupTab(),
                new AppsFlyerSetupTab(),
                new GameAnalyticsSetupTab(),
                new AppmetricaSetupTab(),
                new IronSourceSetupTab(),
                new MaxSetupTab(),
                new AdmobSetupTab(),
                new FirebaseRCSetupTab()
            };
            RefreshVisibleTabs();
            LoadAllData();
        }

        private void RefreshVisibleTabs()
        {
            _visibleTabs = _allTabs.Where(t => t.IsVisible).ToList();
            if (_activeTabIndex >= _visibleTabs.Count) _activeTabIndex = 0;
        }

        private void LoadAllData()
        {
            _loadErrors = null;
            if (!CanSetupFromWritablePrefabs(out var lockReason))
            {
                _loadErrors = lockReason;
                return;
            }
            foreach (var tab in _allTabs) tab.Load();
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            if (!string.IsNullOrEmpty(_loadErrors)) EditorGUILayout.HelpBox(_loadErrors, MessageType.Warning);
            if (!string.IsNullOrEmpty(_saveErrors))
            {
                EditorGUILayout.HelpBox(_saveErrors, MessageType.Error);
                EditorGUILayout.Space();
                if (Event.current.type == EventType.Repaint) _saveErrors = null;
            }

            if (RequiresPrefabCloneBeforeSetup())
            {
                EditorGUILayout.HelpBox("Bạn cần clone prefab từ Package sang Assets để kích hoạt chỉnh sửa.", MessageType.Warning);
                if (GUILayout.Button("Clone Prefab từ Package → " + GameUpSetupPaths.WritablePrefabsRoot, GUILayout.Height(30)))
                {
                    TryClonePackagePrefabsToWritable();
                    LoadAllData();
                }
                EditorGUILayout.EndScrollView();
                return;
            }

            string[] tabNames = _visibleTabs.Select(t => t.Title).ToArray();
            _activeTabIndex = GUILayout.Toolbar(_activeTabIndex, tabNames);
            EditorGUILayout.Space(8);

            bool canSetup = CanSetupFromWritablePrefabs(out var lockReason);
            EditorGUI.BeginDisabledGroup(!canSetup);
            if (_visibleTabs.Count > 0 && _activeTabIndex >= 0)
            {
                _visibleTabs[_activeTabIndex].Draw();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(16);
            EditorGUI.BeginDisabledGroup(!canSetup);
            if (GUILayout.Button("Save Configuration", GUILayout.Height(32)))
            {
                SaveAllData();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Tạo SDK trong Scene hiện tại", GUILayout.Height(28)))
            {
                CreateSDKInCurrentScene();
            }

            // =========================================================
            // NÚT MỚI: TẠO FILE CONSTANTS (AdPlacement.cs)
            // =========================================================
            EditorGUILayout.Space(4);
            GUI.backgroundColor = new Color(0.8f, 1f, 0.8f);
            if (GUILayout.Button("Tạo Class AdPlacement (Constants)", GUILayout.Height(28)))
            {
                SaveAllData();
                GenerateAdPlacementConstants();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.Space(8);
            EditorGUILayout.EndScrollView();
        }

        private void SaveAllData()
        {
            foreach (var tab in _allTabs)
            {
                try
                {
                    tab.Save();
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[GameUp.SDK] Lỗi khi lưu {tab.Title}: {e.Message}");
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[GameUp.SDK] Đã lưu cấu hình thành công vào tất cả các Prefab/Asset độc lập!");
        }

        private static bool RequiresPrefabCloneBeforeSetup() => AssetDatabase.LoadAssetAtPath<GameObject>(GameUpSetupPaths.WritablePrefabsRoot + "/SDK.prefab") == null;

        private static bool CanSetupFromWritablePrefabs(out string reason)
        {
            if (RequiresPrefabCloneBeforeSetup())
            {
                reason = "Chưa có prefab clone tại " + GameUpSetupPaths.WritablePrefabsRoot + ". Hãy clone trước.";
                return false;
            }
            reason = null; return true;
        }

        private static void TryClonePackagePrefabsToWritable()
        {
            EnsureFolderExists(GameUpSetupPaths.WritablePrefabsRoot);

            var srcDir = GameUpSetupPaths.GetPackagePrefabDirectory().Replace('\\', '/');
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { srcDir });

            // 1. Copy tất cả các file trước
            foreach (var g in guids)
            {
                var src = AssetDatabase.GUIDToAssetPath(g);
                var dst = GameUpSetupPaths.WritablePrefabsRoot + "/" + Path.GetFileName(src);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(dst) == null) AssetDatabase.CopyAsset(src, dst);
            }

            AssetDatabase.Refresh();

            // 2. Chạy logic vá lỗi liên kết Nested Prefabs
            FixNestedPrefabReferences(GameUpSetupPaths.WritablePrefabsRoot + "/SDK.prefab");
        }

        // Tạo folder đệ quy theo từng cấp (AssetDatabase.CreateFolder chỉ tạo được 1 cấp/lần)
        private static void EnsureFolderExists(string folderPath)
        {
            folderPath = folderPath.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            var parts = folderPath.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        // =========================================================================
        // LOGIC FIX LỖI NESTED PREFAB TRỌ BẬY SAU KHI CLONE
        // =========================================================================

        private struct PrefabReplacementPair
        {
            public GameObject OldChild;
            public GameObject NewPrefab;
        }

        private static void FixNestedPrefabReferences(string sdkPrefabPath)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(sdkPrefabPath) == null) return;

            // Mở SDK.prefab lên dưới dạng Memory Instance để chỉnh sửa
            var root = PrefabUtility.LoadPrefabContents(sdkPrefabPath);
            if (root == null) return;

            bool changed = false;
            var childrenToReplace = new List<PrefabReplacementPair>();

            // Quét qua các object con trực tiếp
            foreach (Transform child in root.transform)
            {
                // Nếu child này là một Prefab instance (nested prefab)
                if (PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject))
                {
                    var originalPrefab = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject);
                    if (originalPrefab != null)
                    {
                        string oldPath = AssetDatabase.GetAssetPath(originalPrefab);
                        string fileName = Path.GetFileName(oldPath);
                        string expectedNewPath = GameUpSetupPaths.WritablePrefabsRoot + "/" + fileName;

                        // Nếu cái cũ đang trỏ về thư mục Package -> Đánh dấu cần thay thế
                        if (!string.Equals(oldPath, expectedNewPath, System.StringComparison.OrdinalIgnoreCase))
                        {
                            var newPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(expectedNewPath);
                            if (newPrefab != null)
                            {
                                childrenToReplace.Add(new PrefabReplacementPair
                                {
                                    OldChild = child.gameObject,
                                    NewPrefab = newPrefab
                                });
                            }
                        }
                    }
                }
            }

            // Thực hiện thay thế từng cục con
            foreach (var pair in childrenToReplace)
            {
                var oldChild = pair.OldChild;
                var newPrefab = pair.NewPrefab;

                // Ghi nhớ lại vị trí, thông số của prefab cũ
                int siblingIndex = oldChild.transform.GetSiblingIndex();
                string objName = oldChild.name;
                Vector3 pos = oldChild.transform.localPosition;
                Quaternion rot = oldChild.transform.localRotation;
                Vector3 scale = oldChild.transform.localScale;

                // Phá huỷ prefab gốc (Cái đang trỏ về Package)
                GameObject.DestroyImmediate(oldChild);

                // Khởi tạo Prefab mới (Cái nằm trong WritablePrefabsRoot) và nhét vào làm con của SDK.prefab
                var newInstance = (GameObject)PrefabUtility.InstantiatePrefab(newPrefab, root.transform);
                newInstance.name = objName;
                newInstance.transform.localPosition = pos;
                newInstance.transform.localRotation = rot;
                newInstance.transform.localScale = scale;
                newInstance.transform.SetSiblingIndex(siblingIndex);

                changed = true;
            }

            if (changed)
            {
                // Áp dụng lưu và đóng file prefab
                PrefabUtility.SaveAsPrefabAsset(root, sdkPrefabPath);
                Debug.Log("[GameUp.SDK] Đã cập nhật lại link các Prefab con trong SDK.prefab trỏ đúng về thư mục " + GameUpSetupPaths.WritablePrefabsRoot + ".");
            }

            PrefabUtility.UnloadPrefabContents(root);
        }

        private void CreateSDKInCurrentScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameUpSetupPaths.PathSDK);
            if (prefab == null) { _saveErrors = "Không tìm thấy prefab SDK!"; return; }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (instance != null)
            {
                Selection.activeGameObject = instance;
                EditorGUIUtility.PingObject(instance);
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
        }

        // =========================================================================
        // LOGIC TẠO FILE CONSTANTS CHỨA TÊN PLACEMENT (WHERE)
        // =========================================================================

        private void GenerateAdPlacementConstants()
        {
            HashSet<string> placements = new HashSet<string>();

            // Các placement hệ thống mặc định (nếu muốn)
            placements.Add("default");
            placements.Add("main");

            // Quét các Prefab chứa logic Ads
            ExtractPlacementsFromPrefab(GameUpSetupPaths.PathAdMob, placements);
            ExtractPlacementsFromPrefab(GameUpSetupPaths.PathMax, placements);
            ExtractPlacementsFromPrefab(GameUpSetupPaths.PathIronSource, placements);

            // Sinh mã nguồn C#
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("// ========================================================");
            sb.AppendLine("// AUTO-GENERATED FILE BY GAMEUP SDK. DO NOT MODIFY DIRECTLY.");
            sb.AppendLine("// ========================================================");
            sb.AppendLine("");
            sb.AppendLine("namespace GameUp.SDK");
            sb.AppendLine("{");
            sb.AppendLine("    public static class AdPlacement");
            sb.AppendLine("    {");

            foreach (var p in placements)
            {
                string varName = SanitizeToVariableName(p);
                sb.AppendLine($"        public const string {varName} = \"{p}\";");
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            // Tạo thư mục Scripts nếu chưa có
            string dir = "Assets/_MainProject/Scripts/SDK";
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            // Ghi file
            string path = dir + "/AdPlacement.cs";
            File.WriteAllText(path, sb.ToString());

            AssetDatabase.Refresh();
            Debug.Log($"[GameUp.SDK] Đã tạo thành công file Constants tại: {path}");

            // Highlight file cho bạn dễ thấy
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (asset != null) EditorGUIUtility.PingObject(asset);
        }

        private void ExtractPlacementsFromPrefab(string prefabPath, HashSet<string> placements)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null) return;
            var go = PrefabUtility.LoadPrefabContents(prefabPath);
            if (go == null) return;

            try
            {
                var network = go.GetComponent<IAdNetwork>();
                if (network != null)
                {
                    var so = new SerializedObject((MonoBehaviour)network);
                    string[] configs = { "bannerConfig", "interstitialConfig", "rewardedConfig", "appOpenConfig", "nativeConfig" };

                    foreach (var c in configs)
                    {
                        var configProp = so.FindProperty(c);
                        if (configProp != null)
                        {
                            ExtractFromList(configProp.FindPropertyRelative("multiIdsAndroid"), placements);
                            ExtractFromList(configProp.FindPropertyRelative("multiIdsIOS"), placements);
                        }
                    }
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(go);
            }
        }

        private void ExtractFromList(SerializedProperty listProp, HashSet<string> placements)
        {
            if (listProp == null || !listProp.isArray) return;
            for (int i = 0; i < listProp.arraySize; i++)
            {
                var element = listProp.GetArrayElementAtIndex(i);
                var nameProp = element.FindPropertyRelative("NameId"); // Đây chính là biến string "Where"

                if (nameProp != null && !string.IsNullOrWhiteSpace(nameProp.stringValue))
                {
                    placements.Add(nameProp.stringValue.Trim());
                }
            }
        }

        private string SanitizeToVariableName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "Unknown";

            // 1. Thay thế các ký tự không phải chữ/số thành dấu cách (để dễ viết hoa)
            char[] chars = raw.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i])) chars[i] = ' ';
            }

            // 2. Chuyển thành PascalCase (VD: "main menu" -> "Main Menu")
            string cleaned = new string(chars);
            System.Globalization.TextInfo textInfo = new System.Globalization.CultureInfo("en-US", false).TextInfo;
            cleaned = textInfo.ToTitleCase(cleaned).Replace(" ", "");

            // 3. Đảm bảo biến hợp lệ trong C# (nếu lỡ bắt đầu bằng số, thì thêm dấu _)
            if (!char.IsLetter(cleaned[0]) && cleaned[0] != '_')
            {
                cleaned = "_" + cleaned;
            }

            return cleaned;
        }
    }
}