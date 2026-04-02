namespace SimpleVPN.Core.Models;

public class ChinaModeSavedProfile
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = "기본 프로필";
    public ChinaModeSettings Settings { get; init; } = new();
}
