using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using System.IO;
using GameUp.SDK;

namespace GameUp.SDK.Editor.Setup
{
    public class GameUpSetupWindow : EditorWindow
    {
        private const int OverviewIndex = -1;
        private const float SidebarWidth = 200f;

        private static readonly Dictionary<string, string> TabDescriptions = new Dictionary<string, string>
        {
            { "Facebook", "App ID / Client Token dùng cho Facebook SDK." },
            { "AppsFlyer", "Dev Key và App ID để đo lường attribution." },
            { "Game Analytics", "Game Key / Secret Key cho từng nền tảng." },
            { "AppMetrica", "API Key của AppMetrica." },
            { "IronSource Mediation", "App Key và các ad unit của IronSource." },
            { "MAX Mediation", "SDK Key và các ad unit của AppLovin MAX." },
            { "AdMob / AppOpen", "App ID, ad unit AdMob và cấu hình App Open." },
            { "Firebase RC", "Khai báo các key Remote Config mặc định." }
        };

        private List<SetupTabBase> _allTabs;
        private List<SetupTabBase> _visibleTabs;
        private int _activeTabIndex = OverviewIndex;
        private string _loadErrors;
        private string _saveErrors;
        private Vector2 _sidebarScroll;
        private Vector2 _contentScroll;
        private GameUpAdsConfig _adsConfig;
        private GameUpSdkConfig _sdkConfig;
        private bool _hasPendingLegacyAdsData;
        private bool _hasPendingLegacySdkData;
        private bool _showAdvanced;

        private GUIStyle _sidebarItemStyle;
        private GUIStyle _sidebarItemSelectedStyle;
        private GUIStyle _sidebarSectionStyle;
        private GUIStyle _panelTitleStyle;
        private GUIStyle _panelSubtitleStyle;
        private GUIStyle _paddedAreaStyle;

        [MenuItem("GameUp/SDK/Setup")]
        public static void ShowWindow()
        {
            if (!Installer.GameUpDependenciesWindow.AreAllRequiredPackagesInstalled())
            {
                Installer.GameUpDependenciesWindow.ShowWindow();
                return;
            }
            GetWindow<GameUpSetupWindow>("GameUp SDK Setup").minSize = new Vector2(820, 560);
        }

        private void OnEnable()
        {
            wantsMouseMove = true;
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
            if (_activeTabIndex >= _visibleTabs.Count) _activeTabIndex = OverviewIndex;
        }

        private void LoadAllData()
        {
            _loadErrors = null;
            bool prefabsReady = CanSetupFromWritablePrefabs(out var lockReason);
            if (!prefabsReady) _loadErrors = lockReason;

            // Mọi cấu hình đều nằm trong ScriptableObject nên luôn load được, kể cả khi chưa clone prefab.
            _adsConfig = GameUpAdsConfigAsset.GetOrCreate();
            _sdkConfig = GameUpSdkConfigAsset.GetOrCreate();
            _hasPendingLegacyAdsData = GameUpAdsConfigAsset.HasPendingLegacyData(_adsConfig);
            _hasPendingLegacySdkData = GameUpSdkConfigAsset.HasPendingLegacyData(_sdkConfig);

            foreach (var tab in _allTabs)
            {
                if (tab.RequiresWritablePrefab && !prefabsReady) continue;
                tab.Load();
            }
        }

        private void OnGUI()
        {
            EnsureStyles();
            if (Event.current.type == EventType.MouseMove) Repaint();

            bool prefabsReady = CanSetupFromWritablePrefabs(out var lockReason);

            DrawTopBar();

            EditorGUILayout.BeginHorizontal();
            DrawSidebar(prefabsReady);
            DrawVerticalSeparator();
            DrawContentPanel(prefabsReady, lockReason);
            EditorGUILayout.EndHorizontal();
        }

        // =========================================================================
        // TOP BAR
        // =========================================================================

        private void DrawTopBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("GameUp SDK Setup", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(_adsConfig == null))
                if (GUILayout.Button("Ads Config", EditorStyles.toolbarButton)) EditorGUIUtility.PingObject(_adsConfig);

            using (new EditorGUI.DisabledScope(_sdkConfig == null))
                if (GUILayout.Button("SDK Config", EditorStyles.toolbarButton)) EditorGUIUtility.PingObject(_sdkConfig);

            if (GUILayout.Button("Tải lại", EditorStyles.toolbarButton)) LoadAllData();
            EditorGUILayout.EndHorizontal();
        }

        // =========================================================================
        // SIDEBAR (danh sách tab bên trái)
        // =========================================================================

        private void DrawSidebar(bool prefabsReady)
        {
            var area = EditorGUILayout.BeginVertical(GUILayout.Width(SidebarWidth), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(area, SidebarBackground);

            _sidebarScroll = EditorGUILayout.BeginScrollView(_sidebarScroll, GUILayout.Width(SidebarWidth));
            GUILayout.Space(6);

            DrawSidebarSection("TỔNG QUAN");
            if (DrawSidebarItem("Config & Công cụ", _activeTabIndex == OverviewIndex, false, false))
                _activeTabIndex = OverviewIndex;

            DrawSidebarGroup("QUẢNG CÁO", tab => tab is AdsConfigTabBase, prefabsReady);
            DrawSidebarGroup("ANALYTICS & DỊCH VỤ", tab => tab is not AdsConfigTabBase, prefabsReady);

            GUILayout.Space(8);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawSidebarGroup(string sectionTitle, System.Func<SetupTabBase, bool> filter, bool prefabsReady)
        {
            var indices = new List<int>();
            for (int i = 0; i < _visibleTabs.Count; i++)
                if (filter(_visibleTabs[i])) indices.Add(i);

            if (indices.Count == 0) return;

            DrawSidebarSection(sectionTitle);
            for (int n = 0; n < indices.Count; n++)
            {
                int i = indices[n];
                var tab = _visibleTabs[i];
                bool locked = tab.RequiresWritablePrefab && !prefabsReady;
                // Không kẻ line dưới item cuối nhóm vì đã có line của section kế tiếp.
                if (DrawSidebarItem(tab.Title, _activeTabIndex == i, locked, n < indices.Count - 1))
                    _activeTabIndex = i;
            }
        }

        private void DrawSidebarSection(string title)
        {
            GUILayout.Space(10);
            var rect = GUILayoutUtility.GetRect(new GUIContent(title), _sidebarSectionStyle, GUILayout.Height(16), GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                _sidebarSectionStyle.Draw(rect, title, false, false, false, false);
                EditorGUI.DrawRect(InsetLine(rect, rect.yMax + 3f), SeparatorColor);
            }
            GUILayout.Space(6);
        }

        /// <summary>Vẽ 1 dòng trong sidebar, trả về true khi user vừa bấm vào nó.</summary>
        private bool DrawSidebarItem(string label, bool selected, bool locked, bool underline)
        {
            var content = locked
                ? new GUIContent(label, EditorGUIUtility.IconContent("console.warnicon.sml").image, "Tab này cần clone prefab trước khi sửa.")
                : new GUIContent(label);
            var style = selected ? _sidebarItemSelectedStyle : _sidebarItemStyle;
            var rect = GUILayoutUtility.GetRect(content, style, GUILayout.Height(28), GUILayout.ExpandWidth(true));
            var e = Event.current;
            bool hover = rect.Contains(e.mousePosition);

            if (e.type == EventType.Repaint)
            {
                if (selected)
                {
                    EditorGUI.DrawRect(rect, SelectionBackground);
                    EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), SelectionAccent);
                }
                else if (hover)
                {
                    EditorGUI.DrawRect(rect, HoverBackground);
                }

                style.Draw(rect, content, false, false, false, false);

                if (underline && !selected) EditorGUI.DrawRect(InsetLine(rect, rect.yMax - 1f), ItemSeparatorColor);
            }

            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);

            if (e.type == EventType.MouseDown && e.button == 0 && hover)
            {
                e.Use();
                GUI.FocusControl(null);
                return true;
            }
            return false;
        }

        /// <summary>Line ngang 1px thụt vào 2 bên, dùng cho các đường chia trong sidebar.</summary>
        private static Rect InsetLine(Rect source, float y) => new Rect(source.x + 12f, y, Mathf.Max(0f, source.width - 24f), 1f);

        // =========================================================================
        // CONTENT PANEL (nội dung chi tiết bên phải)
        // =========================================================================

        private void DrawContentPanel(bool prefabsReady, string lockReason)
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            var activeTab = _activeTabIndex >= 0 && _activeTabIndex < _visibleTabs.Count ? _visibleTabs[_activeTabIndex] : null;
            if (activeTab == null)
                DrawPanelHeader("Config & Công cụ", "Vị trí các file cấu hình và những thao tác chung của SDK.");
            else
                DrawPanelHeader(activeTab.Title, TabDescriptions.TryGetValue(activeTab.Title, out var desc) ? desc : null);

            _contentScroll = EditorGUILayout.BeginScrollView(_contentScroll);
            EditorGUILayout.BeginVertical(_paddedAreaStyle);

            DrawPendingBanners();

            if (activeTab == null)
            {
                DrawOverviewPage();
            }
            else if (activeTab.RequiresWritablePrefab && !prefabsReady)
            {
                EditorGUILayout.HelpBox($"Tab \"{activeTab.Title}\" ghi cấu hình vào prefab. {lockReason}", MessageType.Warning);
                if (GUILayout.Button("Clone Prefab từ Package → " + GameUpSetupPaths.WritablePrefabsRoot, GUILayout.Height(30)))
                {
                    TryClonePackagePrefabsToWritable();
                    LoadAllData();
                }
            }
            else
            {
                activeTab.Draw();
            }

            GUILayout.Space(12);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();

            DrawFooterBar();
            EditorGUILayout.EndVertical();
        }

        private void DrawPanelHeader(string title, string subtitle)
        {
            var rect = EditorGUILayout.BeginVertical(_paddedAreaStyle);
            EditorGUI.DrawRect(rect, HeaderBackground);
            GUILayout.Label(title, _panelTitleStyle);
            if (!string.IsNullOrEmpty(subtitle)) GUILayout.Label(subtitle, _panelSubtitleStyle);
            EditorGUILayout.EndVertical();
            DrawHorizontalSeparator();
        }

        private void DrawPendingBanners()
        {
            if (!string.IsNullOrEmpty(_saveErrors))
            {
                EditorGUILayout.HelpBox(_saveErrors, MessageType.Error);
                GUILayout.Space(4);
                if (Event.current.type == EventType.Repaint) _saveErrors = null;
            }

            if (!_hasPendingLegacyAdsData && !_hasPendingLegacySdkData) return;

            EditorGUILayout.HelpBox("Phát hiện dữ liệu còn nằm trong prefab (bản cũ). Chuyển sang ScriptableObject để dùng.", MessageType.Warning);
            if (GUILayout.Button("Migrate dữ liệu từ Prefab → ScriptableObject")) MigrateLegacyData(logReport: true);
            GUILayout.Space(6);
        }

        private void DrawOverviewPage()
        {
            EditorGUILayout.LabelField("Config Assets (nguồn dữ liệu duy nhất)", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            DrawConfigRow("Ads Config", _adsConfig);
            DrawConfigRow("SDK Config", _sdkConfig);
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);
            EditorGUILayout.LabelField("Bắt đầu nhanh", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "1. Chọn từng mục ở cột trái để điền key của network.\n" +
                "2. Bấm \"Lưu cấu hình\" ở thanh dưới.\n" +
                "3. Bấm \"Tạo SDK trong Scene\" để đưa prefab SDK vào scene hiện tại.",
                MessageType.Info);

            GUILayout.Space(10);
            DrawAdvancedSection();
        }

        private void DrawFooterBar()
        {
            DrawHorizontalSeparator();
            var rect = EditorGUILayout.BeginHorizontal(_paddedAreaStyle, GUILayout.Height(40));
            EditorGUI.DrawRect(rect, HeaderBackground);

            if (GUILayout.Button("Lưu cấu hình", GUILayout.Height(26), GUILayout.MinWidth(120))) SaveAllData();
            if (GUILayout.Button("Tạo SDK trong Scene", GUILayout.Height(26), GUILayout.MinWidth(140))) CreateSDKInCurrentScene();

            GUILayout.FlexibleSpace();

            GUI.backgroundColor = new Color(0.8f, 1f, 0.8f);
            if (GUILayout.Button("Tạo Class AdPlacement (Constants)", GUILayout.Height(26), GUILayout.MinWidth(200)))
            {
                SaveAllData();
                AdPlacementGenerator.Generate();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        private void DrawConfigRow(string label, ScriptableObject asset)
        {
            EditorGUILayout.BeginHorizontal();
            if (asset == null)
            {
                EditorGUILayout.LabelField(label, "chưa tạo");
                if (GUILayout.Button("Tạo", GUILayout.Width(60))) LoadAllData();
            }
            else
            {
                EditorGUILayout.LabelField(label, AssetDatabase.GetAssetPath(asset));
                if (GUILayout.Button("Ping", GUILayout.Width(60))) EditorGUIUtility.PingObject(asset);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawAdvancedSection()
        {
            _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "Nâng cao", true);
            if (!_showAdvanced) return;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.HelpBox(
                "Toàn bộ cấu hình đã nằm trong ScriptableObject nên không cần clone prefab nữa. " +
                "Chỉ clone khi muốn sửa cấu trúc prefab (thêm/bớt component, đổi hierarchy).",
                MessageType.None);

            if (GUILayout.Button("Clone Prefab từ Package → " + GameUpSetupPaths.WritablePrefabsRoot))
            {
                TryClonePackagePrefabsToWritable();
                LoadAllData();
            }

            if (GUILayout.Button("Migrate lại dữ liệu từ Prefab → ScriptableObject")) MigrateLegacyData(logReport: false);

            if (!string.IsNullOrEmpty(_loadErrors)) EditorGUILayout.HelpBox(_loadErrors, MessageType.None);
            EditorGUILayout.EndVertical();
        }

        private void MigrateLegacyData(bool logReport)
        {
            var adsReport = GameUpAdsConfigAsset.MigrateFromPrefabs(_adsConfig, overwriteExisting: true);
            var sdkReport = GameUpSdkConfigAsset.MigrateFromPrefabs(_sdkConfig, overwriteExisting: true);
            if (_adsConfig != null) EditorUtility.SetDirty(_adsConfig);
            if (_sdkConfig != null) EditorUtility.SetDirty(_sdkConfig);
            AssetDatabase.SaveAssets();

            if (logReport)
            {
                var report = string.Join("\n", new[] { adsReport, sdkReport }.Where(s => !string.IsNullOrEmpty(s)));
                Debug.Log("[GameUp.SDK] " + (string.IsNullOrEmpty(report) ? "Không có dữ liệu để migrate." : report));
            }
            LoadAllData();
        }

        // =========================================================================
        // STYLES & MÀU SẮC
        // =========================================================================

        private static Color SidebarBackground => EditorGUIUtility.isProSkin ? new Color(0.19f, 0.19f, 0.19f) : new Color(0.78f, 0.78f, 0.78f);
        private static Color HeaderBackground => EditorGUIUtility.isProSkin ? new Color(0.24f, 0.24f, 0.24f) : new Color(0.85f, 0.85f, 0.85f);
        private static Color SelectionBackground => EditorGUIUtility.isProSkin ? new Color(0.24f, 0.37f, 0.59f) : new Color(0.24f, 0.48f, 0.90f);
        private static Color SelectionAccent => EditorGUIUtility.isProSkin ? new Color(0.45f, 0.68f, 1f) : new Color(0.12f, 0.30f, 0.65f);
        private static Color HoverBackground => EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.06f) : new Color(0f, 0f, 0f, 0.06f);
        private static Color SeparatorColor => EditorGUIUtility.isProSkin ? new Color(0.13f, 0.13f, 0.13f) : new Color(0.6f, 0.6f, 0.6f);
        private static Color ItemSeparatorColor => EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.07f) : new Color(0f, 0f, 0f, 0.10f);

        private void EnsureStyles()
        {
            if (_sidebarItemStyle != null) return;

            _sidebarItemStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(16, 6, 0, 0)
            };

            _sidebarItemSelectedStyle = new GUIStyle(_sidebarItemStyle) { fontStyle = FontStyle.Bold };
            _sidebarItemSelectedStyle.normal.textColor = Color.white;

            _sidebarSectionStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(12, 6, 0, 2)
            };

            _panelTitleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
            _panelSubtitleStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
            _paddedAreaStyle = new GUIStyle { padding = new RectOffset(12, 12, 8, 8) };
        }

        private static void DrawHorizontalSeparator()
        {
            var rect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, SeparatorColor);
        }

        private static void DrawVerticalSeparator()
        {
            var rect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.Width(1f), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(rect, SeparatorColor);
        }

        private void SaveAllData()
        {
            bool prefabsReady = CanSetupFromWritablePrefabs(out _);
            foreach (var tab in _allTabs)
            {
                if (tab.RequiresWritablePrefab && !prefabsReady) continue;
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
            GameUpAdsConfig.ClearCache();
            GameUpSdkConfig.ClearCache();
            Debug.Log("[GameUp.SDK] Đã lưu cấu hình thành công!");
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
            GameUpSetupPaths.EnsureFolderExists(GameUpSetupPaths.WritablePrefabsRoot);

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

        // =========================================================================
        // LOGIC FIX LỖI NESTED PREFAB TRỎ SAI SAU KHI CLONE
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
    }
}
