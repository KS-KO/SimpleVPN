using System;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using SimpleVPNApp.Models;

namespace SimpleVPNApp.Services;

public sealed class OutlineManagementApiService
{
    public async Task<OutlineAccessKeyResult> CreateAccessKeyAsync(
        string managementApiUrl,
        string certificateSha256,
        string keyName)
    {
        if (string.IsNullOrWhiteSpace(managementApiUrl))
        {
            throw new InvalidOperationException("Outline 관리 API URL을 입력해 주세요.");
        }

        using var client = CreateHttpClient(certificateSha256);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{managementApiUrl.TrimEnd('/')}/access-keys");
        request.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                name = string.IsNullOrWhiteSpace(keyName) ? "SimpleVPN China Mode" : keyName.Trim()
            }),
            Encoding.UTF8,
            "application/json");

        using var response = await client.SendAsync(request).ConfigureAwait(false);
        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;

        return new OutlineAccessKeyResult
        {
            Id = root.TryGetProperty("id", out var id) ? id.GetString() ?? string.Empty : string.Empty,
            Name = root.TryGetProperty("name", out var name) ? name.GetString() ?? string.Empty : string.Empty,
            AccessUrl = root.TryGetProperty("accessUrl", out var accessUrl) ? accessUrl.GetString() ?? string.Empty : string.Empty,
            ManagementApiUrl = managementApiUrl,
            CertificateSha256 = certificateSha256
        };
    }

    private static HttpClient CreateHttpClient(string certificateSha256)
    {
        var normalizedPin = NormalizeCertSha256(certificateSha256);
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (_, certificate, _, errors) =>
        {
            if (certificate == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(normalizedPin))
            {
                return errors == SslPolicyErrors.None || errors == SslPolicyErrors.RemoteCertificateChainErrors;
            }

            using var hasher = System.Security.Cryptography.SHA256.Create();
            var hashBytes = hasher.ComputeHash(certificate.RawData);
            var actual = BitConverter.ToString(hashBytes).Replace("-", string.Empty, StringComparison.Ordinal);
            return string.Equals(actual, normalizedPin, StringComparison.OrdinalIgnoreCase);
        };

        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
    }

    private static string NormalizeCertSha256(string value) =>
        value.Replace(":", string.Empty, StringComparison.Ordinal)
             .Replace("-", string.Empty, StringComparison.Ordinal)
             .Trim()
             .ToUpperInvariant();
}
