namespace SimpleVPN.Core.Models;

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
