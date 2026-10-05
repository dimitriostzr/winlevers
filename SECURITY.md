# Security

WinLevers has no security reporting channel; it is published as source for
anyone to take.

That is safe to say because of what the app is. It has no network access,
no installer and no update channel, and it writes only to per-user registry
keys on the machine it runs on, as the user running it, after showing every
write and journaling it. The worst it can do is confined to that machine.
The journal is what puts a change back; the snapshot taken before each batch
is there to inspect, or to recover from by hand.

If you find a way for it to write something the preview did not show, to
skip the snapshot or the journal, to revert over a value changed outside it
without saying so, or to touch anything outside `HKCU` while unelevated, that
is a real defect. If you fork it, the fix belongs there, and the tests in
`WinLevers.Core.Tests` are where to pin it so it stays fixed.

Dependencies are pinned in `Directory.Packages.props`, and the build fails
on a NuGet audit warning, so a published advisory shows up the next time you
build.
