using System.Collections.ObjectModel;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleVPNApp.Models;
using SimpleVPNApp.Services;

namespace SimpleVPNApp.ViewModels;

/// <summary>
/// 메인 화면의 비즈니스 로직 및 트레이 아이콘 명령을 관리하는 ViewModel입니다.
/// </summary>
public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly VpnGateService _vpnGateService = new VpnGateService();
    private readonly NetworkInfoService _networkInfoService = new NetworkInfoService();
    private readonly OutlineManagementApiService _outlineManagementApiService = new();
    private readonly OutlineServerBootstrapService _outlineServerBootstrapService = new();
    private IVpnService _vpnService;
    private CancellationTokenSource? _externalClientMonitorCts;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _statusMessage = "보호되지 않음";

    [ObservableProperty]
    private string _buttonContent = "VPN 연결하기";

    [ObservableProperty]
    private ObservableCollection<VpnServer> _servers = new();

    [ObservableProperty]
    private VpnServer? _selectedServer;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _connectionDetails = "연결 준비 상태";

    [ObservableProperty]
    private ObservableCollection<VpnConnectionOption> _connectionOptions = new();

    [ObservableProperty]
    private VpnConnectionOption? _selectedConnectionOption;

    [ObservableProperty]
    private string _publicIp = "조회 중...";

    [ObservableProperty]
    private string _connectedServerIp = "-";

    [ObservableProperty]
    private string _trayToolTipText = "SimpleVPN - 보안 연동 대기 중";

    [ObservableProperty]
    private string _chinaModeAccessKey = string.Empty;

    [ObservableProperty]
    private bool _isChinaModeSelected;

    [ObservableProperty]
    private string _serverLibraryTitle = "Server Library";

    [ObservableProperty]
    private string _serverLibrarySubtitle = "Select a live endpoint and switch locations in one click.";

    [ObservableProperty]
    private string _chinaModeState = "portable 엔진 대기";

    [ObservableProperty]
    private string _outlineApiUrl = string.Empty;

    [ObservableProperty]
    private string _outlineCertSha256 = string.Empty;

    [ObservableProperty]
    private string _outlineAccessKeyName = "SimpleVPN China Mode";

    [ObservableProperty]
    private string _outlineSshHost = string.Empty;

    [ObservableProperty]
    private string _outlineSshUser = "root";

    [ObservableProperty]
    private string _outlineSshKeyPath = string.Empty;

    [ObservableProperty]
    private string _outlineProvisionHostname = string.Empty;

    [ObservableProperty]
    private string _outlineProvisionPort = "443";

    [ObservableProperty]
    private string _outlineProvisionStatus = "대기 중";

    public MainViewModel()
    {
        _vpnService = new WindowsBuiltInVpnService();
        _vpnService.StatusChanged += OnVpnStatusChanged;

        ConnectionOptions = new ObservableCollection<VpnConnectionOption>
        {
            new() { Key = "windows", DisplayName = "Windows 기본 VPN", Description = "별도 설치 없이 Windows 기본 L2TP/IPsec 사용" },
            new() { Key = "openvpn", DisplayName = "OpenVPN", Description = "OpenVPN GUI가 설치된 경우 OpenVPN 프로필 사용" },
            new() { Key = "china", DisplayName = "China Mode", Description = "중국 환경용 우회 프로필. 설치 없이 portable sing-box와 Outline Access Key 사용", RequiresCustomEndpoint = true, SetupHint = "공개 VPN Gate 서버 대신 `ss://` 형식의 Outline Access Key를 사용하세요. `SimpleVPNApp\\Runtime\\sing-box\\` 아래에 `sing-box.exe`와 필요한 DLL을 두면 앱이 직접 실행합니다." }
        };
        SelectedConnectionOption = ConnectionOptions[0];
        // 최신 버전의 .NET 9을 고려하여 비동기 데이터 초기 로드
        _ = FetchServersAsync();
        _ = RefreshIpInfoAsync();
    }

    /// <summary>
    /// 트레이 아이콘 등을 통해 창을 다시 표시합니다.
    /// </summary>
    [RelayCommand]
    private void ShowWindow()
    {
        Application.Current.MainWindow.Show();
        Application.Current.MainWindow.Activate();
    }

    /// <summary>
    /// 앱을 완전히 종료합니다.
    /// </summary>
    [RelayCommand]
    private void ExitApp()
    {
        Dispose();

        if (Application.Current.MainWindow is MainWindow main)
        {
            main.AppExit();
        }
    }

    [RelayCommand]
    private async Task FetchServersAsync()
    {
        await RunOnUiAsync(() =>
        {
            IsLoading = true;
            StatusMessage = "서버 목록 동기화 중...";
            ConnectionDetails = "VPN Gate 서버 목록을 가져오는 중입니다.";
        }).ConfigureAwait(false);

        try
        {
            var fetchedServers = await _vpnGateService.GetServersAsync().ConfigureAwait(false);

            await RunOnUiAsync(() =>
            {
                Servers.Clear();
                foreach (var server in fetchedServers) Servers.Add(server);

                if (Servers.Count > 0 && SelectedServer == null) SelectedServer = Servers[0];

                StatusMessage = Servers.Count > 0
                    ? $"대기 중 ({Servers.Count}개 서버 발견)"
                    : "서버 목록을 가져오지 못했습니다.";
                ConnectionDetails = Servers.Count > 0
                    ? "서버를 선택한 뒤 연결 버튼을 누르세요."
                    : "다시 시도하거나 네트워크 상태를 확인해 주세요.";
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await RunOnUiAsync(() =>
            {
                StatusMessage = $"서버 목록 조회 실패: {ex.Message}";
                ConnectionDetails = "서버 목록을 불러오지 못했습니다.";
            }).ConfigureAwait(false);
        }
        finally
        {
            await RunOnUiAsync(() =>
            {
                IsLoading = false;
            }).ConfigureAwait(false);
        }
    }

    [RelayCommand]
    private async Task LoadExistingServerAccessInfoAsync()
    {
        try
        {
            await RunOnUiAsync(() =>
            {
                IsLoading = true;
                OutlineProvisionStatus = "기존 서버에서 access.txt 조회 중...";
            }).ConfigureAwait(false);

            var existing = await _outlineServerBootstrapService.ReadExistingServerAccessAsync(
                OutlineSshHost,
                OutlineSshUser,
                OutlineSshKeyPath).ConfigureAwait(false);

            await RunOnUiAsync(() =>
            {
                OutlineApiUrl = existing.ManagementApiUrl;
                OutlineCertSha256 = existing.CertificateSha256;
                OutlineProvisionStatus = "apiUrl 및 certSha256 자동 입력 완료";
                ConnectionDetails = "기존 Outline 서버의 access.txt를 읽어 관리 정보가 채워졌습니다.";
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await RunOnUiAsync(() =>
            {
                OutlineProvisionStatus = $"access.txt 조회 실패: {ex.Message}";
                ConnectionDetails = "SSH 접속 정보와 `/opt/outline/access.txt` 접근 권한을 확인해 주세요.";
            }).ConfigureAwait(false);
        }
        finally
        {
            await RunOnUiAsync(() => IsLoading = false).ConfigureAwait(false);
        }
    }

    [RelayCommand]
    private async Task GenerateAccessKeyFromExistingServerAsync()
    {
        try
        {
            await RunOnUiAsync(() =>
            {
                IsLoading = true;
                OutlineProvisionStatus = "기존 Outline 서버에서 Access Key 생성 중...";
            }).ConfigureAwait(false);

            var result = await _outlineManagementApiService.CreateAccessKeyAsync(
                OutlineApiUrl,
                OutlineCertSha256,
                OutlineAccessKeyName).ConfigureAwait(false);

            await RunOnUiAsync(() =>
            {
                ChinaModeAccessKey = result.AccessUrl;
                OutlineProvisionStatus = "기존 서버에서 Access Key 생성 완료";
                ConnectionDetails = "새 Access Key가 생성되어 China Mode 입력칸에 채워졌습니다.";
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await RunOnUiAsync(() =>
            {
                OutlineProvisionStatus = $"기존 서버 생성 실패: {ex.Message}";
                ConnectionDetails = "Outline 관리 API 설정을 확인해 주세요.";
            }).ConfigureAwait(false);
        }
        finally
        {
            await RunOnUiAsync(() => IsLoading = false).ConfigureAwait(false);
        }
    }

    [RelayCommand]
    private async Task BootstrapOutlineServerAsync()
    {
        try
        {
            await RunOnUiAsync(() =>
            {
                IsLoading = true;
                OutlineProvisionStatus = "새 Outline 서버 설치 및 초기화 중...";
            }).ConfigureAwait(false);

            var bootstrap = await _outlineServerBootstrapService.BootstrapAsync(
                OutlineSshHost,
                OutlineSshUser,
                OutlineSshKeyPath,
                OutlineProvisionHostname,
                OutlineProvisionPort).ConfigureAwait(false);

            var accessKey = await _outlineManagementApiService.CreateAccessKeyAsync(
                bootstrap.ManagementApiUrl,
                bootstrap.CertificateSha256,
                OutlineAccessKeyName).ConfigureAwait(false);

            await RunOnUiAsync(() =>
            {
                OutlineApiUrl = bootstrap.ManagementApiUrl;
                OutlineCertSha256 = bootstrap.CertificateSha256;
                ChinaModeAccessKey = accessKey.AccessUrl;
                OutlineProvisionStatus = "새 Outline 서버 생성 및 Access Key 발급 완료";
                ConnectionDetails = "새 서버가 준비되었고 China Mode 입력칸에 새 Access Key가 채워졌습니다.";
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await RunOnUiAsync(() =>
            {
                OutlineProvisionStatus = $"새 서버 생성 실패: {ex.Message}";
                ConnectionDetails = "SSH 접속 정보와 서버 권한을 확인해 주세요.";
            }).ConfigureAwait(false);
        }
        finally
        {
            await RunOnUiAsync(() => IsLoading = false).ConfigureAwait(false);
        }
    }

    [RelayCommand]
    private async Task ToggleConnectionAsync()
    {
        if (SelectedServer == null)
        {
            if (SelectedConnectionOption?.RequiresCustomEndpoint == true)
            {
                try
                {
                    await RunOnUiAsync(() => IsLoading = true).ConfigureAwait(false);
                    await _vpnService.ConnectAsync(new VpnServer()).ConfigureAwait(false);

                    await RunOnUiAsync(() =>
                    {
                        if (SelectedConnectionOption.UsesExternalClient)
                        {
                            IsConnected = false;
                            StatusMessage = "China Mode 외부 엔진 호출됨";
                            ButtonContent = "China Mode 다시 열기";
                            ConnectionDetails = "China Mode 외부 엔진이 열렸습니다. 앱 밖에서 연결 승인을 진행해 주세요.";
                            ChinaModeState = "연결 승인 대기";
                        }
                        else
                        {
                            IsConnected = _vpnService.IsConnected;
                            StatusMessage = _vpnService.IsConnected ? "China Mode 연결됨" : "China Mode 대기";
                            ButtonContent = _vpnService.IsConnected ? "연결 해제" : "VPN 연결하기";
                            ConnectionDetails = _vpnService.IsConnected
                                ? "portable China Mode 엔진이 활성화되었습니다."
                                : ConnectionDetails;
                            ChinaModeState = _vpnService.IsConnected ? "엔진 실행 중" : "엔진 대기";
                        }
                    }).ConfigureAwait(false);

                    if (SelectedConnectionOption.UsesExternalClient)
                    {
                        StartExternalClientMonitor();
                    }
                }
                catch (Exception ex)
                {
                    await RunOnUiAsync(() =>
                    {
                        IsConnected = false;
                        ButtonContent = "VPN 연결하기";
                        StatusMessage = $"오류: {ex.Message}";
                        ConnectionDetails = "China Mode 설정을 확인해 주세요.";
                        ConnectedServerIp = "-";
                    }).ConfigureAwait(false);
                }
                finally
                {
                    await RunOnUiAsync(() => IsLoading = false).ConfigureAwait(false);
                }

                return;
            }

            await RunOnUiAsync(() =>
            {
                StatusMessage = "서버를 선택해 주세요.";
                ConnectionDetails = "먼저 목록에서 연결할 서버를 고르세요.";
            }).ConfigureAwait(false);
            return;
        }

        try
        {
            await RunOnUiAsync(() => IsLoading = true).ConfigureAwait(false);

            if (IsConnected)
            {
                await RunOnUiAsync(() => StatusMessage = "연결 해제 중...").ConfigureAwait(false);
                await _vpnService.DisconnectAsync().ConfigureAwait(false);

                await RunOnUiAsync(() =>
                {
                    IsConnected = false;
                    StatusMessage = "보호되지 않음";
                    ButtonContent = "VPN 연결하기";
                    ConnectionDetails = "VPN 연결이 해제되었습니다.";
                    ConnectedServerIp = "-";
                }).ConfigureAwait(false);
                await RefreshIpInfoAsync().ConfigureAwait(false);
            }
            else
            {
                var selectedServer = SelectedServer;

                await RunOnUiAsync(() =>
                {
                    StatusMessage = $"{selectedServer.CountryLong} 연결 시도...";
                    ConnectionDetails = $"{SelectedConnectionOption?.DisplayName ?? "VPN"} 연결을 시작합니다.";
                }).ConfigureAwait(false);

                await _vpnService.ConnectAsync(selectedServer).ConfigureAwait(false);

                await RunOnUiAsync(() =>
                {
                    IsConnected = true;
                    StatusMessage = $"VPN 연결됨 ({selectedServer.CountryShort})";
                    ButtonContent = "연결 해제";
                    ConnectionDetails = $"{selectedServer.HostName} 터널 연결이 활성화되었습니다.";
                    ConnectedServerIp = selectedServer.IP;
                }).ConfigureAwait(false);
                await RefreshIpInfoAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            await RunOnUiAsync(() =>
            {
                IsConnected = false;
                ButtonContent = "VPN 연결하기";
                StatusMessage = $"오류: {ex.Message}";
                ConnectionDetails = "아래 최근 로그를 확인해 주세요.";
                ConnectedServerIp = "-";
            }).ConfigureAwait(false);
            await RefreshIpInfoAsync().ConfigureAwait(false);
        }
        finally
        {
            await RunOnUiAsync(() =>
            {
                IsLoading = false;
            }).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        _vpnService.StatusChanged -= OnVpnStatusChanged;
        _vpnService.Dispose();
        StopExternalClientMonitor();
        _vpnGateService.Dispose();
    }

    private static Task RunOnUiAsync(Action action) =>
        Application.Current.Dispatcher.InvokeAsync(action).Task;

    private void OnVpnStatusChanged(string message)
    {
        _ = RunOnUiAsync(() => ConnectionDetails = message);
    }

    partial void OnSelectedConnectionOptionChanged(VpnConnectionOption? value)
    {
        if (value == null)
        {
            return;
        }

        var oldService = _vpnService;
        _vpnService = CreateVpnService(value);
        _vpnService.StatusChanged += OnVpnStatusChanged;

        if (oldService != null)
        {
            oldService.StatusChanged -= OnVpnStatusChanged;
            oldService.Dispose();
        }

        _ = RunOnUiAsync(() =>
        {
            IsChinaModeSelected = value.Key == "china";
            ServerLibraryTitle = IsChinaModeSelected ? "Gateway Library" : "Server Library";
            ServerLibrarySubtitle = IsChinaModeSelected
                ? "China Mode uses your own resilient endpoint instead of public VPN Gate nodes."
                : "Select a live endpoint and switch locations in one click.";
            ChinaModeState = IsChinaModeSelected ? "portable 엔진 대기" : "사용 안 함";
            IsConnected = false;
            ButtonContent = "VPN 연결하기";
            ConnectionDetails = $"{value.DisplayName} 모드가 선택되었습니다. {value.Description}";
            ConnectedServerIp = "-";
        });
        StopExternalClientMonitor();
        _ = RefreshIpInfoAsync();
    }

    partial void OnChinaModeAccessKeyChanged(string value)
    {
        if (SelectedConnectionOption?.Key != "china")
        {
            return;
        }

        var oldService = _vpnService;
        _vpnService = CreateVpnService(SelectedConnectionOption, value);
        _vpnService.StatusChanged += OnVpnStatusChanged;
        oldService.StatusChanged -= OnVpnStatusChanged;
        oldService.Dispose();

        _ = RunOnUiAsync(() =>
        {
            ConnectionDetails = string.IsNullOrWhiteSpace(value)
                ? "China Mode용 Outline Access Key를 입력해 주세요."
                : "China Mode Access Key가 입력되었습니다. portable 엔진으로 연결을 시도할 수 있습니다.";
        });
    }

    private void StartExternalClientMonitor()
    {
        StopExternalClientMonitor();
        _externalClientMonitorCts = new CancellationTokenSource();
        _ = MonitorExternalClientConnectionAsync(_externalClientMonitorCts.Token);
    }

    private void StopExternalClientMonitor()
    {
        if (_externalClientMonitorCts == null)
        {
            return;
        }

        _externalClientMonitorCts.Cancel();
        _externalClientMonitorCts.Dispose();
        _externalClientMonitorCts = null;
    }

    private async Task MonitorExternalClientConnectionAsync(CancellationToken cancellationToken)
    {
        var baselineIp = PublicIp;

        try
        {
            for (var attempt = 0; attempt < 12; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);

                string currentIp;
                try
                {
                    currentIp = await _networkInfoService.GetPublicIpAsync().ConfigureAwait(false) ?? "확인 불가";
                }
                catch
                {
                    currentIp = "확인 불가";
                }

                await RunOnUiAsync(() =>
                {
                    PublicIp = currentIp;
                    TrayToolTipText = $"SimpleVPN - China Mode{Environment.NewLine}Public IP: {PublicIp}";
                }).ConfigureAwait(false);

                if (!string.Equals(currentIp, baselineIp, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(currentIp, "확인 불가", StringComparison.OrdinalIgnoreCase))
                {
                    await RunOnUiAsync(() =>
                    {
                        IsConnected = true;
                        StatusMessage = "China Mode 연결 감지";
                        ButtonContent = "China Mode 다시 열기";
                        ConnectionDetails = "공인 IP 변경이 감지되었습니다. China Mode 연결이 활성화된 것으로 보입니다.";
                        ChinaModeState = "공인 IP 변경 감지";
                    }).ConfigureAwait(false);
                    return;
                }

                await RunOnUiAsync(() =>
                {
                    ChinaModeState = $"연결 대기 중 ({attempt + 1}/12)";
                    ConnectionDetails = "China Mode 엔진에서 연결을 확인해 주세요. 공인 IP 변화를 계속 확인 중입니다.";
                }).ConfigureAwait(false);
            }

            await RunOnUiAsync(() =>
            {
                ChinaModeState = "자동 감지 시간 초과";
                ConnectionDetails = "앱에서 공인 IP 변화를 확인하지 못했습니다. China Mode 엔진 상태를 직접 확인해 주세요.";
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static IVpnService CreateVpnService(VpnConnectionOption? option, string? chinaModeAccessKey = null) =>
        option?.Key switch
        {
            "openvpn" => new OpenVpnService(),
            "china" => new ChinaOptimizedVpnService(chinaModeAccessKey ?? string.Empty),
            _ => new WindowsBuiltInVpnService()
        };

    private async Task RefreshIpInfoAsync()
    {
        string publicIp;

        try
        {
            publicIp = await _networkInfoService.GetPublicIpAsync().ConfigureAwait(false) ?? "확인 불가";
        }
        catch
        {
            publicIp = "확인 불가";
        }

        await RunOnUiAsync(() =>
        {
            PublicIp = publicIp;
            TrayToolTipText = IsConnected
                ? $"SimpleVPN - 연결됨{Environment.NewLine}Public IP: {PublicIp}{Environment.NewLine}Server IP: {ConnectedServerIp}"
                : $"SimpleVPN - 연결 대기{Environment.NewLine}Public IP: {PublicIp}";
        }).ConfigureAwait(false);
    }
}
