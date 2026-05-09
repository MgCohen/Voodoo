# Prototype Benchmark Harness (shared)

A common, dependency-free benchmark setup reused by all three skin-rendering prototypes. Drop the same component + scenario script into each prototype scene so results are directly comparable.

---

## Goal
Produce comparable per-prototype numbers for: average frame time, 99th-percentile frame time, draw calls / batches, set-pass calls, and texture/RT memory delta. Output to a CSV that can be diffed across prototypes.

---

## Scripts

All three live under `Assets/Scripts/Prototypes/Shared/`. None of them depend on Zenject, services, views, or anything else from the production codebase.

### `BenchmarkOverlay.cs`
On-screen text overlay (legacy `IMGUI` `OnGUI`, no UGUI dependency so it can't perturb UI batches in the prototype).

Tracks, every frame:
- Frame time (ms): rolling 1s avg, rolling 5s 99p, instantaneous
- FPS (1s window)
- Total allocated managed memory delta vs. scene-start baseline (`Profiler.GetTotalAllocatedMemoryLong()`)
- Texture memory (`Profiler.GetAllocatedMemoryForGraphicsDriver()`)
- Editor-only: draw calls, batches, SetPass calls (`UnityStats.drawCalls` etc.)

Tracks per scenario phase (see `BenchmarkScenario.cs`):
- Phase name, duration, avg frame time, 99p frame time, peak frame time, mem delta in/out

Public API:
```csharp
public void StartPhase(string name);
public void EndPhase();
public void DumpCsv(string path);  // Application.persistentDataPath
```

### `BenchmarkScenario.cs`
Drives a fixed test sequence so all prototypes are exercised identically. No user input needed — fully scripted so device runs are repeatable.

Sequence (total ~60s):
1. **Idle baseline** — 5s, screen open, nothing happening.
2. **Continuous scroll** — 20s, programmatically drives the `ScrollRect.verticalNormalizedPosition` from 1→0→1 at 0.25 cycles/sec.
3. **Selection storm** — 10s, simulates a tap on a different cell every 0.5s by invoking the cell's selection callback directly (no `EventSystem` raycast — keeps it deterministic).
4. **Color-change storm** — 10s, re-applies a different `SkinData` color to all cells every 0.5s. Validates worst-case for tint/bake paths.
5. **Idle recovery** — 10s, back to idle, measure return to baseline.

At end of sequence, calls `BenchmarkOverlay.DumpCsv` and writes `prototype_<name>_<deviceModel>.csv` to `Application.persistentDataPath`.

### `FrameTimeProbe.cs`
Wraps Unity's `FrameTimingManager` if available (`SystemInfo.supportsGpuRecorder` check) for true GPU vs CPU times. Falls back to `Time.deltaTime` only.

---

## Scene additions (per prototype)

Every prototype scene contains, in addition to its own setup:
- A single empty GameObject `__Benchmark` with `BenchmarkOverlay` + `BenchmarkScenario` + `FrameTimeProbe`.
- A `BenchmarkScenario` field referencing the prototype's `ScrollRect` (only `Continuous scroll` phase needs it).
- A `BenchmarkScenario` field referencing an array of cell selection callbacks (provided per prototype).

---

## How to run

### Editor
1. Open the prototype scene.
2. Press Play.
3. Wait 60s — overlay shows live stats.
4. CSV is written; path is logged to console.

### Device (Android, the real test)
1. Build & Run with **Development Build + Autoconnect Profiler** off (profiler skews results).
2. **Disable VSync** in Quality settings for the prototype scenes — `Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;` set in `BenchmarkScenario.Awake()`.
3. Lock CPU governor / thermals as best you can: run on the same device, same battery level (>50%), same ambient temperature, screen at 50% brightness.
4. Pull CSV via `adb pull /sdcard/Android/data/<package>/files/`.

---

## CSV format

```
phase,duration_s,frame_avg_ms,frame_99p_ms,frame_peak_ms,fps_avg,mem_managed_delta_mb,mem_gfx_delta_mb,draw_calls_avg,batches_avg,setpass_avg
idle_baseline,5.00,12.41,16.20,18.30,80.6,0.1,0.4,8,3,2
scroll,20.00,13.05,17.90,21.10,76.6,0.2,0.6,12,5,4
selection_storm,10.00,...
color_storm,10.00,...
idle_recovery,10.00,...
```

---

## Pass criteria (what "good" looks like on a mid-tier Android, e.g. Pixel 4a / Galaxy A52)

| Metric | Pass | Concerning | Fail |
|---|---|---|---|
| Avg frame time delta vs. empty scene baseline | < 2 ms | 2–4 ms | > 4 ms |
| 99p frame time | < 18 ms (smooth 50fps) | 18–25 ms | > 25 ms |
| Mem delta over scenario | < 10 MB | 10–30 MB | > 30 MB |
| Draw calls in `scroll` phase | < 30 added vs. baseline | 30–60 | > 60 |

Apply the same thresholds to all three prototypes and compare deltas, not absolutes — device variance is huge.

---

## Folder layout for the prototypes

```
Assets/
  Scenes/Prototypes/
    Prototype_06_Atlas.unity
    Prototype_13_Flipbook.unity
    Prototype_12_Direct3D.unity
  Scripts/Prototypes/
    Shared/
      BenchmarkOverlay.cs
      BenchmarkScenario.cs
      FrameTimeProbe.cs
    Atlas/                  (per Prototype #06)
    Flipbook/               (per Prototype #13)
    Direct3D/               (per Prototype #12)
  Prefabs/Prototypes/
    Cell_Atlas.prefab
    Cell_Flipbook.prefab
    Cell_Direct3D.prefab
```

Nothing in `Prototypes/` references project services/views, so the whole folder can be deleted at the end of the spike with no production fallout.
