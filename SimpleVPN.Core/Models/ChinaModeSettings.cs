namespace SimpleVPN.Core.Models;

public class ChinaModeSettings
{
    public string SelectedProfileKey { get; set; } = "outline";
    public string OutlineAccessKey { get; set; } = string.Empty;
    public string OutlineApiUrl { get; set; } = string.Empty;
    public string OutlineCertSha256 { get; set; } = string.Empty;
    public string OutlineSshHost { get; set; } = string.Empty;
    public string OutlineSshUser { get; set; } = "root";
    public string OutlineSshKeyPath { get; set; } = string.Empty;
    public string OutlineProvisionHostname { get; set; } = string.Empty;
    public string OutlineProvisionPort { get; set; } = "443";
    public string VlessRealityServer { get; set; } = string.Empty;
    public string VlessRealityPort { get; set; } = "443";
    public string VlessRealityUuid { get; set; } = string.Empty;
    public string VlessRealityPublicKey { get; set; } = string.Empty;
    public string VlessRealityShortId { get; set; } = string.Empty;
    public string VlessRealityServerName { get; set; } = string.Empty;
    public string VlessRealityFingerprint { get; set; } = "chrome";
    public string TrojanServer { get; set; } = string.Empty;
    public string TrojanPort { get; set; } = "443";
    public string TrojanPassword { get; set; } = string.Empty;
    public string TrojanServerName { get; set; } = string.Empty;
    public string TrojanFingerprint { get; set; } = "chrome";
}
