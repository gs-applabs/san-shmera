# Chronikologio — staged Opsis.UI 6 adoption

The existing `libs/Opsis.UI.dll` is an older assembly (see `libs/Opsis.UI.version.txt`). **Do not enable `OPSIS_UI6` yet**: it would reference `OpsisMediaTile` / `OpsisTimelineCard` which do not exist in the current binary.

The optional `src/Chronikologio.UI/OpsisUi6Cards.cs` is compiled only with `OPSIS_UI6` and provides shared visual factories for cover-art/media cards and historical timelines.

## Safe release sequence

1. Upstream Opsis.UI 6 CI passes and publishes an identical SHA256 `Opsis.UI.dll`.
2. Upgrade the **Opsis host and its installed plugins together**, then Chronikologio Windows and plugin copies of `libs/Opsis.UI.dll`; preserve the **AssemblyVersion 0.1.0.0**.
3. Opt in with `<DefineConstants>$(DefineConstants);OPSIS_UI6</DefineConstants>` in the UI project only after the binary upgrade. Then wire factories into selected visual pages one at a time.
4. Check standalone startup, hosted plugin startup, theme changes, 150/200% DPI, RTL, image ownership and keyboard behavior.
5. Keep existing `EventCarousel`, `ImageStrip`, `DayDetailsView` untouched until visual parity is verified.

No existing functionality or UI path is changed by this staging PR.
