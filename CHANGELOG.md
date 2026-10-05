# Changelog

Notable changes, newest first. Versions are git tags.

## Unreleased

- A risk notice on every launch, before the app can be used. It recommends a
  full System Restore point, with a button that opens the Windows System
  Protection page, states that the app comes with no warranty and no support,
  and requires a ticked checkbox to continue. Anything else — Exit, Esc, or a
  notice that could not be shown — closes the app.
- A *Trademarks and affiliation* notice on the About page, in the README and
  in the launch notice: not affiliated with, endorsed by or supported by
  Microsoft or by any company whose apps appear in the grid; Microsoft,
  Windows, Visual Studio, WinUI and .NET are Microsoft's trademarks, and every
  other name shown — including the app names, which are read from this
  machine's own records — belongs to its owner and is used only to identify
  the software.
- README leads with a *Use at your own risk* section covering the restore
  point and the no-warranty terms.
- About and Settings lay their cards out in two columns on a window wide
  enough for them, and stack them when it is not. Both pages used to pin a
  720px column to the left edge, which left two thirds of the default 1500px
  window empty. About also carries its own *Use at your own risk* card, so the
  terms are readable after the launch notice has been dismissed.
- Released under the MIT licence. The About page shows the copyright line
  and links to the licence.
- Unmaintained. The project is provided as is, for anyone to fork.
- Docs: an [architecture](docs/architecture.md) page with the project
  dependency graph and the apply and revert pipelines as diagrams, a note in
  the design on where the code has moved away from it, and a contributing
  file written for forks.
- No support channel, by decision. Issues and pull requests are off, and the
  About page no longer offers *Report a problem*. The licence is the offer:
  take the code and use it.
- Corrected claims in the README, About and Settings pages. The grid lists
  desktop apps from the uninstall records and packaged apps once Windows has
  recorded a permission for them, not every installed app. A snapshot is
  written before every batch but cannot be restored from the app yet. A
  revert shows a value changed since and leaves it alone; there is no
  per-item choice. "Every write is reversible" became "every change that
  lands can be put back": a write that fails its read-back is journaled as
  failed and left out of a revert.
- README reordered to lead with what the app is for and is not, then a quick
  start. Build quirks moved under Troubleshooting, the support policy is
  stated once, and the note on how it was made sits at the end.

## 0.1.0 — preview

First public release. Everything in it is a per-user (`HKCU`) setting.

- Privacy permission lenses: one per capability Windows lists under
  *App permissions*, with allow / deny / not set per app.
- GPU preference lever for desktop apps: power saving or high performance.
- Select any set of apps, set several levers at once, preview the exact
  writes, apply as one batch.
- Journal of every write with the value it replaced; revert a change or a
  batch from *History*; a revert that finds a value changed since reports it.
- Snapshot of the values each batch will write, saved as JSON and flushed
  before the first write.
- Profiles: capture the current settings and apply them again later.
- Dashboard with counts per lever, and CSV export of every table.
- Privacy advisor: the permissions that matter most, who holds each, and a
  deny for the allowed apps or for the ones with no setting.
- A privacy bar on the dashboard and the advisor, and a bar per lever tile.
- Sort the grid by any column from its header.
- Remembers window placement, theme and table options.

There is no download: build it from source, as the README explains.

Known limits: the background-activity and startup levers described in the
design are not in this release, and nothing writes machine-wide under `HKLM`.
