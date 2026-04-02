namespace SimpleVPN.Core.Models;

public record VpnStatistics
{
    public long BytesReceived { get; init; }
    public long BytesSent { get; init; }
    public long DownloadSpeed { get; init; }
    public long UploadSpeed { get; init; }
    public TimeSpan Duration { get; init; }
}
