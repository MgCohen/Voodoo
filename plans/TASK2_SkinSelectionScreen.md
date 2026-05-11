# Task 2 — Skin Selection Screen

## Goal
Dedicated skin selection screen with 12 scrollable 3D skins (2 models x 6 colors), replacing the inline selector.

---

## Step 1: Understand the existing pattern

The main menu already shows a 3D brush. Here's how it works — follow this exactly:

- **No RenderTexture.** The brush is a world-space 3D object at `(~0, 3, 0)`.
- One camera renders both 3D world and UI Canvas.
- `BrushRotation.cs` on the parent auto-rotates at 90 deg/s around Y: `m_Transform.RotateAround(m_Transform.position, m_Transform.up, Time.deltaTime * 90f)`
- `BrushMainMenu.Set(SkinData)` instantiates the brush prefab, parents it to `m_BrushParent`, resets transform, applies color to all renderers.
- Menu-specific brush prefabs live in `Prefabs/Brushs/MainMenuBrushes/` (`Brush_1-Menu.prefab` through `Brush_4-Menu.prefab`). These are separate from gameplay prefabs.
- Skin selection is left/right arrows in `MainMenuView.ChangeBrush()` cycling `m_IdSkin`.
- Saved via `StatsService.FavoriteSkin` (PlayerPrefs `"FavoriteSkin"`, int, default 0).

---

## Step 2: Create the 12 SkinData assets

Currently: 6 SkinData in `Resources/Skins/`, 4 BrushData in `Resources/Brushs/`, 13 ColorData in `Resources/Colors/`.

Test asks for 2 models × 6 colors = 12 skins. Pick 2 of the 4 existing brush models, pair each with 6 of the existing colors.

Create 6 new `SkinData` assets in `Resources/Skins/`:
- `Skin07.asset` through `Skin12.asset`
- Each references an existing `BrushData` + existing `ColorData`

No new scripts needed for this step — just asset creation in the Unity editor.

---

## Step 3: SkinSelectionView

### Pattern choice: View\<T> or standalone MonoBehaviour?

Two precedents in the codebase:
- **View\<T>** — phase-driven views (MainMenuView, EndView). Use CanvasGroup alpha fade, respond to `onGamePhaseChanged`.
- **SettingsPanel** — standalone MonoBehaviour with Animator. Toggled independently of game phase.

The skin selection screen opens/closes from MainMenu without changing game phase → **follow the SettingsPanel pattern**. It's a panel overlay, not a phase transition.

### SkinSelectionScreen.cs
`Assets/Scripts/UI/SkinSelectionScreen.cs`

```csharp
public class SkinSelectionScreen : SingletonMB<SkinSelectionScreen>
{
    [SerializeField] Animator m_Animator;
    [SerializeField] RectTransform m_GridParent;    // ScrollRect content
    [SerializeField] Transform m_HeroBrushParent;   // large preview container
    [SerializeField] GameObject m_SkinCellPrefab;   // RawImage + Button

    [Header("Atlas rendering")]
    [SerializeField] Camera m_AtlasCamera;          // ortho, layer 31 only
    [SerializeField] Transform m_GridRoot;           // world-space brush parent
    [SerializeField] int m_SkinPreviewLayer = 31;

    IGameService m_GameService;
    IStatsService m_StatsService;

    bool m_Visible;
    int m_SelectedSkin;
    List<SkinCell> m_Cells = new List<SkinCell>();
    RenderTexture m_AtlasRT;

    [Inject]
    public void Construct(IGameService gameService, IStatsService statsService) { ... }
}
```

Open/close via Animator (matches SettingsPanel):
```csharp
public void Show()
{
    m_SelectedSkin = m_StatsService.FavoriteSkin;
    m_Visible = true;
    m_Animator.SetBool("Visible", true);
    RefreshSelection();
}

public void Hide()
{
    m_Visible = false;
    m_Animator.SetBool("Visible", false);
}
```

---

## Step 4: 3D skin rendering in the grid

### Approach: Atlas RenderTexture (prototyped and validated)

> **Prototype reference:** `Assets/Prototype/06_Atlas/` — working scene with 30 skins at 590+ FPS.
> Build it via `Tools > Prototype > Build #06 Atlas Scene`, then hit Play.

One offscreen orthographic camera renders ALL brush prefabs into a **single shared RenderTexture** every frame. Each UI cell is a `RawImage` whose `uvRect` samples its sub-region of that RT. Brushes rotate live, keep their original materials, and scrolling/masking is handled entirely by standard Unity UI.

```
  World space (layer 31, hidden from main camera)
  ┌──────────────────────────────────┐
  │  GridRoot  (at y = -100)         │
  │  ┌───┐ ┌───┐ ┌───┐              │
  │  │ B0│ │ B1│ │ B2│              │       ┌────────────────┐
  │  └───┘ └───┘ └───┘              │──────>│ RenderTexture   │
  │  ┌───┐ ┌───┐ ┌───┐              │       │ (512 x 768)    │
  │  │ B3│ │ B4│ │ B5│              │       └───────┬────────┘
  │  └───┘ └───┘ └───┘              │               │
  │  ...                             │               v
  └──────────────────────────────────┘       ┌────────────────┐
         ^                                   │ UI ScrollRect   │
    AtlasCamera                              │ ┌────┐  ┌────┐ │
    (ortho, layer 31 only)                   │ │cell│  │cell│ │
                                             │ │uv00│  │uv10│ │
                                             │ └────┘  └────┘ │
                                             └────────────────┘
```

### 4.1 — Layer setup

Create layer 31 `SkinPreview` (or any free layer).
- Main UI camera: `cullingMask = ~(1 << 31)` — excludes brush meshes.
- Atlas camera: `cullingMask = 1 << 31` — renders ONLY brush meshes.

### 4.2 — RenderTexture creation

One RT for all skins. Aspect ratio must match the grid layout (cols : rows).

```csharp
// 12 skins in 3×4 grid → 512 × 768 px.  (For 30 skins: 3×10 → 512 × 1708 px.)
int rtWidth  = 512;
int rtHeight = Mathf.RoundToInt(rtWidth * ((float)rows / cols));

var rt = new RenderTexture(rtWidth, rtHeight, 16, RenderTextureFormat.ARGB32);
rt.useMipMap = false;
rt.antiAliasing = 1;
rt.Create();
```

### 4.3 — Atlas camera

```csharp
atlasCamera.targetTexture     = rt;
atlasCamera.cullingMask       = 1 << 31;
atlasCamera.clearFlags        = CameraClearFlags.SolidColor;
atlasCamera.backgroundColor   = new Color(0, 0, 0, 0);   // transparent
atlasCamera.orthographic      = true;
atlasCamera.orthographicSize  = rows * worldCellSize * 0.5f;
atlasCamera.aspect            = (float)cols / rows;
```

### 4.4 — Spawn brush prefabs in a world-space grid

```csharp
for (int i = 0; i < skinCount; i++)
{
    int col = i % cols;
    int row = i / cols;

    // Center grid on GridRoot origin.
    Vector3 pos = new Vector3(
        (col - (cols - 1) * 0.5f) * worldCellSize,
       -(row - (rows - 1) * 0.5f) * worldCellSize,
        0f);

    // Use menu-variant prefabs for correct selective coloring.
    GameObject brush = Instantiate(menuPrefab, gridRoot);
    brush.transform.localPosition = pos;

    SetLayerRecursive(brush, 31);        // atlas camera only
    brush.AddComponent<BrushRotation>(); // 90 deg/s auto-rotate

    // Color only the parts BrushMenu.m_BrushParts specifies (roller + grip, NOT handle).
    var renderers = CollectColorableRenderers(brush);
    ApplyBrushColor(brush, renderers, skinColor);
}
```

### 4.5 — UI cells (RawImage + uvRect)

Each cell is a `RawImage` that samples its sub-rect of the shared RT:

```csharp
float uvW = 1f / cols;   // e.g. 1/3
float uvH = 1f / rows;   // e.g. 1/4

for (int i = 0; i < skinCount; i++)
{
    int col = i % cols;
    int row = i / cols;

    // UV origin is bottom-left; grid row 0 is visual top.
    Rect uv = new Rect(col * uvW, 1f - (row + 1) * uvH, uvW, uvH);

    var rawImage = cell.GetComponent<RawImage>();
    rawImage.texture = rt;
    rawImage.uvRect  = uv;
}
```

### 4.6 — Lighting

Replicate the production main menu Spotlight so brushes look correct:

```csharp
RenderSettings.ambientMode  = AmbientMode.Flat;
RenderSettings.ambientLight = new Color(0.85f, 0.80f, 0.75f);  // warm gray

var light       = new GameObject("Light").AddComponent<Light>();
light.type      = LightType.Directional;
light.color     = new Color(1f, 1f, 0.835f);   // warm white
light.intensity = 1.1f;
light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
```

### 4.7 — Selective coloring (important!)

The production system does NOT color every renderer. Only specific parts get colored:

| Prefab type | Source list | What gets colored |
|-------------|-----------|-------------------|
| Menu (`BrushMenu`) | `BrushMenu.m_BrushParts` | Roller + grip only |
| Gameplay (`Brush`) | `Brush.m_Renderers` + `Brush.m_DarkRenderers` | Main body + darkened parts (×0.8) |

The handle (Manche) stays its original dark material. See `PrototypeBrushUtil.CollectColorableRenderers()` and `ApplyBrushColor()` for the working implementation.

### Why this approach

| Concern | Answer |
|---------|--------|
| Performance | 590 FPS idle, 394 FPS under selection stress (30 skins, desktop). Trivial cost for 12. |
| Draw calls | All RawImage cells share one texture → ~1 UI draw call for the entire grid |
| Scrolling | Standard ScrollRect + Mask — cells are normal UI elements, scrolling just works |
| Materials | Original brush shaders preserved — no clipping shaders, no material replacement |
| Memory | 1 RT (512×768 for 12 skins) ≈ 1.5 MB. Negligible. |
| Cleanup | Disable atlas camera in `OnDisable`, release RT in `OnDestroy` |

---

## Step 5: Grid layout with ScrollRect

First ScrollRect in this project. Layout:

```
ScrollRect (vertical)
  └── Viewport (Image + Mask)
       └── Content (GridLayoutGroup, 3 columns)
            ├── SkinCell_0  (RawImage sampling uvRect of shared RT)
            ├── SkinCell_1
            ├── ...
            └── SkinCell_11
```

### SkinCell.cs
`Assets/Scripts/UI/SkinCell.cs`

Each cell uses a `RawImage` (not `Image`) because it samples a sub-rect of the shared atlas RenderTexture.

```csharp
public class SkinCell : MonoBehaviour
{
    [SerializeField] RawImage m_RawImage;
    [SerializeField] Button m_Button;

    int m_SkinIndex;
    System.Action<int> m_OnClick;

    /// Called once after instantiation.
    /// _Atlas  — the shared RenderTexture all cells sample from.
    /// _UvRect — the sub-region of the atlas this cell should display.
    public void Setup(int index, Texture atlas, Rect uvRect, Action<int> onClick)
    {
        m_SkinIndex = index;
        m_OnClick = onClick;

        m_RawImage.texture = atlas;
        m_RawImage.uvRect  = uvRect;

        m_Button.onClick.RemoveAllListeners();
        m_Button.onClick.AddListener(() => m_OnClick?.Invoke(m_SkinIndex));
    }
}
```

Cells are instantiated dynamically — follows `LoadingView` pattern:
```csharp
float uvW = 1f / cols;
float uvH = 1f / rows;

for (int i = 0; i < skins.Length; i++)
{
    int col = i % cols;
    int row = i / cols;
    Rect uv = new Rect(col * uvW, 1f - (row + 1) * uvH, uvW, uvH);

    var obj = Instantiate(m_SkinCellPrefab, m_GridParent);
    var cell = obj.GetComponent<SkinCell>();
    cell.Setup(i, m_AtlasRT, uv, OnCellClicked);
    m_Cells.Add(cell);
}
```

Selection highlight can be a semi-transparent `Image` that reparents to the selected cell (same approach as prototype):
```csharp
void OnCellClicked(int index)
{
    m_SelectedSkin = index;
    m_SelectionHighlight.SetParent(m_Cells[index].transform, false);
    m_SelectionHighlight.anchorMin = Vector2.zero;
    m_SelectionHighlight.anchorMax = Vector2.one;
    m_SelectionHighlight.offsetMin = Vector2.zero;
    m_SelectionHighlight.offsetMax = Vector2.zero;
    // Update hero preview...
}
```

---

## Step 6: Hero preview (top of screen)

Reuse the **exact** BrushMainMenu/BrushRotation pattern:

- A world-space container (e.g. `SkinPreviewHero` GameObject at `(0, -30, 0)`)
- Child `BrushRotation` component for auto-rotation
- When selection changes: call same logic as `BrushMainMenu.Set(SkinData)` — destroy old, instantiate new, apply color
- Position the secondary camera to frame this object, or reuse the same camera trick the main menu uses

Alternatively, if the skin selection screen replaces the main menu visually, just reuse the existing `BrushMainMenu` and `BrushRotation` objects directly — update them when the user taps a cell.

---

## Step 7: MainMenuView integration

### Changes to MainMenuView.cs
- Hide the left/right arrow buttons and inline skin cycling (`ChangeBrush()` calls)
- Add a "Select Skin" button in their place
- On tap → `SkinSelectionScreen.Instance.Show()`
- Guard with feature flag:
  ```csharp
  bool skinScreenEnabled = PlayerPrefs.GetInt(Constants.c_DebugSkinSelectionSave, 1) == 1;
  selectSkinButton.SetActive(skinScreenEnabled);
  leftArrow.SetActive(!skinScreenEnabled);
  rightArrow.SetActive(!skinScreenEnabled);
  ```
- When SkinSelectionScreen closes with a new selection, update the main menu brush display via `BrushMainMenu.Set()`

### Persistence
Use existing `StatsService.FavoriteSkin` (int index into `GameService.m_Skins` array). No new save keys.

---

## Step 8: Polish (optional extras noted in test)

DOTween is available. Low-effort additions:
- Scale bounce on cell selection: `cell.transform.DOPunchScale(Vector3.one * 0.1f, 0.3f)`
- Hero brush swap with a quick scale-down/scale-up: `DOScale(0, 0.15f).OnComplete(() => { swap; DOScale(1, 0.15f); })`
- Particle burst behind hero on selection change (reuse existing particle prefabs)

---

## Corrections from original plan

| Original plan | Problem | Fix |
|--------------|---------|-----|
| RenderTexture per skin (128x128 × 12) | 12 separate RTs = 12 cameras or 12 Render calls | Single shared atlas RT + 1 ortho camera (prototyped, validated at 590+ FPS with 30 skins) |
| 2D sprite thumbnails | Static images, no live rotation, requires pre-baked assets | Atlas RT gives live rotating 3D for free — no thumbnail assets needed |
| Direct 3D in UI (no RT) | Scissor/clipping is fragile across render pipelines, broke on Deferred | Atlas RT approach avoids clipping entirely — UI Mask handles it via standard ScrollRect |
| `SkinPreviewItem.cs` with RT setup | Foreign pattern, one camera per skin | Single SkinCell with RawImage + uvRect sampling shared atlas |
| `View<SkinSelectionView>` | SettingsPanel (closest pattern) is NOT a View\<T>, uses Animator | Follow SettingsPanel: `SingletonMB<T>` + Animator |
| "SkinPreviewManager" managing 12 cameras | Overengineered | One atlas camera + one hero preview reusing BrushMainMenu pattern |
| "Only render visible cells" optimization | No ScrollRect exists in project to reference | Simple ScrollRect + GridLayoutGroup, all cells rendered (trivial count) |
| Save to `PlayerPrefs.SetInt("FavoriteSkin", index)` directly | Existing code uses `StatsService.FavoriteSkin` property | Use `StatsService.FavoriteSkin` |
| Menu brush prefabs not mentioned | `Prefabs/Brushs/MainMenuBrushes/Brush_X-Menu.prefab` exist for menu display | Use menu-variant prefabs — they have `BrushMenu.m_BrushParts` for correct selective coloring |
| Color all renderers | Handles (Manche) get colored, looks wrong | Use `BrushMenu.m_BrushParts` (menu prefabs) or `Brush.m_Renderers`+`m_DarkRenderers` (gameplay prefabs) |

---

## Prototype reference

The atlas RT approach has been prototyped and benchmarked:

| | |
|---|---|
| **Prototype scene** | `Assets/Prototype/06_Atlas/Prototype_06_Atlas.unity` |
| **Build it** | `Tools > Prototype > Build #06 Atlas Scene` |
| **Implementation guide** | `Assets/Prototype/06_Atlas/IMPLEMENTATION_GUIDE.md` |
| **Key runtime files** | `SkinAtlasRenderer.cs`, `AtlasCell.cs` |
| **Shared helpers** | `Shared/PrototypeBrushUtil.cs`, `Shared/PrototypeSkinSet.cs` |

Desktop benchmark results (30 skins, uncapped FPS):

| Phase | FPS | Frame avg |
|-------|-----|-----------|
| Idle | 607 | 1.65 ms |
| Scroll | 590 | 1.70 ms |
| Selection | 394 | 2.54 ms |
| Recolor | 549 | 1.82 ms |

---

## File Checklist

| File | Type | Location |
|------|------|----------|
| `SkinSelectionScreen.cs` | SingletonMB\<T> | `Scripts/UI/` |
| `SkinCell.cs` | MonoBehaviour (RawImage + Button) | `Scripts/UI/` |
| SkinSelectionScreen | Scene GameObject | Canvas hierarchy in Game.unity |
| SkinCell prefab | UI Prefab (RawImage + Button) | `Prefabs/UI/` |
| 6 new SkinData assets | ScriptableObject | `Resources/Skins/` |
| Animator controller | Animation | `Assets/Animations/` |
| MainMenuView.cs | Edit existing | `Scripts/UI/` |
| Hero preview container | Scene GameObject | World-space in Game.unity |
| Atlas camera | Scene GameObject (layer 31 only) | World-space in Game.unity |
| GridRoot | Scene GameObject (brush instances, layer 31) | World-space in Game.unity |
