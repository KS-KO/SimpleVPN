using System.Collections.ObjectModel;
using System;
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
    private IVpnService _vpnService;

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

    public MainViewModel()
    {
        _vpnService = new WindowsBuiltInVpnService();
        _vpnService.StatusChanged += OnVpnStatusChanged;

        ConnectionOptions = new ObservableCollection<VpnConnectionOption>
        {
            new() { Key = "windows", DisplayName = "Windows 기본 VPN", Description = "별도 설치 없이 Windows 기본 L2TP/IPsec 사용" },
            new() { Key = "openvpn", DisplayName = "OpenVPN", Description = "OpenVPN GUI가 설치된 경우 OpenVPN 프로필 사용" }
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
    private async Task ToggleConnectionAsync()
    {
        if (SelectedServer == null)
        {
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
            IsConnected = false;
            ButtonContent = "VPN 연결하기";
            ConnectionDetails = $"{value.DisplayName} 모드가 선택되었습니다. {value.Description}";
            ConnectedServerIp = "-";
        });
        _ = RefreshIpInfoAsync();
    }

    private static IVpnService CreateVpnService(VpnConnectionOption? option) =>
        option?.Key == "openvpn"
            ? new OpenVpnService()
            : new WindowsBuiltInVpnService();

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
