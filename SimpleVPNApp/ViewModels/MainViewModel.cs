using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleVPNApp.Models;
using SimpleVPNApp.Services;

namespace SimpleVPNApp.ViewModels;

/// <summary>
/// 메인 화면의 상태와 연결 동작을 관리합니다.
/// </summary>
public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly VpnGateService _vpnGateService = new();
    private readonly NetworkInfoService _networkInfoService = new();
    private readonly OutlineManagementApiService _outlineManagementApiService = new();
    private readonly OutlineServerBootstrapService _outlineServerBootstrapService = new();
    private readonly RealityServerBootstrapService _realityServerBootstrapService = new();
    private readonly LocalSettingsService _localSettingsService = new();
    private readonly FirewallService _firewallService = new();
    private IVpnService _vpnService;
    private readonly DispatcherTimer _statsTimer;
    private CancellationTokenSource? _externalClientMonitorCts;
    private bool _isRestoringSettings;

    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _statusMessage = "보호되지 않음";
    [ObservableProperty] private string _buttonContent = "VPN 연결하기";
    [ObservableProperty] private ObservableCollection<VpnServer> _servers = new();
    [ObservableProperty] private VpnServer? _selectedServer;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _connectionDetails = "연결 준비 상태";
    [ObservableProperty] private ObservableCollection<VpnConnectionOption> _connectionOptions = new();
    [ObservableProperty] private VpnConnectionOption? _selectedConnectionOption;
    [ObservableProperty] private string _publicIp = "조회 중...";
    [ObservableProperty] private string _connectedServerIp = "-";
    [ObservableProperty] private string _trayToolTipText = "SimpleVPN - 보호 대기 중";
    [ObservableProperty] private string _chinaModeAccessKey = string.Empty;
    [ObservableProperty] private bool _isOutlineAccessKeyVisible;
    [ObservableProperty] private ObservableCollection<ChinaModeProfileOption> _chinaModeProfiles = new();
    [ObservableProperty] private ChinaModeProfileOption? _selectedChinaModeProfile;
    [ObservableProperty] private ObservableCollection<ChinaModeSavedProfile> _savedChinaProfiles = new();
    [ObservableProperty] private ChinaModeSavedProfile? _selectedSavedChinaProfile;
    [ObservableProperty] private string _chinaModeProfileName = "기본 프로필";
    [ObservableProperty] private bool _isChinaModeSelected;
    [ObservableProperty] private bool _isOutlineProfileSelected;
    [ObservableProperty] private bool _isVlessRealityProfileSelected;
    [ObservableProperty] private bool _isTrojanProfileSelected;
    [ObservableProperty] private string _serverLibraryTitle = "Server Library";
    [ObservableProperty] private string _serverLibrarySubtitle = "Select a live endpoint and switch locations in one click.";
    [ObservableProperty] private string _chinaModeState = "portable 엔진 대기";
    [ObservableProperty] private string _outlineApiUrl = string.Empty;
    [ObservableProperty] private string _outlineCertSha256 = string.Empty;
    [ObservableProperty] private string _outlineAccessKeyName = "SimpleVPN China Mode";
    [ObservableProperty] private string _outlineSshHost = string.Empty;
    [ObservableProperty] private string _outlineSshUser = "root";
    [ObservableProperty] private string _outlineSshKeyPath = string.Empty;
    [ObservableProperty] private string _outlineProvisionHostname = string.Empty;
    [ObservableProperty] private string _outlineProvisionPort = "443";
    [ObservableProperty] private string _outlineProvisionStatus = "대기 중";
    [ObservableProperty] private string _chinaModeProfileHint = "Outline 또는 VLESS REALITY 프로필을 선택해 주세요.";
    [ObservableProperty] private string _vlessRealityServer = string.Empty;
    [ObservableProperty] private string _vlessRealityPort = "443";
    [ObservableProperty] private string _vlessRealityUuid = string.Empty;
    [ObservableProperty] private string _vlessRealityPublicKey = string.Empty;
    [ObservableProperty] private string _vlessRealityShortId = string.Empty;
    [ObservableProperty] private string _vlessRealityServerName = string.Empty;
    [ObservableProperty] private string _vlessRealityFingerprint = "chrome";
    [ObservableProperty] private string _trojanServer = string.Empty;
    [ObservableProperty] private string _trojanPort = "443";
    [ObservableProperty] private string _trojanPassword = string.Empty;
    [ObservableProperty] private bool _isTrojanPasswordVisible;
    [ObservableProperty] private string _trojanServerName = string.Empty;
    [ObservableProperty] private string _trojanFingerprint = "chrome";

    [ObservableProperty] private string _downloadSpeedText = "0 B/s";
    [ObservableProperty] private string _uploadSpeedText = "0 B/s";
    [ObservableProperty] private string _totalReceivedText = "0 B";
    [ObservableProperty] private string _totalSentText = "0 B";
    [ObservableProperty] private string _connectionDurationText = "00:00:00";
    [ObservableProperty] private bool _isKillSwitchEnabled;
    [ObservableProperty] private bool _isTestingPing;

    [ObservableProperty] private ObservableCollection<VpnServer> _customServers = new();
    [ObservableProperty] private string _newCustomServerName = string.Empty;
    [ObservableProperty] private string _newCustomServerIp = string.Empty;

    [ObservableProperty] private ObservableCollection<SshConfigParserService.SshHostInfo> _knownSshHosts = new();
    [ObservableProperty] private SshConfigParserService.SshHostInfo? _selectedKnownSshHost;

    public MainViewModel()
    {
        _vpnService = new WindowsBuiltInVpnService();
        _vpnService.StatusChanged += OnVpnStatusChanged;

        _statsTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _statsTimer.Tick += OnStatsTimerTick;
        _statsTimer.Start();

        ConnectionOptions = new ObservableCollection<VpnConnectionOption>
        {
            new() { Key = "windows", DisplayName = "Windows 기본 VPN", Description = "별도 설치 없이 Windows 기본 L2TP/IPsec 사용" },
            new() { Key = "openvpn", DisplayName = "OpenVPN", Description = "OpenVPN GUI가 설치된 경우 OpenVPN 프로필 사용" },
            new() { Key = "china", DisplayName = "China Mode", Description = "portable sing-box로 Outline, VLESS REALITY, Trojan 연결", RequiresCustomEndpoint = true, SetupHint = "China Profile에서 Outline, VLESS REALITY, Trojan 중 하나를 고른 뒤 값을 입력하세요. `SimpleVPNApp\\Runtime\\sing-box\\` 아래에 `sing-box.exe`와 필요한 DLL을 두면 앱이 직접 실행합니다." }
        };

        ChinaModeProfiles = new ObservableCollection<ChinaModeProfileOption>
        {
            new() { Key = "outline", DisplayName = "Outline", Description = "ss:// Access Key 기반 연결" },
            new() { Key = "vless-reality", DisplayName = "VLESS REALITY", Description = "UUID, Public Key, SNI 기반 연결" },
            new() { Key = "trojan", DisplayName = "Trojan TLS", Description = "Password와 SNI 기반 연결" }
        };

        SelectedChinaModeProfile = ChinaModeProfiles[0];
        SelectedConnectionOption = ConnectionOptions[0];

        _ = RestoreAppSettingsAsync();
        _ = FetchServersAsync();
        _ = RefreshIpInfoAsync();
        TryAutoDetectSshSettings();
        DiscoverKnownSshHosts();
    }

    [RelayCommand]
    private void ShowWindow()
    {
        Application.Current.MainWindow.Show();
        Application.Current.MainWindow.Activate();
    }

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
    private async Task RefreshPingCommandAsync()
    {
        if (IsTestingPing) return;
        IsTestingPing = true;
        try
        {
            var servers = Servers.ToList();
            var tasks = servers.Select(async s =>
            {
                s.Ping = await PingService.GetLatencyAsync(s.IP).ConfigureAwait(false);
            });
            await Task.WhenAll(tasks).ConfigureAwait(false);
            
            // Ping 값 갱신 후 리스트 정렬 (ObservableCollection은 정렬 시 컬렉션 재생성 권장)
            var sorted = servers.OrderBy(s => s.Ping <= 0 ? int.MaxValue : s.Ping).ToList();
            await RunOnUiAsync(() =>
            {
                Servers.Clear();
                foreach (var s in sorted) Servers.Add(s);
            });
        }
        finally
        {
            IsTestingPing = false;
        }
    }

    [RelayCommand]
    private async Task SmartConnectCommandAsync()
    {
        await RefreshPingCommandAsync().ConfigureAwait(false);
        var best = Servers.FirstOrDefault(s => s.Ping > 0 && s.Ping < 500);
        if (best != null)
        {
            SelectedServer = best;
            await ToggleConnectionCommand.ExecuteAsync(null).ConfigureAwait(false);
        }
        else
        {
            await RunOnUiAsync(() => MessageBox.Show("안정적인 연결이 가능한 서버를 찾지 못했습니다. 목록을 새로고침하거나 수동으로 선택해 주세요.", "Smart Connect")).ConfigureAwait(false);
        }
    }

    [RelayCommand]
    private async Task FetchServersAsync()
    {
        await RunOnUiAsync(() =>
        {
            IsLoading = true;
            StatusMessage = "서버 목록 불러오는 중...";
            ConnectionDetails = "VPN Gate 서버 목록을 가져오고 있습니다.";
        }).ConfigureAwait(false);

        try
        {
            var fetchedServers = await _vpnGateService.GetServersAsync().ConfigureAwait(false);
            await RunOnUiAsync(() =>
            {
                Servers.Clear();
                
                // 커스텀 서버 먼저 추가
                foreach (var server in CustomServers)
                {
                    Servers.Add(server);
                }
                
                // API 서버 추가
                foreach (var server in fetchedServers)
                {
                    Servers.Add(server);
                }

                if (Servers.Count > 0 && SelectedServer == null)
                {
                    SelectedServer = Servers[0];
                }

                StatusMessage = Servers.Count > 0 ? $"대기 중 ({Servers.Count}개 서버 발견)" : "서버 목록을 가져오지 못했습니다.";
                ConnectionDetails = Servers.Count > 0 ? "서버를 선택한 뒤 연결 버튼을 눌러 주세요." : "다시 시도하거나 네트워크 상태를 확인해 주세요.";
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
            await RunOnUiAsync(() => IsLoading = false).ConfigureAwait(false);
        }
    }

    [RelayCommand]
    private void CopySelectedServerIpToChinaMode(VpnServer? server)
    {
        var target = server ?? SelectedServer;
        if (target == null) return;
        
        // 필드 업데이트
        OutlineSshHost = target.IP;
        VlessRealityServer = target.IP;
        TrojanServer = target.IP;
        
        var msg = $"서버 IP({target.IP})가 China Mode 설정에 복사되었습니다.";
        StatusMessage = msg;
        ConnectionDetails = $"{msg} (SSH root 권한 필요)";
        
        // 즉시 저장 및 UI 반영 강제
        PersistChinaModeSettings();
    }

    [RelayCommand]
    private void BrowseSshKey()
    {
        var dialog = new OpenFileDialog
        {
            Title = "SSH Private Key 선택",
            Filter = "Private Key 파일|*.pem;*.key;id_rsa|모든 파일|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            OutlineSshKeyPath = dialog.FileName;
        }
    }

    private void TryAutoDetectSshSettings()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(OutlineSshKeyPath)) return;

            var sshDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");
            if (Directory.Exists(sshDir))
            {
                var keyFile = Directory.GetFiles(sshDir, "*.pem")
                    .OrderByDescending(f => File.GetLastWriteTime(f))
                    .FirstOrDefault() ??
                    Directory.GetFiles(sshDir, "id_rsa").FirstOrDefault();
                
                if (keyFile != null)
                {
                    OutlineSshKeyPath = keyFile;
                }
            }
        }
        catch { /* ignored */ }
    }

    [RelayCommand]
    private async Task ProvisionRealityServerAsync()
    {
        try
        {
            await RunOnUiAsync(() =>
            {
                IsLoading = true;
                OutlineProvisionStatus = "VLESS REALITY 서버 자동 구축 중 (SSH)...";
            }).ConfigureAwait(false);

            var result = await _realityServerBootstrapService.BootstrapAsync(
                OutlineSshHost,
                OutlineSshUser,
                OutlineSshKeyPath).ConfigureAwait(false);

            await RunOnUiAsync(() =>
            {
                VlessRealityServer = result.Server;
                VlessRealityPort = result.Port;
                VlessRealityUuid = result.Uuid;
                VlessRealityPublicKey = result.PublicKey;
                VlessRealityShortId = result.ShortId;
                VlessRealityServerName = result.ServerName;
                
                OutlineProvisionStatus = "VLESS REALITY 서버 구축 완료";
                ConnectionDetails = "원격 서버에 sing-box 설치 및 설정을 완료했습니다. 이제 연결할 수 있습니다.";
            }).ConfigureAwait(false);
            
            PersistChinaModeSettings();
        }
        catch (Exception ex)
        {
            await RunOnUiAsync(() =>
            {
                OutlineProvisionStatus = $"서버 구축 실패: {ex.Message}";
                ConnectionDetails = "SSH 정보와 서버 네트워크 상태를 확인해 주세요.";
            }).ConfigureAwait(false);
        }
        finally
        {
            await RunOnUiAsync(() => IsLoading = false).ConfigureAwait(false);
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
                ConnectionDetails = "기존 Outline 서버의 access.txt를 읽어 관리 정보를 채웠습니다.";
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await RunOnUiAsync(() =>
            {
                OutlineProvisionStatus = $"access.txt 조회 실패: {ex.Message}";
                ConnectionDetails = "SSH 정보와 `/opt/outline/access.txt` 접근 권한을 확인해 주세요.";
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
                ConnectionDetails = "새 서버가 준비되었고 China Mode 입력칸에 Access Key가 채워졌습니다.";
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
    private void ToggleOutlineAccessKeyVisibility() =>
        IsOutlineAccessKeyVisible = !IsOutlineAccessKeyVisible;

    [RelayCommand]
    private void ToggleTrojanPasswordVisibility() =>
        IsTrojanPasswordVisible = !IsTrojanPasswordVisible;

    [RelayCommand]
    private async Task TestChinaProfileAsync()
    {
        if (!IsChinaModeSelected)
        {
            return;
        }

        try
        {
            await RunOnUiAsync(() =>
            {
                IsLoading = true;
                OutlineProvisionStatus = "현재 China Profile 설정 검증 중...";
            }).ConfigureAwait(false);

            ChinaOptimizedVpnService.ValidateConnectionPayload(BuildChinaModePayload());

            await RunOnUiAsync(() =>
            {
                OutlineProvisionStatus = "현재 China Profile 설정 검증 완료";
                ConnectionDetails = $"{SelectedChinaModeProfile?.DisplayName ?? "China Profile"} 입력값이 유효합니다. sing-box 설정을 생성할 수 있습니다.";
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await RunOnUiAsync(() =>
            {
                OutlineProvisionStatus = $"설정 검증 실패: {ex.Message}";
                ConnectionDetails = "필수 입력값과 형식을 다시 확인해 주세요.";
            }).ConfigureAwait(false);
        }
        finally
        {
            await RunOnUiAsync(() => IsLoading = false).ConfigureAwait(false);
        }
    }

    [RelayCommand]
    private async Task ExportChinaProfileAsync()
    {
        if (!IsChinaModeSelected)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "China Profile JSON 내보내기",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = ".json",
            AddExtension = true,
            FileName = $"simplevpn-china-{SelectedChinaModeProfile?.Key ?? "outline"}.json"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            await RunOnUiAsync(() =>
            {
                IsLoading = true;
                OutlineProvisionStatus = "China Profile JSON 내보내기 중...";
            }).ConfigureAwait(false);

            await _localSettingsService
                .ExportChinaModeSettingsAsync(dialog.FileName, CreateChinaModeSettings())
                .ConfigureAwait(false);

            await RunOnUiAsync(() =>
            {
                OutlineProvisionStatus = "China Profile JSON 내보내기 완료";
                ConnectionDetails = $"현재 China Mode 설정을 `{dialog.FileName}` 파일로 저장했습니다.";
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await RunOnUiAsync(() =>
            {
                OutlineProvisionStatus = $"내보내기 실패: {ex.Message}";
                ConnectionDetails = "파일 저장 권한과 경로를 다시 확인해 주세요.";
            }).ConfigureAwait(false);
        }
        finally
        {
            await RunOnUiAsync(() => IsLoading = false).ConfigureAwait(false);
        }
    }

    [RelayCommand]
    private async Task ImportChinaProfileAsync()
    {
        if (!IsChinaModeSelected)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "China Profile JSON 가져오기",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            await RunOnUiAsync(() =>
            {
                IsLoading = true;
                OutlineProvisionStatus = "China Profile JSON 가져오는 중...";
            }).ConfigureAwait(false);

            var settings = await _localSettingsService.ImportChinaModeSettingsAsync(dialog.FileName).ConfigureAwait(false);
            await ApplyChinaModeSettingsAsync(settings).ConfigureAwait(false);
            SaveCurrentChinaProfileSlot();
            PersistChinaModeSettings();

            await RunOnUiAsync(() =>
            {
                OutlineProvisionStatus = "China Profile JSON 가져오기 완료";
                ConnectionDetails = $"`{dialog.FileName}` 설정을 적용했습니다.";
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await RunOnUiAsync(() =>
            {
                OutlineProvisionStatus = $"가져오기 실패: {ex.Message}";
                ConnectionDetails = "JSON 형식과 필수 필드를 다시 확인해 주세요.";
            }).ConfigureAwait(false);
        }
        finally
        {
            await RunOnUiAsync(() => IsLoading = false).ConfigureAwait(false);
        }
    }

    [RelayCommand]
    private void CreateChinaProfileSlot()
    {
        var newProfile = new ChinaModeSavedProfile
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = GetNextProfileName(),
            Settings = CreateChinaModeSettings()
        };

        SavedChinaProfiles.Add(newProfile);
        SelectedSavedChinaProfile = newProfile;
        ChinaModeProfileName = newProfile.Name;
        PersistChinaModeSettings();

        OutlineProvisionStatus = "새 China 프로필 슬롯을 만들었습니다.";
        ConnectionDetails = $"`{newProfile.Name}` 프로필이 추가되었습니다.";
    }

    [RelayCommand]
    private void SaveChinaProfileSlot()
    {
        SaveCurrentChinaProfileSlot();
        PersistChinaModeSettings();

        OutlineProvisionStatus = "현재 China 프로필 슬롯 저장 완료";
        ConnectionDetails = $"`{ChinaModeProfileName}` 프로필에 현재 입력값을 저장했습니다.";
    }

    [RelayCommand]
    private void DeleteChinaProfileSlot()
    {
        if (SelectedSavedChinaProfile == null)
        {
            return;
        }

        if (SavedChinaProfiles.Count == 1)
        {
            OutlineProvisionStatus = "삭제할 수 없음";
            ConnectionDetails = "마지막 China 프로필 슬롯 하나는 유지되어야 합니다.";
            return;
        }

        var deletedName = SelectedSavedChinaProfile.Name;
        var removeIndex = SavedChinaProfiles.IndexOf(SelectedSavedChinaProfile);
        SavedChinaProfiles.Remove(SelectedSavedChinaProfile);
        SelectedSavedChinaProfile = SavedChinaProfiles[Math.Max(0, removeIndex - 1)];
        PersistChinaModeSettings();

        OutlineProvisionStatus = "China 프로필 슬롯 삭제 완료";
        ConnectionDetails = $"`{deletedName}` 프로필을 삭제했습니다.";
    }

    [RelayCommand]
    private async Task ToggleConnectionAsync()
    {
        if (SelectedServer == null && SelectedConnectionOption?.RequiresCustomEndpoint != true)
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
                    ConnectionDetails = "VPN 연결을 해제했습니다.";
                    ConnectedServerIp = "-";
                    ChinaModeState = IsChinaModeSelected ? "엔진 대기" : ChinaModeState;
                }).ConfigureAwait(false);
                UpdateKillSwitchState();
                await RefreshIpInfoAsync().ConfigureAwait(false);
                return;
            }

            var server = SelectedServer ?? new VpnServer();
            await RunOnUiAsync(() =>
            {
                StatusMessage = SelectedConnectionOption?.Key == "china"
                    ? $"{SelectedChinaModeProfile?.DisplayName ?? "China"} 연결 시도..."
                    : $"{server.CountryLong} 연결 시도...";
                ConnectionDetails = $"{SelectedConnectionOption?.DisplayName ?? "VPN"} 연결을 시작합니다.";
            }).ConfigureAwait(false);

            await _vpnService.ConnectAsync(server).ConfigureAwait(false);

            await RunOnUiAsync(() =>
            {
                IsConnected = true;
                StatusMessage = SelectedConnectionOption?.Key == "china"
                    ? $"{SelectedChinaModeProfile?.DisplayName ?? "China Mode"} 연결됨"
                    : $"VPN 연결됨 ({server.CountryShort})";
                ButtonContent = "연결 해제";
                ConnectionDetails = SelectedConnectionOption?.Key == "china"
                    ? "portable China Mode 엔진이 활성화되었습니다."
                    : $"{server.HostName} 연결이 활성화되었습니다.";
                ConnectedServerIp = SelectedConnectionOption?.Key == "china" ? (SelectedChinaModeProfile?.Key == "vless-reality" ? VlessRealityServer : "-") : server.IP;
                ChinaModeState = IsChinaModeSelected ? "엔진 실행 중" : ChinaModeState;
            }).ConfigureAwait(false);

            UpdateKillSwitchState();
            await RefreshIpInfoAsync().ConfigureAwait(false);
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
            await RunOnUiAsync(() => IsLoading = false).ConfigureAwait(false);
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

    private void OnStatsTimerTick(object? sender, EventArgs e)
    {
        if (!IsConnected)
        {
            DownloadSpeedText = "0 B/s";
            UploadSpeedText = "0 B/s";
            ConnectionDurationText = "00:00:00";
            return;
        }

        try
        {
            var stats = _vpnService.GetStatistics();
            DownloadSpeedText = FormatSize(stats.DownloadSpeed) + "/s";
            UploadSpeedText = FormatSize(stats.UploadSpeed) + "/s";
            TotalReceivedText = FormatSize(stats.BytesReceived);
            TotalSentText = FormatSize(stats.BytesSent);
            ConnectionDurationText = stats.Duration.ToString(@"hh\:mm\:ss");
        }
        catch
        {
            // Ignore
        }
    }

    private static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double size = bytes;
        int unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }
        return $"{size:F2} {units[unitIndex]}";
    }

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

        IsChinaModeSelected = value.Key == "china";
        IsOutlineProfileSelected = IsChinaModeSelected && SelectedChinaModeProfile?.Key == "outline";
        IsVlessRealityProfileSelected = IsChinaModeSelected && SelectedChinaModeProfile?.Key == "vless-reality";
        IsTrojanProfileSelected = IsChinaModeSelected && SelectedChinaModeProfile?.Key == "trojan";

        if (IsChinaModeSelected)
        {
            RefreshChinaModeService();
        }
        else
        {
            ReplaceVpnService(CreateVpnService(value));
        }

        _ = RunOnUiAsync(() =>
        {
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
        UpdateChinaModeHint();
        _ = RefreshIpInfoAsync();
    }

    partial void OnIsKillSwitchEnabledChanged(bool value)
    {
        UpdateKillSwitchState();
        PersistChinaModeSettings(); // Kill Switch 설정도 함께 저장
    }

    private void UpdateKillSwitchState()
    {
        if (IsKillSwitchEnabled && IsConnected)
        {
            var serverIp = ConnectedServerIp;
            if (string.IsNullOrEmpty(serverIp) || serverIp == "-") 
            {
                serverIp = SelectedServer?.IP;
            }
            
            _ = _firewallService.EnableKillSwitchAsync(serverIp ?? string.Empty);
        }
        else
        {
            _firewallService.DisableKillSwitch();
        }
    }

    partial void OnSelectedChinaModeProfileChanged(ChinaModeProfileOption? value)
    {
        if (value == null)
        {
            return;
        }

        IsOutlineProfileSelected = IsChinaModeSelected && value.Key == "outline";
        IsVlessRealityProfileSelected = IsChinaModeSelected && value.Key == "vless-reality";
        IsTrojanProfileSelected = IsChinaModeSelected && value.Key == "trojan";

        if (!IsChinaModeSelected)
        {
            return;
        }

        RefreshChinaModeService();
        UpdateChinaModeHint();
        PersistChinaModeSettings();

        _ = RunOnUiAsync(() =>
        {
            OutlineProvisionStatus = value.Key == "outline"
                ? "기존 Outline 서버 정보로 Access Key를 만들거나 새 서버를 구축할 수 있습니다."
                : value.Key == "vless-reality"
                    ? "VLESS REALITY 서버 정보를 입력한 뒤 바로 연결할 수 있습니다."
                    : "Trojan TLS 서버 정보를 입력한 뒤 바로 연결할 수 있습니다.";
            ConnectionDetails = $"{value.DisplayName} 프로필이 선택되었습니다. {value.Description}";
        });
    }

    partial void OnSelectedSavedChinaProfileChanged(ChinaModeSavedProfile? value)
    {
        if (value == null)
        {
            return;
        }

        ChinaModeProfileName = value.Name;
        _ = ApplyChinaModeSettingsAsync(value.Settings);
        PersistChinaModeSettings();
    }

    partial void OnChinaModeProfileNameChanged(string value)
    {
        SaveCurrentChinaProfileSlot();
        PersistChinaModeSettings();
    }

    partial void OnChinaModeAccessKeyChanged(string value)
    {
        if (IsOutlineProfileSelected)
        {
            if (SelectedConnectionOption?.Key == "china")
            {
                RefreshChinaModeService();
                UpdateChinaModeHint();
            }
            PersistChinaModeSettings();
        }
    }

    partial void OnOutlineApiUrlChanged(string value) => PersistChinaModeSettings();
    partial void OnOutlineCertSha256Changed(string value) => PersistChinaModeSettings();
    partial void OnOutlineSshHostChanged(string value) => PersistChinaModeSettings();
    partial void OnOutlineSshUserChanged(string value) => PersistChinaModeSettings();
    partial void OnOutlineSshKeyPathChanged(string value) => PersistChinaModeSettings();
    partial void OnOutlineProvisionHostnameChanged(string value) => PersistChinaModeSettings();
    partial void OnOutlineProvisionPortChanged(string value) => PersistChinaModeSettings();

    partial void OnVlessRealityServerChanged(string value) => OnVlessFieldChanged();
    partial void OnVlessRealityPortChanged(string value) => OnVlessFieldChanged();
    partial void OnVlessRealityUuidChanged(string value) => OnVlessFieldChanged();
    partial void OnVlessRealityPublicKeyChanged(string value) => OnVlessFieldChanged();
    partial void OnVlessRealityShortIdChanged(string value) => OnVlessFieldChanged();
    partial void OnVlessRealityServerNameChanged(string value) => OnVlessFieldChanged();
    partial void OnVlessRealityFingerprintChanged(string value) => OnVlessFieldChanged();
    partial void OnTrojanServerChanged(string value) => OnTrojanFieldChanged();
    partial void OnTrojanPortChanged(string value) => OnTrojanFieldChanged();
    partial void OnTrojanPasswordChanged(string value) => OnTrojanFieldChanged();
    partial void OnTrojanServerNameChanged(string value) => OnTrojanFieldChanged();
    partial void OnTrojanFingerprintChanged(string value) => OnTrojanFieldChanged();

    private void OnVlessFieldChanged()
    {
        if (SelectedConnectionOption?.Key == "china" && IsVlessRealityProfileSelected)
        {
            RefreshChinaModeService();
            UpdateChinaModeHint();
            PersistChinaModeSettings();
        }
    }

    private void OnTrojanFieldChanged()
    {
        if (SelectedConnectionOption?.Key == "china" && IsTrojanProfileSelected)
        {
            RefreshChinaModeService();
            UpdateChinaModeHint();
            PersistChinaModeSettings();
        }
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

    private static IVpnService CreateVpnService(VpnConnectionOption? option, string? chinaModePayload = null) =>
        option?.Key switch
        {
            "openvpn" => new OpenVpnService(),
            "china" => new ChinaOptimizedVpnService(chinaModePayload ?? string.Empty),
            _ => new WindowsBuiltInVpnService()
        };

    private void ReplaceVpnService(IVpnService newService)
    {
        var oldService = _vpnService;
        _vpnService = newService;
        _vpnService.StatusChanged += OnVpnStatusChanged;
        oldService.StatusChanged -= OnVpnStatusChanged;
        oldService.Dispose();
    }

    private void RefreshChinaModeService()
    {
        if (SelectedConnectionOption?.Key != "china")
        {
            return;
        }

        ReplaceVpnService(CreateVpnService(SelectedConnectionOption, BuildChinaModePayload()));
    }

    private string BuildChinaModePayload()
    {
        if (SelectedChinaModeProfile?.Key == "vless-reality")
        {
            var port = int.TryParse(VlessRealityPort, out var parsedPort) ? parsedPort : 0;
            var profile = new ChinaModeConnectionProfile
            {
                ProfileType = "vless-reality",
                Server = VlessRealityServer,
                Port = port,
                Uuid = VlessRealityUuid,
                PublicKey = VlessRealityPublicKey,
                ShortId = VlessRealityShortId,
                ServerName = VlessRealityServerName,
                Fingerprint = VlessRealityFingerprint
            };
            return JsonSerializer.Serialize(profile);
        }

        if (SelectedChinaModeProfile?.Key == "trojan")
        {
            var port = int.TryParse(TrojanPort, out var parsedPort) ? parsedPort : 0;
            var profile = new ChinaModeConnectionProfile
            {
                ProfileType = "trojan",
                Server = TrojanServer,
                Port = port,
                Password = TrojanPassword,
                ServerName = TrojanServerName,
                Fingerprint = TrojanFingerprint
            };
            return JsonSerializer.Serialize(profile);
        }

        return ChinaModeAccessKey ?? string.Empty;
    }

    private void UpdateChinaModeHint()
    {
        if (!IsChinaModeSelected)
        {
            return;
        }

        if (IsVlessRealityProfileSelected)
        {
            ChinaModeProfileHint =
                string.IsNullOrWhiteSpace(VlessRealityServer) ||
                string.IsNullOrWhiteSpace(VlessRealityUuid) ||
                string.IsNullOrWhiteSpace(VlessRealityPublicKey) ||
                string.IsNullOrWhiteSpace(VlessRealityServerName)
                    ? "VLESS REALITY의 Server, Port, UUID, Public Key, Server Name(SNI)을 입력해 주세요."
                    : "VLESS REALITY 정보가 입력되었습니다. portable 엔진으로 연결을 시도할 수 있습니다.";
            return;
        }

        if (IsTrojanProfileSelected)
        {
            ChinaModeProfileHint =
                string.IsNullOrWhiteSpace(TrojanServer) ||
                string.IsNullOrWhiteSpace(TrojanPassword) ||
                string.IsNullOrWhiteSpace(TrojanServerName)
                    ? "Trojan TLS의 Server, Port, Password, Server Name(SNI)을 입력해 주세요."
                    : "Trojan TLS 정보가 입력되었습니다. portable 엔진으로 연결을 시도할 수 있습니다.";
            return;
        }

        ChinaModeProfileHint = string.IsNullOrWhiteSpace(ChinaModeAccessKey)
            ? "Outline용 ss:// Access Key를 입력해 주세요."
            : "Outline Access Key가 입력되었습니다. portable 엔진으로 연결을 시도할 수 있습니다.";
    }

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

    private async Task RestoreAppSettingsAsync()
    {
        _isRestoringSettings = true;
        try
        {
            var library = await _localSettingsService.LoadChinaModeProfileLibraryAsync().ConfigureAwait(false);
            var isKillSwitch = await _localSettingsService.LoadKillSwitchStatusAsync().ConfigureAwait(false);
            var customServers = await _localSettingsService.LoadCustomServersAsync().ConfigureAwait(false);

            await RunOnUiAsync(() =>
            {
                IsKillSwitchEnabled = isKillSwitch;
                
                CustomServers.Clear();
                foreach (var s in customServers) CustomServers.Add(s);
                
                SavedChinaProfiles.Clear();
                foreach (var profile in library.Profiles)
                {
                    SavedChinaProfiles.Add(profile);
                }

                SelectedSavedChinaProfile =
                    SavedChinaProfiles.FirstOrDefault(p => p.Id == library.SelectedSavedProfileId) ??
                    SavedChinaProfiles.FirstOrDefault();
            }).ConfigureAwait(false);

            if (SelectedSavedChinaProfile != null)
            {
                ChinaModeProfileName = SelectedSavedChinaProfile.Name;
                await ApplyChinaModeSettingsAsync(SelectedSavedChinaProfile.Settings).ConfigureAwait(false);
            }
        }
        finally
        {
            _isRestoringSettings = false;
        }
    }

    private void PersistChinaModeSettings()
    {
        if (_isRestoringSettings) return;

        SaveCurrentChinaProfileSlot();

        var library = new ChinaModeProfileLibrary
        {
            SelectedSavedProfileId = SelectedSavedChinaProfile?.Id ?? SavedChinaProfiles.FirstOrDefault()?.Id ?? "default",
            Profiles = SavedChinaProfiles.ToList()
        };

        _ = _localSettingsService.SaveAppSettingsAsync(
            SelectedSavedChinaProfile?.Settings ?? new ChinaModeSettings(),
            library,
            IsKillSwitchEnabled,
            CustomServers.ToList());
    }

    private void PersistAppSettings() => PersistChinaModeSettings();

    private ChinaModeSettings CreateChinaModeSettings() =>
        new()
        {
            SelectedProfileKey = SelectedChinaModeProfile?.Key ?? "outline",
            OutlineAccessKey = ChinaModeAccessKey,
            OutlineApiUrl = OutlineApiUrl,
            OutlineCertSha256 = OutlineCertSha256,
            OutlineSshHost = OutlineSshHost,
            OutlineSshUser = OutlineSshUser,
            OutlineSshKeyPath = OutlineSshKeyPath,
            OutlineProvisionHostname = OutlineProvisionHostname,
            OutlineProvisionPort = OutlineProvisionPort,
            VlessRealityServer = VlessRealityServer,
            VlessRealityPort = VlessRealityPort,
            VlessRealityUuid = VlessRealityUuid,
            VlessRealityPublicKey = VlessRealityPublicKey,
            VlessRealityShortId = VlessRealityShortId,
            VlessRealityServerName = VlessRealityServerName,
            VlessRealityFingerprint = VlessRealityFingerprint,
            TrojanServer = TrojanServer,
            TrojanPort = TrojanPort,
            TrojanPassword = TrojanPassword,
            TrojanServerName = TrojanServerName,
            TrojanFingerprint = TrojanFingerprint
        };

    private async Task ApplyChinaModeSettingsAsync(ChinaModeSettings settings)
    {
        _isRestoringSettings = true;
        try
        {
            await RunOnUiAsync(() =>
            {
                ChinaModeAccessKey = settings.OutlineAccessKey;
                OutlineApiUrl = settings.OutlineApiUrl;
                OutlineCertSha256 = settings.OutlineCertSha256;
                OutlineSshHost = settings.OutlineSshHost;
                OutlineSshUser = string.IsNullOrWhiteSpace(settings.OutlineSshUser) ? "root" : settings.OutlineSshUser;
                OutlineSshKeyPath = settings.OutlineSshKeyPath;
                OutlineProvisionHostname = settings.OutlineProvisionHostname;
                OutlineProvisionPort = string.IsNullOrWhiteSpace(settings.OutlineProvisionPort) ? "443" : settings.OutlineProvisionPort;
                VlessRealityServer = settings.VlessRealityServer;
                VlessRealityPort = string.IsNullOrWhiteSpace(settings.VlessRealityPort) ? "443" : settings.VlessRealityPort;
                VlessRealityUuid = settings.VlessRealityUuid;
                VlessRealityPublicKey = settings.VlessRealityPublicKey;
                VlessRealityShortId = settings.VlessRealityShortId;
                VlessRealityServerName = settings.VlessRealityServerName;
                VlessRealityFingerprint = string.IsNullOrWhiteSpace(settings.VlessRealityFingerprint) ? "chrome" : settings.VlessRealityFingerprint;
                TrojanServer = settings.TrojanServer;
                TrojanPort = string.IsNullOrWhiteSpace(settings.TrojanPort) ? "443" : settings.TrojanPort;
                TrojanPassword = settings.TrojanPassword;
                TrojanServerName = settings.TrojanServerName;
                TrojanFingerprint = string.IsNullOrWhiteSpace(settings.TrojanFingerprint) ? "chrome" : settings.TrojanFingerprint;

                var matchedProfile = ChinaModeProfiles.FirstOrDefault(profile => profile.Key == settings.SelectedProfileKey);
                if (matchedProfile != null)
                {
                    SelectedChinaModeProfile = matchedProfile;
                }
            }).ConfigureAwait(false);
        }
        finally
        {
            _isRestoringSettings = false;
            UpdateChinaModeHint();
            RefreshChinaModeService();
        }
    }

    private void SaveCurrentChinaProfileSlot()
    {
        if (_isRestoringSettings)
        {
            return;
        }

        if (SelectedSavedChinaProfile == null)
        {
            var defaultProfile = new ChinaModeSavedProfile
            {
                Id = "default",
                Name = string.IsNullOrWhiteSpace(ChinaModeProfileName) ? "기본 프로필" : ChinaModeProfileName,
                Settings = CreateChinaModeSettings()
            };

            SavedChinaProfiles.Add(defaultProfile);
            SelectedSavedChinaProfile = defaultProfile;
            return;
        }

        var index = SavedChinaProfiles.IndexOf(SelectedSavedChinaProfile);
        if (index < 0)
        {
            return;
        }

        var updatedProfile = new ChinaModeSavedProfile
        {
            Id = SelectedSavedChinaProfile.Id,
            Name = string.IsNullOrWhiteSpace(ChinaModeProfileName) ? "이름 없는 프로필" : ChinaModeProfileName,
            Settings = CreateChinaModeSettings()
        };

        SavedChinaProfiles[index] = updatedProfile;
        if (!ReferenceEquals(SelectedSavedChinaProfile, updatedProfile))
        {
            _isRestoringSettings = true;
            SelectedSavedChinaProfile = updatedProfile;
            _isRestoringSettings = false;
        }
    }

    private string GetNextProfileName()
    {
        const string baseName = "새 프로필";
        var suffix = 1;

        while (SavedChinaProfiles.Any(profile => string.Equals(profile.Name, $"{baseName} {suffix}", StringComparison.OrdinalIgnoreCase)))
        {
            suffix++;
        }

        return $"{baseName} {suffix}";
    }

    private void DiscoverKnownSshHosts()
    {
        try
        {
            var hosts = SshConfigParserService.ParseKnownHosts();
            foreach (var h in hosts) KnownSshHosts.Add(h);
        }
        catch { /* ignored */ }
    }

    partial void OnSelectedKnownSshHostChanged(SshConfigParserService.SshHostInfo? value)
    {
        if (value == null) return;
        
        OutlineSshHost = value.HostName;
        OutlineSshUser = value.User;
        if (!string.IsNullOrEmpty(value.IdentityFile))
        {
            OutlineSshKeyPath = value.IdentityFile;
        }
        PersistChinaModeSettings();
    }

    [RelayCommand]
    private void AddCustomServer()
    {
        if (string.IsNullOrWhiteSpace(NewCustomServerIp))
        {
            MessageBox.Show("서버 IP를 입력해 주세요.", "알림");
            return;
        }

        var name = string.IsNullOrWhiteSpace(NewCustomServerName) ? "Custom Server" : NewCustomServerName;
        var newServer = new VpnServer
        {
            HostName = name,
            IP = NewCustomServerIp,
            CountryShort = "MY",
            CountryLong = "Manually Added",
            Score = 1000000, 
            Ping = 0
        };

        CustomServers.Add(newServer);
        Servers.Insert(0, newServer); 
        
        NewCustomServerName = string.Empty;
        NewCustomServerIp = string.Empty;
        
        PersistAppSettings();
        MessageBox.Show($"'{name}' 서버가 라이브러리에 추가되었습니다.", "완료");
    }

    [RelayCommand]
    private void RemoveCustomServer(VpnServer? server)
    {
        if (server == null) return;
        if (!CustomServers.Contains(server)) return;

        CustomServers.Remove(server);
        Servers.Remove(server);
        PersistAppSettings();
    }
}
