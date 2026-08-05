using GameUp.Core;
using UnityEngine;
using UnityEngine.UI;

namespace GameUp.Samples
{
    /// <summary>
    /// Mẫu luồng khởi động: chạy các bước init theo thứ tự, hiển thị tiến độ, rồi vào scene chính.
    /// Đặt trên một GameObject trong scene Boot.
    /// </summary>
    public class BootstrapExample : MonoBehaviour
    {
        [Header("Scene")]
        [SerializeField] private string nextScene = "MainMenu";

        [Tooltip("Giữ màn loading tối thiểu bao nhiêu giây để không bị nhấp nháy.")]
        [SerializeField, Min(0f)] private float minLoadingDuration = 1f;

        [Header("UI (tuỳ chọn)")]
        [SerializeField] private Slider progressBar;
        [SerializeField] private Text progressLabel;

        private void Start()
        {
            // 1. Đăng ký các bước khởi tạo. Thứ tự đăng ký chính là thứ tự chạy.

            // Bước bất đồng bộ: implement IInitial, GUBootstrap sẽ chờ Initialized = true.
            GUBootstrap.AddStep(AddressableDataHolder.Instance);

            // Bước đồng bộ: chỉ cần chạy xong là qua bước kế.
            GUBootstrap.AddStep("Audio", () => AudioManager.PreloadIdentities());

            GUBootstrap.AddStep("Player data", () =>
            {
                // Ví dụ: nạp save của người chơi ở đây.
                // PlayerData.Create();
            });

            // 2. Nghe tiến độ để vẽ thanh loading.
            GUBootstrap.OnProgress += OnBootstrapProgress;

            // 3. Chạy, xong thì sang scene chính.
            GUBootstrap.Run(OnBootstrapCompleted);
        }

        private void OnBootstrapProgress(float progress, string stepName)
        {
            if (progressBar) progressBar.value = progress;
            if (progressLabel) progressLabel.text = string.IsNullOrEmpty(stepName) ? "Ready" : stepName;
        }

        private void OnBootstrapCompleted()
        {
            GUBootstrap.OnProgress -= OnBootstrapProgress;

            for (var i = 0; i < GUBootstrap.FailedSteps.Count; i++)
            {
                // Bước lỗi không chặn vào game, nhưng nên gửi về analytics để còn biết mà sửa.
                GULogger.Error("BootstrapExample", $"Bước khởi tạo lỗi: {GUBootstrap.FailedSteps[i]}");
            }

            // Trong lúc load scene, GUSceneLoader.OnProgress tiếp tục báo tiến độ.
            GUSceneLoader.OnProgress += OnSceneProgress;
            GUSceneLoader.LoadAsync(nextScene, () => GUSceneLoader.OnProgress -= OnSceneProgress, minLoadingDuration);
        }

        private void OnSceneProgress(float progress)
        {
            if (progressBar) progressBar.value = progress;
        }
    }
}
