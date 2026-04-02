using System;
using System.Threading.Tasks;
using SimpleVPN.Core.Models;

namespace SimpleVPNApp.Services;

/// <summary>
/// VPN 연결 및 통신을 담당하는 서비스 인터페이스입니다.
/// </summary>
public interface IVpnService : IDisposable
{
    bool IsConnected { get; }
    event Action<string>? StatusChanged;
    Task ConnectAsync(VpnServer server);
    Task DisconnectAsync();
    VpnStatistics GetStatistics();
}

/// <summary>
/// VPN 서비스 프로토타입 구현체입니다.
/// Rule: IDisposable 강제 및 리소스 관리 예시
/// </summary>
public class MockVpnService : IVpnService
{
    private bool _disposed = false;
    // Rule: 하드웨어 또는 네트워크 리소스 점유 가상
    private System.Net.Sockets.Socket? _fakeSocket;
    private DateTime? _startTime;
    private long _totalReceived;
    private long _totalSent;
    private readonly Random _random = new();

    public bool IsConnected { get; private set; }
    public event Action<string>? StatusChanged;

    public MockVpnService()
    {
        // 런타임에 소켓 생성 시뮬레이션
        _fakeSocket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
    }

    public async Task ConnectAsync(VpnServer server)
    {
        if (IsConnected) return;
        
        // 가상의 서버 연결 시연
        StatusChanged?.Invoke("연결 준비 중...");
        await Task.Delay(2000).ConfigureAwait(false);
        IsConnected = true;
        _startTime = DateTime.Now;
        _totalReceived = 0;
        _totalSent = 0;
        StatusChanged?.Invoke("연결 완료");
    }

    public async Task DisconnectAsync()
    {
        if (!IsConnected) return;
        
        // 가상의 접속 해제 시연
        StatusChanged?.Invoke("연결 해제 중...");
        await Task.Delay(1000).ConfigureAwait(false);
        IsConnected = false;
        _startTime = null;
        StatusChanged?.Invoke("연결 해제 완료");
    }

    public VpnStatistics GetStatistics()
    {
        if (!IsConnected || _startTime == null)
        {
            return new VpnStatistics { Duration = TimeSpan.Zero };
        }

        // 가상 트래픽 발생
        var downlink = _random.Next(1024, 1024 * 1024 * 5); // 1KB~5MB/s
        var uplink = _random.Next(512, 1024 * 1024);     // 512B~1MB/s
        
        _totalReceived += downlink;
        _totalSent += uplink;

        return new VpnStatistics
        {
            BytesReceived = _totalReceived,
            BytesSent = _totalSent,
            DownloadSpeed = downlink,
            UploadSpeed = uplink,
            Duration = DateTime.Now - _startTime.Value
        };
    }

    // Rule: IDisposable 인터페이스 구현
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // 관리되는 리소스 해제
                _fakeSocket?.Dispose();
                _fakeSocket = null;
            }

            // 관리되지 않는 리소스 해제 (필요시)
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
