using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using SimpleVPNApp.Models;

namespace SimpleVPNApp.Services;

/// <summary>
/// VPN Gate Open API를 사용하여 서버 목록을 관리하는 서비스입니다.
/// </summary>
public class VpnGateService : IDisposable
{
    private static readonly HttpClient _httpClient = new HttpClient();
    private const string ApiUrl = "https://www.vpngate.net/api/iphone/";
    private bool _disposed = false;

    /// <summary>
    /// 웹에서 무료 VPN 서버 리스트를 비동기로 가져옵니다.
    /// (Rule: 비동기 패턴 및 ConfigureAwait 준수)
    /// </summary>
    public async Task<List<VpnServer>> GetServersAsync()
    {
        var servers = new List<VpnServer>();

        try
        {
            // 네트워크 제한 시간이 지나면 예외 발생 가능
            using var response = await _httpClient.GetAsync(ApiUrl).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var data = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            
            // CSV 파싱 시작 (Rule: 메모리 할당 최소화를 위해 StringReader 사용)
            using (var reader = new StringReader(data))
            {
                string? line;
                bool startParsing = false;

                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("*"))
                    {
                        if (line.Contains("*vpn_servers")) continue;
                        if (line.Contains("!")) break; // 데이터의 종료 지점
                        continue;
                    }

                    if (line.StartsWith("#")) // 헤더 영역은 스킵
                    {
                        startParsing = true;
                        continue;
                    }

                    if (startParsing)
                    {
                        var parts = line.Split(',');
                        if (parts.Length >= 15)
                        {
                            servers.Add(new VpnServer
                            {
                                HostName = parts[0],
                                IP = parts[1],
                                Score = int.TryParse(parts[2], out var s) ? s : 0,
                                Ping = int.TryParse(parts[3], out var p) ? p : 0,
                                Speed = long.TryParse(parts[4], out var v) ? v : 0,
                                CountryLong = parts[5],
                                CountryShort = parts[6],
                                NumVpnSessions = int.TryParse(parts[7], out var n) ? n : 0,
                                OpenVPN_ConfigData_Base64 = parts[14]
                            });
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Error handling (Rule: 빈 catch 금지 및 구조화된 예외 처리)
            // TODO: Log the error
            System.Diagnostics.Debug.WriteLine($"Error fetching VPN list: {ex.Message}");
        }

        return servers;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            // HttpClient는 static이므로 별도 dispose 불필요 (재사용)
            _disposed = true;
        }
    }
}
