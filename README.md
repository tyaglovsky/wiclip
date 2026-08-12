# WiClip — clipboard history for Windows

**English** · [Русский](README.ru.md)

[Download the installer](https://github.com/tyaglovsky/wiclip/releases/latest)

A clipboard manager for Windows 10 / 11 / Server 2016+. It sits in the tray, opens a
window with your recent clips on a hotkey, and pastes the one you pick into whatever
window you were working in.

> The user interface is in Russian. English localisation is not implemented yet.

## Features

- **History window on a hotkey** (`Ctrl+Shift+V` by default) — opens at the cursor.
- **Text, images and file lists** captured from the clipboard.
- **Search** — just start typing; quick pick with `Alt+1…9`.
- **Pinned entries** stay at the top and are never evicted by the size limit.
- **Auto-paste**: the selected entry goes to the clipboard and `Ctrl+V` is sent to the
  window that was active before WiClip was opened.
- **Privacy**: clipboard content flagged by password managers (`Clipboard Viewer Ignore`,
  `ExcludeClipboardContentFromMonitorProcessing`, `CanIncludeInClipboardHistory`) is
  never stored; there is also a per-process blocklist.
- **Light / dark / system theme**, correct behaviour across monitors with different DPI.
- **MSI installer** with silent install, in-place upgrades and autostart.

## Keyboard and mouse

| Input | Action |
|---|---|
| `Ctrl+Shift+V` | open the window (configurable) |
| single click | copy the entry, window stays open |
| double click | paste into the active window |
| ↑ / ↓, PgUp / PgDn | move through the list |
| `Enter` | paste into the active window |
| `Ctrl+Enter` | copy to the clipboard without pasting |
| `Alt+1…9` | pick an entry by its number |
| `Ctrl+P` | pin / unpin |
| `Shift+Delete` | delete the entry from history |
| `Esc` | close the window |

Anything else you type goes into the search box.

## Building

You need **Windows**, **.NET SDK 8.0+** and **WiX 5**:

```powershell
dotnet tool install --global wix --version 5.0.2
```

Pinning the version matters: WiX 6 and later require accepting the
[Open Source Maintenance Fee EULA](https://wixtoolset.org/osmf/) and fail with `WIX7015`
otherwise. If 6 or 7 is already installed, run `dotnet tool uninstall --global wix` first.

Then, from the repository root:

```powershell
.\build.ps1
```

This produces `dist\WiClip-1.0.0-x64.msi` and a ready-to-run app in `publish\x64`.

Useful parameters:

```powershell
.\build.ps1 -Version 1.2.0 -Arch x64 -Culture ru-RU
```

- `-SkipMsi` — build the executable only, no installer.
- `-Arch x86|arm64` — a different architecture.
- `-Culture en-US` — installer UI language (the app itself is Russian-only).

`build.ps1` is deliberately pure ASCII. Windows PowerShell 5.1 decodes BOM-less scripts
as the system ANSI code page, so non-ASCII characters in a script would break parsing
(`UnexpectedToken`) unless the file carried a UTF-8 BOM. Keeping it ASCII sidesteps the
issue entirely — please keep new comments and messages in English.

The app is published **self-contained**, so no .NET Desktop Runtime is required on the
target machine — convenient for servers and locked-down environments. The price is an
MSI of roughly 50–90 MB. If the runtime is already deployed, set
`<SelfContained>false</SelfContained>` in `src/WiClip/WiClip.csproj` and the package
drops to about 1 MB.

## Installing

Double-click the MSI — the wizard asks whether to install **for all users** or
**for me only**:

| Mode | Location | Rights | Autostart |
|---|---|---|---|
| All users | `C:\Program Files\WiClip` | administrator | `HKLM\...\Run` — every user of the machine |
| Just me | `%LocalAppData%\Programs\WiClip` | no elevation | `HKCU\...\Run` — current user only |

Silent install:

```powershell
# all users (default, requires administrator rights)
msiexec /i WiClip-1.0.0-x64.msi /qn

# current user only, no elevation
msiexec /i WiClip-1.0.0-x64.msi /qn MSIINSTALLPERUSER=1
```

Other options:

```powershell
# no autostart, but with a desktop shortcut
msiexec /i WiClip-1.0.0-x64.msi /qn ADDLOCAL=Main,DesktopShortcutFeature

# custom directory
msiexec /i WiClip-1.0.0-x64.msi /qn APPLICATIONFOLDER="D:\Apps\WiClip"

# uninstall
msiexec /x WiClip-1.0.0-x64.msi /qn
```

Features: `Main` (required), `AutostartFeature` (autostart, enabled by default),
`DesktopShortcutFeature` (desktop shortcut, disabled by default).

Upgrades install over the top — the previous version is removed automatically and a
running instance is closed without a reboot.

The installer is **not code-signed**, so Windows SmartScreen shows a warning on first
run: choose "More info" → "Run anyway".

## Where data lives

`%APPDATA%\WiClip\`:

- `settings.json` — settings;
- `history.json` — the history itself;
- `images\` — images captured from the clipboard;
- `wiclip.log` — log, truncated at 512 KB.

Uninstalling does not remove these files — the MSI never touches user history. To keep
history out of the filesystem entirely, clear "Сохранять историю между запусками"
(*Keep history between sessions*) in the settings: the files are deleted and history
lives in memory only.

**Security note:** history is stored in plain text, exactly like the built-in `Win+V`
history. On shared and terminal servers keep that in mind — a password that ends up in
the clipboard ends up in the file. The "ignore password managers" option and the
memory-only mode both help.

## Autostart

- On first run WiClip registers itself for the **current user** (`HKCU\...\Run`); this can
  be turned off in the settings.
- The `AutostartFeature` installer component (enabled by default) writes to `HKLM\...\Run`
  for an all-users install, or `HKCU\...\Run` for a per-user install. When the machine-wide
  entry exists, the checkbox in the settings is shown as checked and locked, since removing
  it needs administrator rights.

## Known limitations

- The simulated `Ctrl+V` does not reach windows running **elevated** while WiClip itself
  is not — that is Windows integrity-level isolation (UIPI). Copying still works, so the
  user can press `Ctrl+V` manually.
- The hotkey may already be taken by another program — WiClip shows a tray notification
  and the combination can be changed in the settings. `Win+V` belongs to the built-in
  Windows clipboard history and cannot be used.
- Only plain text is stored; RTF/HTML formatting is lost on paste.

## Project layout

```
src/WiClip/
  App.xaml(.cs)          startup, tray icon, single-instance guard
  HistoryWindow.xaml     history window: search, list, paste
  SettingsWindow.xaml    settings dialog
  ClipboardMonitor.cs    WM_CLIPBOARDUPDATE handling, privacy filters
  HistoryStore.cs        history, deduplication, size limit, persistence
  HotKeyManager.cs       global hotkey (RegisterHotKey)
  Paster.cs              clipboard writes and Ctrl+V into the target window
  Native.cs              P/Invoke declarations
installer/
  WiClip.wxs             MSI definition (WiX 5)
  License.rtf            licence text shown by the installer
build.ps1                publish + file-list generation + MSI build
```

## Licence

MIT.
