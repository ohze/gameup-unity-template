#if UNITY_EDITOR
namespace GameUp.Core.Editor
{
    /// <summary>Một bước của trình cài Claude Code × Unity (plugin skills + MCP) cùng kết quả kiểm tra gần nhất.</summary>
    public sealed class GUClaudeUnityMcpStep
    {
        public enum Kind
        {
            ClaudeCode,
            Marketplace,
            Plugin,
            UnityCli,
            McpServer,
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

        public GUClaudeUnityMcpStep(Kind id, string title, bool perMachine)
        {
            Id = id;
            Title = title;
            PerMachine = perMachine;
        }

        public Kind Id { get; }

        public string Title { get; }

        /// <summary>true = cài một lần cho máy dev (scope user); false = gắn với project đang mở.</summary>
        public bool PerMachine { get; }

        public Status State { get; internal set; } = Status.Unknown;

        public string Detail { get; internal set; } = "chưa kiểm tra";
    }
}
#endif
