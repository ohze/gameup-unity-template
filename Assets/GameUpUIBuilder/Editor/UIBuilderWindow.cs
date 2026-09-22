using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameUp.Core;
using GameUp.Core.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>
    /// GameUp → UI → UI Builder: ảnh demo + art → định vị sprite (hoặc đọc file PSD) → spec → prefab → đối chiếu.
    /// Mỗi bước là một card có trạng thái và hướng dẫn; bước chạy lâu (cài Python, định vị) chạy nền, không chặn Editor.
    /// </summary>
    public sealed class UIBuilderWindow : EditorWindow
    {
        private const string MenuPath = "GameUp/UI/UI Builder (Demo → Prefab)";
        private const string LogTag = "UIBuilder";
        private const float MinLeftWidth = 440f;
        private const float MinPreviewWidth = 160f;
        private const float RowLabelWidth = 72f;
        private const double RepaintInterval = 0.1;
        private const double StampCheckInterval = 1.0;
        private const int BroadArtWarning = 200;
        private const int SkipSnap = 16; // px demo — cạnh vùng bỏ qua gần mép ảnh thì dính vào mép
        private const int AutoWorkerCap = 12; // khớp MAX_WORKERS trong Tools~/ui_locate.py

        private UIBuilderPython _pythonSetup;
        private UIBuilderLocator _locator;
        private int _locatingIndex;
        private readonly Queue<int> _locateQueue = new Queue<int>();
        private bool _crossCheckPass; // đang ở lượt đối chiếu phần chung giữa các tab
        private bool _drawingSkip;    // đang kéo khung "phần đã có sẵn" trên ảnh demo
        private Vector2? _dragStart;
        private Vector2 _dragEnd;
        private List<LocateResult> _locates = new List<LocateResult>();
        private LocateResult _locate; // của demo đang xem trước
        private int _previewIndex;
        private string _locateMessage;
        private string _locateError; // lỗi của lần định vị gần nhất — hiện bằng hộp lỗi, không lẫn vào gợi ý xám
        private UISpec _spec;
        private string _specError;
        private UIBuildReport _report;
        private string _comparePath;
        private string _systemPython;
        private Vector2 _scroll;
        private LocateProgress _lastProgress; // nhật ký lần định vị gần nhất trong phiên
        private bool _showUnmatched;
        private bool _showTexts;
        private bool _showDropped;
        private bool _showLocateLog = true;
        private Vector2 _logScroll;
        private double _lastRepaint;
        private double _lastStampCheck;
        private string _artCountKey; // thư mục art + cờ thư mục con lúc đếm — đếm lại khi đổi
        private int _artCount;
        private bool _showPythonLog;
        private string _highlightSprite;
        private DateTime _jobStamp;
        private readonly Dictionary<string, Object> _assetCache = new Dictionary<string, Object>();

        private static UIBuilderSettings Settings => UIBuilderSettings.instance;

        private static bool IsPsdMode => Settings.source == UIBuilderSource.Psd;

        private string SpecPath => UIBuilderPaths.SpecPath(Settings.jobName);

        /// <summary>
        /// Demo 1 + demo các trạng thái khác (tab 2…) còn tồn tại. Chế độ PSD sau khi đọc: mỗi trạng thái của PSD một ảnh —
        /// demo người dùng đưa cho tab đó, không có thì ảnh ghép từ PSD.
        /// </summary>
        private List<string> Demos
        {
            get
            {
                if (IsPsdMode && _locates.Count > 0)
                    return _locates.Select(l => ProjectPath(l.demo)).ToList();
                var demos = new List<string>();
                if (HasDemo()) demos.Add(Settings.demoPath);
                demos.AddRange(Settings.extraDemos.Where(d => !string.IsNullOrEmpty(d) && File.Exists(UIBuilderPaths.ToAbsolute(d))));
                return demos;
            }
        }

        private string PreviewDemo
        {
            get
            {
                var demos = Demos;
                return demos.Count == 0 ? null : demos[Mathf.Clamp(_previewIndex, 0, demos.Count - 1)];
            }
        }

        private string OutputPrefab => $"{Settings.outputFolder.TrimEnd('/')}/{Settings.jobName}.prefab";

        [MenuItem(MenuPath)]
        public static void Open()
        {
            var isNew = !HasOpenInstances<UIBuilderWindow>();
            var window = GetWindow<UIBuilderWindow>();
            window.titleContent = new GUIContent("UI Builder");
            window.minSize = new Vector2(MinLeftWidth, 480f);
            // Lần đầu mở: đủ rộng cho cột thông tin + cột ảnh demo tỉ lệ 1:2.
            if (isNew) window.position = new Rect(window.position.x, window.position.y, 900f, 780f);
            window.Show();
        }

        private void OnEnable()
        {
            wantsMouseMove = true; // để nhãn tên sprite trên preview bám theo con trỏ
            _systemPython = UIBuilderPython.FindSystemPython();
            ReloadJob();
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            _locator?.Cancel();
            _pythonSetup?.Cancel();
            UIMockupOverlay.Hide();
            UIDemoTexture.Release();
        }

        private void OnGUI()
        {
            if (Event.current.type == EventType.MouseMove) Repaint();
            // locate.json / spec.json có thể bị Claude hoặc terminal ghi lại → tự tải lại khi file đổi. Kiểm tra tối đa
            // mỗi giây một lần, và không kiểm khi đang định vị (script tự ghi locate.json, Tick nạp kết quả khi xong).
            if (Event.current.type == EventType.Layout && _locator == null
                && EditorApplication.timeSinceStartup - _lastStampCheck >= StampCheckInterval)
            {
                _lastStampCheck = EditorApplication.timeSinceStartup;
                if (JobStamp() != _jobStamp) ReloadJob();
            }
            var texture = PreviewDemo != null ? UIDemoTexture.Get(PreviewDemo) : null;
            var previewWidth = PreviewWidth(texture);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            GUInstallerUI.SectionHeader("UI BUILDER", "Ảnh demo + art đã cắt → prefab uGUI đúng vị trí, kích thước.");

            DrawPythonStep();
            DrawInputStep(texture, previewWidth > 0f);
            DrawLocateStep();
            DrawSpecStep();
            DrawBuildStep();
            DrawCompareStep();

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            if (previewWidth > 0f) DrawPreviewPanel(texture, previewWidth);
            EditorGUILayout.EndHorizontal();
        }

        // ─── Cột phải — ảnh demo ────────────────────────────────────────────

        /// <summary>Rộng vừa đủ để ảnh demo cao bằng cửa sổ; 0 nếu tắt, chưa có demo, hoặc cửa sổ quá hẹp.</summary>
        private float PreviewWidth(Texture2D texture)
        {
            if (!Settings.showDemoPreview || texture == null) return 0f;
            var ideal = (position.height - 8f) * texture.width / texture.height;
            var width = Mathf.Floor(Mathf.Min(ideal, position.width - MinLeftWidth));
            return width >= MinPreviewWidth ? width : 0f;
        }

        private void DrawPreviewPanel(Texture2D texture, float width)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(width), GUILayout.ExpandHeight(true));
            var area = GUILayoutUtility.GetRect(width, width, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            // Đang định vị đúng demo đang xem → vẽ kết quả tạm, tô nổi sprite vừa dò xong.
            var live = _locator != null && _locatingIndex == _previewIndex ? _locator.Progress : null;
            var caption = live != null ? $"{LocateProgress.Stages[live.Stage]} · {live.Status}" : null;
            var hovered = UIDemoPreviewDrawer.Draw(area, texture, live?.Snapshot ?? _locate, Settings.previewLayers, _highlightSprite,
                live?.LatestSprite, caption);
            var image = UIDemoPreviewDrawer.FitRect(area, texture.width, texture.height);
            var scale = image.width / texture.width;
            UIDemoPreviewDrawer.DrawSkipRegions(image, scale, Settings.skipRegions, _dragStart.HasValue ? DragRect() : (Rect?)null);
            if (_drawingSkip) HandleSkipDrag(image, scale, texture);
            else if (hovered != null && Event.current.type == EventType.MouseDown)
            {
                _highlightSprite = hovered;
                Event.current.Use();
            }

            EditorGUILayout.EndVertical();
        }

        // ─── BƯỚC 1 — Python ────────────────────────────────────────────────

        private void DrawPythonStep()
        {
            var busy = _pythonSetup != null && _pythonSetup.IsRunning;
            var ready = UIBuilderPython.IsReady;
            var outdated = ready && UIBuilderPython.IsOutdated;
            var state = busy ? GUSetupState.Busy : ready && !outdated ? GUSetupState.Done : GUSetupState.Missing;

            using (GUInstallerUI.BeginCard())
            {
                GUInstallerUI.CardHeader("BƯỚC 1", "Môi trường Python (OpenCV + OCR + PSD)", state);
                GUILayout.Label("Định vị sprite bằng OpenCV, đọc chữ bằng RapidOCR, đọc PSD bằng psd-tools — chạy trên máy, không cần mạng, không cần Photoshop. Cài một lần cho mọi project trên máy.",
                    GUInstallerUI.Desc);

                GUInstallerUI.StatusRow("Python hệ thống", _systemPython != null ? GUSetupState.Done : GUSetupState.Missing,
                    _systemPython ?? "không tìm thấy — cài Python 3.9+");
                GUInstallerUI.StatusRow("Venv", ready && !outdated ? GUSetupState.Done : GUSetupState.Missing,
                    outdated ? "bản cũ — bấm Cập nhật (thêm đọc PSD)" : UIBuilderPaths.VenvFolder);

                EditorGUILayout.BeginHorizontal();
                var setupLabel = outdated ? "Cập nhật" : ready ? "Cài lại" : "Cài môi trường";
                if (GUInstallerUI.MiniButton(setupLabel, !busy && _systemPython != null, 140f))
                {
                    _pythonSetup = new UIBuilderPython();
                    _pythonSetup.StartSetup(_systemPython);
                    _showPythonLog = true;
                    StartTicking();
                }

                if (GUInstallerUI.MiniButton("Dò lại Python", !busy, 110f)) _systemPython = UIBuilderPython.FindSystemPython();
                if (busy && GUInstallerUI.MiniButton("Huỷ", true, 60f)) _pythonSetup.Cancel();
                EditorGUILayout.EndHorizontal();

                if (_pythonSetup != null)
                {
                    _showPythonLog = EditorGUILayout.Foldout(_showPythonLog, _pythonSetup.Failed ? "Log (lỗi)" : "Log", true);
                    if (_showPythonLog) EditorGUILayout.HelpBox(_pythonSetup.Log.TrimEnd(), _pythonSetup.Failed ? MessageType.Error : MessageType.None);
                }
            }
        }

        // ─── BƯỚC 2 — Đầu vào ───────────────────────────────────────────────

        private void DrawInputStep(Texture2D texture, bool previewShown)
        {
            var hasInput = IsPsdMode ? HasPsd() : HasDemo() && Settings.artFolders.Count > 0;
            using (GUInstallerUI.BeginCard())
            {
                GUInstallerUI.CardHeader("BƯỚC 2", IsPsdMode ? "File PSD, art và ảnh demo" : "Ảnh demo và thư mục art",
                    hasInput ? GUSetupState.Done : GUSetupState.Missing);

                EditorGUI.BeginChangeCheck();
                var jobName = EditorGUILayout.TextField(new GUIContent("Tên UI", "Tên prefab và thư mục UIBuilder/<Tên>/"), Settings.jobName);
                if (EditorGUI.EndChangeCheck())
                {
                    Settings.jobName = SanitizeName(jobName);
                    Settings.Save();
                    ReloadJob();
                }

                DrawSourceToggle();
                if (IsPsdMode) DrawPsdInput();

                if (IsPsdMode)
                    SubHeader("ẢNH DEMO — TUỲ CHỌN, ĐỂ SO SÁNH", "Theo thứ tự tab trong PSD (xem danh sách trạng thái ở trên). Tab không có demo dùng ảnh ghép từ PSD. Demo khác PSD → ghi chú trong spec.");
                else
                    SubHeader("ẢNH DEMO", "Mỗi ảnh là một trạng thái/tab của cùng UI — phần giống nhau dựng một lần, phần riêng vào nhóm của tab.");
                DrawDemoList();
                if (texture != null) DrawDemoDetails(texture, previewShown);

                SubHeader("THƯ MỤC ART", IsPsdMode
                    ? "Art đã cắt để nối với layer (cùng tên trước, rồi theo pixel). Layer không có art → xuất PNG từ PSD (nếu bật)."
                    : "Thư mục art riêng của màn + thư mục dùng chung (_Shared, Avatar…). Art cắt đúng tỉ lệ với demo.");
                DrawArtFolders();

                SubHeader("ĐẦU RA");
                EditorGUI.BeginChangeCheck();
                var output = (DefaultAsset)EditorGUILayout.ObjectField("Thư mục prefab", LoadAsset<DefaultAsset>(Settings.outputFolder),
                    typeof(DefaultAsset), false);
                if (EditorGUI.EndChangeCheck())
                {
                    var path = output != null ? AssetDatabase.GetAssetPath(output) : null;
                    if (path != null && AssetDatabase.IsValidFolder(path)) Settings.outputFolder = path;
                    Settings.Save();
                }

                GUInstallerUI.Hint($"Prefab: {OutputPrefab}");
                GUInstallerUI.Hint($"Spec: {SpecPath}");
            }
        }

        private void DrawSourceToggle()
        {
            SubHeader("NGUỒN TỌA ĐỘ");
            EditorGUI.BeginChangeCheck();
            var source = (UIBuilderSource)GUILayout.Toolbar((int)Settings.source,
                new[] { "Dò sprite trên ảnh demo", "Đọc file PSD" }, GUILayout.Height(22f));
            if (EditorGUI.EndChangeCheck())
            {
                Settings.source = source;
                Settings.Save();
                ReloadJob();
            }

            GUInstallerUI.Hint(IsPsdMode
                ? "PSD có sẵn tọa độ từng layer, nội dung/màu/viền chữ, các tab (nhóm layer ẩn/hiện). Không cần OCR, không lệch pixel."
                : "Máy tìm vị trí art đã cắt trên ảnh demo (OpenCV) và đọc chữ (OCR).");
        }

        /// <summary>File PSD + tuỳ chọn xuất PNG cho layer không có art; sau khi đọc: danh sách trạng thái (tab) tìm thấy.</summary>
        private void DrawPsdInput()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            var path = EditorGUILayout.TextField(new GUIContent("File PSD", "PSD/PSB — nằm ngoài Assets cũng được (không cần import vào Unity)."),
                Settings.psdPath);
            if (EditorGUI.EndChangeCheck())
            {
                Settings.psdPath = path?.Trim();
                Settings.Save();
            }

            if (GUILayout.Button("Chọn…", EditorStyles.miniButton, GUILayout.Width(60f)))
            {
                var folder = HasPsd() ? Path.GetDirectoryName(UIBuilderPaths.ToAbsolute(Settings.psdPath)) : string.Empty;
                var picked = EditorUtility.OpenFilePanelWithFilters("Chọn file PSD", folder, new[] { "Photoshop", "psd,psb" });
                if (!string.IsNullOrEmpty(picked))
                {
                    Settings.psdPath = UIBuilderPaths.ToAssetPath(picked) ?? picked;
                    Settings.Save();
                    GUIUtility.ExitGUI();
                }
            }

            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(Settings.psdPath) && !HasPsd()) GUInstallerUI.Hint("⚠ Không thấy file.");

            EditorGUI.BeginChangeCheck();
            Settings.psdExportMissing = EditorGUILayout.ToggleLeft(
                new GUIContent("Xuất PNG cho layer không có art", "Pixel layer / smart object xuất đúng hình; shape có hiệu ứng (stroke, bóng) xuất thiếu hiệu ứng — có ghi chú."),
                Settings.psdExportMissing);
            if (Settings.psdExportMissing)
            {
                var export = (DefaultAsset)EditorGUILayout.ObjectField("Thư mục xuất", LoadAsset<DefaultAsset>(Settings.psdExportFolder),
                    typeof(DefaultAsset), false);
                var exportPath = export != null ? AssetDatabase.GetAssetPath(export) : null;
                if (exportPath != null && AssetDatabase.IsValidFolder(exportPath)) Settings.psdExportFolder = exportPath;
                GUInstallerUI.Hint($"PNG ghi vào {PsdExportFolder}/ (import thành Sprite).");
            }

            if (EditorGUI.EndChangeCheck()) Settings.Save();

            if (_locates.Count > 0)
                GUInstallerUI.Hint($"Trạng thái trong PSD: {string.Join(" · ", _locates.Select((l, i) => $"Tab {i + 1} = '{l.state}'"))}");
        }

        private static string PsdExportFolder => $"{Settings.psdExportFolder.TrimEnd('/')}/{Settings.jobName}";

        private static void SubHeader(string title, string hint = null)
        {
            EditorGUILayout.Space(6);
            GUILayout.Label(title, EditorStyles.miniBoldLabel);
            if (!string.IsNullOrEmpty(hint)) GUInstallerUI.Hint(hint);
        }

        /// <summary>
        /// Danh sách demo gọn một dòng mỗi ảnh: nút "Tab k" chọn ảnh xem trước ở cột phải, ô chọn ảnh, nút xoá.
        /// Ảnh 1 lưu ở <c>demoPath</c>, các ảnh sau ở <c>extraDemos</c>.
        /// </summary>
        private void DrawDemoList()
        {
            var slots = new List<string> { Settings.demoPath };
            slots.AddRange(Settings.extraDemos);
            if (string.IsNullOrEmpty(slots[0]) && slots.Count == 1) slots.Clear();
            var line = GUILayout.Height(EditorGUIUtility.singleLineHeight);

            for (var i = 0; i < slots.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                var selected = GUILayout.Toggle(i == _previewIndex, $"Tab {i + 1}", EditorStyles.miniButton, GUILayout.Width(RowLabelWidth));
                if (selected && i != _previewIndex) SelectPreview(i);

                EditorGUI.BeginChangeCheck();
                var demo = (Texture2D)EditorGUILayout.ObjectField(LoadAsset<Texture2D>(slots[i]), typeof(Texture2D), false, line);
                if (EditorGUI.EndChangeCheck() && demo != null) SetDemo(i, AssetDatabase.GetAssetPath(demo));

                if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(22f)))
                {
                    RemoveDemo(i);
                    GUIUtility.ExitGUI();
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(slots.Count == 0 ? "＋ Ảnh demo" : "＋ Thêm tab", EditorStyles.miniLabel, GUILayout.Width(RowLabelWidth + 4f));
            var added = (Texture2D)EditorGUILayout.ObjectField(null, typeof(Texture2D), false, line);
            if (added != null) SetDemo(slots.Count, AssetDatabase.GetAssetPath(added));
            GUILayout.Space(26f);
            EditorGUILayout.EndHorizontal();
        }

        private void SetDemo(int index, string path)
        {
            if (index == 0 || string.IsNullOrEmpty(Settings.demoPath)) Settings.demoPath = path;
            else if (index - 1 < Settings.extraDemos.Count) Settings.extraDemos[index - 1] = path;
            else Settings.extraDemos.Add(path);
            Settings.Save();
            ReloadJob();
        }

        private void RemoveDemo(int index)
        {
            if (index == 0)
            {
                Settings.demoPath = Settings.extraDemos.Count > 0 ? Settings.extraDemos[0] : null;
                if (Settings.extraDemos.Count > 0) Settings.extraDemos.RemoveAt(0);
            }
            else
            {
                Settings.extraDemos.RemoveAt(index - 1);
            }

            _previewIndex = 0;
            Settings.Save();
            ReloadJob();
        }

        private void SelectPreview(int index)
        {
            _previewIndex = index;
            _locate = index < _locates.Count ? _locates[index] : null;
            if (UIMockupOverlay.Enabled) UIMockupOverlay.Show(PreviewDemo);
        }

        /// <summary>Kích thước/tỉ lệ ảnh đang xem + tuỳ chọn hiển thị (ảnh nằm ở cột phải).</summary>
        private void DrawDemoDetails(Texture2D texture, bool previewShown)
        {
            var ratio = (float)texture.height / texture.width;
            var isReference = texture.width == 1080 && texture.height == 2160;
            var mismatch = !IsPsdMode && Demos.Select(DemoSize).Distinct().Count() > 1;
            var source = IsPsdMode && _locates.Count > 0 && ProjectPath(_locate?.demo) == PreviewDemo && !IsUserDemo(PreviewDemo)
                ? " · ảnh ghép từ PSD" : string.Empty;
            GUInstallerUI.Hint($"Tab {_previewIndex + 1}: {texture.width}×{texture.height} · tỉ lệ 1:{ratio:0.##}{source}"
                               + (isReference ? string.Empty : " · khác 1080×2160 mặc định")
                               + (mismatch ? " · ⚠ các demo khác kích thước nhau" : string.Empty));

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            Settings.showDemoPreview = GUILayout.Toggle(Settings.showDemoPreview, "Hiện ảnh bên phải", GUILayout.Width(130f));
            GUILayout.FlexibleSpace();
            if (GUInstallerUI.MiniButton("Mở ảnh gốc", true, 90f))
                EditorUtility.OpenWithDefaultApp(UIBuilderPaths.ToAbsolute(PreviewDemo));
            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck()) Settings.Save();

            if (Settings.showDemoPreview && !previewShown)
                GUInstallerUI.Hint("Cửa sổ hẹp — kéo rộng ra để hiện ảnh demo ở cột phải.");
        }

        private static Vector2Int DemoSize(string assetPath)
        {
            if (AssetImporter.GetAtPath(assetPath) is TextureImporter importer)
            {
                importer.GetSourceTextureWidthAndHeight(out var width, out var height);
                return new Vector2Int(width, height);
            }

            return Vector2Int.zero;
        }

        private void DrawArtFolders()
        {
            var folders = Settings.artFolders;
            var line = GUILayout.Height(EditorGUIUtility.singleLineHeight);
            for (var i = 0; i < folders.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                var folder = (DefaultAsset)EditorGUILayout.ObjectField(LoadAsset<DefaultAsset>(folders[i]), typeof(DefaultAsset), false, line);
                if (EditorGUI.EndChangeCheck() && folder != null && AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(folder)))
                {
                    folders[i] = AssetDatabase.GetAssetPath(folder);
                    Settings.Save();
                }

                if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(22f)))
                {
                    folders.RemoveAt(i);
                    Settings.Save();
                    GUIUtility.ExitGUI();
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("＋ Thêm", EditorStyles.miniLabel, GUILayout.Width(RowLabelWidth + 4f));
            var dropped = (DefaultAsset)EditorGUILayout.ObjectField(null, typeof(DefaultAsset), false, line);
            if (dropped != null) AddArtFolder(AssetDatabase.GetAssetPath(dropped));
            var demoFolder = HasDemo() ? Path.GetDirectoryName(Settings.demoPath)?.Replace('\\', '/') : null;
            if (GUInstallerUI.MiniButton("Thư mục chứa demo", demoFolder != null && !folders.Contains(demoFolder), 130f))
                AddArtFolder(demoFolder);
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            Settings.includeSubfolders = EditorGUILayout.ToggleLeft("Quét cả thư mục con", Settings.includeSubfolders);
            if (EditorGUI.EndChangeCheck()) Settings.Save();

            var count = ArtSpriteCount();
            if (count >= BroadArtWarning)
                EditorGUILayout.HelpBox($"{count} ảnh PNG — thư mục art rộng làm định vị chậm (vài phút lần đầu) và dễ khớp nhầm art "
                                        + "của màn khác. Nên chọn thư mục của màn này + thư mục dùng chung (_Shared).", MessageType.Warning);
            else if (count > 0)
                GUInstallerUI.Hint($"{count} ảnh PNG sẽ được dò.");
        }

        /// <summary>Số PNG sẽ được dò (bỏ file demo*) — đếm lại chỉ khi danh sách thư mục hoặc cờ thư mục con đổi.</summary>
        private int ArtSpriteCount()
        {
            var key = $"{Settings.includeSubfolders}|{string.Join("|", Settings.artFolders)}";
            if (key == _artCountKey) return _artCount;
            _artCountKey = key;
            var option = Settings.includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            _artCount = Settings.artFolders
                .Select(UIBuilderPaths.ToAbsolute)
                .Where(Directory.Exists)
                .SelectMany(folder => Directory.EnumerateFiles(folder, "*.png", option))
                .Count(file => !Path.GetFileName(file).TrimStart('_').StartsWith("demo", StringComparison.OrdinalIgnoreCase));
            return _artCount;
        }

        private static void AddArtFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path) || Settings.artFolders.Contains(path)) return;
            Settings.artFolders.Add(path);
            Settings.Save();
        }

        // ─── BƯỚC 3 — Định vị ───────────────────────────────────────────────

        private void DrawLocateStep()
        {
            var busy = _locator != null;
            var canRun = MissingForLocate() == null && !busy;
            var state = busy ? GUSetupState.Busy : _locate != null ? GUSetupState.Done : canRun ? GUSetupState.Missing : GUSetupState.Blocked;

            using (GUInstallerUI.BeginCard())
            {
                if (IsPsdMode)
                {
                    GUInstallerUI.CardHeader("BƯỚC 3", "Đọc PSD → tọa độ layer + art", state);
                    GUILayout.Label("Mỗi trạng thái (tab) một kết quả. Layer nối với art đã cắt (cùng tên / cùng pixel, cả art nằm trong layer gộp); "
                                    + "text layer thành chữ TMP; lớp dim đen thành imgDim; layer không có art xuất PNG.", GUInstallerUI.Desc);
                }
                else
                {
                    GUInstallerUI.CardHeader("BƯỚC 3", "Định vị sprite trên demo", state);
                    GUILayout.Label("Tìm vị trí chính xác từng sprite: đúng tỉ lệ, bị scale, kéo giãn 9-slice, lặp nhiều lần, bị che một phần. Kết quả cache theo hash file.",
                        GUInstallerUI.Desc);
                    DrawWorkersOption(busy);
                }

                EditorGUILayout.BeginHorizontal();
                var runLabel = IsPsdMode ? busy ? "Đang đọc PSD…" : "Đọc PSD" : busy ? "Đang định vị…" : "Chạy định vị";
                if (GUInstallerUI.PrimaryButton(runLabel, canRun, 26f)) StartLocate();
                if (busy && GUILayout.Button("Huỷ", GUILayout.Width(60f), GUILayout.Height(26f))) CancelLocate();
                EditorGUILayout.EndHorizontal();

                busy = _locator != null;
                var missing = MissingForLocate();
                if (!busy && missing != null) GUInstallerUI.Hint($"Cần: {missing}.");
                if (Demos.Count > 1) DrawLocateTabs();
                if (!string.IsNullOrEmpty(_locateMessage)) GUInstallerUI.Hint(_locateMessage);
                if (!string.IsNullOrEmpty(_locateError)) EditorGUILayout.HelpBox(_locateError, MessageType.Error);
                if (busy) DrawLocateProgress(_locator.Progress);
                if (busy || _locate != null) DrawPreviewLayers();
                if (!busy && _locate != null) DrawLocateResult();
                if (!busy && _lastProgress != null) DrawLocateLog(_lastProgress, "Nhật ký lần chạy vừa rồi");
            }
        }

        /// <summary>Số process song song (0 = tự động). Nhiều hơn ~12 chỉ tranh cache/băng thông bộ nhớ, không nhanh hơn.</summary>
        private void DrawWorkersOption(bool busy)
        {
            var logical = Environment.ProcessorCount;
            var auto = Mathf.Clamp(logical - 1, 1, AutoWorkerCap);
            using (new EditorGUI.DisabledScope(busy))
            {
                EditorGUI.BeginChangeCheck();
                var workers = EditorGUILayout.IntSlider(
                    new GUIContent("Process song song", $"0 = tự động ({auto} trên máy này). Chạy trên CPU; nhiều hơn ~{AutoWorkerCap} thường chậm hơn vì tranh bộ nhớ."),
                    Settings.locateWorkers, 0, logical);
                if (EditorGUI.EndChangeCheck())
                {
                    Settings.locateWorkers = workers;
                    Settings.Save();
                }
            }

            GUInstallerUI.Hint(Settings.locateWorkers == 0
                ? $"Tự động: {auto} process / {logical} luồng CPU."
                : $"{Settings.locateWorkers} process / {logical} luồng CPU.");
        }

        /// <summary>Lý do chưa chạy được định vị (hiện ngay dưới nút bị mờ); null nếu đủ điều kiện.</summary>
        private string MissingForLocate()
        {
            var missing = new List<string>();
            if (!UIBuilderPython.IsReady) missing.Add("cài môi trường Python ở Bước 1");
            else if (IsPsdMode && UIBuilderPython.IsOutdated) missing.Add("bấm Cập nhật ở Bước 1 (cài psd-tools)");
            if (IsPsdMode)
            {
                if (!HasPsd()) missing.Add("chọn file PSD ở Bước 2");
                if (Settings.artFolders.Count == 0 && !Settings.psdExportMissing) missing.Add("thêm thư mục art hoặc bật xuất PNG ở Bước 2");
                return missing.Count == 0 ? null : string.Join(", ", missing);
            }

            if (!HasDemo()) missing.Add("chọn ảnh demo ở Bước 2");
            if (Settings.artFolders.Count == 0) missing.Add("thêm thư mục art ở Bước 2");
            return missing.Count == 0 ? null : string.Join(", ", missing);
        }

        private void CancelLocate()
        {
            _locator.Cancel();
            _lastProgress = _locator.Progress;
            _locator = null;
            _locateQueue.Clear();
            _locateMessage = $"Đã huỷ định vị demo {_locatingIndex + 1}. Kết quả cũ (nếu có) giữ nguyên.";
        }

        /// <summary>Nhiều demo: trạng thái định vị từng tab (bấm để xem trên ảnh bên phải).</summary>
        private void DrawLocateTabs()
        {
            var demos = Demos;
            for (var i = 0; i < demos.Count; i++)
            {
                var result = i < _locates.Count ? _locates[i] : null;
                var running = _locator != null && _locatingIndex == i;
                var queued = _locator != null && _locateQueue.Contains(i);
                var state = running ? GUSetupState.Busy : result != null ? GUSetupState.Done : GUSetupState.Missing;
                var detail = running ? "đang định vị…" : queued ? "chờ tới lượt"
                    : result != null ? $"{result.sprites.Count(s => s.IsMatched)} sprite khớp · {result.texts.Count} dòng chữ"
                    : "chưa định vị";
                if (GUInstallerUI.StatusRow($"Tab {i + 1} · {Path.GetFileName(demos[i])}", state, detail,
                        i == _previewIndex ? "Đang xem" : "Xem", i != _previewIndex, 70f))
                    SelectPreview(i);
            }
        }

        /// <summary>Các giai đoạn (đã qua / đang chạy / chưa tới), thanh tiến độ, việc đang làm và nhật ký trực tiếp.</summary>
        private void DrawLocateProgress(LocateProgress progress)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            for (var i = 0; i < LocateProgress.Stages.Length; i++)
            {
                var color = i < progress.Stage ? GUInstallerUI.OkColor : i == progress.Stage ? GUInstallerUI.BusyColor : GUInstallerUI.MutedColor;
                var mark = i < progress.Stage ? "✔ " : i == progress.Stage ? "● " : string.Empty;
                GUInstallerUI.DrawBadge($"{mark}{LocateProgress.Stages[i]}", color, 90f);
                if (i < LocateProgress.Stages.Length - 1) GUILayout.Label("→", EditorStyles.miniLabel, GUILayout.Width(14f));
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (progress.Total > 0) GUInstallerUI.ProgressBar(LocateProgress.Stages[progress.Stage], progress.Done, progress.Total, 18f);
            GUInstallerUI.Hint(progress.Status);
            var snapshot = progress.Snapshot;
            GUInstallerUI.Hint($"Đến giờ: {snapshot.sprites.Count(s => s.IsMatched)} sprite khớp · {snapshot.dropped.Count} vị trí bị loại · {snapshot.texts.Count} dòng chữ");
            DrawLocateLog(progress, "Nhật ký");
        }

        /// <summary>Nhật ký sự kiện của script định vị: sprite khớp thế nào, cái gì bị loại vì sao, chữ đọc được.</summary>
        private void DrawLocateLog(LocateProgress progress, string title)
        {
            _showLocateLog = EditorGUILayout.Foldout(_showLocateLog, $"{title} ({progress.Events.Count})", true);
            if (!_showLocateLog || progress.Events.Count == 0) return;

            _logScroll = EditorGUILayout.BeginScrollView(_logScroll, EditorStyles.helpBox, GUILayout.Height(160f));
            foreach (var line in progress.Events) GUILayout.Label(line, EditorStyles.miniLabel);
            EditorGUILayout.EndScrollView();

            // Đang chạy: luôn cuộn xuống dòng mới nhất.
            if (_locator != null && Event.current.type == EventType.Repaint) _logScroll.y = float.MaxValue;
            if (GUInstallerUI.MiniButton("Copy nhật ký", true, 100f))
            {
                EditorGUIUtility.systemCopyBuffer = string.Join("\n", progress.Events);
                ShowNotification(new GUIContent("Đã copy nhật ký"));
            }
        }

        /// <summary>Bật/tắt lớp khung trên ảnh demo + chú thích màu.</summary>
        private void DrawPreviewLayers()
        {
            EditorGUILayout.Space(4);
            using (new EditorGUI.DisabledScope(!Settings.showDemoPreview))
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Hiện trên ảnh:", EditorStyles.miniLabel, GUILayout.Width(RowLabelWidth + 4f));
                var layers = Settings.previewLayers;
                layers = LayerToggle(layers, PreviewLayers.Sprites, "Sprite");
                layers = LayerToggle(layers, PreviewLayers.Texts, "Chữ");
                layers = LayerToggle(layers, PreviewLayers.Dropped, "Bị loại");
                layers = LayerToggle(layers, PreviewLayers.UIRegion, "Vùng UI");
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                if (EditorGUI.EndChangeCheck())
                {
                    Settings.previewLayers = layers;
                    Settings.Save();
                }
            }

            GUInstallerUI.Hint("Xanh lá = khớp · cam = 9-slice · xanh dương = chữ · đỏ đứt = bị loại · vàng = đang chọn / vừa dò · "
                               + "phần tối = dưới lớp phủ (UI màn phía sau, bị bỏ). Rê chuột lên khung để xem máy khớp thế nào, bấm để chọn.");
        }

        private static PreviewLayers LayerToggle(PreviewLayers layers, PreviewLayers layer, string label)
        {
            var on = GUILayout.Toggle((layers & layer) != 0, label, EditorStyles.miniButton, GUILayout.Width(64f));
            return on ? layers | layer : layers & ~layer;
        }

        /// <summary>Định vị lần lượt từng demo (mỗi demo một process song song nhiều nhân — chạy tuần tự để không tranh CPU).</summary>
        private void StartLocate()
        {
            _locateQueue.Clear();
            _locateError = null;
            _crossCheckPass = false;
            if (IsPsdMode)
            {
                // một process đọc mọi trạng thái; demo người dùng đưa theo thứ tự tab chỉ để so sánh
                _locatingIndex = 0;
                _locateMessage = "Đang đọc PSD…";
                _locator = UIBuilderLocator.StartPsd(Settings.psdPath, Settings.artFolders, Settings.includeSubfolders, Settings.jobName,
                    UserDemoSlots(), Settings.psdExportMissing ? PsdExportFolder : null);
                StartTicking();
                return;
            }

            for (var i = 0; i < Demos.Count; i++) _locateQueue.Enqueue(i);
            StartNextLocate();
            StartTicking();
        }

        private void StartNextLocate()
        {
            _locatingIndex = _locateQueue.Dequeue();
            var demos = Demos;
            _locateMessage = demos.Count <= 1 ? null
                : _crossCheckPass ? $"Đối chiếu phần chung giữa các tab — demo {_locatingIndex + 1}/{demos.Count}…"
                : $"Đang định vị demo {_locatingIndex + 1}/{demos.Count}…";
            SelectPreview(_locatingIndex); // xem trực tiếp demo đang được dò
            var hints = Enumerable.Range(0, demos.Count).Where(k => k != _locatingIndex)
                .Select(k => UIBuilderPaths.LocatePath(Settings.jobName, k))
                .Where(p => File.Exists(UIBuilderPaths.ToAbsolute(p)));
            _locator = UIBuilderLocator.Start(demos[_locatingIndex], Settings.artFolders, Settings.includeSubfolders,
                UIBuilderPaths.LocatePath(Settings.jobName, _locatingIndex), hints);
        }

        private void DrawLocateResult()
        {
            var matched = _locate.sprites.Where(s => s.IsMatched).ToList();
            EditorGUILayout.Space(4);
            GUInstallerUI.Hint($"Dò {_locate.sprites.Count} sprite ({_locate.cachedCount} từ cache) → {matched.Count} khớp, "
                               + $"{matched.Sum(s => s.matches.Count)} vị trí · lọc bỏ {_locate.dropped.Count} vị trí · "
                               + $"{_locate.texts.Count} dòng chữ ({OcrLabel(_locate.ocr)}) · {_locate.elapsedMs / 1000f:0.#} s"
                               + (_locate.dimAlpha > 0f ? $" · lớp dim {_locate.dimAlpha:P0}{(_locate.dimEstimated ? " (ước lượng)" : string.Empty)}" : string.Empty)
                               + (_locate.uiRegions.Count > 0 ? $" · {_locate.uiRegions.Count} vùng UI chính, phần dưới lớp phủ bị bỏ" : string.Empty));

            foreach (var sprite in matched)
            {
                var assetPath = UIBuilderPaths.ToAssetPath(sprite.sprite);
                var asset = LoadAsset<Sprite>(assetPath);
                var first = sprite.matches[0];
                var detail = $"{sprite.matches.Count}× · {first.w}×{first.h} @({first.x},{first.y}) · {first.MethodLabel}"
                             + (sprite.lowTexture ? " · một màu" : string.Empty) + $" · {first.ScoreLabel}";

                EditorGUILayout.BeginHorizontal();
                var icon = asset != null ? AssetPreview.GetAssetPreview(asset) ?? AssetPreview.GetMiniThumbnail(asset) : null;
                GUILayout.Label(icon, GUILayout.Width(28f), GUILayout.Height(28f));
                EditorGUILayout.BeginVertical();
                GUILayout.Label(sprite.name == _highlightSprite ? $"▶ {sprite.name}" : sprite.name, EditorStyles.boldLabel);
                GUInstallerUI.Hint(detail);
                EditorGUILayout.EndVertical();
                GUILayout.FlexibleSpace();

                var needsBorder = sprite.matches.Any(m => m.sliced) && (asset == null || asset.border == Vector4.zero);
                if (needsBorder)
                    GUInstallerUI.DrawBadge("CẦN BORDER", GUInstallerUI.BlockedColor, 90f);
                if (GUInstallerUI.MiniButton("Chọn", assetPath != null, 50f))
                {
                    _highlightSprite = sprite.name;
                    GUInstallerUI.PingPath(assetPath);
                }
                EditorGUILayout.EndHorizontal();

                if (needsBorder)
                    GUInstallerUI.Hint($"   Mở Sprite Editor, set border — gợi ý {sprite.suggestedBorder}");
            }

            _showTexts = EditorGUILayout.Foldout(_showTexts, $"Chữ tìm được ({_locate.texts.Count})", true);
            if (_showTexts)
            {
                foreach (var text in _locate.texts)
                {
                    var content = string.IsNullOrEmpty(text.text) ? "(chưa đọc nội dung)" : $"“{text.text}” {text.confidence:P0}";
                    var warn = !string.IsNullOrEmpty(text.text) && text.confidence < 0.8f ? "⚠ " : string.Empty;
                    var outline = string.IsNullOrEmpty(text.outlineColor) ? string.Empty : $" · viền {text.outlineColor}";
                    GUInstallerUI.Hint($"   {warn}{content} · {text.color}{outline} · {text.w}×{text.h} @({text.x},{text.y})");
                }
            }

            _showDropped = EditorGUILayout.Foldout(_showDropped, $"Bị lọc bỏ ({_locate.dropped.Count})", true);
            if (_showDropped)
            {
                foreach (var drop in _locate.dropped)
                    GUInstallerUI.Hint($"   {drop.name} @({drop.x},{drop.y}) {drop.w}×{drop.h} — {drop.ReasonLabel}");
            }

            var unmatched = _locate.sprites.Where(s => !s.IsMatched).ToList();
            _showUnmatched = EditorGUILayout.Foldout(_showUnmatched, $"Không khớp ({unmatched.Count})", true);
            if (!_showUnmatched) return;
            foreach (var sprite in unmatched)
                GUInstallerUI.Hint($"   {sprite.name} — {sprite.ReasonLabel}");
        }

        private static string OcrLabel(string ocr)
        {
            switch (ocr)
            {
                case "ok": return "OCR đã đọc";
                case "unavailable": return "chưa cài OCR";
                case "skipped": return "bỏ qua OCR";
                case "none": return "không thấy chữ";
                case "psd": return "từ text layer PSD";
                default: return "OCR ?";
            }
        }

        // ─── BƯỚC 4 — Spec ──────────────────────────────────────────────────

        private void DrawSpecStep()
        {
            var state = _spec != null ? GUSetupState.Done : _locate != null ? GUSetupState.Missing : GUSetupState.Blocked;
            using (GUInstallerUI.BeginCard())
            {
                GUInstallerUI.CardHeader("BƯỚC 4", "Spec (JSON)", state);
                GUILayout.Label("Spec nháp có đủ sprite đã định vị. Text, nhóm, anchor, glow, art thiếu: nhờ Claude hoàn thiện (skill /gu-ui) hoặc sửa tay.",
                    GUInstallerUI.Desc);

                EditorGUI.BeginChangeCheck();
                var font = (TMP_FontAsset)EditorGUILayout.ObjectField(
                    new GUIContent("Font cho text", "Font TMP gán cho các dòng chữ tìm được trên demo; trống = font mặc định TMP."),
                    LoadAsset<TMP_FontAsset>(Settings.textFont), typeof(TMP_FontAsset), false);
                var outline = (Material)EditorGUILayout.ObjectField(
                    new GUIContent("Material viền chữ", "Gán cho chữ có viền trên demo. Trống = tự chọn preset outline (cùng atlas) trong thư mục font có độ dày gần nhất."),
                    LoadAsset<Material>(Settings.textOutlineMaterial), typeof(Material), false);
                if (EditorGUI.EndChangeCheck())
                {
                    Settings.textFont = font != null ? AssetDatabase.GetAssetPath(font) : null;
                    Settings.textOutlineMaterial = outline != null ? AssetDatabase.GetAssetPath(outline) : null;
                    Settings.Save();
                }

                DrawSkipRegions();

                EditorGUILayout.BeginHorizontal();
                if (GUInstallerUI.MiniButton(_spec != null ? "Sinh lại spec nháp" : "Tạo spec nháp", _locate != null, 140f))
                    GenerateSpec();
                if (GUInstallerUI.MiniButton("Mở spec", _spec != null, 80f))
                    EditorUtility.OpenWithDefaultApp(UIBuilderPaths.ToAbsolute(SpecPath));
                if (GUInstallerUI.MiniButton("Tải lại", true, 70f)) ReloadJob();
                if (GUInstallerUI.MiniButton("Copy prompt cho Claude", PreviewDemo != null, 170f))
                {
                    var psdCommand = IsPsdMode && HasPsd()
                        ? UIBuilderLocator.BuildPsdCommandLine(Settings.psdPath, Settings.artFolders, Settings.includeSubfolders,
                            Settings.jobName, UserDemoSlots(), Settings.psdExportMissing ? PsdExportFolder : null)
                        : null;
                    EditorGUIUtility.systemCopyBuffer = UIBuilderAiToolkit.BuildPrompt(
                        Settings.jobName, Demos, Settings.artFolders, Settings.includeSubfolders, OutputPrefab, Settings.skipRegions, psdCommand);
                    ShowNotification(new GUIContent("Đã copy prompt"));
                }

                EditorGUILayout.EndHorizontal();

                var skillState = !UIBuilderAiToolkit.IsInstalled ? GUSetupState.Missing
                    : UIBuilderAiToolkit.IsOutdated ? GUSetupState.Blocked : GUSetupState.Done;
                var skillDetail = skillState == GUSetupState.Blocked ? "có bản mới" : ".claude/skills/gameup-ui-builder";
                if (GUInstallerUI.StatusRow("Skill Claude Code (/gu-ui)", skillState, skillDetail,
                        skillState == GUSetupState.Done ? "Cài lại" : "Cài skill"))
                {
                    UIBuilderAiToolkit.Install();
                    ShowNotification(new GUIContent("Đã cài skill /gu-ui"));
                }

                if (!string.IsNullOrEmpty(_specError)) EditorGUILayout.HelpBox(_specError, MessageType.Error);
                if (_spec == null) return;
                GUInstallerUI.Hint($"{_spec.nodes.Count} node · {SpecPath}");
                foreach (var note in _spec.notes) GUInstallerUI.Hint($"• {note}");
            }
        }

        /// <summary>
        /// Phần đã có sẵn (thanh điều hướng, thanh trên cùng…): kéo khung trên ảnh demo; mỗi vùng có tên và prefab thay thế
        /// (tuỳ chọn). Áp dụng khi sinh spec — node nằm phần lớn trong vùng bị bỏ, vùng có prefab thành node instance.
        /// </summary>
        private void DrawSkipRegions()
        {
            SubHeader("PHẦN ĐÃ CÓ SẴN — KHÔNG DỰNG",
                "Kéo khung trên ảnh demo quanh phần đã có prefab (thanh điều hướng, thanh trên…). Chọn prefab để đặt đúng chỗ, bỏ trống = không đặt gì.");
            var regions = Settings.skipRegions;
            var line = GUILayout.Height(EditorGUIUtility.singleLineHeight);
            for (var i = 0; i < regions.Count; i++)
            {
                var region = regions[i];
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                region.name = EditorGUILayout.TextField(region.name, GUILayout.Width(RowLabelWidth + 30f));
                var prefab = (GameObject)EditorGUILayout.ObjectField(LoadAsset<GameObject>(region.prefab), typeof(GameObject), false, line);
                if (EditorGUI.EndChangeCheck())
                {
                    region.prefab = prefab != null ? AssetDatabase.GetAssetPath(prefab) : null;
                    Settings.Save();
                }

                GUILayout.Label($"{region.w}×{region.h} @({region.x},{region.y})", EditorStyles.miniLabel, GUILayout.Width(130f));
                if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(22f)))
                {
                    regions.RemoveAt(i);
                    Settings.Save();
                    GUIUtility.ExitGUI();
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            var canDraw = PreviewDemo != null && Settings.showDemoPreview;
            var label = _drawingSkip ? "Đang chọn — kéo trên ảnh (Esc huỷ)" : "＋ Kéo khung trên ảnh demo";
            var drawing = GUILayout.Toggle(_drawingSkip, label, EditorStyles.miniButton, GUILayout.Width(220f));
            if (drawing != _drawingSkip && (canDraw || !drawing))
            {
                _drawingSkip = drawing;
                _dragStart = null;
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            if (!canDraw) GUInstallerUI.Hint("Bật 'Hiện ảnh bên phải' ở Bước 2 để kéo khung.");
            if (regions.Count > 0 && _spec != null)
                GUInstallerUI.Hint("Đổi vùng xong bấm 'Sinh lại spec nháp' để áp dụng.");
        }

        /// <summary>Kéo chuột trên ảnh demo tạo vùng đã có sẵn; cạnh cách mép ảnh ≤ 16 px thì dính vào mép.</summary>
        private void HandleSkipDrag(Rect image, float scale, Texture2D texture)
        {
            var e = Event.current;
            var id = GUIUtility.GetControlID(FocusType.Passive);
            EditorGUIUtility.AddCursorRect(image, MouseCursor.ArrowPlus);
            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown when e.button == 0 && image.Contains(e.mousePosition):
                    _dragStart = _dragEnd = e.mousePosition;
                    GUIUtility.hotControl = id;
                    e.Use();
                    break;
                case EventType.MouseDrag when GUIUtility.hotControl == id:
                    _dragEnd = e.mousePosition;
                    Repaint();
                    e.Use();
                    break;
                case EventType.MouseUp when GUIUtility.hotControl == id:
                    GUIUtility.hotControl = 0;
                    AddSkipRegion(DragRect(), image, scale, texture);
                    _dragStart = null;
                    _drawingSkip = false;
                    e.Use();
                    break;
                case EventType.KeyDown when e.keyCode == KeyCode.Escape:
                    _dragStart = null;
                    _drawingSkip = false;
                    e.Use();
                    Repaint();
                    break;
            }
        }

        private Rect DragRect()
        {
            var a = _dragStart ?? Vector2.zero;
            return Rect.MinMaxRect(Mathf.Min(a.x, _dragEnd.x), Mathf.Min(a.y, _dragEnd.y), Mathf.Max(a.x, _dragEnd.x), Mathf.Max(a.y, _dragEnd.y));
        }

        private static void AddSkipRegion(Rect screen, Rect image, float scale, Texture2D texture)
        {
            var x0 = ToDemoPixel(screen.xMin, image.x, scale, texture.width);
            var y0 = ToDemoPixel(screen.yMin, image.y, scale, texture.height);
            var x1 = ToDemoPixel(screen.xMax, image.x, scale, texture.width);
            var y1 = ToDemoPixel(screen.yMax, image.y, scale, texture.height);
            if (x1 - x0 < 8 || y1 - y0 < 8) return; // bấm nhầm, không phải kéo
            var name = y1 == texture.height ? "NavBar" : y0 == 0 ? "TopBar" : $"Vung{Settings.skipRegions.Count + 1}";
            Settings.skipRegions.Add(new UISkipRegion { name = name, x = x0, y = y0, w = x1 - x0, h = y1 - y0 });
            Settings.Save();
        }

        /// <summary>Tọa độ màn hình → pixel demo, kẹp trong ảnh; cách mép ≤ <see cref="SkipSnap"/> px thì dính vào mép.</summary>
        private static int ToDemoPixel(float screen, float origin, float scale, int size)
        {
            var value = Mathf.Clamp(Mathf.RoundToInt((screen - origin) / scale), 0, size);
            return value <= SkipSnap ? 0 : value >= size - SkipSnap ? size : value;
        }

        private void GenerateSpec()
        {
            var path = UIBuilderPaths.ToAbsolute(SpecPath);
            if (File.Exists(path) && !EditorUtility.DisplayDialog("Ghi đè spec?",
                    $"{SpecPath} đã có (có thể đã được Claude/bạn chỉnh). Sinh lại sẽ ghi đè toàn bộ.", "Ghi đè", "Huỷ"))
                return;

            var demos = Demos;
            if (_locates.Count < demos.Count || _locates.Any(l => l == null))
            {
                EditorUtility.DisplayDialog("Thiếu kết quả định vị", "Chạy định vị cho đủ mọi demo trước khi tạo spec.", "OK");
                return;
            }

            UISpecFile.Save(UISpecGenerator.Generate(_locates, Settings.jobName, demos, OutputPrefab, Settings.textFont,
                Settings.textOutlineMaterial, Settings.skipRegions), SpecPath);
            ReloadJob();
        }

        // ─── BƯỚC 5 — Dựng prefab ───────────────────────────────────────────

        private void DrawBuildStep()
        {
            var state = _report == null ? (_spec != null ? GUSetupState.Missing : GUSetupState.Blocked)
                : _report.Success ? GUSetupState.Done : GUSetupState.Missing;
            using (GUInstallerUI.BeginCard())
            {
                GUInstallerUI.CardHeader("BƯỚC 5", "Dựng / cập nhật prefab", state);
                GUILayout.Label("Prefab đã có thì chỉ cập nhật node theo tên — object/component thêm tay được giữ nguyên. Dựng xong tự render ảnh so sánh với demo.", GUInstallerUI.Desc);

                EditorGUILayout.BeginHorizontal();
                if (GUInstallerUI.PrimaryButton("Dựng prefab từ spec", _spec != null, 26f)) BuildPrefab();
                var prefabPath = _spec?.output;
                var prefabExists = prefabPath != null && AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null;
                if (GUILayout.Button("Mở prefab", GUILayout.Width(90f), GUILayout.Height(26f)) && prefabExists)
                    AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
                EditorGUILayout.EndHorizontal();

                if (_report == null) return;
                EditorGUILayout.HelpBox(_report.Summary, _report.Success ? MessageType.Info : MessageType.Error);
                foreach (var warning in _report.Warnings) GUInstallerUI.Hint($"⚠ {warning}");
                if (_comparePath != null && GUInstallerUI.MiniButton("Mở ảnh so sánh (demo | prefab | chồng 50%)", true, 280f))
                    EditorUtility.OpenWithDefaultApp(UIBuilderPaths.ToAbsolute(_comparePath));
            }
        }

        private void BuildPrefab()
        {
            ReloadJob();
            if (_spec == null) return;
            _report = UISpecBuilder.Build(_spec);
            _comparePath = _report.Success ? UIPrefabRenderer.RenderCompare(_spec, UIBuilderPaths.JobFolder(Settings.jobName)).FirstOrDefault() : null;
            if (_report.Success) GULogger.Log(LogTag, _report.Summary);
            else GULogger.Error(LogTag, _report.Summary);
        }

        // ─── Đối chiếu ──────────────────────────────────────────────────────

        private void DrawCompareStep()
        {
            using (GUInstallerUI.BeginCard())
            {
                GUInstallerUI.CardHeader(null, "Đối chiếu với demo", UIMockupOverlay.Enabled ? GUSetupState.Done : GUSetupState.Optional);
                GUILayout.Label("Phủ ảnh demo lên Scene View, khớp rect của prefab đang mở (Prefab Mode) hoặc root UI đang chọn. Không sửa prefab.",
                    GUInstallerUI.Desc);

                EditorGUILayout.BeginHorizontal();
                var label = UIMockupOverlay.Enabled ? "Tắt overlay" : "Bật overlay";
                if (GUInstallerUI.MiniButton(label, PreviewDemo != null, 110f))
                {
                    if (UIMockupOverlay.Enabled) UIMockupOverlay.Hide();
                    else UIMockupOverlay.Show(PreviewDemo);
                }

                EditorGUI.BeginChangeCheck();
                Settings.overlayOpacity = EditorGUILayout.Slider(Settings.overlayOpacity, 0f, 1f);
                if (EditorGUI.EndChangeCheck())
                {
                    UIMockupOverlay.Opacity = Settings.overlayOpacity;
                    Settings.Save();
                }

                EditorGUILayout.EndHorizontal();

                if (UIMockupOverlay.Enabled && UIMockupOverlay.FindTarget() == null)
                    GUInstallerUI.Hint("Mở prefab UI (Prefab Mode) hoặc chọn một object UI để hiện overlay.");
                GUInstallerUI.Hint("Đặt Game View đúng tỉ lệ demo (vd 1080×2160) để so chính xác.");
            }
        }

        // ─── Nền ────────────────────────────────────────────────────────────

        private void StartTicking()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private void Tick()
        {
            var running = false;

            if (_pythonSetup != null && _pythonSetup.IsRunning)
                running |= !_pythonSetup.Poll();

            if (_locator != null)
            {
                if (_locator.Poll())
                {
                    var result = _locator.Result;
                    if (result != null && result.IsPsd)
                    {
                        FinishPsd(result);
                        EditorApplication.update -= Tick;
                        Repaint();
                        return;
                    }

                    while (_locates.Count <= _locatingIndex) _locates.Add(null);
                    _locates[_locatingIndex] = result;
                    _locate = _locates[Mathf.Clamp(_previewIndex, 0, _locates.Count - 1)];
                    var prefix = Demos.Count > 1 ? $"Demo {_locatingIndex + 1}: " : string.Empty;
                    _locateMessage = result != null
                        ? $"{prefix}{result.sprites.Count(s => s.IsMatched)}/{result.sprites.Count} sprite khớp, {result.texts.Count} dòng chữ · {result.elapsedMs} ms"
                        : null;
                    if (result == null) _locateError = $"{prefix}Định vị thất bại: {_locator.Error}\nChi tiết đầy đủ trong Console.";
                    if (result == null) GULogger.Error(LogTag, $"Định vị thất bại:\n{_locator.Output}");
                    _lastProgress = _locator.Progress;
                    _locator = null;
                    // Nhiều tab: lượt 2 cho các demo chạy trước — lúc đó chưa có kết quả các tab sau để đối chiếu phần
                    // chung (nhanh: sprite và OCR đã cache, chỉ còn kiểm tra tại chỗ + lọc).
                    if (result != null && _locateQueue.Count == 0 && !_crossCheckPass && Demos.Count > 1)
                    {
                        _crossCheckPass = true;
                        for (var i = 0; i < Demos.Count - 1; i++) _locateQueue.Enqueue(i);
                    }

                    if (result != null && _locateQueue.Count > 0)
                    {
                        StartNextLocate();
                        running = true;
                    }
                    else
                    {
                        _locateQueue.Clear();
                        _jobStamp = JobStamp();
                    }
                }
                else
                {
                    running = true;
                }
            }

            if (!running) EditorApplication.update -= Tick;
            // EditorApplication.update chạy hàng trăm lần/giây — vẽ lại tối đa ~10 lần/giây là đủ mượt cho tiến trình.
            var now = EditorApplication.timeSinceStartup;
            if (!running || now - _lastRepaint >= RepaintInterval)
            {
                _lastRepaint = now;
                Repaint();
            }
        }

        /// <summary>Đọc PSD xong: PNG xuất ra → import thành Sprite (để spec gán được), rồi nạp kết quả mọi trạng thái.</summary>
        private void FinishPsd(LocateResult result)
        {
            _lastProgress = _locator.Progress;
            _locator = null;
            ImportExportedSprites(result.exported);
            ReloadJob();
            _locateMessage = $"Đọc PSD xong: {result.states.Count} trạng thái, {result.sprites.Count(s => s.IsMatched)} art ở tab 1, "
                             + $"{result.exported.Count} PNG xuất từ layer · {result.elapsedMs / 1000f:0.#} s";
        }

        /// <summary>PNG xuất từ PSD → texture type Sprite (không mipmap). Chỉ đổi file vừa xuất, không đụng art có sẵn.</summary>
        private static void ImportExportedSprites(IEnumerable<string> files)
        {
            AssetDatabase.Refresh();
            foreach (var file in files)
            {
                var assetPath = UIBuilderPaths.ToAssetPath(file);
                if (assetPath == null || !(AssetImporter.GetAtPath(assetPath) is TextureImporter importer)) continue;
                if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
        }

        /// <summary>Kết quả định vị đã lưu: chế độ PSD = locate.json, locate_2.json… liền nhau do lần đọc PSD ghi.</summary>
        private List<LocateResult> LoadLocates()
        {
            if (!IsPsdMode)
                return Demos.Select((_, i) => UIBuilderLocator.Load(UIBuilderPaths.LocatePath(Settings.jobName, i))).ToList();
            var results = new List<LocateResult>();
            for (var i = 0; ; i++)
            {
                var result = UIBuilderLocator.Load(UIBuilderPaths.LocatePath(Settings.jobName, i));
                if (result == null || !result.IsPsd) return results;
                results.Add(result);
            }
        }

        /// <summary>Ô demo người dùng chọn, theo thứ tự tab (ô trống giữ chỗ bằng chuỗi rỗng).</summary>
        private static List<string> UserDemoSlots()
        {
            var slots = new List<string> { Settings.demoPath ?? string.Empty };
            slots.AddRange(Settings.extraDemos.Select(d => d ?? string.Empty));
            return slots;
        }

        private static bool IsUserDemo(string path)
        {
            return !string.IsNullOrEmpty(path) && UserDemoSlots().Any(d => !string.IsNullOrEmpty(d) && ProjectPath(d) == path);
        }

        /// <summary>Đường dẫn tuyệt đối → asset path (trong Assets) hoặc tương đối gốc project; giữ nguyên nếu nằm ngoài.</summary>
        private static string ProjectPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            var asset = UIBuilderPaths.ToAssetPath(path);
            if (asset != null) return asset;
            var full = Path.GetFullPath(UIBuilderPaths.ToAbsolute(path)).Replace('\\', '/');
            var root = Path.GetFullPath(UIBuilderPaths.ProjectRoot).Replace('\\', '/') + "/";
            return full.StartsWith(root, StringComparison.Ordinal) ? full.Substring(root.Length) : full;
        }

        private void ReloadJob()
        {
            _locates = LoadLocates();
            _previewIndex = Mathf.Clamp(_previewIndex, 0, Mathf.Max(0, _locates.Count - 1));
            _locate = _locates.Count > 0 ? _locates[_previewIndex] : null;
            _spec = File.Exists(UIBuilderPaths.ToAbsolute(SpecPath)) ? UISpecFile.Load(SpecPath, out _specError) : null;
            if (_spec == null && !File.Exists(UIBuilderPaths.ToAbsolute(SpecPath))) _specError = null;
            _locateMessage = _locate != null ? $"Kết quả trước: {_locate.sprites.Count(s => s.IsMatched)}/{_locate.sprites.Count} sprite khớp." : null;
            _report = null;
            _comparePath = null;
            _jobStamp = JobStamp();
        }

        private DateTime JobStamp()
        {
            var stamp = File.GetLastWriteTimeUtc(UIBuilderPaths.ToAbsolute(SpecPath));
            for (var i = 0; i < Demos.Count; i++)
            {
                var locate = File.GetLastWriteTimeUtc(UIBuilderPaths.ToAbsolute(UIBuilderPaths.LocatePath(Settings.jobName, i)));
                if (locate > stamp) stamp = locate;
            }

            return stamp;
        }

        private bool HasDemo() => !string.IsNullOrEmpty(Settings.demoPath) && File.Exists(UIBuilderPaths.ToAbsolute(Settings.demoPath));

        private static bool HasPsd() => !string.IsNullOrEmpty(Settings.psdPath) && File.Exists(UIBuilderPaths.ToAbsolute(Settings.psdPath));

        private T LoadAsset<T>(string assetPath) where T : Object
        {
            if (string.IsNullOrEmpty(assetPath)) return null;
            var key = $"{typeof(T).Name}:{assetPath}";
            if (!_assetCache.TryGetValue(key, out var asset) || asset == null)
            {
                asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
                _assetCache[key] = asset;
            }

            return asset as T;
        }

        private static string SanitizeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "NewUI";
            var invalid = Path.GetInvalidFileNameChars();
            return new string(value.Trim().Where(c => !invalid.Contains(c) && c != ' ').ToArray());
        }
    }
}
