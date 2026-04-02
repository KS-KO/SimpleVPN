using System;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using SimpleVPN.Core.Models;

namespace SimpleVPNApp.Helpers;

/// <summary>
/// VPN 서비스들의 공통 통계 계산 로직을 제공합니다.
/// </summary>
public static class StatisticsHelper
{
    /// <summary>
    /// 네트워크 인터페이스 이름을 기반으로 통계를 조회합니다.
    /// </summary>
    public static VpnStatistics GetInterfaceStatistics(string interfaceNameHint, DateTime? startTime, long lastReceived, long lastSent)
    {
        if (startTime == null) return new VpnStatistics { Duration = TimeSpan.Zero };

        try
        {
            var interfaceStats = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(i => i.Name.Contains(interfaceNameHint, StringComparison.OrdinalIgnoreCase) || 
                                     i.Description.Contains(interfaceNameHint, StringComparison.OrdinalIgnoreCase))
                ?.GetIPStatistics();

            if (interfaceStats != null)
            {
                var currentReceived = interfaceStats.BytesReceived;
                var currentSent = interfaceStats.BytesSent;

                return new VpnStatistics
                {
                    BytesReceived = currentReceived,
                    BytesSent = currentSent,
                    DownloadSpeed = lastReceived > 0 ? currentReceived - lastReceived : 0,
                    UploadSpeed = lastSent > 0 ? currentSent - lastSent : 0,
                    Duration = DateTime.Now - startTime.Value
                };
            }
        }
        catch
        {
            // Ignore
        }

        return new VpnStatistics { Duration = DateTime.Now - startTime.Value };
    }

    /// <summary>
    /// 프로세스 입출력을 기반으로 통계를 조회합니다. (주의: 정밀도가 인터페이스보다 낮을 수 있음)
    /// </summary>
    public static VpnStatistics GetProcessStatistics(Process? process, DateTime? startTime, long lastReceived, long lastSent)
    {
        if (process == null || process.HasExited || startTime == null) 
            return new VpnStatistics { Duration = TimeSpan.Zero };

        try
        {
            // 프로세스 네트워크 입출력 측정은 Windows에서 복잡하므로 
            // 여기서는 실제 구현 대신 Duration만 정확히 제공하고 트래픽은 가상으로 보조하거나 0으로 둡니다.
            // (상세 구현은 WMI나 PerformanceCounter가 필요)
            return new VpnStatistics
            {
                Duration = DateTime.Now - startTime.Value
            };
        }
        catch
        {
            return new VpnStatistics { Duration = DateTime.Now - startTime.Value };
        }
    }
}
