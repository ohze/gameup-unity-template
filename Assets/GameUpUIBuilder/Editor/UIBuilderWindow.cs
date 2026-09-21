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
    /// GameUp → UI → UI Builder: ảnh demo + art → định vị sprite → spec → prefab → đối chiếu.
    /// Mỗi bước là một card có trạng thái và hướng dẫn; bước chạy lâu (cài Python, định vị) chạy nền, không chặn Editor.
    /// </summary>
    public sealed class UIBuilderWindow : EditorWindow
    {
        private const string MenuPath = "GameUp/UI/UI Builder (Demo → Prefab)";
        private const string LogTag = "UIBuilder";
        private const float MinLeftWidth = 440f;
        private const float MinPreviewWidth = 160f;

        private UIBuilderPython _pythonSetup;
        private UIBuilderLocator _locator;
        private LocateResult _locate;
        private string _locateMessage;
        private UISpec _spec;
        private string _specError;
        private UIBuildReport _report;
        private string _comparePath;
        private string _systemPython;
        private Vector2 _scroll;
        private bool _showUnmatched;
        private bool _showPythonLog;
        private string _highlightSprite;
        private DateTime _jobStamp;
        private readonly Dictionary<string, Object> _assetCache = new Dictionary<string, Object>();

        private static UIBuilderSettings Settings => UIBuilderSettings.instance;

        private string SpecPath => UIBuilderPaths.SpecPath(Settings.jobName);

        private string LocatePath => UIBuilderPaths.LocatePath(Settings.jobName);

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
            // locate.json / spec.json có thể bị Claude hoặc terminal ghi lại → tự tải lại khi file đổi.
            if (Event.current.type == EventType.Layout && JobStamp() != _jobStamp) ReloadJob();
            var texture = HasDemo() ? UIDemoTexture.Get(Settings.demoPath) : null;
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
            var hovered = UIDemoPreviewDrawer.Draw(area, texture, _locate, Settings.showMatchRects, _highlightSprite);
            if (hovered != null && Event.current.type == EventType.MouseDown)
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
                GUInstallerUI.CardHeader("BƯỚC 1", "Môi trường Python (OpenCV + OCR)", state);
                GUILayout.Label("Định vị sprite bằng OpenCV, đọc chữ bằng RapidOCR — chạy trên máy, không cần mạng. Cài một lần cho mọi project trên máy.",
                    GUInstallerUI.Desc);

                GUInstallerUI.StatusRow("Python hệ thống", _systemPython != null ? GUSetupState.Done : GUSetupState.Missing,
                    _systemPython ?? "không tìm thấy — cài Python 3.9+");
                GUInstallerUI.StatusRow("Venv", ready && !outdated ? GUSetupState.Done : GUSetupState.Missing,
                    outdated ? "bản cũ — bấm Cập nhật để có OCR đọc chữ" : UIBuilderPaths.VenvFolder);

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
            var hasInput = HasDemo() && Settings.artFolders.Count > 0;
            using (GUInstallerUI.BeginCard())
            {
                GUInstallerUI.CardHeader("BƯỚC 2", "Ảnh demo và thư mục art", hasInput ? GUSetupState.Done : GUSetupState.Missing);
                GUILayout.Label("Art cắt đúng tỉ lệ với demo (mặc định 1080×2160). Thêm cả thư mục art riêng của màn và thư mục dùng chung (_Shared).",
                    GUInstallerUI.Desc);

                EditorGUI.BeginChangeCheck();
                var jobName = EditorGUILayout.TextField(new GUIContent("Tên UI", "Tên prefab và thư mục UIBuilder/<Tên>/"), Settings.jobName);
                var demo = (Texture2D)EditorGUILayout.ObjectField("Ảnh demo", LoadAsset<Texture2D>(Settings.demoPath), typeof(Texture2D), false);
                if (EditorGUI.EndChangeCheck())
                {
                    Settings.jobName = SanitizeName(jobName);
                    Settings.demoPath = demo != null ? AssetDatabase.GetAssetPath(demo) : null;
                    Settings.Save();
                    ReloadJob();
                }

                if (texture != null) DrawDemoDetails(texture, previewShown);
                DrawArtFolders();

                EditorGUI.BeginChangeCheck();
                Settings.includeSubfolders = EditorGUILayout.Toggle("Quét thư mục con", Settings.includeSubfolders);
                var output = (DefaultAsset)EditorGUILayout.ObjectField("Thư mục prefab", LoadAsset<DefaultAsset>(Settings.outputFolder), typeof(DefaultAsset), false);
                if (EditorGUI.EndChangeCheck())
                {
                    var path = output != null ? AssetDatabase.GetAssetPath(output) : null;
                    if (path != null && AssetDatabase.IsValidFolder(path)) Settings.outputFolder = path;
                    Settings.Save();
                }

                GUInstallerUI.Hint($"Prefab: {OutputPrefab}   ·   Spec: {SpecPath}");
            }
        }

        /// <summary>Thông tin + tuỳ chọn hiển thị ảnh demo (ảnh nằm ở cột phải).</summary>
        private void DrawDemoDetails(Texture2D texture, bool previewShown)
        {
            var ratio = (float)texture.height / texture.width;
            var isReference = texture.width == 1080 && texture.height == 2160;
            GUInstallerUI.Hint($"Kích thước gốc {texture.width}×{texture.height} · tỉ lệ 1:{ratio:0.##}"
                               + (isReference ? string.Empty : " · khác 1080×2160 mặc định"));

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            Settings.showDemoPreview = GUILayout.Toggle(Settings.showDemoPreview, "Hiện ảnh demo", GUILayout.Width(110f));
            using (new EditorGUI.DisabledScope(_locate == null || !Settings.showDemoPreview))
                Settings.showMatchRects = GUILayout.Toggle(Settings.showMatchRects, "Khung sprite đã dò", GUILayout.Width(140f));
            GUILayout.FlexibleSpace();
            if (GUInstallerUI.MiniButton("Mở ảnh gốc", true, 90f))
                EditorUtility.OpenWithDefaultApp(UIBuilderPaths.ToAbsolute(Settings.demoPath));
            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck()) Settings.Save();

            if (Settings.showDemoPreview && !previewShown)
                GUInstallerUI.Hint("Cửa sổ hẹp — kéo rộng ra để hiện ảnh demo ở cột phải.");
            else if (previewShown && _locate != null && Settings.showMatchRects)
                GUInstallerUI.Hint("Khung trên ảnh: xanh = khớp · cam = 9-slice · vàng = đang chọn (bấm khung để chọn).");
        }

        private void DrawArtFolders()
        {
            EditorGUILayout.LabelField("Thư mục art");
            var folders = Settings.artFolders;
            for (var i = 0; i < folders.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                var folder = (DefaultAsset)EditorGUILayout.ObjectField(LoadAsset<DefaultAsset>(folders[i]), typeof(DefaultAsset), false);
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
            var dropped = (DefaultAsset)EditorGUILayout.ObjectField("＋ Thêm", null, typeof(DefaultAsset), false);
            if (dropped != null) AddArtFolder(AssetDatabase.GetAssetPath(dropped));
            var demoFolder = HasDemo() ? Path.GetDirectoryName(Settings.demoPath)?.Replace('\\', '/') : null;
            if (GUInstallerUI.MiniButton("Thư mục chứa demo", demoFolder != null && !folders.Contains(demoFolder), 130f))
                AddArtFolder(demoFolder);
            EditorGUILayout.EndHorizontal();
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
            var canRun = UIBuilderPython.IsReady && HasDemo() && Settings.artFolders.Count > 0 && !busy;
            var state = busy ? GUSetupState.Busy : _locate != null ? GUSetupState.Done : canRun ? GUSetupState.Missing : GUSetupState.Blocked;

            using (GUInstallerUI.BeginCard())
            {
                GUInstallerUI.CardHeader("BƯỚC 3", "Định vị sprite trên demo", state);
                GUILayout.Label("Tìm vị trí chính xác từng sprite: đúng tỉ lệ, bị scale, kéo giãn 9-slice, lặp nhiều lần, bị che một phần. Kết quả cache theo hash file.",
                    GUInstallerUI.Desc);

                EditorGUILayout.BeginHorizontal();
                if (GUInstallerUI.PrimaryButton(busy ? "Đang định vị…" : "Chạy định vị", canRun, 26f)) StartLocate();
                if (busy && GUILayout.Button("Huỷ", GUILayout.Width(60f), GUILayout.Height(26f)))
                {
                    _locator.Cancel();
                    _locator = null;
                }

                EditorGUILayout.EndHorizontal();

                if (!string.IsNullOrEmpty(_locateMessage)) GUInstallerUI.Hint(_locateMessage);
                if (_locate != null) DrawLocateResult();
            }
        }

        private void StartLocate()
        {
            _locateMessage = "Đang chạy…";
            _locator = UIBuilderLocator.Start(Settings.demoPath, Settings.artFolders, Settings.includeSubfolders, LocatePath);
            StartTicking();
        }

        private void DrawLocateResult()
        {
            foreach (var sprite in _locate.sprites.Where(s => s.IsMatched))
            {
                var assetPath = UIBuilderPaths.ToAssetPath(sprite.sprite);
                var asset = LoadAsset<Sprite>(assetPath);
                var first = sprite.matches[0];
                var detail = $"{sprite.matches.Count}× · {first.w}×{first.h} @({first.x},{first.y})"
                             + (first.sliced ? " · 9-slice" : first.scale != 1f ? $" · scale {first.scale:0.##}" : string.Empty);

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

            var unmatched = _locate.sprites.Where(s => !s.IsMatched).ToList();
            _showUnmatched = EditorGUILayout.Foldout(_showUnmatched, $"Không khớp ({unmatched.Count})", true);
            if (!_showUnmatched) return;
            foreach (var sprite in unmatched)
                GUInstallerUI.Hint($"   {sprite.name} — {sprite.ReasonLabel}");
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
                if (EditorGUI.EndChangeCheck())
                {
                    Settings.textFont = font != null ? AssetDatabase.GetAssetPath(font) : null;
                    Settings.Save();
                }

                EditorGUILayout.BeginHorizontal();
                if (GUInstallerUI.MiniButton(_spec != null ? "Sinh lại spec nháp" : "Tạo spec nháp", _locate != null, 140f))
                    GenerateSpec();
                if (GUInstallerUI.MiniButton("Mở spec", _spec != null, 80f))
                    EditorUtility.OpenWithDefaultApp(UIBuilderPaths.ToAbsolute(SpecPath));
                if (GUInstallerUI.MiniButton("Tải lại", true, 70f)) ReloadJob();
                if (GUInstallerUI.MiniButton("Copy prompt cho Claude", HasDemo(), 170f))
                {
                    EditorGUIUtility.systemCopyBuffer = UIBuilderAiToolkit.BuildPrompt(
                        Settings.jobName, Settings.demoPath, Settings.artFolders, Settings.includeSubfolders, OutputPrefab);
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

                if (_specError != null) EditorGUILayout.HelpBox(_specError, MessageType.Error);
                if (_spec == null) return;
                GUInstallerUI.Hint($"{_spec.nodes.Count} node · {SpecPath}");
                foreach (var note in _spec.notes) GUInstallerUI.Hint($"• {note}");
            }
        }

        private void GenerateSpec()
        {
            var path = UIBuilderPaths.ToAbsolute(SpecPath);
            if (File.Exists(path) && !EditorUtility.DisplayDialog("Ghi đè spec?",
                    $"{SpecPath} đã có (có thể đã được Claude/bạn chỉnh). Sinh lại sẽ ghi đè toàn bộ.", "Ghi đè", "Huỷ"))
                return;

            UISpecFile.Save(UISpecGenerator.Generate(_locate, Settings.jobName, Settings.demoPath, OutputPrefab, Settings.textFont), SpecPath);
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
            _comparePath = _report.Success ? UIPrefabRenderer.RenderCompare(_spec, UIBuilderPaths.JobFolder(Settings.jobName)) : null;
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
                if (GUInstallerUI.MiniButton(label, HasDemo(), 110f))
                {
                    if (UIMockupOverlay.Enabled) UIMockupOverlay.Hide();
                    else UIMockupOverlay.Show(Settings.demoPath);
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
                    _locate = _locator.Result;
                    _locateMessage = _locate != null
                        ? $"{_locate.sprites.Count(s => s.IsMatched)}/{_locate.sprites.Count} sprite khớp · {_locate.elapsedMs} ms · cache {_locate.cachedCount}"
                        : $"Lỗi: {_locator.Error}";
                    if (_locate == null) GULogger.Error(LogTag, $"Định vị thất bại:\n{_locator.Output}");
                    _locator = null;
                }
                else
                {
                    running = true;
                }
            }

            if (!running) EditorApplication.update -= Tick;
            Repaint();
        }

        private void ReloadJob()
        {
            _locate = UIBuilderLocator.Load(LocatePath);
            _spec = File.Exists(UIBuilderPaths.ToAbsolute(SpecPath)) ? UISpecFile.Load(SpecPath, out _specError) : null;
            if (_spec == null && !File.Exists(UIBuilderPaths.ToAbsolute(SpecPath))) _specError = null;
            _locateMessage = _locate != null ? $"Kết quả trước: {_locate.sprites.Count(s => s.IsMatched)}/{_locate.sprites.Count} sprite khớp." : null;
            _report = null;
            _comparePath = null;
            _jobStamp = JobStamp();
        }

        private DateTime JobStamp()
        {
            var locate = File.GetLastWriteTimeUtc(UIBuilderPaths.ToAbsolute(LocatePath));
            var spec = File.GetLastWriteTimeUtc(UIBuilderPaths.ToAbsolute(SpecPath));
            return locate > spec ? locate : spec;
        }

        private bool HasDemo() => !string.IsNullOrEmpty(Settings.demoPath) && File.Exists(UIBuilderPaths.ToAbsolute(Settings.demoPath));

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
