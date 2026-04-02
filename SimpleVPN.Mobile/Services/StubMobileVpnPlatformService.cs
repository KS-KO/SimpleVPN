namespace SimpleVPN.Mobile.Services;

public sealed class StubMobileVpnPlatformService : IMobileVpnPlatformService
{
    public bool IsSupported => false;

    public string PlatformName
    {
        get
        {
            if (DeviceInfo.Platform == DevicePlatform.Android)
            {
                return "Android";
            }

            if (DeviceInfo.Platform == DevicePlatform.iOS)
            {
                return "iOS";
            }

            return DeviceInfo.Platform.ToString();
        }
    }

    public Task<string> RequestPermissionSummaryAsync()
    {
        return Task.FromResult($"{PlatformName} VPN platform service is not wired yet.");
    }

    public Task ConnectAsync(string connectionPayload, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException($"{PlatformName} VPN platform service is not implemented yet.");
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException($"{PlatformName} VPN platform service is not implemented yet.");
    }
}
