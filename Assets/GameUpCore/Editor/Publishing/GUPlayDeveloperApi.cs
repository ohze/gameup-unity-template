#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace GameUp.Core.Editor
{
    /// <summary>
    /// Đọc version code lớn nhất đã upload lên Play Console (mọi track, kể cả internal/closed test và bản nháp)
    /// qua Google Play Developer API bằng service account JSON. Chỉ đọc: tạo edit tạm rồi xoá, không commit gì.
    /// Service account cần được mời vào Play Console (Users and permissions) với quyền xem app.
    /// </summary>
    public static class GUPlayDeveloperApi
    {
        private const string Scope = "https://www.googleapis.com/auth/androidpublisher";
        private const string DefaultTokenUri = "https://oauth2.googleapis.com/token";
        private const string EditsUrlFormat = "https://androidpublisher.googleapis.com/androidpublisher/v3/applications/{0}/edits";

        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        // DTO khớp tên key JSON của Google nên giữ snake_case / camelCase gốc.
        [Serializable]
        private sealed class ServiceAccountKey
        {
            public string client_email;
            public string private_key;
            public string token_uri;
        }

        [Serializable]
        private sealed class TokenResponse
        {
            public string access_token;
        }

        [Serializable]
        private sealed class EditResponse
        {
            public string id;
        }

        [Serializable]
        private sealed class VersionCodeEntry
        {
            public int versionCode;
        }

        [Serializable]
        private sealed class BundleList
        {
            public VersionCodeEntry[] bundles;
        }

        [Serializable]
        private sealed class ApkList
        {
            public VersionCodeEntry[] apks;
        }

        [Serializable]
        private sealed class TrackRelease
        {
            public string[] versionCodes;
        }

        [Serializable]
        private sealed class Track
        {
            public TrackRelease[] releases;
        }

        [Serializable]
        private sealed class TrackList
        {
            public Track[] tracks;
        }

        /// <summary>Trả <see cref="GUAndroidVersionUtility.NoCode"/> nếu app chưa có bản upload nào.</summary>
        public static async Task<int> FetchMaxVersionCodeAsync(string serviceAccountJsonPath, string packageName)
        {
            if (!File.Exists(serviceAccountJsonPath))
                throw new FileNotFoundException($"Không thấy file service account: {serviceAccountJsonPath}");

            var key = JsonUtility.FromJson<ServiceAccountKey>(File.ReadAllText(serviceAccountJsonPath));
            if (string.IsNullOrEmpty(key.client_email) || string.IsNullOrEmpty(key.private_key))
                throw new InvalidDataException("File JSON không phải service account key (thiếu client_email / private_key).");

            var accessToken = await RequestAccessTokenAsync(key);
            var editsUrl = string.Format(EditsUrlFormat, packageName);
            var editId = JsonUtility.FromJson<EditResponse>(await SendAsync(HttpMethod.Post, editsUrl, accessToken)).id;
            var editUrl = $"{editsUrl}/{editId}";

            try
            {
                var bundles = JsonUtility.FromJson<BundleList>(await SendAsync(HttpMethod.Get, $"{editUrl}/bundles", accessToken));
                var apks = JsonUtility.FromJson<ApkList>(await SendAsync(HttpMethod.Get, $"{editUrl}/apks", accessToken));
                var tracks = JsonUtility.FromJson<TrackList>(await SendAsync(HttpMethod.Get, $"{editUrl}/tracks", accessToken));
                return CollectMaxVersionCode(bundles, apks, tracks);
            }
            finally
            {
                await SendAsync(HttpMethod.Delete, editUrl, accessToken);
            }
        }

        private static int CollectMaxVersionCode(BundleList bundles, ApkList apks, TrackList tracks)
        {
            var max = GUAndroidVersionUtility.NoCode;
            foreach (var entry in bundles?.bundles ?? Array.Empty<VersionCodeEntry>()) max = Math.Max(max, entry.versionCode);
            foreach (var entry in apks?.apks ?? Array.Empty<VersionCodeEntry>()) max = Math.Max(max, entry.versionCode);

            foreach (var track in tracks?.tracks ?? Array.Empty<Track>())
            {
                foreach (var release in track.releases ?? Array.Empty<TrackRelease>())
                {
                    foreach (var code in release.versionCodes ?? Array.Empty<string>())
                    {
                        if (int.TryParse(code, out var value)) max = Math.Max(max, value);
                    }
                }
            }

            return max;
        }

        private static async Task<string> RequestAccessTokenAsync(ServiceAccountKey key)
        {
            var tokenUri = string.IsNullOrEmpty(key.token_uri) ? DefaultTokenUri : key.token_uri;
            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer" },
                { "assertion", CreateSignedJwt(key, tokenUri) }
            });

            using var response = await Http.PostAsync(tokenUri, form);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Xin access token thất bại ({(int)response.StatusCode}): {body}");

            return JsonUtility.FromJson<TokenResponse>(body).access_token;
        }

        private static async Task<string> SendAsync(HttpMethod method, string url, string accessToken)
        {
            using var request = new HttpRequestMessage(method, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await Http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"{method} {url} thất bại ({(int)response.StatusCode}): {body}");

            return body;
        }

        private static string CreateSignedJwt(ServiceAccountKey key, string tokenUri)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var header = EncodeBase64Url(Encoding.UTF8.GetBytes("{\"alg\":\"RS256\",\"typ\":\"JWT\"}"));
            var claims = EncodeBase64Url(Encoding.UTF8.GetBytes(
                $"{{\"iss\":\"{key.client_email}\",\"scope\":\"{Scope}\",\"aud\":\"{tokenUri}\",\"iat\":{now},\"exp\":{now + 3600}}}"));
            var unsigned = $"{header}.{claims}";

            using var rsa = RSA.Create();
            rsa.ImportParameters(ReadPkcs8RsaKey(key.private_key));
            var signature = rsa.SignData(Encoding.ASCII.GetBytes(unsigned), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return $"{unsigned}.{EncodeBase64Url(signature)}";
        }

        private static string EncodeBase64Url(byte[] data)
        {
            return Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        /// <summary>
        /// Tự đọc PEM PKCS#8 ("BEGIN PRIVATE KEY") rồi <c>ImportParameters</c> — không phụ thuộc <c>ImportPkcs8PrivateKey</c>/<c>ImportFromPem</c> vốn không chắc có trên Mono của Unity.
        /// PrivateKeyInfo ::= SEQUENCE { version, algorithm, OCTET STRING(RSAPrivateKey) }
        /// RSAPrivateKey ::= SEQUENCE { version, n, e, d, p, q, dp, dq, qinv }
        /// </summary>
        private static RSAParameters ReadPkcs8RsaKey(string pem)
        {
            var base64 = pem.Replace("-----BEGIN PRIVATE KEY-----", string.Empty)
                .Replace("-----END PRIVATE KEY-----", string.Empty)
                .Replace("\n", string.Empty).Replace("\r", string.Empty).Trim();
            var der = Convert.FromBase64String(base64);

            var offset = 0;
            ReadDerHeader(der, ref offset, 0x30);
            SkipDerElement(der, ref offset);
            SkipDerElement(der, ref offset);
            ReadDerHeader(der, ref offset, 0x04);
            ReadDerHeader(der, ref offset, 0x30);
            SkipDerElement(der, ref offset);

            var modulus = ReadDerInteger(der, ref offset);
            var exponent = ReadDerInteger(der, ref offset);
            var halfLength = (modulus.Length + 1) / 2;
            return new RSAParameters
            {
                Modulus = modulus,
                Exponent = exponent,
                D = PadLeft(ReadDerInteger(der, ref offset), modulus.Length),
                P = PadLeft(ReadDerInteger(der, ref offset), halfLength),
                Q = PadLeft(ReadDerInteger(der, ref offset), halfLength),
                DP = PadLeft(ReadDerInteger(der, ref offset), halfLength),
                DQ = PadLeft(ReadDerInteger(der, ref offset), halfLength),
                InverseQ = PadLeft(ReadDerInteger(der, ref offset), halfLength)
            };
        }

        /// <summary>Đọc tag + length, để offset ở đầu nội dung, trả độ dài nội dung.</summary>
        private static int ReadDerHeader(byte[] der, ref int offset, byte expectedTag)
        {
            if (der[offset] != expectedTag)
                throw new InvalidDataException($"private_key sai định dạng PKCS#8 (tag 0x{der[offset]:X2} tại {offset}, cần 0x{expectedTag:X2}).");
            offset++;

            int length = der[offset++];
            if (length < 0x80) return length;

            var byteCount = length & 0x7F;
            length = 0;
            for (var i = 0; i < byteCount; i++) length = (length << 8) | der[offset++];
            return length;
        }

        private static void SkipDerElement(byte[] der, ref int offset)
        {
            var length = ReadDerHeader(der, ref offset, der[offset]);
            offset += length;
        }

        private static byte[] ReadDerInteger(byte[] der, ref int offset)
        {
            var length = ReadDerHeader(der, ref offset, 0x02);
            var start = offset;
            offset += length;

            // Bỏ byte 0x00 đệm dấu của INTEGER dương.
            while (length > 1 && der[start] == 0)
            {
                start++;
                length--;
            }

            var value = new byte[length];
            Buffer.BlockCopy(der, start, value, 0, length);
            return value;
        }

        private static byte[] PadLeft(byte[] value, int length)
        {
            if (value.Length >= length) return value;

            var padded = new byte[length];
            Buffer.BlockCopy(value, 0, padded, length - value.Length, value.Length);
            return padded;
        }
    }
}
#endif
