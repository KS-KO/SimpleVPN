namespace SimpleVPNApp.Models;

/// <summary>
/// China Mode 연결에 필요한 프로필별 입력값을 담습니다.
/// </summary>
public class ChinaModeConnectionProfile
{
    public string ProfileType { get; init; } = "outline";
    public string AccessKey { get; init; } = string.Empty;
    public string Server { get; init; } = string.Empty;
    public int Port { get; init; }
    public string Uuid { get; init; } = string.Empty;
    public string PublicKey { get; init; } = string.Empty;
    public string ShortId { get; init; } = string.Empty;
    public string ServerName { get; init; } = string.Empty;
    public string Fingerprint { get; init; } = "chrome";
    public string Password { get; init; } = string.Empty;
}
