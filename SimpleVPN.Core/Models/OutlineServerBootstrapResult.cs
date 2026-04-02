namespace SimpleVPN.Core.Models;

public sealed class OutlineServerBootstrapResult
{
    public string ManagementApiUrl { get; init; } = string.Empty;
    public string CertificateSha256 { get; init; } = string.Empty;
    public string Host { get; init; } = string.Empty;
}
