using Android.App;
using Android.Content;
using Android.Net;
using Android.OS;
using Java.IO;

namespace SimpleVPN.Mobile.Services;

[Service(
    Name = "com.simplevpn.mobile.SimpleVpnTunnelService",
    Permission = "android.permission.BIND_VPN_SERVICE",
    Exported = true)]
[IntentFilter([VpnService.ServiceInterface])]
public sealed class SimpleVpnTunnelService : VpnService
{
    public const string ActionConnect = "com.simplevpn.mobile.action.CONNECT";
    public const string ActionDisconnect = "com.simplevpn.mobile.action.DISCONNECT";
    public const string ExtraConnectionPayload = "connection_payload";

    private ParcelFileDescriptor? _tunnelInterface;

    public override IBinder? OnBind(Intent? intent)
    {
        return base.OnBind(intent);
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        var action = intent?.Action;
        if (string.Equals(action, ActionDisconnect, StringComparison.Ordinal))
        {
            StopTunnel();
            StopSelf();
            return StartCommandResult.NotSticky;
        }

        if (string.Equals(action, ActionConnect, StringComparison.Ordinal))
        {
            var payload = intent?.GetStringExtra(ExtraConnectionPayload);
            StartTunnel(payload);
        }

        return StartCommandResult.Sticky;
    }

    public override void OnRevoke()
    {
        StopTunnel();
        base.OnRevoke();
    }

    public override void OnDestroy()
    {
        StopTunnel();
        base.OnDestroy();
    }

    private void StartTunnel(string? connectionPayload)
    {
        if (string.IsNullOrWhiteSpace(connectionPayload))
        {
            return;
        }

        StopTunnel();

        var builder = new Builder(this)
            .SetSession("SimpleVPN")
            .SetMtu(1500)
            .AddAddress("10.8.0.2", 24)
            .AddRoute("0.0.0.0", 0)
            .AddDnsServer("1.1.1.1")
            .AddDnsServer("8.8.8.8");

        _tunnelInterface = builder.Establish();
    }

    private void StopTunnel()
    {
        _tunnelInterface?.Close();
        _tunnelInterface?.Dispose();
        _tunnelInterface = null;
    }
}
