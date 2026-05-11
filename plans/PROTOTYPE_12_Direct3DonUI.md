# Prototype #12 — 3D Models Drawn Directly Over UI (with scroll)

Validate: 12 actual 3D brush meshes are drawn in world space and made to follow their corresponding UI cell positions. **No RenderTexture.** Cells are invisible UI rects that act only as positional anchors and click targets. A custom clip shader masks each brush to the ScrollRect viewport.

Hypothesis: ~0.5 ms perpetual cost (less than the atlas — no RT blit), but significantly more code and shader complexity once scrolling/masking enters the picture.

---

## Scene

**File:** `Assets/Scenes/Prototypes/Prototype_12_Direct3D.unity`

```
__Benchmark
EventSystem
UICamera                                 (Camera, Depth=0, Orthographic OFF — this matters,
                                          see "Why Screen Space - Camera" below)
Canvas (Screen Space - Camera, refCam=UICamera, plane distance e.g. 100)
  ScrollView (vertical)
    Viewport (RectMask2D)
      Content (GridLayoutGroup, 3 cols, cell 256x256)
        Cell_00 .. Cell_11               (Cell_Direct3D prefab: RectTransform + selection
                                          border Image + Button only; no RawImage)
ClipRectFeeder                           (script reading Viewport screen rect → global shader prop)
BrushSyncRoot                            (parent of 12 brush instances)
  Brush_00 .. Brush_11                   (Brush_X-Menu prefab + BrushRotation +
                                          BrushScreenSync; material uses BrushClipped shader)
SkinDirect3DController                   (root MonoBehaviour, wires everything)
```

UICamera renders **both** the Canvas (Screen Space - Camera mode) and the brushes — they live in world space at the same depth as the canvas plane. Only one camera total.

---

## Why Screen Space - Camera (not Overlay)

`Screen Space - Overlay` renders the canvas after everything else with no depth interaction. We want the brushes intermixed with UI elements (selection border drawn over brush, click target invisible above brush). `Screen Space - Camera` puts the canvas at a known world depth so we can put brushes between the canvas plane and the camera — they appear "in" the UI naturally and respect z-sorting against UI sprites.

---

## The clipping problem (the real reason this is hard)

`RectMask2D` and `Mask` only clip `Graphic` components (Image/RawImage/TMP). They do **not** clip arbitrary 3D meshes. With brushes drawn in world space, anything scrolled past the viewport edges will render on top of other UI.

### Clip shader

`Assets/Shaders/Prototypes/BrushClipped.shader` — minimal Built-in pipeline surface or vertex/fragment shader that:

- Takes a global `float4 _SkinClipRect` in pixel coordinates `(xMin, yMin, xMax, yMax)`, set by `ClipRectFeeder`.
- In the fragment shader, reads screen position via `VPOS` (Built-in: `UNITY_VPOS_TYPE screenPos : VPOS`).
- `clip(any(screenPos.xy < _SkinClipRect.xy || screenPos.xy > _SkinClipRect.zw) ? -1 : 1);`
- Otherwise behaves like the brush's existing simple lit/unlit shader (porting from the project's existing shader is a 5-line copy).

`SkinDirect3DController.Awake` swaps each brush renderer's material to use this shader and applies the per-skin color via `MaterialPropertyBlock` to keep instancing happy.

### Edge clipping

Cells partially off-screen at the top/bottom of the viewport need their brush partially clipped, not all-or-nothing. The fragment-level clip above handles that automatically — fragments on the visible side of the line render, the rest discard. This is the main reason for fragment clip vs. e.g. a per-object cull check.

---

## Per-frame sync

`BrushScreenSync` (one per brush, runs in `LateUpdate` after Canvas update):

```
Vector3 cellWorld = m_UICamera.ScreenToWorldPoint(new Vector3(
    m_CellRect.position.x,
    m_CellRect.position.y,
    m_DistanceToCanvas));
m_Transform.position = cellWorld + m_LocalOffsetToCenterBrush;
m_Transform.localScale = Vector3.one * m_ScaleMatchedToCellSize;
```

Notes:
- Use `LateUpdate`, after Canvas has positioned UI for this frame, otherwise brushes lag scrolling by one frame.
- `m_DistanceToCanvas` is the canvas's plane distance from the camera, set via Inspector.
- `m_ScaleMatchedToCellSize` derived once at Awake from `Cell.rect.size` × `m_UICamera.orthographicSize`-relevant factor (perspective: from `cellRect.size * m_DistanceToCanvas / m_UICamera.focalLength`-equivalent — pre-compute it; a few minutes of Inspector tweaking gets it right and it's stable across resolutions if it's a function of cell pixel size).

---

## Assets used

- `Brush_1-Menu.prefab` and `Brush_2-Menu.prefab` (2 models)
- `Skin01..Skin06.asset` (extend to 12 reusing existing colors)
- New shader: `BrushClipped.shader`
- No RenderTextures. No flipbooks.

---

## New scripts

`Assets/Scripts/Prototypes/Direct3D/`:
- `SkinDirect3DController.cs` — instantiates 12 brushes parented to `BrushSyncRoot`, pairs each with its `Cell_Direct3D` (by index), assigns `BrushClipped` material + per-skin color via `MaterialPropertyBlock`.
- `BrushScreenSync.cs` — per-frame world position sync (above).
- `ClipRectFeeder.cs` — every frame, computes the ScrollRect viewport's screen-space rect (`RectTransformUtility.GetWorldCornersInScreenSpace` equivalent) and `Shader.SetGlobalVector("_SkinClipRect", rect)`.
- `Cell_Direct3D.cs` — RectTransform + Button + selection border Image. No 3D.

---

## Selection / color change

Selection: toggles a UI border `Image` over the cell. Brush is untouched.

Color change (color storm): `MaterialPropertyBlock.SetColor(_BaseColor)` per renderer. Cheap. Measure that instancing batches stay merged after the change.

---

## Run procedure

1. Open `Prototype_12_Direct3D.unity`.
2. Build & Run, scenario runs.
3. Specifically watch:
   - `scroll` phase: are brushes correctly clipped at viewport edges? Visual verification by recording the screen.
   - `scroll` phase: is sync 1-frame lagged or 0-frame? Verify by stepping the scenario in editor with high `Application.targetFrameRate = 10` to exaggerate any lag.
   - Mid-scroll layout breaks: tap a cell while scrolling. Does the border align with the brush?

---

## What we expect to see

- `idle_baseline`, `scroll`, `selection_storm`, `idle_recovery`: 0.3–1 ms perpetual delta — likely the cheapest of the three options on raw frame time.
- Draw calls: ideally 1–2 instanced calls (12 brush meshes batched) + UI batches. If batching breaks (per-cell material instances), draw calls jump to 12–24, which is still fine but watch for it.
- Memory: minimal — no RTs, no flipbook strips.

---

## What would invalidate this approach

- 1-frame visible lag between cell and brush during scroll (sync ordering wrong) — fix by ensuring `BrushScreenSync` runs in `LateUpdate` *after* the Canvas. If still lagged, hook `Canvas.willRenderCanvases`.
- Brushes visibly leak past the ScrollRect viewport — clip shader broken (`_SkinClipRect` in wrong space, or shader didn't get the global). Validate by drawing a debug rectangle in the same coordinates.
- GPU instancing breaks because of per-renderer materials — confirm via Frame Debugger that batches stay merged.
- Resolution-dependent scaling drift on different aspect ratios — recompute `m_ScaleMatchedToCellSize` from current cell pixel size each frame instead of caching at Awake.
- ScrollRect uses `RectMask2D` AND a softness/falloff that we can't replicate in clip shader → results look subtly different from neighboring UI elements at the edges. Acceptable for prototype, would need a smoother clip in production.

---

## Decision rule

Compare against #06:
- If #12 is meaningfully faster (delta > 0.5 ms perpetual) **and** clipping/sync works visually correctly across the 60s scenario → ship #12.
- If #12 is within noise of #06 → ship #06 (less code, no shader, no sync ordering trap, no clip math).
- If #12 has any visible glitch in the 60s scenario → ship #06.

The bar for #12 to win is high precisely because the failure modes are visual (and embarrassing on a portfolio test) rather than just numerical.
