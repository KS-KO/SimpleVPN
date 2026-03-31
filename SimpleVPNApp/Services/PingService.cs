using System;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace SimpleVPNApp.Services;

/// <summary>
/// VPN 서버의 지연 시간(Ping)을 측정하여 성능을 평가합니다.
/// </summary>
public static class PingService
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMilliseconds(2000);

    /// <summary>
    /// 단일 서버의 지연 시간을 측정합니다. 실패 시 int.MaxValue를 반환합니다.
    /// </summary>
    public static async Task<int> GetLatencyAsync(string hostOrIp)
    {
        if (string.IsNullOrWhiteSpace(hostOrIp)) return int.MaxValue;

        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(hostOrIp, (int)DefaultTimeout.TotalMilliseconds).ConfigureAwait(false);

            if (reply.Status == IPStatus.Success)
            {
                return (int)reply.RoundtripTime;
            }
        }
        catch
        {
            // 타임아웃 또는 네트워크 오류
        }

        return int.MaxValue;
    }
}
