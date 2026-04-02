namespace SimpleVPN.Mobile.Services;

public interface IMobileVpnPlatformService
{
    bool IsSupported { get; }
    string PlatformName { get; }
    Task<string> RequestPermissionSummaryAsync();
    Task ConnectAsync(string connectionPayload, CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
