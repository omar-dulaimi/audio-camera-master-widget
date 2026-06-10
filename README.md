# Audio Camera Master Widget

Windows WPF app for local audio endpoint controls, microphone metering, camera preview, and Windows settings shortcuts.

The app starts with the compact master widget. The full control panel opens from the widget.

## Install

Download the installer from GitHub Releases:

```text
https://github.com/omar-dulaimi/audio-camera-master-widget/releases
```

Use this asset:

```text
AudioCameraMasterWidgetSetup.exe
```

Run the installer. It installs per-user and does not require admin rights.

Installed app path:

```text
%LOCALAPPDATA%\Programs\AudioCameraMasterWidget\AudioCameraMasterWidget.exe
```

Desktop shortcut:

```text
%USERPROFILE%\Desktop\Audio Camera Master Widget.lnk
```

Start Menu folder:

```text
%APPDATA%\Microsoft\Windows\Start Menu\Programs\Audio Camera Master Widget
```

## Run

Use the desktop shortcut, the Start Menu shortcut, or run:

```powershell
& "$env:LOCALAPPDATA\Programs\AudioCameraMasterWidget\AudioCameraMasterWidget.exe"
```

The master widget opens first. Use **Full control panel** to open the larger window.

## Upgrade

Download the newer `AudioCameraMasterWidgetSetup.exe` from GitHub Releases and run it.

The installer overwrites the app files in:

```text
%LOCALAPPDATA%\Programs\AudioCameraMasterWidget
```

Saved device selections remain in the app settings file.

## Uninstall

Use **Uninstall Audio Camera Master Widget** from the Start Menu folder, or run:

```powershell
& "$env:LOCALAPPDATA\Programs\AudioCameraMasterWidget\Uninstall.exe"
```

## User Prerequisites

- Windows 10 or Windows 11 on x64 hardware.

The release installer is self-contained. It does not require a separate .NET runtime install.

The app and installer are unsigned. Windows Application Control, Smart App Control, or enterprise code integrity policy can block unsigned executables. This repo does not include a signing certificate.

## Features

- Lists active output and input audio devices.
- Shows the default output and input endpoint reported by Windows.
- Reads endpoint volume and mute state when available.
- Sets endpoint volume and mute state when available.
- Provides 0%, 30%, 35%, 40%, 45%, and 60% volume preset buttons. Clicking a preset also unmutes the endpoint.
- Provides Meeting (mic 40%, output 45%), Private (both muted at 0%), and Media (mic muted at 0%, output 35%) quick presets.
- Shows a live microphone peak meter when available.
- Lists local cameras.
- Starts and stops local camera preview.
- Opens Windows settings pages for sound, app volume, camera, camera privacy, and microphone privacy.
- Stores the last selected output, input, and camera IDs.

## Stored Data

Settings are stored at:

```text
%LOCALAPPDATA%\AudioCameraControlPanel\settings.json
```

The settings file stores device IDs only. It does not store audio, video, or images.

## Developer Prerequisites

- Windows.
- .NET 10 SDK, as selected by `global.json`.
- .NET 8 Windows Desktop Runtime for framework-dependent local runs.
- NSIS `makensis.exe` only when building the installer locally.

The app targets:

```text
net8.0-windows10.0.19041.0
```

## Build And Test

Run from this directory in Windows PowerShell:

```powershell
dotnet restore AudioCameraControlPanel.sln
dotnet build AudioCameraControlPanel.sln --no-restore
dotnet test AudioCameraControlPanel.sln --no-build
```

Run from source:

```powershell
dotnet run --project AudioCameraControlPanel\AudioCameraControlPanel.csproj
```

Generated outputs are ignored by git:

```text
bin/
obj/
artifacts/
tools/
TestResults/
```

## Build Installer Locally

Build a self-contained publish and NSIS installer:

```powershell
.\scripts\build-installer.ps1 -Version 1.0.0
```

Installer output:

```text
artifacts\AudioCameraMasterWidgetSetup.exe
```

Self-contained publish output:

```text
artifacts\publish-win-x64
```

`scripts\build-installer.ps1` looks for `makensis.exe` in:

```text
tools\nsis-msys2\mingw32\bin\makensis.exe
```

If that file is not present, it checks common NSIS install paths and then `PATH`.

## Release Automation

Releases are automated by:

```text
.github/workflows/release.yml
```

The workflow runs on:

- Tags matching `v*.*.*`.
- Manual `workflow_dispatch` with a SemVer input.

Create a release from a tag:

```powershell
git tag v1.0.1
git push origin v1.0.1
```

The workflow:

- Restores, builds, and tests the solution.
- Installs NSIS on the Windows runner.
- Runs `scripts\build-installer.ps1`.
- Creates `AudioCameraMasterWidgetSetup.exe`.
- Creates `AudioCameraMasterWidget-win-x64-self-contained.zip`.
- Uploads both files as workflow artifacts.
- Creates a GitHub Release with generated release notes.

Manual release workflow input uses the version without `v`, for example:

```text
1.0.1
```

Prerelease versions such as `1.0.1-beta.1` are marked as GitHub prereleases.

## CI

Pull requests and pushes to `main` run:

```text
.github/workflows/windows-ci.yml
```

The CI workflow restores, builds, and tests on `windows-2025`.

## Contributing

Issues and pull requests are welcome. See `CONTRIBUTING.md` for local setup, test commands, and pull request expectations.

Do not post private device IDs, logs, screenshots, or vulnerability details in public issues.

## Security

See `SECURITY.md` for vulnerability reporting guidance.

## License

This project is licensed under the MIT License. See `LICENSE`.

## Implementation Notes

- Audio device enumeration uses Core Audio MMDevice APIs.
- Endpoint volume and mute use `IAudioEndpointVolume`.
- Audio device/default changes use `IMMNotificationClient`.
- Endpoint volume/mute changes use `IAudioEndpointVolumeCallback`.
- Camera enumeration and preview use WinRT media capture APIs.
- Tests use MSTest and fake hardware services.

## Limitations

- The app does not change the Windows default audio device.
- Some devices do not expose endpoint volume, mute, or metering controls.
- Compact widgets are normal WPF windows, not Windows Widgets board integrations.
- Camera preview is local only. The app does not record or save camera frames.
- Building or running the WPF app requires Windows. Non-Windows environments can only cross-target with `/p:EnableWindowsTargeting=true`.
