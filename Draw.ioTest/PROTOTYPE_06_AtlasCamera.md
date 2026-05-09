# Prototype #06 — Single-Camera Atlas + UV Sub-Rects

Validate: one secondary camera renders all 12 brushes (live, rotating, colored) into one RenderTexture every frame. Each UI cell is a `RawImage` that samples a fixed sub-rect of that RT.

Hypothesis: ~1 ms perpetual GPU/CPU cost on mid-Android. Scrolls and masks natively because cells are plain UI quads.

---

## Scene

**File:** `Assets/Scenes/Prototypes/Prototype_06_Atlas.unity`

Empty scene — no Zenject, no `Game.unity` dependencies.

Hierarchy:
```
__Benchmark                              (BenchmarkOverlay + BenchmarkScenario + FrameTimeProbe)
UICamera                                 (Camera, Depth=0, Clear=Solid colour, Culling Mask=UI only)
EventSystem
Canvas (Screen Space - Camera, refCam=UICamera)
  ScrollView (vertical)
    Viewport (RectMask2D)
      Content (GridLayoutGroup, 3 cols, cell 256x256)
        Cell_00 .. Cell_11               (Cell_Atlas prefab, RawImage referencing m_AtlasRT)
SkinAtlas                                (root, layer=SkinPreview, position=(0,-100,0))
  AtlasCamera                            (Camera, Depth=-10, Clear=Solid transparent,
                                          Culling Mask=SkinPreview only,
                                          Orthographic, Target Texture=m_AtlasRT)
  Grid                                   (4 rows x 3 cols of brush slots)
    Slot_00 .. Slot_11                   (each has a Brush_X-Menu instance + BrushRotation,
                                          colored from SkinDataX at Awake)
SkinAtlasRenderer                        (root MonoBehaviour, drives setup)
```

New layer: `SkinPreview` (added in Tags & Layers). `UICamera` excludes it. `AtlasCamera` includes only it.

---

## Assets used

- `Assets/Prefabs/Brushs/MainMenuBrushes/Brush_1-Menu.prefab` and `Brush_2-Menu.prefab` (2 models)
- `Assets/Resources/Skins/Skin01..Skin06.asset` (6 colors — extend with 6 more SkinData by reusing existing `ColorData` if needed for the full 12)
- `Assets/Scripts/UI/BrushRotation.cs` (existing, reused as-is)
- New `m_AtlasRT` RenderTexture asset, 512×768, ARGB32, no depth, point/bilinear filter — created in script, not on disk.

---

## New scripts

`Assets/Scripts/Prototypes/Atlas/`:

### `SkinAtlasRenderer.cs`
- Holds: `SkinData[12] m_Skins`, `Transform m_GridRoot`, `Camera m_AtlasCamera`, `RawImage[] m_Cells`, atlas size + cell layout consts.
- `Awake()`:
  1. Create `m_AtlasRT` (`new RenderTexture(512, 768, 0, RenderTextureFormat.ARGB32)`, `useMipMap=false`, `antiAliasing=1`). Assign to `m_AtlasCamera.targetTexture`.
  2. For each skin index: instantiate `Skin.Brush.m_Prefab` under `m_GridRoot`, position at `worldGridPos(i)`, color via the same logic as `BrushMainMenu.Set()` (loop `Renderer.material.color`), add `BrushRotation`.
  3. Frame the atlas camera around the grid: ortho size = grid half-height + margin; XY = grid center.
  4. For each `m_Cells[i]`: `texture = m_AtlasRT`; `uvRect = computeUv(i)`.
- `OnDisable()`: `m_AtlasCamera.enabled = false` (prove the "0 cost when hidden" claim).
- `OnEnable()`: `m_AtlasCamera.enabled = true`.

### `Cell_Atlas.cs` (on the cell prefab)
- Trivial: `RawImage` ref + `int Index` + selection-border `Image` toggle + `Action<int> onTap`.
- No 3D, no animation logic — animation comes from the atlas RT itself.

---

## UV math (one helper, ~10 lines)

```
gridCols = 3, gridRows = 4
cellUv.width  = 1f / gridCols
cellUv.height = 1f / gridRows
uvRect[i] = new Rect((i % gridCols) * cellUv.width,
                     1 - ((i / gridCols) + 1) * cellUv.height,
                     cellUv.width, cellUv.height)
```

The same i→worldGridPos mapping is used for placing brushes; that guarantees cell-to-brush alignment without manual tuning.

---

## Selection / color change

`Cell_Atlas.onTap(i)` calls `SkinAtlasRenderer.SetSelected(i)` → moves a single highlight `Image` over cell i. No 3D state changes.

For the **color storm** scenario: `BenchmarkScenario` calls `SkinAtlasRenderer.RecolorAll()` which reapplies fresh colors to each brush instance via the same `Renderer.material.color = ...` loop. This is the worst case for material instancing — measure it.

---

## Run procedure

1. Open `Prototype_06_Atlas.unity`.
2. Build & Run on target device (see `PROTOTYPE_00_BenchmarkHarness.md`).
3. Let the 60s scenario run.
4. Pull CSV. Repeat 3× to get variance.

Optional sanity checks:
- Toggle `m_AtlasCamera.enabled = false` from a button — atlas freezes, perpetual cost should drop to baseline.
- Render scale: try 256×384 atlas vs 512×768 vs 1024×1536 — confirm cost scales with pixels, not with scroll activity.

---

## What we expect to see

- `idle_baseline`: tiny delta vs. empty scene (~0.5–1 ms).
- `scroll`: same as idle (UV sub-rects are static, scrolling moves the RawImage rect on screen, doesn't invalidate the atlas).
- `selection_storm`: same as idle (selection is UI only).
- `color_storm`: small spike (12 material color writes per change × 2 per second). Probably <0.2 ms.
- `idle_recovery`: returns cleanly to baseline.

---

## What would invalidate this approach

- `scroll` 99p > 25 ms on target device → atlas RT resolve is too expensive.
- `color_storm` causing material leaks → confirm with `Profiler.GetAllocatedMemoryForGraphicsDriver()` not growing across the 10s phase.
- Visible aliasing on the brush silhouettes at 256×256 cells — bump atlas size and re-measure.

If atlas approach passes here, no need to try #13. If it marginally fails on perpetual cost, fall back to #13 (same scene + capture FSM).
