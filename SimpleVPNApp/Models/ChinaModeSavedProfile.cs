namespace SimpleVPNApp.Models;

/// <summary>
/// China Mode 설정 슬롯 하나를 나타냅니다.
/// </summary>
public class ChinaModeSavedProfile
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = "기본 프로필";
    public ChinaModeSettings Settings { get; init; } = new();
}
