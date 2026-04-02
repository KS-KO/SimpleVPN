# GitHub Update Setup

SimpleVPN is configured to update from GitHub Releases by using Velopack.

## How it works

1. The app calls `VelopackApp.Build().Run()` during startup.
2. After the main window opens, the app checks `https://github.com/KS-KO/SimpleVPN` for a newer release.
3. If a new release exists, the user is prompted to download it and restart.
4. GitHub Actions builds the Windows package and uploads the Velopack release assets to GitHub Releases whenever a tag like `v1.2.3` is pushed.

## Release steps

1. Commit your changes.
2. Create a tag such as `v0.1.0`.
3. Push the branch and tag to GitHub.
4. Wait for the `Release` workflow to finish.
5. Install the generated setup package from the GitHub Release once, then future app launches can self-update.

## Notes

- Auto-update works only when the app is running from a Velopack-installed release, not from Visual Studio or a plain `dotnet run`.
- The workflow uses the repository URL `https://github.com/KS-KO/SimpleVPN`.
- The package id is `KSKO.SimpleVPN`.
