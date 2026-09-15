#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GameUp.Core.Editor
{
    /// <summary>Trình cài một nút Claude Code × Unity (plugin skills <c>unity</c> + Unity MCP).</summary>
    public sealed class GUClaudeUnityMcpWindow : GUMcpInstallerWindowBase
    {
        public const string MenuPath = "GameUp/Project/Claude Code × Unity (Plugin + MCP)";

        [MenuItem(MenuPath)]
        public static void Open()
        {
            var window = GetWindow<GUClaudeUnityMcpWindow>(utility: false, title: "Claude × Unity MCP", focus: true);
            window.minSize = new Vector2(580f, 600f);
            window.Show();
        }

        protected override GUMcpInstallRunner Runner => GUClaudeUnityMcpInstaller.Runner;

        protected override string Heading => "Claude Code × Unity: Plugin + MCP";

        protected override string Description =>
            "Claude nhìn thấy và thao tác trên Editor đang mở: đọc scene, sửa GameObject qua API Editor (không sửa YAML), "
            + "bấm Play, đọc Console, chụp Game View, chạy test — kèm bộ skill chính thức của Unity.";

        protected override string Requirements =>
            $"Yêu cầu: Claude Code ≥ {GUClaudeUnityMcpInstaller.MinClaudeCodeVersion} · Unity {GUMcpInstallRunner.MinUnityMajorVersion}.0+ · Git · mạng. "
            + "Plugin và pipeline đang beta/experimental.";

        protected override string ConfirmCommands =>
            $"• claude update — chỉ khi Claude Code < {GUClaudeUnityMcpInstaller.MinClaudeCodeVersion}\n"
            + "• claude plugin marketplace add Unity-Technologies/unity-agent-plugin\n"
            + "• claude plugin install unity@unity-agent-plugin --scope user\n"
            + "• Cài Unity CLI (channel beta): tải script từ public-cdn.cloud.unity3d.com và chạy bằng bash/PowerShell\n"
            + "• claude mcp add --scope user unity-editor-mcp → unity mcp (ghi vào cấu hình Claude Code của tài khoản)\n"
            + "• unity pipeline install — sửa Packages/manifest.json, Editor sẽ import và compile lại (Unity 6+)";

        protected override string ManualFollowUp =>
            "• Reload Window VS Code (Ctrl+Shift+P → Developer: Reload Window) hoặc mở lại Claude Code để tool MCP hiện ra.";
    }
}
#endif
