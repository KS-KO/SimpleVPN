namespace SimpleVPNApp.Models;

/// <summary>
/// China Mode 입력값을 로컬에 저장하기 위한 설정 모델입니다.
/// </summary>
public class ChinaModeSettings
{
    public string SelectedProfileKey { get; init; } = "outline";
    public string OutlineAccessKey { get; init; } = string.Empty;
    public string OutlineApiUrl { get; init; } = string.Empty;
    public string OutlineCertSha256 { get; init; } = string.Empty;
    public string OutlineSshHost { get; init; } = string.Empty;
    public string OutlineSshUser { get; init; } = "root";
    public string OutlineSshKeyPath { get; init; } = string.Empty;
    public string OutlineProvisionHostname { get; init; } = string.Empty;
    public string OutlineProvisionPort { get; init; } = "443";
    public string VlessRealityServer { get; init; } = string.Empty;
    public string VlessRealityPort { get; init; } = "443";
    public string VlessRealityUuid { get; init; } = string.Empty;
    public string VlessRealityPublicKey { get; init; } = string.Empty;
    public string VlessRealityShortId { get; init; } = string.Empty;
    public string VlessRealityServerName { get; init; } = string.Empty;
    public string VlessRealityFingerprint { get; init; } = "chrome";
    public string TrojanServer { get; init; } = string.Empty;
    public string TrojanPort { get; init; } = "443";
    public string TrojanPassword { get; init; } = string.Empty;
    public string TrojanServerName { get; init; } = string.Empty;
    public string TrojanFingerprint { get; init; } = "chrome";
}
