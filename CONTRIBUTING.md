# Working on your own copy

WinLevers does not take contributions and has no support channel. It is a
tool I built for my own machines and published so that anyone can take it; the
[MIT licence](LICENSE) is the whole arrangement. Fork it, change it, and ship
it under your own name if you like, as long as the copyright and licence
notice go with it.

Everything below is written for that fork. It is what I would want to know
on day one.

## Reading order

The [architecture](docs/architecture.md) is short and says where things
are. The [design](docs/design.md) says why, and its *Decisions* section is
the place to look before changing one of them; each was a fork in the road
with reasons recorded.

## Building

There are two solutions.

- **`WinLevers.Engine.slnx`** contains everything except the WinUI shell and
  builds anywhere the .NET 10 SDK runs, including macOS and Linux. Most of
  the code and all of the tests live here.

  ```
  dotnet test WinLevers.Engine.slnx
  ```

- **`WinLevers.sln`** is the full solution, for Visual Studio 2026 on
  Windows. It needs the **.NET Desktop Development** workload and the
  **Windows App SDK** component; the C++ workload is not required. Set
  `WinLevers.App` as the startup project and pick **x64** or **ARM64**, since
  WinUI 3 cannot build for Any CPU. The README covers the two build
  surprises: a stale `.vs` folder after a pull, and the XAML targets crash
  when the C++ workload is absent.

On Windows, build the full solution. Building only the engine there leaves
the shell uncompiled and a shell error unnoticed.

## What keeps a change safe

**A change to what gets written to the registry needs a test that pins it.**
Every lever, every state transition and every rule in the apply and revert
pipeline has one, against the in-memory `IRegistry`. Tests are named as
sentences describing the rule, in the style of
`TheSnapshotIsFlushedBeforeTheFirstRegistryWrite`, so a failing test reads as
a broken promise. Keep that up in your fork and the guardrails stay real.

Never write a test that touches a real registry key. There is no
Windows-only test project; if you add one, confine it to a sandbox key such
as `HKCU\Software\WinLevers\TestHive`. A test that wrote a real ConsentStore
key would silently reconfigure your own machine.

Logic goes in `WinLevers.Presentation` or below, never in `WinLevers.App`.
A view model in the shell is a view model nobody can test. The shell holds
XAML, code-behind that binds and forwards, and the composition root.

## House conventions

These are enforced by the build where they can be, in
`Directory.Build.props` and `Directory.Packages.props`.

- Warnings are errors, including NuGet audit warnings. This engine decides
  what to write into other applications' keys; a warning here is usually a
  real defect.
- Package versions are central. Add a version to `Directory.Packages.props`,
  not to a project file. Core takes no packages at all.
- Nullable is on everywhere. Absent registry values are `null`, never a
  default; see the remarks on `IRegistry.GetValue` for why.
- Comments say *why*, not *what*. Most classes open with a remark explaining
  the failure they exist to prevent.
- Public members carry XML doc comments; the build generates the
  documentation file and fails on a missing one.

## Common changes

- **A new capability.** One line in `CapabilityCatalog`, and one in
  `PrivacyCatalog` if it belongs on the advisor. A capability the catalogue
  does not know is still discovered and shown by its raw id, so this is a
  naming change, not a feature.
- **A new lever.** Implement `ILever` in Core, add it to `LeverSet`, and
  write a test for every state it can read and every transition it can plan,
  including `NotSet`, `NotApplicable` and `Unrecognised`. `GpuPreferenceLever`
  is the smaller of the two existing levers and the better template.
- **A new setting.** Add it to `UiSettings`. Settings live in
  `%LOCALAPPDATA%\WinLevers\settings.json`, never in the registry.
- **Your own name on it.** `AppInfo` in Presentation holds the product
  name, author, repository and licence; the About page reads them from
  there. `Directory.Build.props` holds the version and the assembly
  metadata. Add your name rather than replacing mine: the MIT licence keeps
  the existing copyright notice with the code.
