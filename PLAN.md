# NewKlear — Windows 11 Cleaner & Optimizer — Build Plan

Status: APPROVED 2026-11-12. .NET 8 WPF, name NewKlear, dev caches yes, privacy tweaks yes, Copilot removal yes.

## 1. Goal

A desktop app for Windows 11 that (a) finds and removes temporary / cache / leftover
files, (b) removes preinstalled bloatware apps, (c) manages startup programs, and
(d) applies well-known, reversible Windows 11 performance and privacy settings.
Everything is scan-first, opt-in, logged, and undoable where possible.

Deliberately NOT included (snake oil or risky): registry "cleaning", RAM boosters,
driver updaters, deleting Prefetch (it makes boots slower, not faster).

## 2. Tech stack (recommended)

- .NET 8 + WPF, with the WPF-UI library for a Fluent / Windows 11 look (dark & light,
  follows system theme). Requires installing the .NET 8 SDK (`winget install Microsoft.DotNet.SDK.8`).
- MVVM via CommunityToolkit.Mvvm.
- Native APIs where possible: Microsoft.Win32.Registry, System.ServiceProcess,
  System.IO. PowerShell (hosted via Microsoft.PowerShell.SDK) only for Appx
  package work and DISM.
- App manifest requests Administrator at launch.
- Output: single self-contained `tclear.exe` for win-x64 (no runtime install needed).

Alternative if you prefer: Electron (Node is already installed) with the same core
logic written in TypeScript shelling out to PowerShell. Heavier app, faster to style.

## 3. Project layout

```
tclear/
  tclear.sln
  src/TClear.Core/        # cleaners, tweaks, bloat catalog, startup mgr, backup/undo, logging
  src/TClear.App/         # WPF UI (views, viewmodels, resources)
  tests/TClear.Core.Tests/ # xunit tests for scanners/tweaks (dry-run paths)
  PLAN.md, README.md
```

Core interfaces:

```
ICleaner : Name, Category, Risk (Safe/Caution), Default-on?, ScanAsync() -> items + bytes, CleanAsync(progress)
ITweak   : Name, Description, Category, Risk, GetStateAsync(), ApplyAsync(), RevertAsync()
```

## 4. Cleaning modules (scan -> show size -> you tick -> clean)

Safe (on by default):
- User temp (%TEMP%), Windows temp (C:\Windows\Temp)
- Windows Update download cache (SoftwareDistribution\Download) — stops/restarts wuauserv
- Delivery Optimization cache
- Recycle Bin
- Thumbnail cache, icon cache, font cache
- DirectX shader cache
- Windows Error Reporting files, minidumps, memory.dmp
- Windows logs: CBS, Panther, DISM, setup logs
- Browser caches only (never cookies/passwords/history): Edge, Chrome, Brave, Firefox
- Microsoft Store cache
- App caches: Teams, Discord, Spotify, Adobe media cache
- Developer caches (optional group): npm, pip, NuGet, VS Code cache/logs

Caution (off by default, with explanation):
- Windows.old / previous installations (blocks rollback of a feature update)
- DISM component store cleanup (StartComponentCleanup; blocks uninstalling updates)
- Hibernation file removal (hiberfil.sys; disables hibernate and fast startup)
- Event logs clear

Rules: skip locked files, never touch Documents/Desktop/Program Files, show per-item
sizes before deleting, write a log of every path removed.

## 5. Bloatware removal

- Lists installed Appx packages (current user + provisioned for all users).
- Curated catalog tags each as Recommended-to-remove / Optional / Protected.
  Recommended: Xbox apps & Game Bar, Clipchamp, Solitaire Collection, Bing News/Weather,
  Teams (consumer), Cortana, Feedback Hub, Get Help, Tips, 3D Viewer, Mixed Reality
  Portal, Power Automate, Skype, Microsoft To Do, Office Hub, Phone Link, Copilot (optional).
  Protected (hidden by default): Store, Calculator, Photos, Terminal, Notepad, Snipping
  Tool, Camera, Settings/shell components, Edge (WebView2 dependency).
- Removes for current user; optional checkbox to also deprovision for new accounts.

## 6. Startup manager

- Reads HKCU/HKLM Run, RunOnce, StartupApproved, and Startup folders.
- Enable / disable per entry (uses StartupApproved so Task Manager stays in sync).
- Shows publisher, path, and Windows' own startup-impact rating where available.

## 7. Optimization settings (toggle pages, each shows current state)

Performance:
- Visual effects: Best performance / Balanced (animations, transparency, shadows)
- Power plan: Balanced / High performance / Ultimate performance
- Disable Game DVR background recording
- Disable background apps for Store apps
- Fast startup on/off
- Storage Sense on + schedule
- Disable Delivery Optimization peer upload
- Services (each with a plain-English note): Xbox services, Fax, Remote Registry,
  Print Spooler (if no printer), Windows Search indexing scope

Privacy / telemetry:
- Diagnostic data to Required only, disable tailored experiences, advertising ID,
  activity history, feedback requests, suggested content in Settings, Start suggestions
- Disable Bing / web results in Start search
- Disable Copilot, Widgets, Chat taskbar buttons

Explorer / shell quality of life:
- Show file extensions, classic right-click menu, End Task on taskbar, Explorer opens to This PC

## 8. Safety & undo

- "Create System Restore point" button, and prompted automatically before the first tweak in a session.
- Every registry change is backed up to `%LocalAppData%\tclear\backups\<timestamp>.reg` before applying; each tweak has Revert.
- Undo history page listing applied tweaks and removed apps (Appx reinstall where possible).
- Nothing destructive runs without an explicit click; a dry-run scan is always the first step.
- Full log file per session.

## 9. UI screens

1. Dashboard — disk usage bar, RAM/CPU/uptime, "Quick Scan", last-clean summary
2. Clean — grouped checklist with sizes, Select recommended, Clean selected, progress
3. Bloatware — searchable list with tags, Remove selected
4. Startup — list with on/off switches
5. Optimize — categorized toggle cards with explanations, Apply, Revert
6. History & Log — undo list, log viewer, restore point button
7. Settings — theme, developer caches on/off, open backups folder

## 10. Build phases

1. Scaffold solution, install SDK, UI shell with navigation, Core interfaces, the
   Safe cleaners, Dashboard + Clean pages. (Working, usable app at end of phase.)
2. Bloatware catalog + removal, Startup manager.
3. Tweaks engine with registry backup/revert, restore point, Optimize + History pages.
4. Caution cleaners, dev caches, tests, single-file publish, README.

## 11. Open questions (answer when approving)

1. Stack: .NET 8 WPF as recommended (needs SDK install), or Electron?
2. App name: "tclear" (from the folder), or something else?
3. Include developer caches (npm, pip, NuGet, VS Code) as an optional group?
4. Include the privacy/telemetry tweaks, or performance only?
5. Should Copilot removal be offered?

## 12. Progress

- [x] .NET 8 SDK installed, solution scaffolded (Core, App, Tests), NuGet packages restored
- [x] Logo assets: app icon (.ico), N mark, wordmark
- [x] Core engine for all phases (cleaners, bloat, startup, tweaks, restore points, history)
- [x] Phase 1: UI shell, 5 themes, Dashboard, Clean page
- [x] Phase 2: Bloatware page (incl. one-click Copilot removal), Startup page
- [x] Phase 3: Optimize page (36 tweaks + power plans), restore points, History & Undo, Settings
- [x] Phase 4: Caution cleaners, dev caches, 45 unit tests (all passing), single-file publish (publish/NewKlear.exe), README

Themes: Ember (default, black + logo orange), Ember Light, Reactor (green, a nod to the "nuclear" name), Carbon Cyan, Midnight Violet.
