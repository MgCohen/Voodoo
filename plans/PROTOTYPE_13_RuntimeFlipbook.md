# Prototype #13 — Runtime-Captured Flipbook

Validate: capture the rotation as a finite flipbook **once** at scene open, then disable the rendering camera permanently. Each cell plays back the captured frames as 2D animation.

Hypothesis: ~60 ms one-shot startup spike (amortized across the panel-open animation), then **zero** perpetual GPU/CPU cost for the cells. Memory cost: a few MB of texture in VRAM.

---

## Scene

**File:** `Assets/Scenes/Prototypes/Prototype_13_Flipbook.unity`

Hierarchy is identical to Prototype #06 except:
- `AtlasCamera` is disabled after the capture phase finishes.
- `Cell_Flipbook` advances UV across a flipbook strip instead of pointing at a single sub-rect.

```
__Benchmark
UICamera
EventSystem
Canvas (Screen Space - Camera)
  ScrollView ... Cell_00 .. Cell_11      (Cell_Flipbook prefab)
SkinFlipbookCapturer                     (root MonoBehaviour, drives capture FSM + playback)
  AtlasCamera                            (Camera, target=m_FrameRT, disabled after capture)
  Grid (Slot_00..Slot_11 with Brush_X-Menu + BrushRotation)
m_FlipbookRT                             (created at runtime: see "Atlas layout" below)
```

---

## Atlas layout

One big strip RT laid out as **frames horizontally, cells vertically** — easy UV math, easy to allocate as a single texture.

Defaults (tunable consts in `SkinFlipbookCapturer`):
- `c_FrameCount = 30`         (one full 360° rotation captured at 30 frames → playback at 30 fps = 1.0s loop. Brushes rotate 4× faster than current 90°/s in capture mode to fit 360° in 1s.)
- `c_CellPx = 128`
- Strip dims: `(c_FrameCount * c_CellPx) × (12 * c_CellPx)` = 3840 × 1536, ARGB32 → ~24 MB.
- Lower-mem variant: `c_CellPx = 96, c_FrameCount = 24` → 2304 × 1152 → ~10 MB.
- Even lower: capture into `Texture2DArray` of 12 layers × `c_FrameCount` slices to avoid the 4K texture, but stick with the 2D strip for prototype simplicity.

A **per-frame intermediate RT** (`m_FrameRT`, e.g. 384×512 = 3 cols × 4 rows of `c_CellPx`) is what the AtlasCamera renders into each capture step; we then `Graphics.CopyTexture` 12 sub-rects out of that into the strip's correct cell × frame slot.

---

## Capture FSM

State stored on `SkinFlipbookCapturer`:

```
enum Phase { Initializing, Capturing, Ready }
m_Phase
m_FrameIdx          // 0..c_FrameCount-1
m_CapturedThisFrame // for amortized capture
```

Each `LateUpdate` while `m_Phase == Capturing`:

1. Advance every `BrushRotation` by `360f / c_FrameCount` degrees deterministically (override `BrushRotation.Update` for prototype, or set `m_StepMode = true` and call `Step()`).
2. `m_AtlasCamera.Render()` (manual, not auto — we control timing).
3. For each cell i (0..11): compute its sub-rect in `m_FrameRT` and the destination sub-rect in `m_FlipbookRT` (column = `m_FrameIdx`, row = i), call `Graphics.CopyTexture(m_FrameRT, 0, 0, srcX, srcY, c_CellPx, c_CellPx, m_FlipbookRT, 0, 0, dstX, dstY)`.
4. `m_FrameIdx++`. If `m_FrameIdx == c_FrameCount`: `m_AtlasCamera.enabled = false; Destroy(m_FrameRT); m_Phase = Ready;` and notify cells they can start playback.

Two capture pacings to test:
- **Amortized**: 1 frame captured per render frame → 30 captures / 60 fps = 0.5s wall time. Hidden behind the panel-open animation.
- **Burst**: capture all `c_FrameCount` frames in one frame inside `Awake`. Single huge spike (~60 ms) but no perceived hitch since it happens before first render.

Both are worth measuring.

---

## Playback

`Cell_Flipbook` (per cell):
- Holds `int CellRow`, `RawImage img`, ref to `m_FlipbookRT` and `c_FrameCount`.
- `Update()`: `int frame = (int)(Time.time * c_PlaybackFps) % c_FrameCount;`
  Set `img.uvRect = new Rect(frame / (float)c_FrameCount, (11 - CellRow) / 12f, 1f / c_FrameCount, 1f / 12f);`
- That's it. No camera, no 3D, no allocation.

For the **selection** scenario: just toggle the highlight `Image`, no flipbook change.

For the **color storm** scenario, two handling strategies — measure both:
- **Strategy A: Re-capture on color change.** When `RecolorAll()` is called, set `m_Phase = Capturing`, `m_FrameIdx = 0`, re-enable camera. Worst case: re-capture every 0.5s for 10s. This is how we'd know if recapture is fast enough to be live-recolorable.
- **Strategy B: Don't support live recolor.** Skip `color_storm` for this prototype, document that flipbook flavors are "captured at panel open, immutable until next open." This is the realistic shipping mode.

Run **A** in the benchmark by default; report **B**'s implied perpetual cost (zero).

---

## Assets used

Same as Prototype #06: 2 menu brush prefabs + 6 (or 12) `SkinData`. No new shaders. No editor-time bake — entirely runtime.

---

## New scripts

`Assets/Scripts/Prototypes/Flipbook/`:
- `SkinFlipbookCapturer.cs` — FSM above.
- `Cell_Flipbook.cs` — playback above.
- (Optional) `BrushRotationStepped.cs` — variant of `BrushRotation` that exposes `Step(float deg)` instead of running on `Time.deltaTime`. Needed so capture is deterministic and not coupled to wall-clock.

---

## Run procedure

1. Open `Prototype_13_Flipbook.unity`.
2. Build & Run; scenario runs.
3. Phases of interest:
   - First ~0.5 s of `idle_baseline` includes capture spike — `BenchmarkOverlay` should report this as a separate `capture` sub-phase (the capturer raises an event the overlay subscribes to).
   - `scroll`, `selection_storm`, `idle_recovery`: should be at empty-scene baseline.
   - `color_storm`: spikes if Strategy A is on; nothing if Strategy B.

Repeat with three configs to bracket the trade space:
- (a) `c_CellPx=128, c_FrameCount=30, amortized capture, Strategy A`
- (b) `c_CellPx=96,  c_FrameCount=24, amortized capture, Strategy A`
- (c) `c_CellPx=128, c_FrameCount=30, burst capture, Strategy B`

---

## What we expect to see

- Capture spike: 30–80 ms total, distributed across ~0.5 s (amortized) or all in one frame (burst).
- Perpetual cost in `scroll`/`selection`/`recovery`: indistinguishable from empty scene.
- Memory: +10–24 MB texture depending on config.
- `color_storm` Strategy A: repeated mini-spikes every 0.5s.
- `color_storm` Strategy B: flat baseline (recolor not supported).

---

## What would invalidate this approach

- Capture spike > 16 ms in burst mode AND > 4 ms in amortized mode → playback wins, but capture is hard to hide.
- Memory delta > 30 MB at default config → drop to 96px / 24 frames; if still > 30 MB something is wrong (look for accidental mip allocation).
- Visible "frame jitter" during playback at 30 fps loop → bump frame count or use `Mathf.RoundToInt` with subframe interpolation between two adjacent frames (sample two and lerp in shader — keeps frame count low).
- `Graphics.CopyTexture` not supported on the GPU/format combo → fall back to per-cell `RenderTexture.active` + `Graphics.Blit` with a UV offset material. Slower capture, same playback.

---

## Decision rule vs. Prototype #06

Run #06 first.
- If #06's `scroll` perpetual cost is < 1.5 ms → **ship #06**, archive #13.
- If #06's `scroll` perpetual cost is 1.5–4 ms → **ship #13** (config c, burst+B is simplest), archive #06.
- If both are within 0.5 ms of each other → ship #06 for code simplicity.
