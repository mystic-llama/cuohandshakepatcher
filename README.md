# CUO Handshake Fix Patcher

CUO Handshake Fix Patcher is a portable Windows utility for Razor Enhanced users who get disconnected after about four minutes when playing through ClassicUO or Legacy TazUO. It updates Razor Enhanced’s handling of the `0xD5` server handshake and backs up `RazorEnhanced.exe` before patching it.

## Download

Open **Releases** on the GitHub repository and download `CuoHandshakePatcher-v1.0.0-win-x64.zip`. The portable build includes the .NET Desktop runtime. Compatibility covers **ClassicUO** and **Legacy TazUO (.NET Framework 4.7.2)** with Razor Enhanced in plugin mode.

## Apply the patch

1. Close Razor Enhanced and ClassicUO or Legacy TazUO.
2. Extract the ZIP and run `CuoHandshakePatcher.exe`. Keep the `Assets` folder next to the executable for the banner artwork.
3. Confirm the detected Razor Enhanced folder or select it with **Browse**.
4. Click **Apply Patch**.
5. Open Razor Enhanced from the same folder and select **Launch CUO**.

To undo the patch, close both apps, reopen the patcher, and select **Restore Backup**. The patcher restores the latest backup of `RazorEnhanced.exe` and removes `CuoHandshakeFix.dll`.

## Build the portable Windows x64 release

On Windows, install the .NET 8 SDK and the .NET Framework 4.7.2 Developer Pack. The command builds the project directly. Opening or building the `.slnx` solution requires Visual Studio 2022 17.13 or later, or .NET SDK 9.0.200 or later. Microsoft ended support for .NET 8 on November 10, 2026.

Run this from the project root in PowerShell.

```powershell
$sourceRoot = (Get-Location).Path
dotnet publish src/CuoHandshakePatcher/CuoHandshakePatcher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false "-p:PathMap=$sourceRoot=/_/" --output ./publish/win-x64
```

For a GitHub release, package the published executable and `Assets` folder with the README and applicable third-party notices. Attach the ZIP to the release for the matching source tag. GitHub generates source archives from that tag automatically.

## Project layout

- `src/CuoHandshakeFix` — net472 runtime fix (`Bootstrap.Apply`, `ServerCheck`)
- `src/CuoHandshakePatcher` — WPF UI + Mono.Cecil injector

See [third-party notices](THIRD-PARTY-NOTICES.md) for the provenance and licenses of bundled components.

## Banner art

Replace `src/CuoHandshakePatcher/Assets/banner.png` and republish if you want custom artwork.
