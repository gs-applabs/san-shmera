# P1 Chronikologio ↔ Opsis News Crawler suite integration

This PR preserves the existing **legacy text feed**, including its key `san-simera` and its UI-thread-safe getter. A new `RichDayProjection` computes stable entry IDs, `GR/CY` country codes, language, holiday/saint/history type, optional historical year, priority and detail ID from the preloaded daily snapshot. It requires no API or network calls.

The optional `OPSIS_P1_BRIDGE` build enables `PluginFeeds.RegisterRich` + `SuiteEventHub` *only* when the new version of `OpsisNewsCrawler.Plugin.Abstractions.dll` is installed in the Opsis **host** and every dependent plugin shares that identical DLL. The assembly identity remains **0.8.0.0** for legacy compatibility. New members require the updated binary despite the unchanged identity.

The GitHub workflow builds two configurations:
1. Existing standalone/hosted Chronikologio with its unchanged `libs` contract.
2. Isolated runner copies the P1 Crawler contract DLL into the Chronikologio workspace, compiles the feature-flagged bridge and tests compatibility (does *not* commit or deploy the new DLL).

**Do not enable OPSIS_P1_BRIDGE in a public installer until the Opsis host and Crawler are deployed with the same binary, and host/plugin runtime checks pass.** Rich feeds keep historical metadata without converting calendar events into Breaking News. Images are null when no verified asset exists. A stable DetailId is prepared for future local navigation but is not yet wired to an active detail-page handler.
