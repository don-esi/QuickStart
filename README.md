# Quick Start Menu

Minimalist Windows start-menu alternative (PSMenu-like) built with **WPF** for **Visual Studio 2026**.

Repository: https://github.com/don-esi/QuickStartMenu

| | |
|---|---|
| Solution | `QuickStartMenu.sln` (VS Version 18) |
| TFM | `net10.0-windows` |
| SDK on machine | .NET 10 (`10.0.403`) |
| Language | C# (`LangVersion=latest`; C# 13+ with .NET 10) |

## Features

- System tray icon → flyout menu to launch configured items
- Launch `.exe`, `.cmd` / `.bat`, `.ps1`, `.lnk`, and other shell-openable paths
- Folder grouping
- Management window: drag/drop files from Explorer, reorder/regroup in the tree
- Start with Windows (HKCU Run key)
- Import taskbar-pinned apps from the classic `User Pinned\TaskBar` shortcuts folder

## Open in Visual Studio 2026

1. Open `D:\Source\QuickStartMenu\QuickStartMenu.sln` in **Visual Studio Community 2026**
2. Set **QuickStartMenu** as the startup project (if needed)
3. Press **F5**

## Run from CLI

```powershell
cd D:\Source\QuickStartMenu
dotnet build QuickStartMenu.sln -c Release
dotnet run --project QuickStartMenu.csproj -c Release
```

Or launch the built exe:

```powershell
.\bin\Release\net10.0-windows\QuickStartMenu.exe
```

## Config / debug log

- Menu: `%AppData%\QuickStartMenu\menu.json`
- Debug log: `%AppData%\QuickStartMenu\debug.log`

Left-click the tray icon to open the flyout; right-click for Manage / Exit.

## Tray icon visibility (Windows 10/11)

Microsoft does **not** provide a supported API to force a notification icon into the always-visible tray. New icons go to the overflow (`^`) by default.

On **Windows 11**, Quick Start Menu best-effort sets the undocumented `IsPromoted=1` value under `HKCU\Control Panel\NotifyIconSettings` when Explorer creates our entry (and never overrides an explicit user hide). First run also shows a balloon + short guidance and can open Taskbar settings.

**Manual (any Windows 10/11):** Settings → Personalization → Taskbar → **Other system tray icons** → enable Quick Start Menu.  
Tray menu / Manage → **Keep icon visible…** / **Tray icon settings…** opens the same Settings page.

## Installer (Inno Setup)

Self-contained **win-x64** single-file publish + Inno Setup 6 setup exe. No separate .NET runtime install required. “Start with Windows” stays the in-app toggle (HKCU Run); uninstall clears that key if present.

**Prerequisite:** [Inno Setup 6](https://jrsoftware.org/isinfo.php) (`winget install JRSoftware.InnoSetup`)

```powershell
cd D:\Source\QuickStartMenu
.\scripts\build-installer.ps1
# optional: .\scripts\build-installer.ps1 -Version 1.0.1
```

**Output:** `artifacts\installer\QuickStartMenu-Setup-<version>.exe`  
(e.g. `D:\Source\QuickStartMenu\artifacts\installer\QuickStartMenu-Setup-1.0.0.exe`)

The installer adds a Start Menu shortcut; desktop shortcut is optional. Per-user install by default (`PrivilegesRequired=lowest`).

Script / ISS: `scripts\build-installer.ps1`, `installer\QuickStartMenu.iss`

## Smoke test

```powershell
dotnet run --project QuickStartMenu.csproj -c Release -- --smoke-flyout
```

Expect exit code `0` and `Smoke: ... result=PASS` / `onScreen=True` in `%AppData%\QuickStartMenu\debug.log`.
Startup stamp should be `build=2026-11-09-dpi-fix`.

## Pinned-apps note

Import reads `.lnk` files under:

`%AppData%\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar`

That covers classic taskbar pins. Some Win11 pin metadata is not exposed via a stable public API; those may not appear until Microsoft documents a supported surface.
