namespace SimpleVPNApp.Models;

/// <summary>
/// VPN 서버 정보를 담는 데이터 모델입니다.
/// </summary>
public class VpnServer
{
    public string HostName { get; set; } = string.Empty;
    public string IP { get; set; } = string.Empty;
    public int Score { get; set; }
    public int Ping { get; set; }
    public long Speed { get; set; }
    public string CountryLong { get; set; } = string.Empty;
    public string CountryShort { get; set; } = string.Empty;
    public int NumVpnSessions { get; set; }
    public string OpenVPN_ConfigData_Base64 { get; set; } = string.Empty;

    // UI 표시용 가공 속성
    public string DisplayInfo => $"[{CountryShort}] {HostName} - {Ping}ms ({Speed / 1000000.0:F1} Mbps)";
}
