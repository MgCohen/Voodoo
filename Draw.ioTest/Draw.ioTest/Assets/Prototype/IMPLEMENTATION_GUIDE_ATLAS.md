# Skin Preview via Atlas RenderTexture — Implementation Guide

## Summary

One offscreen camera renders all brush previews into a single shared RenderTexture.
Each UI cell is a RawImage that displays a sub-rect (uvRect) of that RT.
Scrolling, masking, and layout are handled by standard Unity UI (ScrollRect + Mask).

## Architecture

```
  World space (layer 31, hidden from main camera)
  ┌──────────────────────────────────┐
  │  GridRoot                        │
  │  ┌───┐ ┌───┐ ┌───┐              │
  │  │ B0│ │ B1│ │ B2│   (row 0)    │
  │  └───┘ └───┘ └───┘              │
  │  ┌───┐ ┌───┐ ┌───┐              │       ┌─────────────────┐
  │  │ B3│ │ B4│ │ B5│   (row 1)    │──────▶│  RenderTexture   │
  │  └───┘ └───┘ └───┘              │       │  (512 x 1708)   │
  │  ...                             │       └────────┬────────┘
  └──────────────────────────────────┘                │
         ▲                                            ▼
    AtlasCamera                              ┌─────────────────┐
    (ortho, layer 31 only)                   │  UI ScrollView   │
                                             │  ┌────┐ ┌────┐  │
                                             │  │cell│ │cell│  │
                                             │  │uv00│ │uv10│  │
                                             │  └────┘ └────┘  │
                                             │  ┌────┐ ┌────┐  │
                                             │  │cell│ │cell│  │
                                             │  └────┘ └────┘  │
                                             └─────────────────┘
```

## Step-by-step implementation

### Step 1 — Layer setup

Create a dedicated layer (e.g. layer 31 "SkinPreview").
- The main UI camera must EXCLUDE this layer (`cullingMask = ~(1 << 31)`)
- The atlas camera must ONLY render this layer (`cullingMask = 1 << 31`)

### Step 2 — Create the RenderTexture

```csharp
// Aspect ratio must match the grid layout (cols : rows).
// For 3 columns x 10 rows:  512 / (3/10) = 512 x 1708
var rt = new RenderTexture(512, 1708, 16, RenderTextureFormat.ARGB32);
rt.useMipMap = false;
rt.antiAliasing = 1;
rt.Create();
```

### Step 3 — Configure the atlas camera

```csharp
atlasCamera.targetTexture = rt;
atlasCamera.cullingMask = 1 << skinPreviewLayer;
atlasCamera.clearFlags = CameraClearFlags.SolidColor;
atlasCamera.backgroundColor = new Color(0, 0, 0, 0);  // transparent
atlasCamera.orthographic = true;
atlasCamera.orthographicSize = rows * worldCellSize * 0.5f;
atlasCamera.aspect = (float)cols / rows;
```

### Step 4 — Spawn brush prefabs in a world-space grid

```csharp
for (int i = 0; i < skinCount; i++)
{
    int col = i % cols;
    int row = i / cols;
    
    // Center the grid on the GridRoot origin.
    Vector3 pos = new Vector3(
        (col - (cols - 1) * 0.5f) * worldCellSize,
       -(row - (rows - 1) * 0.5f) * worldCellSize,
        0f);
    
    GameObject brush = Instantiate(prefab, gridRoot);
    brush.transform.localPosition = pos;
    
    // Move to preview layer so only atlas camera sees it.
    SetLayerRecursive(brush, skinPreviewLayer);
    
    // Add rotation, apply color, etc.
}
```

### Step 5 — Create UI cells (RawImages with uvRect)

```csharp
float uvW = 1f / cols;   // each cell = 1/3 of RT width
float uvH = 1f / rows;   // each cell = 1/10 of RT height

for (int i = 0; i < skinCount; i++)
{
    int col = i % cols;
    int row = i / cols;
    
    // UV origin is bottom-left, but grid row 0 is at the top.
    Rect uv = new Rect(
        col * uvW,
        1f - (row + 1) * uvH,
        uvW,
        uvH);
    
    var rawImage = cell.GetComponent<RawImage>();
    rawImage.texture = rt;
    rawImage.uvRect = uv;
}
```

### Step 6 — Lighting

Replicate the production main menu lighting so brushes look correct:

```csharp
RenderSettings.ambientMode = AmbientMode.Flat;
RenderSettings.ambientLight = new Color(0.85f, 0.80f, 0.75f);  // warm gray

var light = new GameObject("Light").AddComponent<Light>();
light.type = LightType.Directional;
light.color = new Color(1f, 1f, 0.835f);  // warm white
light.intensity = 1.1f;
light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
```

### Step 7 — Coloring (selective, not all renderers)

The production system only colors specific parts of the brush:
- **Menu prefabs**: `BrushMenu.m_BrushParts` (roller + grip, NOT the handle)
- **Gameplay prefabs**: `Brush.m_Renderers` + `Brush.m_DarkRenderers` (darkened at 80%)

```csharp
// Get only the colorable renderers (not all of them).
var renderers = PrototypeBrushUtil.CollectColorableRenderers(brush);

// Apply with two-tone darkening for Brush.m_DarkRenderers.
PrototypeBrushUtil.ApplyBrushColor(brush, renderers, skinColor);
```

## File inventory

| File | Lines | Purpose |
|------|-------|---------|
| `06_Atlas/SkinAtlasRenderer.cs` | ~210 | Main controller: RT, brushes, cells, camera |
| `06_Atlas/AtlasCell.cs` | ~35 | Per-cell: RawImage + uvRect + Button |
| `Shared/PrototypeSkinSet.cs` | ~30 | Grid config: count, cols, rows, colors |
| `Shared/PrototypeBrushUtil.cs` | ~100 | Helpers: layer, strip, color |
| `Editor/AtlasSceneBuilder.cs` | ~100 | Builds entire scene from code |
| `Editor/PrototypeBuilderUtil.cs` | ~340 | Shared editor helpers |

## Key decisions

| Decision | Rationale |
|----------|-----------|
| One RT for all skins | Avoids N render textures; UI batches all cells in ~1 draw call |
| Ortho camera | No perspective distortion; grid maps 1:1 to UV rects |
| Layer isolation | Atlas camera only sees brushes; UI camera ignores them |
| Transparent clear | UI cells composit cleanly over any background |
| No mipmaps | Cells display at roughly 1:1 pixel ratio; mipmaps waste memory |
| Menu prefabs preferred | BrushMenu.m_BrushParts gives correct selective coloring |

## Performance (30 skins, uncapped, desktop)

| Metric | Value |
|--------|-------|
| Idle FPS | ~607 |
| Scroll FPS | ~590 |
| Selection FPS | ~394 |
| Recolor FPS | ~549 |
| GFX memory | +0.01 MB (stable) |

## Adapting for production

1. Replace `PrototypeSkinSet` constants with the real skin catalog
2. Replace brush prefab loading with the production asset pipeline
3. Replace `PrototypeBrushUtil.ApplyBrushColor` with the production `BrushMainMenu.Set()` / `Brush.SetColor()`
4. The RT resolution can scale with screen DPI — currently fixed at 512x1708
5. Consider disabling the atlas camera when the menu is not visible (`OnEnable`/`OnDisable` already do this)
