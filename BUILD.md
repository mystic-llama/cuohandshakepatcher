# Build from source

## Build the portable Windows x64 release

On Windows, install the .NET 8 SDK and the .NET Framework 4.7.2 Developer Pack. The command builds the project directly. Opening or building the `.slnx` solution requires Visual Studio 2022 17.13 or later, or .NET SDK 9.0.200 or later. Microsoft ended support for .NET 8 on November 10, 2026.

Run this from the project root in PowerShell.

```powershell
$sourceRoot = (Get-Location).Path
dotnet publish src/CuoHandshakePatcher/CuoHandshakePatcher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false "-p:PathMap=$sourceRoot=/_/" --output ./publish/win-x64
```

## Project layout

- `src/CuoHandshakeFix` — net472 runtime fix (`Bootstrap.Apply`, `ServerCheck`)
- `src/CuoHandshakePatcher` — WPF UI + Mono.Cecil injector

## Banner art

Replace `src/CuoHandshakePatcher/Assets/banner.png` and republish if you want custom artwork.
