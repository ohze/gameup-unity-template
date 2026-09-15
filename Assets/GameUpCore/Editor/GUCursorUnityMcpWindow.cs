#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GameUp.Core.Editor
{
    /// <summary>Trình cài một nút Cursor × Unity MCP (server <c>unity</c> + skill chính thức của Unity + rules GameUp).</summary>
    public sealed class GUCursorUnityMcpWindow : GUMcpInstallerWindowBase
    {
        public const string MenuPath = "GameUp/Project/Cursor × Unity (MCP)";

        [MenuItem(MenuPath)]
        public static void Open()
        {
            var window = GetWindow<GUCursorUnityMcpWindow>(utility: false, title: "Cursor × Unity MCP", focus: true);
            window.minSize = new Vector2(580f, 600f);
            window.Show();
        }

        protected override GUMcpInstallRunner Runner => GUCursorUnityMcpInstaller.Runner;

        protected override string Heading => "Cursor × Unity: MCP + Skills";

        protected override string Description =>
            "Agent của Cursor nhìn thấy và thao tác trên Editor đang mở: đọc scene, sửa GameObject qua API Editor (không sửa YAML), "
            + "bấm Play, đọc Console, chạy test — kèm skill chính thức của Unity và rules/skills GameUp.";

        protected override string Requirements =>
            $"Yêu cầu: Cursor đã mở ít nhất một lần · Unity {GUMcpInstallRunner.MinUnityMajorVersion}.0+ · Git · mạng. "
            + "Dùng chung Unity CLI và com.unity.pipeline với Claude Code nếu đã cài.";

        protected override string ConfirmCommands =>
            "• Cài Unity CLI (channel beta) nếu chưa có\n"
            + $"• unity mcp configure cursor — thêm server \"{GUCursorUnityMcpInstaller.McpServerName}\" vào ~/.cursor/mcp.json (giữ server khác, có backup), "
            + "rồi đặt args --project-path ${workspaceFolder} để Cursor thấy tool của Editor\n"
            + "• Chép skill chính thức của Unity vào ~/.cursor/skills (từ cache plugin Claude Code, hoặc git clone unity-agent-plugin)\n"
            + "• Bù .cursor/rules, .cursor/skills GameUp và rule unity-mcp.mdc còn thiếu (không ghi đè file đã có)\n"
            + "• unity pipeline install — sửa Packages/manifest.json, Editor sẽ import và compile lại (Unity 6+)";

        protected override string ManualFollowUp =>
            "• Thoát hẳn Cursor rồi mở lại đúng thư mục project — Cursor chỉ đọc ~/.cursor/mcp.json lúc khởi động (Reload Window không đủ).\n"
            + $"• Cursor Settings → Tools & MCP → bật server \"{GUCursorUnityMcpInstaller.McpServerName}\" (chấm xanh + số tool = đã kết nối).\n"
            + "• Mở chat MỚI: phiên chat cũ đã chốt danh sách tool từ trước nên không thấy tool của Unity.";
    }
}
#endif
