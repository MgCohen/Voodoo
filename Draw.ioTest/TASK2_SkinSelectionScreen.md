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
    [SerializeField] Transform m_GridParent;       // ScrollRect content
    [SerializeField] Transform m_HeroBrushParent;  // large preview container
    [SerializeField] GameObject m_SkinCellPrefab;

    IGameService m_GameService;
    IStatsService m_StatsService;

    bool m_Visible;
    int m_SelectedSkin;
    List<SkinCell> m_Cells = new List<SkinCell>();

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

### Approach: world-space 3D objects (matching existing pattern)

**Do NOT use RenderTextures.** Follow the BrushMainMenu approach:

1. Create an off-screen container at e.g. `(0, -50, 0)` — holds 12 brush instances in a grid layout
2. Each brush instance is a menu-variant prefab (`Brush_X-Menu.prefab`), colored per its SkinData
3. Each instance has a `BrushRotation`-style script for slow auto-rotation
4. A secondary orthographic camera renders ONLY these objects (separate culling layer, e.g. `SkinPreview`)
5. This camera outputs to a RenderTexture displayed in the ScrollRect

**Wait — the test says scrollable list.** With 12 items in a scroll view, world-space 3D objects don't scroll with UI naturally. Two viable approaches:

### Option A: Single RenderTexture camera (simpler, recommended)
- 12 brush models placed off-screen in a grid
- One orthographic camera with `SkinPreview` culling layer → one RenderTexture
- `RawImage` in UI shows the full grid
- Scroll the RawImage's `uvRect` to simulate scrolling, or just make the grid small enough to not need scrolling (3×4 fits)
- Selection via raycasting from UI click position → world position → which brush was hit

### Option B: Individual cells with 3D (complex)
- Each cell is a UI element with a transparent area
- Position 3D brush models to align with each cell's screen position
- Update positions when scrolling — this is fragile and messy

### Option C: Just use 2D thumbnails for the grid, 3D for hero only (pragmatic)
- Grid cells show pre-rendered 2D sprite thumbnails of each skin
- Only the hero preview at top is a live 3D rotating brush (following BrushMainMenu pattern exactly)
- Simplest to implement, most performant, scrolling works naturally
- **This is the recommended approach** — 3D in a scroll list is overengineered for 12 items

For Option C:
- Create 12 sprite thumbnails (screenshot each brush in editor, save as sprites)
- OR render them once at runtime into small RenderTextures on screen open, then display as RawImages

The hero preview reuses `BrushMainMenu.Set(SkinData)` directly — it's already built for this.

---

## Step 5: Grid layout with ScrollRect

First ScrollRect in this project. Layout:

```
ScrollRect (vertical)
  └── Content (GridLayoutGroup, 3 columns)
       ├── SkinCell_0
       ├── SkinCell_1
       ├── ...
       └── SkinCell_11
```

### SkinCell.cs
`Assets/Scripts/UI/SkinCell.cs`

```csharp
public class SkinCell : MonoBehaviour
{
    [SerializeField] Image m_Thumbnail;
    [SerializeField] Image m_SelectedBorder;
    [SerializeField] Button m_Button;

    int m_SkinIndex;
    System.Action<int> m_OnClick;

    public void Setup(int index, Sprite thumbnail, bool selected, Action<int> onClick)
    {
        m_SkinIndex = index;
        m_Thumbnail.sprite = thumbnail;
        m_SelectedBorder.gameObject.SetActive(selected);
        m_OnClick = onClick;
        m_Button.onClick.AddListener(() => m_OnClick?.Invoke(m_SkinIndex));
    }

    public void SetSelected(bool selected)
    {
        m_SelectedBorder.gameObject.SetActive(selected);
    }
}
```

Cells are instantiated dynamically — follows `LoadingView` pattern:
```csharp
for (int i = 0; i < skins.Length; i++)
{
    GameObject obj = Instantiate(m_SkinCellPrefab, Vector3.zero, Quaternion.identity);
    obj.transform.SetParent(m_GridParent, false);
    var cell = obj.GetComponent<SkinCell>();
    cell.Setup(i, thumbnails[i], i == m_SelectedSkin, OnCellClicked);
    m_Cells.Add(cell);
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
| RenderTexture per skin (128x128 × 12) | Project uses world-space 3D objects, no RenderTextures anywhere | Use 2D thumbnails in grid + 3D hero preview following BrushMainMenu pattern |
| `SkinPreviewItem.cs` with RT setup | Foreign pattern | Removed. Use SkinCell with sprite thumbnails |
| `View<SkinSelectionView>` | SettingsPanel (closest pattern) is NOT a View\<T>, uses Animator | Follow SettingsPanel: `SingletonMB<T>` + Animator |
| "SkinPreviewManager" managing 12 cameras | Overengineered | One hero preview reusing BrushMainMenu pattern |
| RenderTexture assets (12×128 + 1×256) | Unnecessary | Sprite thumbnails for grid cells |
| "Only render visible cells" optimization | No ScrollRect exists in project to reference | Simple ScrollRect + GridLayoutGroup, all 12 cells rendered (trivial count) |
| Save to `PlayerPrefs.SetInt("FavoriteSkin", index)` directly | Existing code uses `StatsService.FavoriteSkin` property | Use `StatsService.FavoriteSkin` |
| Menu brush prefabs not mentioned | `Prefabs/Brushs/MainMenuBrushes/Brush_X-Menu.prefab` exist for menu display | Use menu-variant prefabs for hero preview |

---

## File Checklist

| File | Type | Location |
|------|------|----------|
| `SkinSelectionScreen.cs` | SingletonMB\<T> | `Scripts/UI/` |
| `SkinCell.cs` | MonoBehaviour | `Scripts/UI/` |
| SkinSelectionScreen | Scene GameObject | Canvas hierarchy in Game.unity |
| SkinCell prefab | UI Prefab | `Prefabs/UI/` |
| 6 new SkinData assets | ScriptableObject | `Resources/Skins/` |
| 12 thumbnail sprites | Sprite | `Assets/Sprites/SkinThumbnails/` |
| Animator controller | Animation | `Assets/Animations/` |
| MainMenuView.cs | Edit existing | `Scripts/UI/` |
| Hero preview container | Scene GameObject | World-space in Game.unity |
