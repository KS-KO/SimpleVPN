# SimpleVPN Mobile Roadmap

## What Changed

- Added a shared `SimpleVPN.Core` project for cross-platform models.
- Moved portable China Mode payload/config generation into `SimpleVPN.Core`.
- Updated the Windows app to consume the shared core so Android/iOS can reuse the same profile format.

## Reusable From `SimpleVPN.Core`

- `SimpleVPN.Core/Models/*`
- `SimpleVPN.Core/Protocols/ChinaModePayloadBuilder.cs`
- `SimpleVPN.Core/Protocols/SingBoxConfigBuilder.cs`

These pieces are platform-neutral and can be referenced from:

- `.NET MAUI`
- Android native app
- iOS app + extension target

## Still Windows-Specific

- `SimpleVPNApp/Services/WindowsBuiltInVpnService.cs`
- `SimpleVPNApp/Services/OpenVpnService.cs`
- `SimpleVPNApp/Services/ChinaOptimizedVpnService.cs`
- WPF UI in `SimpleVPNApp`

## Recommended Next Steps

1. Create a new `SimpleVPN.Mobile` app shell with `.NET MAUI`.
2. Reuse `SimpleVPN.Core` for server list, saved profiles, and China Mode payload generation.
3. Add an Android VPN implementation using `VpnService`.
4. Add an iOS VPN implementation using `NetworkExtension` / `NEPacketTunnelProvider`.
5. Keep Windows-only VPN services in `SimpleVPNApp`, and hide unsupported connection types on mobile until each protocol is implemented.

## Suggested Phase Order

1. Android support first
2. Shared settings/profile sync
3. iOS tunnel extension
4. Mobile-specific onboarding and permission flows

## Notes

- `SingBoxConfigBuilder` can already generate payload-derived config JSON that a mobile tunnel layer can consume.
- Mobile should not reuse Windows proxy, registry, `netsh`, or `rasdial` logic.
- iOS support will require Apple entitlements and a packet tunnel extension target.
