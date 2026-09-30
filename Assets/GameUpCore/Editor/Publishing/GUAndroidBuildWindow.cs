#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GameUp.Core.Editor
{
    /// <summary>
    /// Build AAB lên Google Play với version tự tăng: lấy version name từ trang store (và version code chính xác
    /// qua Play Developer API nếu có service account), tính name/code kế tiếp, build, chỉ lưu version khi build thành công.
    /// </summary>
    public sealed class GUAndroidBuildWindow : EditorWindow
    {
        private const string WindowTitle = "GU Android Build";
        private const string MenuPath = "GameUp/Build/Android AAB (Google Play)";
        private const string DefaultOutputFolder = "Builds/Android";
        private const string LogTag = "AndroidBuild";

        private string _playUrl;
        private string _serviceAccountPath;
        private string _outputFolder;

        private GUPlayStoreInfo _storeInfo;
        private int _apiMaxCode = GUAndroidVersionUtility.NoCode;
        private string _storeError;
        private string _apiError;
        private string _nextVersionName;
        private int _nextVersionCode;
        private bool _isFetching;
        private Vector2 _scroll;

        [MenuItem(MenuPath)]
        public static void OpenWindow()
        {
            var window = GetWindow<GUAndroidBuildWindow>(WindowTitle);
            window.minSize = new Vector2(460f, 560f);
        }

        private void OnEnable()
        {
            _playUrl = EditorPrefs.GetString(GetPrefsKey("PlayUrl"), string.Empty);
            _serviceAccountPath = EditorPrefs.GetString(GetPrefsKey("ServiceAccountPath"), string.Empty);
            _outputFolder = EditorPrefs.GetString(GetPrefsKey("OutputFolder"), DefaultOutputFolder);
            RecomputeNextVersion();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawStoreSection();
            EditorGUILayout.Space(8f);
            DrawApiSection();
            EditorGUILayout.Space(8f);
            DrawVersionSection();
            EditorGUILayout.Space(8f);
            DrawKeystoreSection();
            EditorGUILayout.Space(8f);
            DrawBuildSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawStoreSection()
        {
            EditorGUILayout.LabelField("Google Play", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _playUrl = EditorGUILayout.TextField(new GUIContent("Link / package", "https://play.google.com/store/apps/details?id=... hoặc package name"), _playUrl);
            if (EditorGUI.EndChangeCheck()) EditorPrefs.SetString(GetPrefsKey("PlayUrl"), _playUrl);

            using (new EditorGUI.DisabledScope(_isFetching || string.IsNullOrWhiteSpace(_playUrl)))
            {
                if (GUILayout.Button(_isFetching ? "Đang lấy thông tin..." : "Lấy thông tin từ Google Play")) FetchVersionInfo();
            }

            if (!string.IsNullOrEmpty(_storeError)) EditorGUILayout.HelpBox(_storeError, MessageType.Error);
            if (_storeInfo == null) return;

            EditorGUILayout.HelpBox(
                $"{_storeInfo.AppTitle}\n" +
                $"Package: {_storeInfo.PackageName}\n" +
                $"Version name (production): {_storeInfo.VersionName}\n" +
                $"Target SDK: {_storeInfo.TargetSdk} · Min SDK: {_storeInfo.MinSdk} (Android {_storeInfo.MinAndroidVersion})\n" +
                $"Cập nhật: {_storeInfo.UpdatedOn}",
                MessageType.Info);

            var localPackage = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            if (localPackage == _storeInfo.PackageName) return;

            EditorGUILayout.HelpBox($"Package trong project ({localPackage}) khác package trên store ({_storeInfo.PackageName}).", MessageType.Error);
            if (GUILayout.Button($"Dùng package {_storeInfo.PackageName}"))
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, _storeInfo.PackageName);
            }
        }

        private void DrawApiSection()
        {
            EditorGUILayout.LabelField("Play Developer API (tuỳ chọn — version code chính xác)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Trang store không công khai version code, và chỉ hiện bản production. Có service account JSON thì tool đọc code lớn nhất đã upload ở mọi track (internal/closed/open/production) nên không bị \"Version code has already been used\".",
                MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                _serviceAccountPath = EditorGUILayout.TextField("Service account JSON", _serviceAccountPath);
                if (GUILayout.Button("...", GUILayout.Width(28f)))
                {
                    var picked = EditorUtility.OpenFilePanel("Chọn service account JSON", string.Empty, "json");
                    if (!string.IsNullOrEmpty(picked)) _serviceAccountPath = picked;
                }

                if (EditorGUI.EndChangeCheck()) EditorPrefs.SetString(GetPrefsKey("ServiceAccountPath"), _serviceAccountPath);
            }

            if (!string.IsNullOrEmpty(_apiError)) EditorGUILayout.HelpBox(_apiError, MessageType.Error);
            else if (_apiMaxCode != GUAndroidVersionUtility.NoCode) EditorGUILayout.LabelField("Code lớn nhất trên Play Console", _apiMaxCode.ToString());
        }

        private void DrawVersionSection()
        {
            EditorGUILayout.LabelField("Version", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Hiện tại trong project", $"{PlayerSettings.bundleVersion}  ({PlayerSettings.Android.bundleVersionCode})");
            if (_storeInfo != null)
            {
                var derived = GUAndroidVersionUtility.DeriveVersionCode(_storeInfo.VersionName);
                var derivedLabel = derived == GUAndroidVersionUtility.NoCode ? "không suy được code" : $"suy ra code {derived}";
                EditorGUILayout.LabelField("Trên store", $"{_storeInfo.VersionName}  ({derivedLabel})");
            }

            _nextVersionName = EditorGUILayout.TextField("Version name mới", _nextVersionName);
            _nextVersionCode = EditorGUILayout.IntField("Version code mới", _nextVersionCode);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Tính lại")) RecomputeNextVersion();
                if (GUILayout.Button("Chỉ ghi version (không build)")) ApplyNextVersion();
            }
        }

        private void DrawKeystoreSection()
        {
            EditorGUILayout.LabelField("Keystore (upload key)", EditorStyles.boldLabel);

            PlayerSettings.Android.useCustomKeystore = EditorGUILayout.Toggle("Dùng keystore riêng", PlayerSettings.Android.useCustomKeystore);
            if (!PlayerSettings.Android.useCustomKeystore) return;

            using (new EditorGUILayout.HorizontalScope())
            {
                PlayerSettings.Android.keystoreName = EditorGUILayout.TextField("Keystore", PlayerSettings.Android.keystoreName);
                if (GUILayout.Button("...", GUILayout.Width(28f)))
                {
                    var picked = EditorUtility.OpenFilePanel("Chọn keystore", string.Empty, "keystore,jks");
                    if (!string.IsNullOrEmpty(picked)) PlayerSettings.Android.keystoreName = picked;
                }
            }

            PlayerSettings.Android.keystorePass = EditorGUILayout.PasswordField("Keystore password", PlayerSettings.Android.keystorePass);
            PlayerSettings.Android.keyaliasName = EditorGUILayout.TextField("Alias", PlayerSettings.Android.keyaliasName);
            PlayerSettings.Android.keyaliasPass = EditorGUILayout.PasswordField("Alias password", PlayerSettings.Android.keyaliasPass);
            EditorGUILayout.HelpBox("Unity không lưu mật khẩu keystore xuống đĩa — mở lại Editor phải nhập lại.", MessageType.None);
        }

        private void DrawBuildSection()
        {
            EditorGUILayout.LabelField("Build", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _outputFolder = EditorGUILayout.TextField("Thư mục output", _outputFolder);
            if (EditorGUI.EndChangeCheck()) EditorPrefs.SetString(GetPrefsKey("OutputFolder"), _outputFolder);
            EditorGUILayout.LabelField("File", GetOutputPath(), EditorStyles.miniLabel);

            foreach (var warning in CollectWarnings()) EditorGUILayout.HelpBox(warning, MessageType.Warning);

            var blockers = CollectBlockers();
            foreach (var blocker in blockers) EditorGUILayout.HelpBox(blocker, MessageType.Error);

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android && GUILayout.Button("Chuyển build target sang Android"))
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            }

            using (new EditorGUI.DisabledScope(blockers.Count > 0 || _isFetching))
            {
                if (GUILayout.Button($"Build AAB {_nextVersionName} ({_nextVersionCode})", GUILayout.Height(32f)))
                {
                    EditorApplication.delayCall += BuildAppBundle;
                }
            }
        }

        private List<string> CollectBlockers()
        {
            var blockers = new List<string>();

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                blockers.Add("Build target đang không phải Android.");
            if (GetEnabledScenes().Length == 0)
                blockers.Add("Build Settings chưa có scene nào được bật.");
            if (!GUAndroidVersionUtility.TryParseVersionName(_nextVersionName, out _))
                blockers.Add($"Version name \"{_nextVersionName}\" không hợp lệ (dạng 1.0.8).");
            if (_nextVersionCode <= 0)
                blockers.Add("Version code phải > 0.");
            if (_apiMaxCode != GUAndroidVersionUtility.NoCode && _nextVersionCode <= _apiMaxCode)
                blockers.Add($"Version code {_nextVersionCode} đã được dùng trên Play Console (lớn nhất: {_apiMaxCode}).");
            if (_storeInfo != null && PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android) != _storeInfo.PackageName)
                blockers.Add("Package khác package trên store.");

            var keystoreReady = PlayerSettings.Android.useCustomKeystore
                                && File.Exists(PlayerSettings.Android.keystoreName)
                                && !string.IsNullOrEmpty(PlayerSettings.Android.keystorePass)
                                && !string.IsNullOrEmpty(PlayerSettings.Android.keyaliasName)
                                && !string.IsNullOrEmpty(PlayerSettings.Android.keyaliasPass);
            if (!keystoreReady)
                blockers.Add("Chưa cấu hình đủ keystore (file, alias, mật khẩu) — Google Play không nhận bản ký bằng debug key.");

            return blockers;
        }

        private IEnumerable<string> CollectWarnings()
        {
            if (_storeInfo == null)
                yield return "Chưa lấy thông tin store — version đang tính từ PlayerSettings của project.";
            else if (_apiMaxCode == GUAndroidVersionUtility.NoCode)
                yield return "Code đang suy từ version name trên store (chỉ bản production). Nếu đã upload bản test với code cao hơn, Play sẽ từ chối — kiểm tra Play Console hoặc dùng service account.";

            if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP)
                yield return "Scripting backend không phải IL2CPP — Google Play yêu cầu 64-bit (ARM64).";
            if ((PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) == 0)
                yield return "Chưa bật ARM64 trong Target Architectures.";

            PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android, out var defines);
            if (defines.Contains("ENABLE_LOG"))
                yield return "Đang bật ENABLE_LOG — tắt qua GameUp/Logger/Disable Logs (Release) trước khi phát hành.";
        }

        private async void FetchVersionInfo()
        {
            _isFetching = true;
            _storeError = null;
            _apiError = null;
            _apiMaxCode = GUAndroidVersionUtility.NoCode;

            try
            {
                _storeInfo = await GUPlayStoreInfo.FetchAsync(_playUrl);
            }
            catch (Exception exception)
            {
                _storeInfo = null;
                _storeError = exception.Message;
            }

            var packageName = _storeInfo?.PackageName ?? GUPlayStoreInfo.ExtractPackageName(_playUrl);
            if (!string.IsNullOrWhiteSpace(_serviceAccountPath) && packageName != null)
            {
                try
                {
                    _apiMaxCode = await GUPlayDeveloperApi.FetchMaxVersionCodeAsync(_serviceAccountPath, packageName);
                }
                catch (Exception exception)
                {
                    _apiError = exception.Message;
                }
            }

            _isFetching = false;
            RecomputeNextVersion();
            Repaint();
        }

        private void RecomputeNextVersion()
        {
            var storeVersionName = _storeInfo?.VersionName;
            _nextVersionName = GUAndroidVersionUtility.ComputeNextVersionName(storeVersionName, PlayerSettings.bundleVersion);
            _nextVersionCode = GUAndroidVersionUtility.ComputeNextVersionCode(
                _nextVersionName, storeVersionName, PlayerSettings.Android.bundleVersionCode, _apiMaxCode);
        }

        private void ApplyNextVersion()
        {
            PlayerSettings.bundleVersion = _nextVersionName;
            PlayerSettings.Android.bundleVersionCode = _nextVersionCode;
            AssetDatabase.SaveAssets();
            GULogger.Log(LogTag, $"Đã ghi version {_nextVersionName} ({_nextVersionCode}) vào PlayerSettings.");
        }

        private void BuildAppBundle()
        {
            var previousVersionName = PlayerSettings.bundleVersion;
            var previousVersionCode = PlayerSettings.Android.bundleVersionCode;
            var previousBuildAppBundle = EditorUserBuildSettings.buildAppBundle;
            var outputPath = GetOutputPath();

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? DefaultOutputFolder);
            PlayerSettings.bundleVersion = _nextVersionName;
            PlayerSettings.Android.bundleVersionCode = _nextVersionCode;
            EditorUserBuildSettings.buildAppBundle = true;

            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = GetEnabledScenes(),
                    locationPathName = outputPath,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.None
                });
            }
            finally
            {
                EditorUserBuildSettings.buildAppBundle = previousBuildAppBundle;
            }

            if (report.summary.result == BuildResult.Succeeded)
            {
                AssetDatabase.SaveAssets();
                GULogger.Log(LogTag, $"Build xong {outputPath} — {_nextVersionName} ({_nextVersionCode}), {report.summary.totalSize / (1024f * 1024f):F1} MB.");
                EditorUtility.RevealInFinder(outputPath);
                return;
            }

            // Build hỏng thì trả version về như cũ để lần sau không nhảy cóc.
            PlayerSettings.bundleVersion = previousVersionName;
            PlayerSettings.Android.bundleVersionCode = previousVersionCode;
            GULogger.Error(LogTag, $"Build AAB thất bại ({report.summary.result}, {report.summary.totalErrors} lỗi) — đã trả version về {previousVersionName} ({previousVersionCode}).");
        }

        private string GetOutputPath()
        {
            var productName = string.Concat(PlayerSettings.productName.Split(Path.GetInvalidFileNameChars())).Replace(' ', '_');
            return Path.Combine(_outputFolder, $"{productName}_{_nextVersionName}_{_nextVersionCode}.aab");
        }

        private static string[] GetEnabledScenes()
        {
            return EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
        }

        private static string GetPrefsKey(string name)
        {
            // productGUID riêng cho từng project, tránh các project dùng chung link/service account.
            return $"GameUp.AndroidBuild.{PlayerSettings.productGUID}.{name}";
        }
    }
}
#endif
