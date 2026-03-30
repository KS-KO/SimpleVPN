namespace SimpleVPNApp.Models;

/// <summary>
/// 사용자가 선택할 수 있는 VPN 연결 방식을 나타냅니다.
/// </summary>
public class VpnConnectionOption
{
    public string Key { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool RequiresCustomEndpoint { get; init; }
    public string SetupHint { get; init; } = string.Empty;
    public bool UsesExternalClient { get; init; }

    public override string ToString() => DisplayName;
}
