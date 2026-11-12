# NewKlear

**Clean · Optimize · Boost** — a Windows 11 cleaner and optimizer.

NewKlear removes temporary files and caches, uninstalls preinstalled bloatware (including Copilot), manages startup apps, and applies proven Windows 11 performance and privacy settings. Everything is scan-first, opt-in, logged, and undoable where Windows allows it.

## Run it

Run `publish\NewKlear.exe`. It is a single self-contained file, so the target PC does not need .NET installed. Windows will ask for administrator permission, which NewKlear needs to clean system folders and change machine-wide settings.

If you decline the prompt, start it another way, or run an unelevated dev build, NewKlear opens in **Limited mode**. User-level items still work and system items show "Needs admin".

## Pages

| Page | What it does |
|---|---|
| Dashboard | Live CPU, memory, disk and uptime. Quick Scan shows how much junk there is. |
| Clean | Grouped checklist with sizes. Nothing is deleted until you confirm the dialog. |
| Bloatware | Lists Store apps tagged Recommended / Optional / Keep. One-click Copilot removal. Optional removal for new user accounts. |
| Startup | Turns startup apps on or off using the same flags as Task Manager. Nothing is deleted. |
| Optimize | 36 tweaks across Performance, Privacy & AI, Explorer & taskbar, and Services, plus power plans. |
| History & Undo | Every change NewKlear made. Tweaks, removed apps and power plans can be undone. |
| Settings | Five themes, developer caches toggle, restore point prompt. |

## Safety design

- **Scan first.** Cleaning always shows a confirmation listing each item and its size.
- **Protected folders.** A guard refuses to sweep drive roots, Windows, Program Files, your profile, Documents, Desktop, Downloads, Pictures and similar, even if a definition is wrong.
- **No junction following.** Symbolic links and junctions inside cache folders are never followed.
- **In-use files are skipped**, never forced.
- **Temp folders keep files newer than 6 hours**, so running installers are not broken.
- **Browser cleaning is cache only.** Cookies, passwords, history and logins are kept.
- **Advanced items are off by default** and carry a warning: Windows.old, DISM component cleanup, hibernation file, event logs.
- **Every tweak backs up the original value** before changing it, both as JSON for exact revert and as a `.reg` export. Turning a tweak off restores the original, not just a guessed default.
- **Restore point prompt** before the first tweak in each session. If System Protection is off, NewKlear offers to turn it on.
- **No snake oil.** No registry "cleaning", RAM boosters, driver updaters or Prefetch deletion.

## Where data lives

`%LocalAppData%\NewKlear\`

- `logs\` holds one log file per session, listing every action.
- `backups\` holds tweak backups (`tweak-state.json`) and `.reg` exports.
- `history.json` holds the History & Undo list.
- `settings.json` holds your theme and preferences.

## Build from source

Requires the .NET 8 SDK.

```powershell
dotnet build                                     # debug build (requires admin to launch)
dotnet build -p:NkAsInvoker=true                 # debug build that launches without admin
dotnet test                                      # 45 unit tests
dotnet publish src/NewKlear.App -c Release -o publish   # single-file NewKlear.exe
```

### Project layout

```
src/NewKlear.Core/    Engine: cleaners, bloat catalog, startup, tweaks, restore points, history
src/NewKlear.App/     WPF UI (WPF-UI Fluent controls, MVVM via CommunityToolkit.Mvvm)
tests/                xUnit tests (safety guard, junctions, exact registry revert, catalogs)
```

To add a cleaning item, edit `CleanerCatalog.cs`. To add a tweak, edit `TweakCatalog.cs`. To add a bloatware rule, edit `BloatCatalog.cs`.

### Developer command-line options

| Option | Effect |
|---|---|
| `--page <name>` | Opens a page on start, e.g. `--page Optimize`. |
| `--theme <id>` | Applies a theme for this run: `ember`, `ember-light`, `reactor`, `cyan`, `violet`. |
| `--offscreen --snapshot <file.png> --delay <s>` | Renders the window to a PNG and exits. Used for screenshots. |
