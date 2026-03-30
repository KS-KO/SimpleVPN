namespace SimpleVPNApp.Models;

public sealed class OutlineAccessKeyResult
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string AccessUrl { get; init; } = string.Empty;
    public string ManagementApiUrl { get; init; } = string.Empty;
    public string CertificateSha256 { get; init; } = string.Empty;
}
