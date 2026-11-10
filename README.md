# QuickStart

**Built with [Cursor](https://cursor.com) / Grok.**

Minimalist Windows start-menu alternative (PSMenu-like) built with **WPF** for **Visual Studio 2026**.

Repository: https://github.com/don-esi/QuickStart

## Overview

Designed to reduce Windows taskbar clutter from massive workflows by moving secondary apps to a clean tray flyout.

Prebuilt installer (optional): [v1.0.0 Setup](https://github.com/don-esi/QuickStart/releases/tag/v1.0.0).

## Features

* **Tray Menu:** Left-click the tray icon to launch tools; right-click for Manage / Exit.
* **Launches Anything:** Supports `.exe`, `.cmd`/`.bat`, `.ps1`, shortcuts (`.lnk`), and other shell-openable paths.
* **Tree Manager:** Drag and drop files from File Explorer into organized folders (folders start collapsed).
* **Taskbar Import:** Imports classic pins from `User Pinned\TaskBar`.
* **Start with Windows:** Optional HKCU Run toggle in Manage.
* **Win11 Tray Fix:** Best-effort `IsPromoted=1` so the icon is more likely to stay visible.

## Tech Stack

* **Framework / Language:** WPF on **.NET 10** (`net10.0-windows`) with **C#** (`LangVersion=latest`).
* **IDE Target:** Visual Studio 2026 (solution format Version 18).
* **Installer:** Inno Setup 6 (self-contained win-x64 single-file) — required only if you want a local Setup build.

## Build and Run

1. Open **`QuickStart.sln`** in **Visual Studio 2026** and set **QuickStart** as the startup project.
2. Press **F5** (Debug) to run from the tray.
3. To produce the installer: switch the solution to **Release**, then **Build → Build Solution**.  
   Output: `artifacts\installer\QuickStart-Setup-1.0.0.exe`  
   (*Debug / F5 does not build the installer. Inno Setup 6 must be installed for Release installer builds.*)

## Optional: smoke test

In Visual Studio: **Project → Properties → Debug → Application arguments** → `--smoke-flyout`, then **F5**.  
Expect exit code `0` and a `PASS` line in `%AppData%\QuickStart\debug.log`.

## Config

* Menu: `%AppData%\QuickStart\menu.json`
* Debug log: `%AppData%\QuickStart\debug.log` (rotates; Release logs exceptions only)
