# Game Cheats Manager WinUI 3 Port

This project is a WinUI 3 port of the original Python `Game-Cheats-Manager` application.

Original upstream project:
`https://github.com/dyang886/Game-Cheats-Manager`

## What is ported

- Native WinUI 3 two-pane shell for installed trainers and download search
- Local trainer library search, launch, delete, import, and download-path migration
- Trainer data refresh, trainer update checks, and download queue handling
- Source management for GCM, FLiNG, XiaoXing, Cheat Tables, Wand, Cheat Engine, and Cheat Evolution
- Upload flow, announcement/update checks, and Windows Defender whitelist action

## Backend configuration

The original app depends on private backend values that are not committed to source control. The WinUI port keeps those values out of tracked files and resolves them in this order:

1. Environment variables
2. `secret_config.py` in the project root
3. `secret_config.py` next to the built executable

Supported variables:

- `SIGNED_URL_DOWNLOAD_ENDPOINT`
- `SIGNED_URL_UPLOAD_ENDPOINT`
- `VERSION_CHECKER_ENDPOINT`
- `PATCH_PATTERNS_ENDPOINT`
- `CLIENT_API_KEY`

If those values are missing, the WinUI app still builds and the local-library features still work, but signed-download/upload/version/patch flows that depend on the original backend will be unavailable.

## Project-local runtime assets

The WinUI project ships its own runtime assets under `Assets` and `Dependencies`. It does not depend on files from any external local clone path at runtime.

## Build

```powershell
dotnet restore
dotnet build
```

## Single-file build

```powershell
dotnet build .\GameCheatsManager.WinUI.csproj -c Release -t:BuildSingleFile
```

The single-file executable is written to `artifacts\release\win-x64\GameCheatsManager.WinUI.exe`.
This build is framework-dependent: the target machine must have the .NET Desktop Runtime and Windows App SDK Runtime installed.

## Release bundle build

```powershell
dotnet build .\GameCheatsManager.WinUI.csproj -c Release -t:BuildSingleFileBundle
```

This creates a zipped release bundle at `artifacts\release\win-x64\GameCheatsManager.WinUI.zip`.
The raw executable stays around `38 MB`, but the zipped bundle is much smaller and is the recommended release artifact.

## GitHub release workflow

`.github/workflows/release.yml` builds the zipped single-file bundle on `windows-latest` and uploads the `.zip` plus a matching `.sha256` file to the GitHub Release asset list.
