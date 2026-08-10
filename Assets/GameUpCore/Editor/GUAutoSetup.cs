#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace GameUp.Core.Editor
{
    /// <summary>
    /// Tuỳ chọn của GameUp Core lưu theo từng project trên máy hiện tại.
    /// Chỉ chứa **sở thích người dùng**, không dùng để suy ra trạng thái cài đặt —
    /// trạng thái luôn tính từ file thật trong project.
    /// </summary>
    public static class GUCoreUserPrefs
    {
        private const string Prefix = "GameUp.Core.";

        private static string ProjectScope
        {
            get
            {
                var path = Application.dataPath;
                // Hash ổn định theo đường dẫn project để hai project trên cùng máy không đè cờ của nhau.
                unchecked
                {
                    var hash = 23;
                    for (var i = 0; i < path.Length; i++)
                        hash = hash * 31 + path[i];
                    return hash.ToString("X8");
                }
            }
        }

        private static string Key(string name) => $"{Prefix}{name}.{ProjectScope}";

        /// <summary>
        /// Dev đã chọn dùng công cụ AI nào cho project này chưa. Khi chưa chọn, GameUp Core
        /// **không tự sinh** `.claude/` hay `.cursor/` — chỉ mở cửa sổ Settings để hỏi.
        /// </summary>
        public static bool AiToolkitChoiceMade
        {
            get => EditorPrefs.GetBool(Key("AiToolkitChoiceMade"), false);
            set => EditorPrefs.SetBool(Key("AiToolkitChoiceMade"), value);
        }

        public static bool UseClaudeToolkit
        {
            get => EditorPrefs.GetBool(Key("UseClaudeToolkit"), true);
            set => EditorPrefs.SetBool(Key("UseClaudeToolkit"), value);
        }

        public static bool UseCursorToolkit
        {
            get => EditorPrefs.GetBool(Key("UseCursorToolkit"), true);
            set => EditorPrefs.SetBool(Key("UseCursorToolkit"), value);
        }

        /// <summary>Tự bù file còn thiếu của các công cụ đã chọn khi mở project. Không bao giờ ghi đè file đã có.</summary>
        public static bool AutoInstallAiToolkit
        {
            get => EditorPrefs.GetBool(Key("AutoInstallAiToolkit"), true);
            set => EditorPrefs.SetBool(Key("AutoInstallAiToolkit"), value);
        }

        /// <summary>Mở cửa sổ GameUp Settings lần đầu tiên mở project này.</summary>
        public static bool OpenSettingsOnFirstRun
        {
            get => EditorPrefs.GetBool(Key("OpenSettingsOnFirstRun"), true);
            set => EditorPrefs.SetBool(Key("OpenSettingsOnFirstRun"), value);
        }

        /// <summary>Đã mở cửa sổ Settings cho project này chưa (chỉ để không mở lại mỗi lần reload).</summary>
        public static bool SettingsWindowShown
        {
            get => EditorPrefs.GetBool(Key("SettingsWindowShown"), false);
            set => EditorPrefs.SetBool(Key("SettingsWindowShown"), value);
        }

        public static void ResetFirstRun()
        {
            EditorPrefs.DeleteKey(Key("SettingsWindowShown"));
        }

        /// <summary>Hỏi lại từ đầu xem project này dùng Claude, Cursor, cả hai hay không dùng.</summary>
        public static void ResetAiToolkitChoice()
        {
            EditorPrefs.DeleteKey(Key("AiToolkitChoiceMade"));
            EditorPrefs.DeleteKey(Key("SettingsWindowShown"));
        }
    }

    /// <summary>
    /// Chạy một lần khi Editor nạp: bù bộ công cụ AI còn thiếu và mở cửa sổ Settings ở lần đầu
    /// mở project. Idempotent, không ghi đè file người dùng, bỏ qua batch mode.
    /// </summary>
    [InitializeOnLoad]
    public static class GUAutoSetup
    {
        private const string SessionKey = "GameUp.Core.AutoSetup.Ran";

        static GUAutoSetup()
        {
            if (Application.isBatchMode)
                return;

            EditorApplication.delayCall += Run;
        }

        private static void Run()
        {
            if (SessionState.GetBool(SessionKey, false))
                return;
            SessionState.SetBool(SessionKey, true);

            if (!GUCoreUserPrefs.AiToolkitChoiceMade)
                TryInferChoiceFromExistingFiles();

            // Dev chưa chọn dùng công cụ AI nào: không sinh file nào cả, chỉ mở cửa sổ để hỏi.
            if (!GUCoreUserPrefs.AiToolkitChoiceMade)
            {
                if (GUCoreUserPrefs.OpenSettingsOnFirstRun && !GUCoreUserPrefs.SettingsWindowShown)
                {
                    GUCoreUserPrefs.SettingsWindowShown = true;
                    GUSettingsWindow.Open();
                }

                return;
            }

            var installedSomething = GUCoreUserPrefs.AutoInstallAiToolkit && TryBootstrapAiToolkit();

            if (!GUCoreUserPrefs.OpenSettingsOnFirstRun || GUCoreUserPrefs.SettingsWindowShown)
                return;

            GUCoreUserPrefs.SettingsWindowShown = true;

            // Chỉ làm phiền khi project thật sự còn việc phải làm.
            if (installedSomething || !GUSettingsWindow.IsProjectFullySetUp())
                GUSettingsWindow.Open();
        }

        /// <summary>
        /// Project clone về đã sẵn <c>.claude/</c> hoặc <c>.cursor/</c> thì suy ra lựa chọn của team
        /// thay vì hỏi lại — dev mới không bị popup vô nghĩa, và công cụ team không dùng cũng không bị sinh thêm.
        /// </summary>
        private static void TryInferChoiceFromExistingFiles()
        {
            var hasClaude = GUClaudeToolkitInstaller.GetStatus().Installed > 0;
            var hasCursor = GUCursorRulesInstaller.IsInstalled();
            if (!hasClaude && !hasCursor)
                return;

            GUCoreUserPrefs.UseClaudeToolkit = hasClaude;
            GUCoreUserPrefs.UseCursorToolkit = hasCursor;
            GUCoreUserPrefs.AiToolkitChoiceMade = true;
        }

        /// <summary>Bù file còn thiếu của những công cụ dev đã chọn. Trả về true nếu có ghi file nào.</summary>
        private static bool TryBootstrapAiToolkit()
        {
            var wrote = false;

            if (GUCoreUserPrefs.UseClaudeToolkit)
            {
                try
                {
                    var status = GUClaudeToolkitInstaller.GetStatus();
                    if (status.TemplatesAvailable && !status.IsComplete)
                    {
                        var isFirstInstall = status.Installed == 0;
                        GUClaudeToolkitInstaller.Install(overwrite: false, log: false);
                        wrote = true;

                        GULogger.Log(
                            "GameUpSetup",
                            isFirstInstall
                                ? "Đã cài bộ công cụ Claude Code của GameUp (CLAUDE.md + .claude/). Mở GameUp → Settings để xem."
                                : "Đã bù các file còn thiếu của bộ công cụ Claude Code.");
                    }
                }
                catch (Exception e)
                {
                    GULogger.Warning("GameUpSetup", $"Không tự cài được bộ công cụ Claude: {e.Message}");
                }
            }

            // Cursor tự bù trong GUCursorRulesInstaller (cũng đã gác theo lựa chọn của dev),
            // ở đây chỉ cần báo lại cho luồng mở cửa sổ lần đầu.
            if (GUCoreUserPrefs.UseCursorToolkit && !GUCursorRulesInstaller.IsInstalled())
                wrote = true;

            return wrote;
        }
    }
}
#endif
