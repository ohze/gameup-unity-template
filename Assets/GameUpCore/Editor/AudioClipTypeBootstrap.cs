#if UNITY_EDITOR
using System.IO;
using UnityEditor;

namespace GameUp.Core.Editor
{
    [InitializeOnLoad]
    public static class AudioClipTypeBootstrap
    {
        static AudioClipTypeBootstrap()
        {
            // Giữ class này để tương thích ngược, nhưng hệ thống Audio hiện tại đã chuyển sang dùng AudioIdentity ScriptableObject.
        }
    }
}
#endif

