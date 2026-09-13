# Performance log

Every stage of the physics and graphics overhaul appends a row here, measured the same way each time,
so a stage that costs frame budget shows it instead of being felt later.

## How it is measured

`PerfProbe` (Game) samples an untouched fight: it waits for the ready-fight intro to release, discards
two settling seconds, then samples. The boss attacks a stationary player throughout, so the scenario
is deterministic enough to compare across stages.

- **Windows:** `Build/Windows/AdaptiveBossArena.exe -screen-fullscreen 0 -screen-width 1280
  -screen-height 720 -perfCapture 60 -perfLog <file> -perfQuit`
- **WebGL:** open the page with `?perf=60`; the `[PERF]` line is written to the browser console.

**Caveats on reading these numbers:**
- Windows frame times are vsync-locked on the test machine (144 Hz), so p50/p95 show the cap rather
  than the headroom. A regression shows as p95 rising above 6.95 ms, or as `max` spikes.
- WebGL frame times are only valid in a **visible, focused** tab. A hidden tab (such as the automation
  browser pane) is throttled to about 1 fps, so its timings are recorded as not measured. Draw calls,
  triangles and build size do not depend on throttling and are valid.

## Budgets

| | WebGL | Windows |
|---|---|---|
| Frame time p95 | ≤ 16.7 ms | ≤ 8.3 ms |
| Draw calls | ≤ 250 | ≤ 600 |
| Triangles | ≤ 150k | ≤ 500k |
| Physics bodies | ≤ 40 | ≤ 200 |
| Build size (gzipped payloads) | ≤ 60 MB | — |

## Log

| Date | Stage | Commit | Platform | p50 | p95 | max | Draw calls | Tris | Build | Notes |
|---|---|---|---|---|---|---|---|---|---|---|
| 2026-09-13 | S0 baseline | af805de | Windows 1280×720 | 6.94 | 6.95 | 20.83 | 155 | 21.8k | — | vsync-locked 144 Hz; 8639 frames / 60 s |
| 2026-09-13 | S0 baseline | af805de | WebGL (local) | not measured | not measured | — | 113 | 15.0k | 15.7 MB | hidden pane throttled to ~1 fps; counts valid |
| 2026-09-13 | P1 | 6440c9d | Windows 1280×720 | 6.94 | 6.95 | 13.89 | 135 | 18.5k | — | vsync-locked; no regression |
| 2026-09-13 | P1 | 6440c9d | WebGL (local) | not measured | not measured | — | — | — | 15.7 MB | fresh load verified; deployed |
