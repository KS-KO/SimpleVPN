using Android.App;
using Android.Content;
using Android.Net;

namespace SimpleVPN.Mobile.Services;

public sealed class AndroidMobileVpnPlatformService : IMobileVpnPlatformService
{
    public bool IsSupported => true;
    public string PlatformName => "Android";

    public Task<string> RequestPermissionSummaryAsync()
    {
        var context = Android.App.Application.Context;
        var prepareIntent = VpnService.Prepare(context);

        return Task.FromResult(
            prepareIntent == null
                ? "Android VPN permission is already granted on this device."
                : "Android VPN permission is required. The next step is to launch the system consent screen from the UI.");
    }

    public Task ConnectAsync(string connectionPayload, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionPayload))
        {
            throw new InvalidOperationException("A connection payload is required before starting the Android VPN service.");
        }

        var context = Android.App.Application.Context;
        var prepareIntent = VpnService.Prepare(context);
        if (prepareIntent != null)
        {
            throw new InvalidOperationException("Android VPN permission has not been granted yet.");
        }

        var intent = new Intent(context, typeof(SimpleVpnTunnelService));
        intent.SetAction(SimpleVpnTunnelService.ActionConnect);
        intent.PutExtra(SimpleVpnTunnelService.ExtraConnectionPayload, connectionPayload);

        context.StartService(intent);
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        var context = Android.App.Application.Context;
        var intent = new Intent(context, typeof(SimpleVpnTunnelService));
        intent.SetAction(SimpleVpnTunnelService.ActionDisconnect);
        context.StartService(intent);
        return Task.CompletedTask;
    }
}
