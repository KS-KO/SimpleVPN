using System;

namespace SimpleVPNApp.Models;

/// <summary>
/// VPN 연결 통계 정보를 담는 모델입니다.
/// </summary>
public record VpnStatistics
{
    /// <summary>
    /// 수신한 총 바이트 수
    /// </summary>
    public long BytesReceived { get; init; }

    /// <summary>
    /// 송신한 총 바이트 수
    /// </summary>
    public long BytesSent { get; init; }

    /// <summary>
    /// 현재 다운로드 속도 (Bytes/s)
    /// </summary>
    public long DownloadSpeed { get; init; }

    /// <summary>
    /// 현재 업로드 속도 (Bytes/s)
    /// </summary>
    public long UploadSpeed { get; init; }

    /// <summary>
    /// 연결 유지 시간
    /// </summary>
    public TimeSpan Duration { get; init; }
}
