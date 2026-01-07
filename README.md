# T3PP_PatchGUIlite

[![CI](https://github.com/Hoshiruna/T3PP_Patch_GUI_Lite/actions/workflows/ci.yml/badge.svg)](https://github.com/Hoshiruna/T3PP_Patch_GUI_Lite/actions/workflows/ci.yml)

![logo](PatchGUIlite/res/t3pp.png)

## About
Windows WPF frontend for Touhou 3rd-party Patch (`.t3pp`) packages.

Get the full version: [Here](https://github.com/CNTianQi233/T3PP-PatchGUI-Public)

## Features
- Apply `.t3pp` patches to a directory or single file with hash readout.
- Generate `.t3pp` patches from old/new directories or files using T3ppNativelite_lib.
- View native log output to troubleshoot patching.
- Language toggle (Simplified Chinese default, English included).

## Requirements
- Windows, .NET 8 SDK (desktop workload) to build; .NET 8 Desktop Runtime to run.
- Visual Studio 2022 (optional) or `dotnet` CLI.
- Native helper: `T3ppNativelite_Debug_x64.dll` / `T3ppNativelite_Release_x64.dll` built via CMake or provided.

## Build
### Native (CMake)
```bash
cmake -S . -B build -G "Visual Studio 17 2022" -A x64
cmake --build build --config Release
```

### WPF app
```bash
dotnet restore PatchGUIlite/PatchGUIlite.csproj
dotnet build   PatchGUIlite/PatchGUIlite.csproj -c Debug
dotnet build   PatchGUIlite/PatchGUIlite.csproj -c Release
```

## Run
- Debug: `PatchGUIlite/bin/Debug/net8.0-windows7.0/PatchGUIlite.exe`
- Release: `PatchGUIlite/bin/Release/net8.0-windows7.0/PatchGUIlite.exe`

Keep alongside the executable:
- `PatchGUIlite.dll`, `PatchGUIlite.deps.json`, `PatchGUIlite.runtimeconfig.json`
- `T3ppNativelite_Debug_x64.dll` (Debug) or `T3ppNativelite_Release_x64.dll` (Release)
- `lang/` (`zh_CN.json`, `en_US.json`)

## Publish (self-contained)
```bash
dotnet publish PatchGUIlite/PatchGUIlite.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```
Output: `PatchGUIlite/bin/Release/net8.0-windows7.0/win-x64/publish/PatchGUIlite.exe`

## Usage
### Apply a patch
1. Choose target mode (directory/file).
2. Pick target.
3. Pick `.t3pp` file.
4. Click **Apply Patch**; watch logs in the console area.

### Generate a patch
1. Choose mode (`file`/`directory`).
2. Pick Original and Modified sources.
3. (Optional) keep `Pack directory` enabled to bundle outputs.
4. Click **Generate Patch** to produce a `.t3pp`.

## Localization
Language files live in `PatchGUIlite/lang/*.json`. Add a new JSON and wire it into the selector in `MainWindow.xaml` / code-behind.

## Notes
- Advantages over raw xdelta: bundles multiple files, embeds hashes/sizes, supports encrypted payloads, and can fall back to full-file data when delta is unsafe or unnecessary.
