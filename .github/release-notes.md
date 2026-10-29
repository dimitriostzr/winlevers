WinLevers changes the privacy permissions and GPU preference of many installed apps at once,
shows every registry write before it happens, and can put any change back.

## Download

| File | What it is |
| --- | --- |
| `WinLevers-*-win-x64-setup.exe` | Installer for Intel and AMD PCs |
| `WinLevers-*-win-arm64-setup.exe` | Installer for ARM PCs (Snapdragon and similar) |
| `WinLevers-*-win-x64-portable.zip` | No install: unzip anywhere and run `WinLevers.App.exe` |
| `WinLevers-*-win-arm64-portable.zip` | The same, for ARM PCs |
| `SHA256SUMS.txt` | Checksums for the files above |

Requires Windows 10 version 1809 or later, 64-bit. Nothing else needs to be installed: the .NET
runtime and the Windows App SDK are included.

The installer installs for the current user only, into `%LOCALAPPDATA%\Programs\WinLevers`, and
never asks for administrator rights. Uninstalling leaves the journal, snapshots and settings in
`%LOCALAPPDATA%\WinLevers`, so the record of what the app changed is not lost.

The builds are not code-signed, so Windows SmartScreen may warn on first launch. Choose
**More info → Run anyway**. If you would rather not run a binary you did not build, the README
explains how to build it from source.

Use at your own risk: create a System Restore point before applying changes. See the
[changelog](https://github.com/dimitriostzr/winlevers/blob/main/CHANGELOG.md) for what is in this
version.
