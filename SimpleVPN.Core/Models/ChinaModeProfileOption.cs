namespace SimpleVPN.Core.Models;

public class ChinaModeProfileOption
{
    public string Key { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;

    public override string ToString() => DisplayName;
}
