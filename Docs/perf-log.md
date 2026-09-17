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
| 2026-09-13 | G1 | fa21a19 | Windows 1280×720 (desktop tier, Ultra) | 6.94 | 6.95 | 62.49 | 294 | 36.9k | 34 MB zip | SSAO+4096 shadows+MSAA4; one 62 ms spike, p95 unaffected |
| 2026-09-13 | G1 | fa21a19 | WebGL (local, web tier) | not measured | not measured | — | — | — | 15.7 MB | ships web pipeline only, no SSAO shader compiled; deployed |
| 2026-09-13 | P2 | 6338ceb | Windows 1280×720 (desktop tier, Ultra) | 6.94 | 6.95 | 13.89 | 290 | 36.7k | — | no regression |
| 2026-09-13 | P2 | 6338ceb | WebGL (local, web tier) | not measured | not measured | — | — | — | 15.7 MB | tier verified in log; deployed |
| 2026-09-13 | G2 | 2c88fdf | Windows 1280×720 (desktop tier, Ultra) | 6.94 | 6.95 | 20.83 | 231 | 153.4k | — | two skinned characters; tris over the WebGL 150k budget by 2% — watch in G3 |
| 2026-09-13 | G2 | 2c88fdf | WebGL (local, web tier) | not measured | not measured | — | — | — | 16.3 MB | tier verified in log, rigs render in browser; deployed |
| 2026-09-13 | P3 | 333da1c | Windows 1280×720 (desktop tier, Ultra) | 6.94 | 6.95 | 13.89 | 233 | 153.5k | — | knockdowns, launches, wall impacts: no cost |
| 2026-09-13 | P3 | 333da1c | WebGL (local, web tier) | not measured | not measured | — | — | — | 16 MB | tier verified in log; knockdown, get-up and wall impact seen in a live run; deployed |
| 2026-09-14 | G3 | e01297a | Windows 1280×720 (desktop tier, Ultra), 12 s | 6.94 | 6.95 | 20.83 | 203 | 119.8k | — | cathedral, haze, shafts, dust; static batching cut draw calls |
| 2026-09-14 | G3 | e01297a | WebGL (local, web tier) | not measured | not measured | — | 166 | 104.6k | 25 MB | tier verified in log; cathedral renders in browser; deployed |
| 2026-09-14 | P4 | 43e60a8 | Windows 1280×720 (desktop tier, Ultra) | 6.94 | 6.95 | 20.84 | 213 | 120.5k | — | ragdolls, breakable parapet, pooled debris (150 cap) |
| 2026-09-14 | P4 | 43e60a8 | WebGL (local, web tier) | not measured | not measured | — | — | — | 25 MB | tier verified in log; debris seen flying in browser; deployed |
| 2026-09-15 | G4 | 8266be4 | Windows 1280×720 (desktop tier, Ultra) | 6.94 | 7.10 | 111.09 | 219 | 121.0k | — | final; one 111 ms spike (single hitch, p95 inside budget) |
| 2026-09-15 | G4 | 8266be4 | WebGL (local, web tier) | not measured | not measured | — | — | — | 25 MB | tier verified in log; deployed |
| 2026-09-15 | fixes | 44eb5cd | Windows 1280×720 (desktop tier, Ultra) | 6.95 | 6.95 | 13.89 | 179 | 105.2k | — | rig facing, camera confinement, roof cookie, baked probes + reflection, breakable columns |
| 2026-09-15 | fixes | 44eb5cd | WebGL (local, web tier) | not measured | not measured | — | — | — | 25 MB | tier verified in log; deployed |
| 2026-09-16 | grim dark | acf3e4c | Windows 1920×1080 (desktop tier, Ultra) | 6.94 | 6.95 | 20.83 | 259 | 118.4k | — | post-processing live for the first time, blood and blade smear, banners, stains, directional locomotion |
| 2026-09-17 | before stage A | bf06164 | WebGL (local, in-app browser pane, web tier) | 14.00 | 21.00 | 1028.00 | 247 | 90.1k | — | post-processing live on web for the first time; one ~1 s hitch |
| 2026-09-17 | stage A | eb20c1e | WebGL (local, separate Chrome window, web tier) | 7.00 | 8.00 | 69.00 | 172 | 63.0k | — | web post profile, 2 hard cascades, no depth copy, FXAA, thinned shafts/dust/candles/pools; different browser harness from the row above |
