using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace SimpleVPNApp.Services;

/// <summary>
/// 현재 공인 IP 정보를 조회합니다.
/// </summary>
public class NetworkInfoService
{
    private static readonly HttpClient _httpClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    public async Task<string?> GetPublicIpAsync()
    {
        using var response = await _httpClient.GetAsync("https://api.ipify.org").ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(content) ? null : content.Trim();
    }
}
