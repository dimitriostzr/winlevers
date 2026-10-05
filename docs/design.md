# WinLevers — design

**Date:** 2026-09-04
**Status:** historical. Approved 2026-09-04, before implementation, and not updated since.
**Name:** WinLevers (`WinLevers.*` namespaces), settled. "Lever" is the domain
term this document uses throughout for a single managed setting, and it stays
accurate for the three-way settings that "toggle" would misdescribe.

> **Historical design record.** This is the design as approved on
> 2026-09-04, before any code existed. It is kept for the reasoning, and it
> is not updated as the code moves. The implementation sections describe
> what was proposed; several were built differently or not at all, and each
> of those carries a note. For what the code does today, read the
> [architecture](architecture.md) page.

## Where the code differs

The [architecture](architecture.md) page describes what is built. Where it
and this document disagree, the code and that page are right. The list
below is the summary; each affected section also carries a note. As of
0.1.0:

- The Windows solution is `WinLevers.sln`, in the classic format. Two
  hand-written `.slnx` files were accepted by the `dotnet` CLI and rejected
  by Visual Studio, so `WinLevers.Engine.slnx` stays for macOS and CI and the
  full solution is `.sln`.
- Core owns `IRegistry` and nothing else; `IPackageCatalog` and `IFileSystem`
  were never needed. `PackageManager` is not an inventory source, because it
  needs WinRT, so packaged apps appear by family name and only once they hold
  a grant. The sources actually used are the three Uninstall keys,
  `ConsentStore` sub-key names and `UserGpuPreferences` value names (D2).
- Only the ConsentStore permission levers and the GPU preference lever are
  built. Background activity and startup are not (D1). No machine-scope lever
  exists yet; the elevation check and the bulk-edit gating are in place for
  when one does (D3).
- The snapshot covers the values a batch is about to write, not every managed
  value on the machine (D4). *Snapshot restore* is not built: the store can
  load a file, but nothing in the shell or the CLI restores one.
- A revert lists every drifted value with what was expected and what is
  there, and leaves all of them alone. There is no per-item override yet.
- Profiles, listed under *Out of scope*, shipped in 0.1.0 as the layer over
  bulk apply that entry predicted.
- `WinLevers.Windows.Tests` does not exist. `WindowsRegistry` is exercised by
  hand on a Windows machine.
- The dashboard, the privacy advisor, CSV export and the *Used in 30 days*
  filter were added without an entry here.

## What this is

> *Original proposal.* As built, there is no download and no GitHub
> Releases; the app is built from source. Snapshot restore was not built.

A Windows desktop application that lets a user select many installed applications
at once and apply battery and privacy-permission settings to all of them in a
single operation, with a full change journal, per-change and per-batch revert, and
snapshot restore.

Distributed as open source. Unpackaged Win32, published via GitHub Releases.
Not a Microsoft Store submission — this was considered and dropped, see
[Decisions](#decisions).

### Goals

1. One row per real installed application, covering both packaged (Store/MSIX) and
   desktop (Win32) apps.
2. For any lever — a permission or a battery setting — show at a glance how many
   apps are in each state, and filter the list by that state.
3. Select an arbitrary set of apps, set several levers at once, preview exactly
   what will change, and apply as one batch.
4. Every write is journaled with its previous value and is revertible.
5. A snapshot taken before every batch, restorable independently of the journal.

### Non-goals

Listed explicitly so they do not creep in. See [Out of scope](#out-of-scope) for
the full list with reasoning.

## Decisions

Each of these was an explicit fork during design. The reasoning is recorded
because the conclusions look arbitrary without it.

### D1 — "Battery optimization" means three levers, not one

> *As built:* of the three levers this decision names, only GPU preference exists. Background activity and startup were not built.

Windows has no single per-app battery toggle equivalent to Android's. What exists
is four unrelated mechanisms:

| Mechanism | Applies to | Persistent |
|---|---|---|
| Background activity (Optimized / Always / Never) | packaged apps only | yes |
| GPU power preference | any executable | yes |
| Startup entry enabled/disabled | both | yes |
| Efficiency mode (EcoQoS) | a running process | **no** |

The app manages the three persistent levers. Efficiency mode is excluded: it does
not survive a restart, so it cannot be journaled or reverted meaningfully, and a
"setting" that silently evaporates is worse than no setting.

Consequence: a Win32 app such as a browser has **no** background-activity setting.
The UI must show this as *not applicable*, never as *not set*, or users will
believe a lever exists that does not.

### D2 — Full inventory, unioned from five sources

> *As built:* three registry sources, not five. The proposal assumed `PackageManager`; it needs WinRT and was left out, so packaged apps appear only once they hold a grant.

The alternative — listing only apps Windows already tracks — was rejected because
it makes it impossible to configure an app *before* it first requests a
permission, and produces a conspicuously short list.

### D3 — Per-user by default, opt-in elevation for machine scope

> *Original proposal.* As built, no machine-scope lever exists in 0.1.0. The
> elevation check and the bulk-edit gating are in place; the separately
> labelled machine-wide scope was not built. The rule on ownership and ACLs
> holds.

Everything in the primary surface runs unelevated: background activity, GPU
preference, per-user startup entries, and every per-app capability grant all live
in `HKCU`.

Items that genuinely require `HKLM` — the device-wide capability master switches,
all-users startup entries, some scheduled tasks — appear in a separately labelled
*machine-wide* scope, disabled until the user explicitly relaunches elevated.

**The app never takes ownership of a registry key and never rewrites an ACL.**
Several `HKLM` ConsentStore keys are owned by TrustedInstaller. Seizing them is
not cleanly revertible and is the standard way a tool of this kind renders a
machine unbootable or unrepairable. Such keys are surfaced read-only with the
reason shown.

### D4 — Journal *and* snapshots

> *As built:* the snapshot holds the values the batch is about to write, not every managed value. Nothing restores one yet.

The journal (per-change, batch-grouped, with previous values) does the real work
and provides surgical revert. A full JSON snapshot is additionally taken before
every batch.

The snapshot is near-free — the entire managed state is a few thousand registry
values — and it covers the one case the journal fundamentally cannot: settings
changed by something other than this app.

### D5 — Selection is the primary object; bulk edit is cross-lever

The user selects apps, then sets *several* levers at once for all of them.
Bulk actions are therefore not scoped to the currently viewed lever.

**Selection persists across lens switches and filter changes.** This is load-bearing:
the intended workflow is "filter to Camera = Allowed, select 30, switch to the
Battery lens, add 5 more, apply to all 35". Resetting selection on navigation
breaks the app's central use case.

### D6 — Partial failure continues and reports

A bulk apply of 35 apps across 4 levers is ~140 registry writes. If 6 fail, the
other 134 are kept. The journal records what actually succeeded, so the batch
remains accurately revertible. The run ends on a summary naming every failure and
its cause.

All-or-nothing was rejected: one ACL-protected key would discard 134 good changes,
and the rollback writes can themselves fail, producing a worse state than the one
being escaped.

### D7 — Open source, unpackaged Win32

Microsoft Store distribution was considered and dropped by the user. Recorded
because the reasoning still constrains the build:

- MSIX packaging gives a packaged app a virtualized registry view. This
  application's entire purpose is writing *other* applications' keys under `HKCU`.
  Whether those writes reach the real hive or land in the package overlay was
  never verified, and a negative result would have been fatal.
- MSIX apps cannot cleanly relaunch themselves elevated, which conflicts with D3.

Unpackaged Win32 has neither problem. Standard UAC relaunch works.

## Architecture

### Solutions

> *As built:* the full solution is `WinLevers.sln`, in the classic format. The proposal assumed `.slnx` for both; Visual Studio rejected a hand-written one.

| File, as proposed | Contains | Used on |
|---|---|---|
| `WinLevers.slnx` (built as `WinLevers.sln`) | everything | Windows / Visual Studio |
| `WinLevers.Engine.slnx` | all but the shell | anywhere, including macOS and CI without a Windows image |

XAML cannot be compiled off Windows. The split exists so the engine — where all
the logic lives — is testable on the development machine. On Windows, always build
the full solution; building only the engine there would leave the shell untested.

### Projects

> *As built:* Core owns `IRegistry` only. `IPackageCatalog` and `IFileSystem` were never needed, and `WinLevers.Windows.Tests` was not created.

```
src/
  WinLevers.Core          net10.0    domain model, lever definitions and logic, plans,
                               diffing. Owns IRegistry / IPackageCatalog /
                               IFileSystem as interfaces.
  WinLevers.Data          net10.0    EF Core over SQLite: batches, changes, log storage
  WinLevers.Presentation  net10.0    view models, filtering, selection, apply orchestration
  WinLevers.Windows       net10.0-windows10.0.19041.0
                               real implementations only. The single project that
                               touches the registry, PackageManager or the shell.
  WinLevers.App           net10.0-windows10.0.19041.0
                               WinUI 3 / Windows App SDK. XAML and wiring only.
tests/
  WinLevers.Core.Tests           runs everywhere
  WinLevers.Data.Tests           runs everywhere
  WinLevers.Presentation.Tests   runs everywhere
  WinLevers.Windows.Tests        Windows only, guarded
```

**Nothing that is not markup or platform wiring goes in `WinLevers.App`.** A view model
in the shell is a view model nobody can test.

The registry sits behind `IRegistry` in `WinLevers.Core`, with an in-memory fake used by
tests. This is what allows the lever logic — which carries essentially all of the
edge cases — to be developed and tested without a Windows machine.

Conventions follow the house style used in the sibling `windows-*` projects:
central package management in `Directory.Packages.props`, `TreatWarningsAsErrors`
on including NuGet audit warnings, comments that say *why* rather than *what*,
tests named as sentences describing the rule they pin.

## Domain model

### AppIdentity

```
AppIdentity
  Key            stable identity string, the journal's foreign key.
                 "packaged:<pfn>" or "desktop:<lower-cased primary exe path>".
                 Must survive a rescan unchanged or history detaches from apps.
  Kind           Packaged | Desktop
  PackageFamilyName    set when Kind == Packaged
  ExecutablePaths      set when Kind == Desktop; normalized, full, case-folded
  DisplayName
  Publisher
  InstallLocation
  IconSource
  IsSystemComponent    inbox/system package or under %SystemRoot%
```

Packaged apps key on **Package Family Name**, which is unambiguous.

Desktop apps key on a **set of executable paths**. Paths are attached to an app by,
in order: exact path match against an already-known executable; residence under a
known `InstallLocation`; otherwise the executable stands alone as its own row,
named from its `FileVersionInfo.FileDescription` falling back to `ProductName`
falling back to the file name.

### Inventory sources

| Source | Contributes |
|---|---|
| `PackageManager.FindPackagesForUser("")` | PFN, display name, logo, install path, system flag |
| Uninstall registry: `HKLM\...\Uninstall`, `HKLM\...\WOW6432Node\...\Uninstall`, `HKCU\...\Uninstall` | display name, publisher, install location, `DisplayIcon` |
| `ConsentStore\<capability>\NonPackaged\*` key names | executable paths, `#`-mangled |
| `HKCU\Software\Microsoft\DirectX\UserGpuPreferences` value names | executable paths |
| `Run` / `RunOnce` / `StartupApproved` keys, Startup folders (`.lnk` targets resolved), logon scheduled tasks | executable paths and command lines |

A "browse for an executable" action adds an arbitrary path as a Desktop identity,
covering anything the five sources miss.

**This merge is the fiddliest component in the project.** It gets a dedicated test
suite driven by fixture data captured from real machines, because getting it wrong
means Slack appears four times and every screenshot looks broken.

### Levers

One uniform abstraction rather than per-setting special cases:

```
ILever
  Id, DisplayName, Category (Battery | Permission), Scope (User | Machine)
  States           ordered valid values, always including NotSet.
                   Mixed and NotApplicable are readable but never targetable.
  AppliesTo(AppIdentity) -> bool
  Read(AppIdentity)      -> LeverState        NotSet and NotApplicable are distinct
  Plan(AppIdentity, target) -> WriteOp[]
```

`NotSet` and `NotApplicable` are different states and must never be collapsed.
`NotSet` means the app has no recorded preference; `NotApplicable` means the lever
does not exist for that app at all.

**Multi-executable identities.** A Desktop identity can own several executables,
and every per-executable lever — ConsentStore non-packaged, GPU preference,
startup — is stored per executable, not per app. So:

- **Reading** aggregates. All executables agree, that is the app's state.
  They disagree, the state is `Mixed`.
- **Writing** fans out to every executable in the identity, which is what makes
  `Mixed` collapse to a single value.
- `Mixed` is a **read-only state**. It appears in the grid and in filters, and it
  can never be selected as a bulk-edit target.
- Each fanned-out write is its own journal row, because each has its own previous
  value and must revert independently.

The ~25 permission levers are **instances of one ConsentStore lever driven by a
capability table**, not 25 classes. The three battery levers are hand-written.

## Lever catalogue

Confidence is stated per row. Every "verify" item is a week-one task; see
[Verification](#verification-required-before-implementation).

### Permissions — ConsentStore

Location: `HKCU\Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\<capability>`

- Packaged: subkey per PFN, string value `Value` = `Allow` | `Deny`
- Desktop: `NonPackaged\<mangled path>`, where the full executable path has `\`
  replaced by `#`, same `Value`
- Absent value = `NotSet`
- `LastUsedTimeStart` / `LastUsedTimeStop` are also present and are surfaced as a
  *last used* column. Read-only, never written.
- The machine-wide master switch is the capability key's own `Value` under `HKLM`
  (machine scope, D3)

Confidence: **high** for the layout, values, and mangling.

Expected capabilities, to be confirmed by enumerating the key on a current
Windows 11 build rather than trusting this list:

`location`, `webcam`, `microphone`, `userNotificationListener`,
`userAccountInformation`, `contacts`, `appointments`, `phoneCall`,
`phoneCallHistory`, `email`, `chat`, `radios`, `bluetoothSync`,
`documentsLibrary`, `picturesLibrary`, `videosLibrary`, `downloadsFolder`,
`broadFileSystemAccess`, `cellularData`, `appDiagnostics`,
`humanInterfaceDevice`, `wifiData`, `gazeInput`, `sensors.custom`,
`graphicsCaptureProgrammatic`, `graphicsCaptureWithoutBorder`,
`backgroundSpatialPerception`, `userDataTasks`.

The capability table is data, not code. Adding a capability Windows introduces
later must be a one-line table edit.

### Battery — background activity

> *Not built.* The proposal assumed this lever; 0.1.0 ships without it. The registry notes below are research, not behaviour.

Packaged apps only. Location believed to be
`HKCU\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications\<PFN>`.
States: Optimized (Windows decides) | Always | Never.

Confidence: **low.** The exact value names and their semantics are unverified.
This is the first thing to establish on a Windows machine, before any other
implementation work, because the lever's shape depends on it.

### Battery — GPU power preference

Applies to any executable.
Location: `HKCU\Software\Microsoft\DirectX\UserGpuPreferences`, value name is the
full executable path, data is `GpuPreference=<n>;` where 0 = let Windows decide,
1 = power saving, 2 = high performance.

Confidence: **high** for the format; the exact enumeration values are to be
confirmed on device.

### Battery — startup

> *Not built.* The proposal assumed this lever; 0.1.0 ships without it. The registry notes below are research, not behaviour.

Applies to both kinds. Composed of several stores that must be read together:

- `HKCU` / `HKLM` `...\CurrentVersion\Run` — presence of the entry
- `...\Explorer\StartupApproved\Run` — a binary blob whose leading byte encodes
  enabled vs disabled. **Format confidence: medium**, verify on device.
- Startup folders, per-user and all-users, with `.lnk` targets resolved
- Logon-triggered scheduled tasks

`HKLM` entries and some scheduled tasks are machine scope per D3.

## Apply pipeline

Five stages. The middle three are where the app earns trust.

1. **Plan.** For each (app, lever) whose target is not *Leave unchanged*, produce
   write ops. Drop ops where current already equals target. Drop and count ops
   where the lever is `NotApplicable`. Machine-scope levers are untargetable
   unless the process is elevated (D3), so an unelevated plan cannot contain
   them — the restriction is enforced in the bulk edit panel, not here.
2. **Preview.** Nothing is written until the user sees a summary — *"128 changes
   across 35 apps · 12 skipped (not applicable) · 3 are Windows components"* —
   expandable to individual ops. This is the gate; Apply
   exists only on this screen.
3. **Snapshot.** Full JSON capture of every managed value, written and flushed to
   `%LOCALAPPDATA%\WinLevers\snapshots\<batchId>.json` **before the first
   write**.
4. **Execute.** Ops run sequentially. Each op re-reads the current value
   immediately before writing, so the journal records the value that was actually
   replaced rather than the value read at scan time. Writes, then reads back to
   confirm. Success and failure are both journaled; execution continues past
   failures (D6).
5. **Report.** Summary naming every failure with its cause and Win32 error code.

## Journal, revert, snapshots

### Schema

> *As built:* close to this. `Batch.Kind` has a `Restore` value that nothing produces, because snapshot restore was not built.

```
Batch  (Id, StartedUtc, FinishedUtc, Kind, AppCount, OpCount, OkCount,
        FailCount, SnapshotPath, Note)
Change (Id, BatchId, AppKey, AppDisplayName, LeverId, Scope, Target,
        OldValue, NewValue, Status, FailureReason, AppliedUtc, RevertedBy)
```

`Batch.Kind` is `Apply` | `Revert` | `Restore`, so reverts are themselves
first-class batches and are therefore revertible.

`Change.Target` names the exact thing written — the PFN, or the executable path
for a fanned-out per-executable lever. One row per registry write, never one row
per app-lever pair. Without it a fan-out across four executables cannot be
reverted to the four places it came from.

`OldValue` and `NewValue` hold the lever's own wire format. **Absent is stored as
null, never as a default value.** `NotSet` is not `Deny`: a revert that turns
`NotSet` into `Deny` grants nothing back, looks successful, and is a silent bug.
Reverting to `NotSet` deletes the value rather than writing one.

### Revert

> *As built:* a drifted value is shown with its expected and actual values and always left alone. The per-item decision proposed here was not built.

Operates on a whole batch, replayed in reverse order, or on a single change.

Before each revert write the current value is re-read and compared against the
recorded `NewValue`:

- **Matches** — revert, and journal the revert as a new `Revert` batch.
- **Drifted** — do not write. Surface as *changed outside this app since*, showing
  the expected and actual values, and let the user decide per item.

Blind reverting is the failure mode that makes a tool like this untrustworthy: the
user changes one setting back in Windows Settings, then a revert silently
overwrites their more recent intent.

### Snapshot restore

> *Not built.* The store writes a snapshot before every batch and can load one, but nothing in the shell or the CLI restores it.

Independent of the journal. Select a snapshot, diff it against current live state,
restore all or a chosen subset. This is the recovery path for changes the journal
never saw.

## User interface

WinUI 3 / Windows App SDK, matching the sibling `windows-*` projects.

### Layout

**Left — lenses.** Two groups plus two fixed entries:

- `Battery`: Background activity, GPU preference, Startup
- `Permissions`: one entry per capability
- `All apps`
- `History`
- `Settings`

Every lever row shows live counts: `Microphone — 8 allowed · 31 denied · 104 not set`.
The sidebar is the "which apps are in which state" overview; no separate screen is
needed for it.

**Centre — app grid.** Columns follow the active lens: icon and name, publisher,
type, the lens's state, and *last used* on permission lenses. `All apps` shows the
wide grid with a column chooser.

**Bottom — selection bar**, visible whenever a selection exists:
`35 selected · Clear · Edit settings…`

**Bulk edit panel.** Every lever, each a tri-state defaulting to *Leave unchanged*.
Each shows its reach against the current selection — *"Background activity —
applies to 12 of 35 selected"*. The footer leads to Preview, never directly to
Apply.

### Filters

Composable: free-text over name, publisher and path; state chips for the active
lens; app type (Packaged / Desktop); scope (User / Machine); *modified by this
app*; *used in the last 30 days*.

### Settings and appearance

**Theme: Light, Dark, or System**, defaulting to System. WinUI 3's `ElementTheme`
gives this almost for free — `Default` follows the OS and updates live when the
user changes their system theme, so System costs no polling and no restart.

Preferences persist to `%LOCALAPPDATA%\WinLevers\settings.json`, **not the registry**.
This app writes enough of the registry as it is; keeping its own configuration out
of a hive it also edits means a bug in the lever code can never corrupt the
settings that would let a user recover.

**Both themes constrain the state colour coding.** Five states — Allowed, Denied,
Not set, Mixed, Not applicable — appear at high density in the grid, which is
exactly where colour-only encoding fails. Every state carries a glyph and a text
label alongside its colour, and both palettes are checked for contrast against
their own background rather than one being derived from the other.

### History view

Batches, drillable to individual changes. Actions: revert batch, revert change,
restore snapshot. Filterable by date, app, lever and status.

## Logging

Two records, deliberately separate. Conflating them is a common mistake and makes
both worse.

- **Journal** — what changed *on the machine*. Structured, queryable, revertible.
  Surfaced as the History view.
- **Diagnostic log** — what *the application did*. Rolling file under
  `%LOCALAPPDATA%\WinLevers\logs`, plus an in-app viewer. Registry error
  codes, scan timings, drift detections, unexpected key shapes. This is what a bug
  report attaches.

## Safety guardrails

- **Preview gate.** No write path exists that does not pass through Preview.
- **Snapshot before every batch**, flushed before the first write.
- **System components are flagged, not blocked.** Inbox packages and executables
  under `%SystemRoot%` carry a badge, and Preview counts them explicitly. Denying
  the Camera app access to the camera is a legitimate thing to want.
- **No ownership seizure, ever** (D3). ACL-protected keys are read-only with the
  reason displayed.
- **Machine scope is visibly separate** and inert until the user relaunches
  elevated.
- **Fail closed on unrecognized key shapes.** Before writing, every lever
  validates what it is writing into: the key sits where expected, the value has
  the expected type, and its current contents parse as a state the lever knows.
  Anything else aborts that op, journals it as a failure recording the shape
  actually observed, and logs it. **A lever never writes a guess.**

  This exists because `ConsentStore` and the background-activity keys are
  undocumented and can be reshaped by a Windows update, and because the app ships
  to machines whose owner cannot be asked what happened. Writing a plausible value
  into a key whose meaning has changed is the one failure mode here that is both
  silent and unrevertible — the journal would faithfully record a revert to a
  previous value that no longer means what it meant.

## Testing strategy

> *As built:* the first three suites exist and run on any machine. `WinLevers.Windows.Tests` was not created; the sandbox rule below stands for anyone who adds it.

The great majority of tests run against the in-memory `IRegistry` fake and
therefore run on the development machine in seconds.

| Suite | Covers |
|---|---|
| `WinLevers.Core.Tests` | lever read and plan logic for every lever and every state transition, including `NotSet` handling and `NotApplicable`; path mangling and unmangling; identity merge against captured fixture data |
| `WinLevers.Data.Tests` | journal round-trip, batch ordering, reverse replay, drift detection, null-vs-default value fidelity |
| `WinLevers.Presentation.Tests` | filter composition, selection persistence across lens and filter changes, tri-state bulk edit to plan, reach counting |
| `WinLevers.Windows.Tests` | thin integration against real registry semantics, **confined to a sandbox key** at `HKCU\Software\WinLevers\TestHive`. Guarded to Windows. Never touches real settings. |

The sandbox-hive rule is absolute: an integration test that writes a real
ConsentStore key would silently reconfigure the developer's own machine.

## Verification required before implementation

Stated plainly because building on a wrong assumption here costs weeks.

| Item | Confidence | Priority |
|---|---|---|
| ConsentStore layout, `Allow`/`Deny`, `#` mangling | high | confirm in passing |
| `UserGpuPreferences` value format and enum values | high | confirm in passing |
| Capability list on current Windows 11 | must enumerate on device | week one |
| `BackgroundAccessApplications` value names and semantics | **low** | **first task, blocking** |
| `StartupApproved` binary blob format | medium | week one |
| Whether changes take effect live or need an app restart or sign-out | unknown | week one |

The last row is a UX requirement, not trivia. If a change needs a sign-out to take
effect, the UI must say so at Preview time, or users will conclude the app does
not work.

## Out of scope

> *As built:* profiles, listed below as out of scope, shipped in 0.1.0.

Deliberately excluded. Each would be a reasonable later addition; none belongs in
the first implementation.

- **Efficiency mode / EcoQoS toggling** — non-persistent, so it cannot be
  journaled or reverted (D1).
- **Saved presets and profiles** — "apply my standard config" is a real want, but
  it is a layer over a working bulk apply, not part of it.
- **Cross-machine export and import** — depends on presets and on identity
  matching across machines.
- **Managing other user profiles** — requires loading other users' hives; large
  additional surface for a narrow benefit.
- **Scheduled or automatic enforcement** — turns a tool the user drives into a
  background agent, which is a different product with different safety needs.
- **MSIX packaging and Store submission** (D7).
