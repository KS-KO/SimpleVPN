using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SimpleVPNApp.Services;

/// <summary>
/// Windows 방화벽을 제어하여 Kill Switch 기능을 구현합니다.
/// </summary>
public class FirewallService
{
    private const string KillSwitchRuleName = "SimpleVPN_KillSwitch_BlockAll";
    private const string AllowVpnServerRuleName = "SimpleVPN_Allow_VpnServer";

    public async Task EnableKillSwitchAsync(string vpnServerIp)
    {
        await Task.Run(() =>
        {
            // 1. 기존 규칙 제거
            DisableKillSwitch();

            // 2. 모든 아웃바운드 차단 규칙 추가 (낮은 우선순위처럼 보이지만 Windows 방화벽은 차단이 허용보다 우선함)
            // 실제 상용 앱은 모든 프로필의 기본 정책을 Block으로 바꾸기도 함.
            // 여기서는 특정 앱 외의 모든 트래픽을 막는 규칙을 시뮬레이션하거나 정책을 변경함.
            
            // 안전을 위해 "Block" 정책보다는 "차단 규칙"을 추가함.
            RunNetsh($"advfirewall firewall add rule name=\"{KillSwitchRuleName}\" dir=out action=block");

            // 3. VPN 서버로의 트래픽만 허용
            if (!string.IsNullOrEmpty(vpnServerIp))
            {
                RunNetsh($"advfirewall firewall add rule name=\"{AllowVpnServerRuleName}\" dir=out action=allow remoteip={vpnServerIp}");
            }

            // 4. (선택사항) 로컬 루프백이나 DNS 허용이 필요할 수 있음
        });
    }

    public void DisableKillSwitch()
    {
        RunNetsh($"advfirewall firewall delete rule name=\"{KillSwitchRuleName}\"");
        RunNetsh($"advfirewall firewall delete rule name=\"{AllowVpnServerRuleName}\"");
    }

    private static void RunNetsh(string arguments)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                }
            };
            process.Start();
            process.WaitForExit();
        }
        catch
        {
            // 권한 부족 등의 이슈 발생 가능
        }
    }
}
