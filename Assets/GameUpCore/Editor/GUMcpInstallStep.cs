#if UNITY_EDITOR
namespace GameUp.Core.Editor
{
    /// <summary>Một bước của trình cài "IDE × Unity MCP" (Claude Code, Cursor) cùng kết quả kiểm tra gần nhất.</summary>
    public sealed class GUMcpInstallStep
    {
        public enum Kind
        {
            ClaudeCode,
            Marketplace,
            Plugin,
            McpServer,
            CursorApp,
            CursorMcpConfig,
            UnitySkills,
            CursorToolkit,
            UnityCli,
            Pipeline,
            EditorReady
        }

        public enum Status
        {
            Unknown,
            Running,
            Done,
            Missing,
            Blocked,
            Failed
        }

        public GUMcpInstallStep(Kind id, string title, bool perMachine, bool requiresUnity6 = false)
        {
            Id = id;
            Title = title;
            PerMachine = perMachine;
            RequiresUnity6 = requiresUnity6;
        }

        public Kind Id { get; }

        public string Title { get; }

        /// <summary>true = cài một lần cho máy dev (scope user); false = gắn với project đang mở.</summary>
        public bool PerMachine { get; }

        /// <summary>Bước dựa trên <c>com.unity.pipeline</c> — không áp dụng (không tính tiến độ) khi Unity cũ hơn 6.</summary>
        public bool RequiresUnity6 { get; }

        public Status State { get; internal set; } = Status.Unknown;

        public string Detail { get; internal set; } = "chưa kiểm tra";
    }
}
#endif
