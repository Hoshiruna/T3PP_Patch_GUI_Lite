# T3PP_PatchGUIlite

[![CI](https://github.com/Hoshiruna/T3PP_Patch_GUI_Lite/actions/workflows/ci.yml/badge.svg)](https://github.com/Hoshiruna/T3PP_Patch_GUI_Lite/actions/workflows/ci.yml)

![logo](PatchGUIlite/res/t3pp.png)

## About
T3PP_PatchGUIlite is a Windows WPF frontend for Touhou 3rd-party Patch (`.t3pp`) packages. It lets you apply patches to game installs or generate new patch files with a simple UI backed by the native T3pp toolkit and xdelta3.

Get the full version: [Here](https://github.com/CNTianQi233/T3PP-PatchGUI-Public)

## Features
- Apply `.t3pp` patches to a directory or single file, with hash readout (CRC32/MD5/SHA1) for the target.
- Generate `.t3pp` patches from an old/new directory pair using bundled xdelta3.
- View native log output inside the app to help troubleshoot patching.
- Language toggle (Simplified Chinese by default, English included).

## Requirements
- Windows with .NET 8 SDK (desktop workload) for building.
- Windows with .NET 8.0 Windows Desktop Runtime installed to run.
- Visual Studio 2022 (optional) or the `dotnet` CLI.
- Native helpers: `T3ppNativeLite.dll` is copied automatically; xdelta3 is embedded as a resource.

## Build
```bash
dotnet restore PatchGUI/PatchGUIlite.csproj
dotnet build PatchGUI/PatchGUIlite.csproj -c Debug
dotnet build PatchGUI/PatchGUIlite.csproj -c Release
```

## Run
- Debug: `PatchGUI/bin/Debug/net8.0-windows7.0/PatchGUIlite.exe`
- Release: `PatchGUI/bin/Release/net8.0-windows7.0/PatchGUIlite.exe`

Keep these files next to the executable:
- `PatchGUIlite.dll`, `PatchGUIlite.deps.json`, `PatchGUIlite.runtimeconfig.json`
- `T3ppNativelite.dll`
- `lang/` folder (`zh_CN.json`, `en_US.json`)

## Updates (Release ZIP)
- Use **Settings → Updates → Check for Updates** to compare the EXE metadata version with the GitHub `version`.
- If a newer version exists, the app downloads the latest GitHub Release `.zip`, extracts it, and then closes to replace local files.
- Publish a `.zip` asset in the latest release so the updater can find it.
- Bump `PatchGUIlite/version.txt` and the version fields in `PatchGUIlite/PatchGUIlite.csproj` when publishing a new version.

## Publish (self-contained)
```bash
dotnet publish PatchGUI/PatchGUIlite.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```
Output: `PatchGUI/bin/Release/net8.0-windows7.0/win-x64/publish/PatchGUIlite.exe`

## Usage
### Apply a patch
1. Choose the target mode (directory or file) from the toolbar.
2. Select the target directory/file.
3. Select a `.t3pp` patch file.
4. Click **Apply Patch** to apply. Progress and native logs appear in the console area.

### Generate a patch 
1. Select the mode for generate patch (`file` or `directory`)
2. Select the source directory or file (`Original`) and the destination directory or file (`Modified`).
3. (Optional) Keep `Pack directory` enabled to bundle outputs.
4. Click **Generate Patch** to create a `.t3pp` file.

## Localization
Language files live in `PatchGUI/lang/*.json`. To add a language, provide a new JSON file and wire it into the language selector in `MainWindow.xaml` / code-behind.

## Development notes
- The run.log file is now only output in debug.
- UI is built with [Wpf.Ui](https://github.com/lepoco/wpfui); code editing view uses [AvalonEdit](https://github.com/icsharpcode/AvalonEdit).
