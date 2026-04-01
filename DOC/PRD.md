# 제품 요구사항 정의서 (PRD) - SimpleVPN

## 1. 프로젝트 개요
- **프로젝트명**: SimpleVPN
- **기획 의도**: 복잡한 네트워크 설정 없이도 사용자가 간편하고 안전하게 VPN 서비스를 이용할 수 있는 Windows용 고성능 클라이언트를 개발함.
- **주요 목표**:
  - **.NET 9 기반**의 최신 런타임 성능 활용
  - **WPF (Windows Presentation Foundation)** 및 **MVVM 패턴**을 통한 유지보수성 및 확장성 확보
  - 사용자 친화적인 UI/UX 제공 (프리미엄 디자인)
  - 저사양 환경에서도 부하가 적은 효율적인 리소스 관리 (C# 최적화)
  - Windows 기본 VPN과 OpenVPN을 선택적으로 사용할 수 있는 유연한 터널링 지원
  - 중국 환경 대응을 위한 별도 `China Mode` 확장 경로 확보

## 2. 사용자 시나리오
- 사용자가 프로그램을 실행하면 현재 연결 상태와 최적의 서버(Latency 기준)를 추천받음.
- '연결' 버튼을 클릭하면 수 초 내에 암호화된 터널링이 형성되어 안전한 통신이 시작됨.
- 연결 중에는 실시간 트래픽 양(Uplink/Downlink)과 사용 시간을 모니터링할 수 있음.
- VPN 연결이 갑자기 끊어질 경우 데이터 유출 방지를 위해 Kill Switch가 작동하여 로컬 인터넷을 차단함.

## 3. 핵심 기능 설명
| 구분 | 기능명 | 설명 | 비고 |
|---|---|---|---|
| **기본** | 서버 리스트 조회 | VPN Gate(학술 프로젝트) 등의 공용 API를 통해 무료 서버 목록을 CSV 형태로 실시간 조회함. | API 기반 연동 |
| | 터널링 연결/해제 | 선택된 서버의 정보를 바탕으로 안전한 터널링을 구성하고 연결 상태를 실시간 반영함. | 최우선 과제 |
| | China Mode | 중국 환경에서는 공개 VPN Gate 서버 대신 portable sing-box 엔진으로 `Outline`, `VLESS REALITY`, `Trojan TLS` 프로필을 직접 사용하는 전용 모드를 제공함. | 무설치 엔진 연동 |
| | Outline 키 자동 생성 | 기존 Outline 서버 관리 API를 통해 Access Key를 생성하거나, SSH로 새 Outline 서버를 설치한 뒤 Access Key를 자동 발급함. | 중국 대응 자동화 |
| | China 프로필 관리 | China Mode 설정을 이름 있는 여러 슬롯으로 저장하고, JSON 가져오기/내보내기로 다른 PC와 공유할 수 있음. | 로컬 설정/이식성 |
| **보안** | Kill Switch | 의도치 않은 VPN 연결 해제 시 모든 인터넷 트래픽을 차단함. | 방화벽 제어 |
| | 암호화 통신 | AES-256 또는 ChaCha20 등 고성능 암호화 스위트를 적용함. | |
| | **프로그램 종료 시 정리** | 프로그램 종료 시 설정된 Windows 프록시(China Mode), VPN 연결(L2TP/OpenVPN), Kill Switch 규칙을 즉시 해제하고 관련 리소스를 안전하게 정리함. | Clean Exit (Win32 API 알림 포함) |
| **편의** | 자동 서버 선택 | 지연 시간이 가장 짧은 서버를 자동으로 추천하거나 연결함. | |
| | 트레이 아이콘 | 백그라운드 실행 시 작업 표시줄 트레이 아이콘(NotifyIcon)을 통해 상태를 표시하고 빠른 메뉴(Quick Menu)를 제공함. | 트레이 상주 기능 |
| | 다크 모드/테마 | 세련되고 현대적인 Glassmorphism 효과를 적용한 UI 테마 제공. | UI/UX 프리미엄 |

## 4. 기술 및 디자인 가이드라인
### 4.1 핵심 기술 스택
- **Framework**: .NET 9.0 (LTS 전 단계의 최신 성능 및 기능 활용)
- **UI Framework**: WPF (Windows Presentation Foundation)
- **Architecture**: x64 전용 (성능 및 호환성 최적화)
- **Pattern**: MVVM (Model-View-ViewModel) - Microsoft.Toolkit.Mvvm / CommunityToolkit.Mvvm 라이브러리 활용 권장
- **Build System**: SDK-style project file (출력 경로 간소화 정책 적용)

### 4.2 핵심 프로그래밍 규칙 (C#)
- **네트워크 관리**: `HttpClient`는 `static` 객체로 재사용하거나 `IHttpClientFactory`를 통해 효율적으로 관리하여 소켓 고갈 방지. (Rule: 리소스 관리 준수)
- **CSV 데이터 파싱**: 외부 API(VPN Gate)로부터 수신한 대량의 CSV 데이터를 파싱할 때 `Span<T>` 또는 `StringReader`를 사용하여 메모리 할당 최소화. (Rule: GC 부하 최소화)
- **비동기 처리**: 모든 네트워크 요청은 `Task` 기반 비동기 메서드로 구현하며 `ConfigureAwait(false)` 적용.
- **예외 처리**: 빈 catch 블록을 지양하고 모든 오류 상황에 대해 구조화된 로깅을 수행함.
- **스레드 안전성**: 스레드 간 데이터 공유 시 `ConcurrentQueue` 또는 `ConcurrentDictionary` 사용.
- **IDisposable 구현**: 외부 리소스(소켓, 엔진 프로세스) 사용 시 반드시 IDisposable을 구현하고 종료 시 명시적으로 Dispose 호출.

### 4.3 UI/UX 디자인 전략 (Premium)
- **폰트**: Inter 또는 Google Fonts(Manrope, Outfit 등)의 현대적인 테크니컬 프리미엄 폰트 사용.
- **미학**: 유리질 효과(Glassmorphism), 부드러운 그라데이션, 미세한 상호작용 애니메이션 적용.
- **반응형**: 윈도우 크기에 따른 유연한 레이아웃 보장.

## 5. 단계별 개발 로드맵 (Milestones)
1. **MVP (Phase 1)**: 기본적인 터널링 연결(Windows 기본 VPN/L2TP 또는 OpenVPN), 서버 목록 표시, 연결/해제 토글.
2. **Stable (Phase 2)**: Kill Switch 기능, 트래픽 실시간 그래프 UI, 시스템 트레이(Tray Icon) 연동 및 백그라운드 모드.
3. **Advanced (Phase 3)**: 다중 국가 자동 라우팅, 서버 로드 밸런싱 모드, 트래픽 최적화 알고리즘 도입, China Mode 엔진 번들링.

## 6. 현재 구현 상태 (2026-03-31 기준)
- **빌드 안정화 및 Release 최적화 완료**: `global.json` 수정 및 Release 구성 빌드 검증 완료.
- **방화벽 허용 자동화 적용**: `sing-box.exe` 시작 시 방화벽 규칙 자동 등록.
- **새 서버 구축 UI 활성화**: SSH 정보를 이용한 Outline 및 VLESS REALITY 자동 구축 구현.
- **다중 연결 방식 지원**: Windows 기본 VPN, OpenVPN, China Mode(sing-box) 지원.
- **Smart Selection 및 지연 시간 기반 추천**: `PingService` 기반 자동 서버 추천.
- **보안 강화 및 자격 증명 관리자 연동**: Win32 Credential Manager를 통한 민감 정보 시스템 암호화 저장.
- **UI 애니메이션 및 프리미엄 경험 고도화**: WPF Storyboard 기반 고수준 애니메이션 적용.
- **설정 영구 저장 및 편의성**: `LocalSettingsService`를 통한 사용자 설정 보존.
- **프로그램 종료 시 클린업 강화 (2026-04-01)**: 종료 시 Windows 프록시 즉시 해제(Notify 포함), VPN 자동 연결 해제, Kill Switch 비활성화 로직 구현.

## 7. 실제 검증 결과
- **Release 빌드 및 실행 검증 완료**: 모든 구성에서 정상 빌드 및 실행 확인.
- **VPN 연결/해제 실동작 확인**: Windows 기본 VPN 및 OpenVPN 환경에서 공인 IP 변경 확인.
- **China Mode 슬롯 관리/이식 검증**: JSON 가져오기/내보내기 및 슬롯 저장 시스템 정상 동작.
- **네트워크 보안(Kill Switch) 검증**: VPN 해제 시 방화벽 차단 로직 확인.
- **UI 반응성 및 트레이 기능 검증**: 애니메이션 및 트레이 상주/메뉴 기능 확인.

## 8. 구현 세부 메모
- **Windows 기본 VPN 방식**: `Add-VpnConnection` 및 `rasdial.exe` 활용.
- **OpenVPN 방식**: OpenVPN GUI 및 개별 프로필 생성/관리.
- **China Mode 방식**: Portable sing-box 엔진을 시스템 프록시/TUN 모드로 연동.
- **민감 정보 관리**: 시스템 자격 증명 관리자(Credential Manager)에 저장하여 정보 유출 방지.
- **종료 처리 (Information Release)**: 
  - 앱 종료 시 활성 VPN 연결을 즉시 해제함.
  - 실행 중인 외부 엔진(sing-box, openvpn-gui 등) 프로세스를 안전하게 종료함.
  - 메모리 내 세션 데이터 및 임시 파일, 방화벽 임시 규칙을 모두 삭제함.
  - 모든 서비스 인스턴스에 대해 `Dispose()`를 명시적으로 호출하여 관리되지 않는 리소스 해제.

## 10. 버그 수정 및 개선 사항
- **시스템 트레이 메뉴 가시성 개선 (2026-04-01)**: `H.NotifyIcon` 연동 시 `ContextMenu`가 `DataContext`를 상속받지 못해 메뉴가 표시되지 않거나 명령이 동작하지 않는 문제를 해결함. `ContextMenu` 스타일 개선 및 명시적 `DataContext` 바인딩 적용.

## 9. 남은 작업
- (현재 모든 배정된 작업이 완료되었습니다.)

---
> **참고**: 모든 코드는 `c:\Project\SimpleVPN` 내에서 프로젝트 표준 스타일을 준수하며 개발됨.
자동화하는 `RealityServerBootstrapService`를 구현함.
- **SSH 설정 자동 감지 및 IP 추출 (2026-03-31)**: 로컬 PC의 `~/.ssh/config` 및 `.ssh` 폴더를 분석하여 알려진 서버 IP와 키 정보를 자동으로 가져오고 드롭다운으로 선택할 수 있는 기능을 추가함.
- **커스텀 서버 영구 관리 기능 (2026-03-31)**: 사용자가 직접 서버 이름과 IP를 입력하여 라이브러리에 등록하고 제거할 수 있으며, 이를 `settings.json`에 영구 저장하는 기능을 완료함.
- **공용 서버 IP 복사 편의성 (2026-03-31)**: 서버 라이브러리 목록에서 우클릭 시, 즉시 해당 IP를 China Mode 설정 필드로 복사해오는 기능을 추가함.
- **설정 영구 저장 및 편의성**: `Kill Switch` 상태와 China Mode 프로필 설정을 `LocalSettingsService`를 통해 영구 저장하도록 고도화함.
- **Outline 자동 발급 흐름 추가**: 기존 Outline 서버의 `apiUrl`/`certSha256`로 Access Key를 자동 생성할 수 있음.
- **Outline 서버 자동 부트스트랩 흐름 추가**: SSH 접속 정보가 있으면 공식 설치 스크립트로 새 Outline 서버를 만들고 Access Key까지 연속 생성할 수 있음.
- **China 프로필 슬롯/JSON 기능 추가**: China Mode 설정을 여러 저장 슬롯으로 관리하고, JSON 파일로 가져오기/내보내기 할 수 있음.
- **별도 프로그램 없는 VPN 연결 완료**: Windows 기본 VPN 클라이언트(L2TP/IPsec)를 사용하여 외부 프로그램 설치 없이 실제 터널 연결/해제를 수행함.
- **OpenVPN 옵션 지원 완료**: OpenVPN GUI가 설치된 경우 VPN Gate의 OpenVPN 프로필을 사용해 실제 터널 연결/해제를 수행할 수 있음.
- **동적 VPN 프로필 구성 완료**: 선택한 VPN Gate 서버의 IP 또는 OpenVPN 설정 데이터를 기준으로 앱 내부에서 필요한 연결 구성을 생성하고 연결/해제까지 자동 처리함.
- **서버 목록 실동작 확인**: VPN Gate CSV 응답을 파싱하여 서버 목록을 UI에 표시하고, 선택 서버 기준으로 연결을 시작할 수 있음.
- **트레이 상주 기능 동작**: 메인 창 닫기 시 프로그램이 종료되지 않고 트레이로 숨김 처리되며, 트레이 메뉴에서 앱 열기/연결 해제/종료가 가능함.
- **단일 인스턴스 실행 적용**: 프로그램은 한 번만 실행되며, 중복 실행 시 기존 창을 활성화하고 새 인스턴스는 종료함.
- **연결 상태 표시 강화**: 메인 화면에 `Recent VPN Status` 패널과 `VPN Mode` 선택 UI를 추가하여 선택한 방식의 연결 진행 상태와 최근 상태 메시지를 표시함.
- **OpenVPN 종료 정책 적용 완료**: 프로그램이 직접 시작한 OpenVPN만 앱 종료 시 함께 종료하며, 사용자가 수동으로 실행한 OpenVPN은 종료하지 않음.
- **중국 환경 대응 경로 추가**: 공개 VPN Gate 서버가 아닌 portable sing-box 엔진 기반 `Outline`, `VLESS REALITY`, `Trojan TLS` 연결 경로와 안내 메시지를 앱에 반영함.
- **빌드 결과물 분리 완료 (2026-03-31)**: `bin\Debug`와 `bin\Release`로 빌드 결과물이 구분되도록 설정 변경 및 검증 완료.
- **보안 강화 및 자격 증명 관리자 연동 (2026-03-31)**: 윈도우 자격 증명 관리자(Credential Manager)를 연동하여 Outline Access Key, Vless UUID, Trojan Password 등 민감 정보를 로컬 JSON 파일이 아닌 시스템 엔진에 암호화하여 저장하도록 고도화함. (P/Invoke 기반 Win32 API 활용)
- **환경 범용성 및 부트스트랩 스크립트 고도화 (2026-03-31)**: 다양한 리눅스 배포판(Ubuntu, Debian, CentOS, Fedora 등)에서 환경 자동 감지 및 패키지 매니저(apt, dnf, yum) 연동 기능을 추가하여 서버 자동 구축 호환성 확보.
- **UI 애니메이션 및 프리미엄 경험 고도화 (2026-03-31)**: 창 로드 애니메이션, 연결 시 펄스 효과, 로고 투명도 전환 등 WPF Storyboard 기반의 고수준 애니메이션 시스템 구축 완료.

## 7. 실제 검증 결과
- **Release 빌드 및 실행 검증 완료 (2026-03-30)**: `dotnet clean -c Release`, `dotnet build -c Release` 후 생성된 `SimpleVPNApp.exe`가 정상적으로 실행됨을 확인함.
- **클린/빌드/실행 검증 완료 (2026-03-30)**: `dotnet clean`, `dotnet build`, `dotnet run` 흐름이 정상 동작함.
- **최신 빌드 및 실행 검증 완료 (2026-03-31)**: .NET 9.0 (net9.0-windows) 환경으로의 전환 및 `GuideWindow.xaml`의 Margin 형식 오류를 수정하여 전체 솔루션 클린, 빌드 및 실행 무결성을 재검증함. (SimpleVPNApp.exe 정상 실행 확인)
- **실제 VPN 연결 검증 완료**: 일본 VPN Gate 서버(`public-vpn-189`)를 대상으로 Windows 기본 VPN 연결이 성공함.
- **OpenVPN 옵션 동작 검증 완료**: OpenVPN 방식 선택 시 OpenVPN GUI 기반 연결 경로가 앱 내 서비스로 복원되었고, 설치 여부에 따라 적절한 안내 또는 연결이 가능함.
- **China Mode 입력 검증 완료**: `Outline(ss://)`, `VLESS REALITY`, `Trojan TLS` 프로필별 필수 입력을 검사하고 잘못된 값에 대해 안내 메시지를 표시함.
- **기존 서버 키 생성 경로 구현**: Outline Server Management API의 `/access-keys` 호출로 새 Access Key를 생성해 China Mode 입력칸에 자동 반영함.
- **China 프로필 저장/이식 검증 완료**: China Mode 설정을 다중 슬롯으로 저장하고, JSON 파일로 내보내거나 다시 가져와 복원할 수 있음.
- **새 서버 생성 경로 구현**: SSH로 공식 `install_server.sh`를 실행하고 `/opt/outline/access.txt`에서 `apiUrl`/`certSha256`를 읽어 새 Access Key를 생성함.
- **공인 IP 변경 검증 완료**: 연결 전 공인 IP `121.137.29.163`, 연결 후 공인 IP `219.100.37.244`, 연결 해제 후 공인 IP `121.137.29.163`으로 복귀함.
- **서버 API 응답 검증 완료**: VPN Gate API 응답 코드 `200` 확인 및 서버 목록 실파싱 확인.
- **중복 실행 방지 검증 완료**: 앱을 연속 두 번 실행해도 `SimpleVPNApp` 프로세스는 1개만 유지됨.
- **별도 프로그램 미사용 검증 완료**: OpenVPN 없이 Windows 기본 `Add-VpnConnection` 및 `rasdial.exe` 경로만으로 연결/해제를 동작함.
- **SSH 자동 감지 및 파싱 검증 완료 (2026-03-31)**: 로컬 `.ssh/config` 기반의 서버 목록 노출 및 선택 시 자동 완성 기능 확인.
- **커스텀 서버 추가 및 복원 검증 완료 (2026-03-31)**: 직접 추가한 서버가 앱 재시작 후에도 목록에 유지되는지 확인.
- **Smart Connect 지연 시간 정렬 확인 (2026-03-31)**: Ping 테스트 후 Latency 낮은 순으로 목록이 재정렬되는지 확인.
- **서버 구축 스크립트 실행 확인 (2026-03-31)**: Ubuntu 22.04 서버를 대상으로 VLESS REALITY 자동 구축 로직 검증.
- **빌드 출력 경로 분리 검증 완료 (2026-03-31)**: Debug/Release 빌드 시 각각 `bin\Debug`, `bin\Release` 폴더에 결과물이 생성됨을 확인함.
- **자격 증명 암호화 저장 검증 완료 (2026-03-31)**: `settings.json` 파일 열람 시 민감 정보가 `[PROTECTED]`로 표시되며, 앱 실행 시에는 정상적으로 복원되어 VPN 연결이 수행됨을 확인함.
- **리눅스 배포판 호환성 검증 완료 (2026-03-31)**: `apt`, `dnf`, `yum` 환경을 모두 고려한 쉘 스크립트 분기 처리 로직 검증 완료.
- **UI 애니메이션 빌드 검증 완료 (2026-03-31)**: XAML Storyboard 및 Triggers를 활용한 실시간 애니메이션 효과 적용 및 컴파일 확인.

## 8. 구현 세부 메모
- **Windows 기본 VPN 방식**: `Add-VpnConnection`으로 L2TP/IPsec 프로필을 생성하고 `rasdial.exe`로 연결/해제를 수행함.
- **Windows 기본 VPN 인증 정보**: 현재 VPN Gate 공용 접속 규칙에 맞춰 L2TP 사전 공유 키 `vpn`, 사용자 이름 `vpn`, 비밀번호 `vpn`을 사용함.
- **OpenVPN 방식**: OpenVPN GUI가 설치된 경우 VPN Gate의 Base64 OpenVPN 설정을 앱이 프로필로 생성하여 GUI 명령으로 연결/해제를 수행함.
- **China Mode 방식**: 공개 VPN Gate 서버 대신 사용자가 직접 보유한 `Outline`, `VLESS REALITY`, `Trojan TLS` 설정을 입력받고, 앱 폴더의 portable sing-box 엔진이 시스템 프록시 모드로 직접 연결하도록 구성함.
- **기존 Outline 서버 자동화 방식**: 사용자가 보유한 `apiUrl`와 `certSha256`를 바탕으로 관리 API에 `POST /access-keys`를 호출해 새 키를 생성함.
- **새 Outline 서버 자동화 방식**: SSH로 공식 설치 스크립트를 실행한 뒤 설치 결과에서 `apiUrl`와 `certSha256`를 추출하고 곧바로 첫 Access Key를 생성함.
- **VLESS REALITY 방식**: `Server`, `Port`, `UUID`, `Public Key`, `Server Name(SNI)`, `Short ID`, `Fingerprint`를 바탕으로 sing-box outbound 설정을 생성함.
- **Trojan TLS 방식**: `Server`, `Port`, `Password`, `Server Name(SNI)`, `Fingerprint`를 바탕으로 sing-box outbound 설정을 생성함.
- **China 프로필 저장 방식**: 선택한 China 프로필 유형과 입력값을 이름 있는 저장 슬롯으로 관리하고 `%LocalAppData%\\SimpleVPN\\settings.json`에 저장함.
- **China 프로필 이식 방식**: 현재 China Mode 설정 전체를 JSON 파일로 내보내고, 다른 환경에서 다시 가져와 적용할 수 있음.
- **OpenVPN 종료 정책**: 앱이 직접 시작한 OpenVPN GUI 및 앱이 직접 만든 연결만 종료 대상으로 관리하며, 수동으로 실행 중인 OpenVPN 프로세스는 유지함.
- **종료 처리 및 프록시 해제**: 
  - 앱 종료 시 `App.OnExit`에서 메인 ViewModel의 `Dispose()`를 호출함.
  - `ChinaOptimizedVpnService` 종료 시 레지스트리 `ProxyEnable`을 0으로 돌린 뒤 `wininet.dll`의 `InternetSetOption`을 호출하여 시스템에 프록시 변경을 즉시 전파함.
  - Windows 기본 VPN 및 OpenVPN은 각각 `rasdial` 명령어와 GUI 제어 명령으로 세션을 종료함.
  - `FirewallService`를 통해 활성화된 Kill Switch(방화벽 규칙)를 삭제하여 일반 인터넷 통신을 복구함.

- **실시간 트래픽 모니터링**: `IVpnService`의 `GetStatistics()` 구현과 `MainViewModel` 타이머를 연동하여 상시 트래픽 모니터링 대시보드 구현 완료.
- **Kill Switch**: `FirewallService`에서 Windows 방화벽(`netsh advfirewall`) 규칙을 동적으로 수정하여 VPN 비연결 시 트래픽 차단 기능 구현 완료.
- **UI 대시보드 리뉴얼**: 업로드/다운로드 속도, 총 전송량, 연결 지속 시간을 보여주는 프리미엄 카드 UI 적용 완료.

## 9. 남은 작업
- (현재 모든 배정된 작업이 완료되었습니다.)

---
> **참고**: 모든 코드는 `c:\Project\SimpleVPN` 내에서 프로젝트 표준 스타일을 준수하며 개발됨.

