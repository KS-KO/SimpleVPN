namespace SimpleVPNApp.Models;

/// <summary>
/// China Mode에서 선택할 수 있는 우회 프로필을 표현합니다.
/// </summary>
public class ChinaModeProfileOption
{
    public string Key { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;

    public override string ToString() => DisplayName;
}
