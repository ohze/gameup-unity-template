#if UNITY_EDITOR
using System;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GameUp.Core.Editor
{
    /// <summary>
    /// Thông số công khai của app trên trang Google Play. Trang store KHÔNG có version code —
    /// chỉ có version name của bản production; code lấy chính xác cần <see cref="GUPlayDeveloperApi"/>.
    /// </summary>
    public sealed class GUPlayStoreInfo
    {
        private const string DetailsUrlFormat = "https://play.google.com/store/apps/details?id={0}&hl=en&gl=US";

        private static readonly Regex PackageInUrlRegex = new Regex(@"[?&]id=([A-Za-z][\w]*(?:\.[A-Za-z0-9_]+)+)");
        private static readonly Regex PackageNameRegex = new Regex(@"^[A-Za-z][\w]*(?:\.[A-Za-z0-9_]+)+$");
        private static readonly Regex TitleRegex = new Regex(@"<title[^>]*>([^<]+?) - Apps on Google Play");
        // Khối dữ liệu trong script trang: [[["<versionName>"]],[[[<targetSdk>]],[[[<minSdk>,"<minAndroid>"]]]]]
        private static readonly Regex VersionBlockRegex = new Regex(@"\[\[\[""([^""]+)""\]\],\[\[\[(\d+)\]\],\[\[\[(\d+),""([^""]*)""\]\]\]\]");
        private static readonly Regex UpdatedOnRegex = new Regex(@"Updated on</div><div[^>]*>([^<]+)<");

        private static readonly HttpClient Http = CreateHttpClient();

        public string PackageName { get; private set; }
        public string AppTitle { get; private set; }
        /// <summary>Có thể là "Varies with device" với app chia APK theo thiết bị.</summary>
        public string VersionName { get; private set; }
        public int TargetSdk { get; private set; }
        public int MinSdk { get; private set; }
        public string MinAndroidVersion { get; private set; }
        public string UpdatedOn { get; private set; }

        /// <summary>Nhận link Play Store hoặc package name trần. Trả null nếu không nhận ra.</summary>
        public static string ExtractPackageName(string urlOrPackage)
        {
            if (string.IsNullOrWhiteSpace(urlOrPackage)) return null;

            var input = urlOrPackage.Trim();
            if (PackageNameRegex.IsMatch(input)) return input;

            var match = PackageInUrlRegex.Match(input);
            return match.Success ? match.Groups[1].Value : null;
        }

        public static GUPlayStoreInfo Parse(string packageName, string html)
        {
            var info = new GUPlayStoreInfo { PackageName = packageName };

            var title = TitleRegex.Match(html);
            if (title.Success) info.AppTitle = WebUtility.HtmlDecode(title.Groups[1].Value);

            var version = VersionBlockRegex.Match(html);
            if (version.Success)
            {
                info.VersionName = version.Groups[1].Value;
                info.TargetSdk = int.Parse(version.Groups[2].Value);
                info.MinSdk = int.Parse(version.Groups[3].Value);
                info.MinAndroidVersion = version.Groups[4].Value;
            }

            var updated = UpdatedOnRegex.Match(html);
            if (updated.Success) info.UpdatedOn = WebUtility.HtmlDecode(updated.Groups[1].Value);

            return info;
        }

        public static async Task<GUPlayStoreInfo> FetchAsync(string urlOrPackage)
        {
            var packageName = ExtractPackageName(urlOrPackage);
            if (packageName == null)
                throw new ArgumentException($"Không nhận ra package name trong \"{urlOrPackage}\". Dán link dạng https://play.google.com/store/apps/details?id=com.company.game");

            using var response = await Http.GetAsync(string.Format(DetailsUrlFormat, packageName));
            if (response.StatusCode == HttpStatusCode.NotFound)
                throw new InvalidOperationException($"Không thấy {packageName} trên Google Play (app chưa publish, bị gỡ, hoặc sai package).");
            response.EnsureSuccessStatusCode();

            var info = Parse(packageName, await response.Content.ReadAsStringAsync());
            if (string.IsNullOrEmpty(info.VersionName))
                throw new InvalidOperationException("Đọc được trang store nhưng không tìm thấy version — Google có thể đã đổi cấu trúc trang.");

            return info;
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            // Giả trình duyệt để Google trả đúng trang desktop như khi mở bằng tay.
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36");
            return client;
        }
    }
}
#endif
