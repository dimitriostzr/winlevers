WinLevers changes the privacy permissions and GPU preference of many installed apps at once,
shows every registry write before it happens, and can put any change back.

## Download

| File | What it is |
| --- | --- |
| `WinLevers-win-x64-setup.exe` | Installer for Intel and AMD PCs |
| `WinLevers-win-arm64-setup.exe` | Installer for ARM PCs (Snapdragon and similar) |
| `WinLevers-win-x64-portable.zip` | No install: unzip anywhere and run `WinLevers.App.exe` |
| `WinLevers-win-arm64-portable.zip` | The same, for ARM PCs |
| `SHA256SUMS.txt` | Checksums for the files above |

Requires Windows 10 version 1809 or later, 64-bit. Nothing else needs to be installed: the .NET
runtime and the Windows App SDK are included.

The installer installs for the current user only, into `%LOCALAPPDATA%\Programs\WinLevers`, and
never asks for administrator rights. Uninstalling leaves the journal, snapshots and settings in
`%LOCALAPPDATA%\WinLevers`, so the record of what the app changed is not lost.

## First run: the Windows warnings

The builds are not signed with a paid code-signing certificate, so Edge and Windows warn about
them until enough people have downloaded them. Both warnings are SmartScreen's reputation check,
not a sign that anything was found in the file.

1. **Edge, "isn't commonly downloaded":** in the downloads panel choose **⋯ → Keep**, then
   **Show more → Keep anyway**.
2. **Windows, "Windows protected your PC":** choose **More info**, then **Run anyway**.
3. **Portable zip:** before unzipping, right-click it, choose **Properties**, tick **Unblock**
   and choose **OK**.

If Microsoft Defender names a threat instead, don't override it: check the file against
`SHA256SUMS.txt`, or build from source. The
[README](https://github.com/dimitriostzr/winlevers#first-run-the-windows-warnings) has the steps
in full.

Use at your own risk: create a System Restore point before applying changes. See the
[changelog](https://github.com/dimitriostzr/winlevers/blob/main/CHANGELOG.md) for what is in this
version.
