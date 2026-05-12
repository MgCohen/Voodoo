# Task 2 — Skin Selection Screen

## Goal
Dedicated skin selection screen with 12 scrollable 3D skins (2 models × 6 colors), replacing the inline left/right selector on the main menu.

## Guiding rules
- **Reuse, don't fork.** Each grid slot is a `BrushMainMenu` — the same component the main menu hero already uses. No new coloring code; we call the existing `Set(SkinData)`.
- **Selection logic is unchanged.** Same `StatsService.FavoriteSkin`, same `GameService.m_PlayerSkinID`, same `BrushMainMenu.Set()` on the hero. We're only changing the *picker UI*, not the picker semantics.
- **Atlas is decoupled.** `SkinAtlas` knows nothing about UI, services, or selection. It takes a list of skins, produces a `Texture` + `Rect GetUV(int)`. The screen consumes the texture.
- **Scales with skin count.** Cols are configured; rows, RT height, camera framing, world-grid positions, and UVs are all derived from `skins.Count`.
- **Animator overlay, not a phase.** Follow `SettingsPanel`, not `View<T>`.
- **No feature flag.** Replace the inline selector outright.

---

## Step 0 — Sprites

Provided sprites at `C:\Unity\Voodoo\Sprites-20260508T101751Z-3-001\Sprites\`. Import them under `Assets/Sprites/UI/SkinSelection/` (Texture Type: Sprite (2D and UI)). Mapping:

| Sprite | Slot in the screen |
|---|---|
| `backButton.png` + `backButton_shine.png` | Close button (shine layered on top via a child Image; optional idle pulse) |
| `case.png` | Cell background — rounded slot behind every brush |
| `baseSkin.png` | Neutral disc under the brush in each cell (and the hero preview default) |
| `baseSkin_colored.png` | Selected-state disc — swapped in on the picked cell, tinted with the skin's color |
| `iconSkin.png` | Icon on the new "Select Skin" button in `MainMenuView` |
| `light.png` | Soft glow behind the hero preview |

No other UI art is added — everything else is built from these.

---

## Step 1 — Asset creation (Unity Editor only, no code)

Current state (verified):
- `Resources/Skins/` — 6 SkinData (`Skin01.asset` … `Skin06.asset`).
- `Resources/Brushs/` — 4 BrushData (`Brush_1.asset` … `Brush_4.asset`); each points at a gameplay prefab via `BrushData.m_Prefab`.
- `Resources/Colors/` — 13 ColorData.

Add 6 more `SkinData` (`Skin07.asset` … `Skin12.asset`) under `Resources/Skins/`. Total = 12 = 2 brush models × 6 colors. Pick any 2 BrushData and any 6 ColorData from what exists.

`GameService.m_Skins` already loads from `Resources/Skins/` (verify before counting on it), so no installer change is needed.

---

## Step 2 — `SkinSelectionScreen` (new, ~one file)

`Assets/Scripts/UI/SkinSelectionScreen.cs` — plain `MonoBehaviour` + Animator. Mirrors [SettingsPanel.cs](Draw.ioTest/Draw.ioTest/Assets/Scripts/UI/SettingsPanel.cs).

Responsibilities:
1. `Show()` / `Hide()` — toggle `m_Animator.SetBool("Visible", ...)`.
2. On first `Show()`: build the atlas (RT + brushes + cells). Idempotent.
3. On cell click: update `m_SelectedSkin`, move the highlight, and call the existing `BrushMainMenu.Set(skin)` on the existing main-menu brush so the hero preview updates.
4. On confirm/close: write `m_StatsService.FavoriteSkin = m_SelectedSkin`, set `GameService.m_PlayerSkinID = m_SelectedSkin`, `Hide()`.

Sketch:
```csharp
public class SkinSelectionScreen : MonoBehaviour
{
    [SerializeField] Animator        m_Animator;
    [SerializeField] SkinAtlas       m_Atlas;
    [SerializeField] RectTransform   m_CellParent;
    [SerializeField] SkinCell        m_CellPrefab;
    [SerializeField] RectTransform   m_SelectionHighlight;
    [SerializeField] BrushMainMenu   m_Hero;             // reuse existing main-menu brush object

    IStatsService m_Stats;
    int m_SelectedSkin = -1;
    bool m_Built;
    readonly List<SkinCell> m_Cells = new();

    [Inject]
    public void Construct(IStatsService stats) { m_Stats = stats; }

    public void Show()
    {
        if (!m_Built) BuildCells();
        m_Atlas.SetActive(true);
        Select(m_Stats.FavoriteSkin);
        m_Animator.SetBool("Visible", true);
    }

    public void Hide()
    {
        // Same writes MainMenuView.ChangeBrush() does today.
        m_Stats.FavoriteSkin = m_SelectedSkin;
        GameService.m_PlayerSkinID = m_SelectedSkin;
        m_Atlas.SetActive(false);
        m_Animator.SetBool("Visible", false);
    }

    void BuildCells()
    {
        var skins = GameService.m_Skins;
        m_Atlas.Build(skins);
        for (int i = 0; i < skins.Count; i++)
        {
            var cell = Instantiate(m_CellPrefab, m_CellParent);
            cell.Setup(i, m_Atlas.Output, m_Atlas.GetUV(i), Select);
            m_Cells.Add(cell);
        }
        m_Built = true;
    }

    void Select(int index) { ... }   // updates highlight, cell disc, hero preview
}
```

The screen never touches a camera, RT, layer, or world transform — that's all inside `SkinAtlas`.

---

## Step 3 — `SkinAtlas` (the one new tech, isolated)

**Decoupled by design.** `SkinAtlas` is a `MonoBehaviour` that does one thing: lay out `N` brush slots in a world grid, render them into an offscreen RT, and expose UVs for the consumer. It does not know about UI, services, selection, or `SkinSelectionScreen`. The consumer hands it skins, gets back a texture + UVs.

`Assets/Scripts/UI/SkinAtlas.cs`:

```csharp
public class SkinAtlas : MonoBehaviour
{
    [SerializeField] Camera        m_Camera;          // ortho, this layer only
    [SerializeField] Transform     m_Root;            // world-space grid parent
    [SerializeField] BrushMainMenu m_SlotPrefab;      // SkinSlot prefab: BrushMainMenu + BrushRotation
    [SerializeField] int           m_Columns        = 3;
    [SerializeField] int           m_Layer          = 31;
    [SerializeField] int           m_RTWidth        = 512;
    [SerializeField] float         m_CellWorldSize  = 3f;

    public Texture Output => m_RT;
    public int     Cols   => m_Columns;
    public int     Rows   { get; private set; }

    RenderTexture m_RT;
    readonly List<BrushMainMenu> m_Slots = new();

    public void Build(IReadOnlyList<SkinData> skins)
    {
        Teardown();

        int count = skins.Count;
        Rows = Mathf.CeilToInt(count / (float)m_Columns);

        int h = Mathf.RoundToInt(m_RTWidth * ((float)Rows / m_Columns));
        m_RT = new RenderTexture(m_RTWidth, h, 16, RenderTextureFormat.ARGB32) { useMipMap = false };
        m_RT.Create();

        m_Camera.targetTexture    = m_RT;
        m_Camera.cullingMask      = 1 << m_Layer;
        m_Camera.clearFlags       = CameraClearFlags.SolidColor;
        m_Camera.backgroundColor  = new Color(0, 0, 0, 0);
        m_Camera.orthographic     = true;
        m_Camera.orthographicSize = Rows * m_CellWorldSize * 0.5f;
        m_Camera.aspect           = (float)m_Columns / Rows;

        for (int i = 0; i < count; i++)
        {
            int col = i % m_Columns;
            int row = i / m_Columns;

            var slot = Instantiate(m_SlotPrefab, m_Root);
            slot.transform.localPosition = new Vector3(
                (col - (m_Columns - 1) * 0.5f) * m_CellWorldSize,
               -(row - (Rows      - 1) * 0.5f) * m_CellWorldSize, 0f);
            SetLayerRecursive(slot.gameObject, m_Layer);

            slot.Set(skins[i]);                       // <-- existing tech, no new color path
            m_Slots.Add(slot);
        }
    }

    public Rect GetUV(int index)
    {
        float uvW = 1f / m_Columns;
        float uvH = 1f / Rows;
        int col = index % m_Columns;
        int row = index / m_Columns;
        return new Rect(col * uvW, 1f - (row + 1) * uvH, uvW, uvH);
    }

    public void SetActive(bool on) => m_Camera.enabled = on;

    public void Teardown()
    {
        foreach (var s in m_Slots) if (s != null) Destroy(s.gameObject);
        m_Slots.Clear();
        if (m_RT != null) { m_Camera.targetTexture = null; m_RT.Release(); Destroy(m_RT); m_RT = null; }
    }

    void OnDestroy() => Teardown();

    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
    }
}
```

### `SkinSlot.prefab` (new prefab, no new script)

Just two existing components on an empty GameObject:
- `BrushMainMenu` with `m_BrushParent` pointing at itself (or a child empty)
- `BrushRotation` (auto-rotates the parent)

That's it. When `SkinAtlas.Build()` calls `slot.Set(skinData)`, it runs the **exact same code path** that builds the main-menu hero today ([BrushMainMenu.cs:11](Draw.ioTest/Draw.ioTest/Assets/Scripts/UI/BrushMainMenu.cs:11)). If that code ever gains particles, shaders, attachments — we get them for free.

### Why this satisfies the four concerns

| Concern | How it's addressed |
|---|---|
| Scales with skin count | `Rows`, RT height, camera ortho size + aspect, world positions, and UVs are all functions of `skins.Count` and `m_Columns`. Pass 12 skins, you get a 3×4 grid; pass 30, you get 3×10; pass 7, you get 3×3 with one empty slot. |
| Decoupled | `SkinAtlas` API = `Build(skins)`, `Output`, `GetUV(i)`, `SetActive(on)`, `Teardown()`. No references to UI types, services, or selection. Reusable in any screen. |
| Uses existing coloring tech | Every grid slot is a `BrushMainMenu`; coloring goes through its existing `Set(SkinData)`. We don't touch renderers, materials, or color logic. |
| Selection tech unchanged | The atlas is a *display*. Selection still writes `StatsService.FavoriteSkin` and `GameService.m_PlayerSkinID` — same writes `MainMenuView.ChangeBrush()` does today. |

---

## Step 4 — `SkinCell` (new, tiny)

`Assets/Scripts/UI/SkinCell.cs` — `RawImage + Button`. Same shape as the `LoadingView` cell pattern.

```csharp
public class SkinCell : MonoBehaviour
{
    [SerializeField] RawImage m_Image;
    [SerializeField] Button   m_Button;

    int m_Index;

    public void Setup(int index, Texture atlas, Rect uv, System.Action<int> onClick)
    {
        m_Index = index;
        m_Image.texture = atlas;
        m_Image.uvRect  = uv;
        m_Button.onClick.RemoveAllListeners();
        m_Button.onClick.AddListener(() => onClick(m_Index));
    }
}
```

Prefab: `Prefabs/UI/SkinCell.prefab` — structure:
```
SkinCell (Button, Image=case.png)
├── Disc (Image=baseSkin.png, swapped to baseSkin_colored.png + tint on selection)
└── Preview (RawImage — atlas sub-rect)
```
The `Disc` Image is exposed on `SkinCell` so the screen can swap sprite + tint when selection changes. Highlight (border/glow) is a single shared `RectTransform` that reparents to the selected cell (see `OnCellClicked` below) — keeps the sprite count down.

```csharp
public class SkinCell : MonoBehaviour
{
    [SerializeField] RawImage m_Image;
    [SerializeField] Image    m_Disc;
    [SerializeField] Button   m_Button;
    [SerializeField] Sprite   m_DiscIdle;       // baseSkin
    [SerializeField] Sprite   m_DiscSelected;   // baseSkin_colored

    int m_Index;

    public void Setup(int index, Texture atlas, Rect uv, System.Action<int> onClick) { ... }

    public void SetSelected(bool selected, Color tint)
    {
        m_Disc.sprite = selected ? m_DiscSelected : m_DiscIdle;
        m_Disc.color  = selected ? tint : Color.white;
    }
}
```

```csharp
void OnCellClicked(int index)
{
    var prev = m_SelectedSkin;
    m_SelectedSkin = index;
    if (prev >= 0 && prev < m_Cells.Count)
        m_Cells[prev].SetSelected(false, Color.white);
    m_Cells[index].SetSelected(true, GameService.m_Skins[index].Color.m_Colors[0]);
    Highlight(index);
    m_Hero.Set(GameService.m_Skins[index]);
}

void Highlight(int index)
{
    m_SelectionHighlight.SetParent(m_Cells[index].transform, false);
    m_SelectionHighlight.anchorMin = Vector2.zero;
    m_SelectionHighlight.anchorMax = Vector2.one;
    m_SelectionHighlight.offsetMin = Vector2.zero;
    m_SelectionHighlight.offsetMax = Vector2.zero;
}
```

---

## Step 5 — Scene wiring (`Game.unity`)

One pass. Additions only — no edits to existing objects except the main camera's culling mask.

| New object | Notes |
|---|---|
| `SkinSelectionScreen` (Canvas child) | Animator + ScrollRect + Viewport + Content (GridLayoutGroup, 3 cols) + close button (`backButton.png` + `backButton_shine.png` child) |
| `SkinSelectionScreen/Hero` panel | `light.png` glow + the existing `BrushMainMenu` re-parented or duplicated here |
| `SkinAtlas` (sibling GameObject) | Holds the `SkinAtlas` component + assigned `m_Camera`, `m_Root`, `m_SlotPrefab` |
| `SkinAtlas/Camera` | Orthographic, `cullingMask = 1<<31`, disabled by default |
| `SkinAtlas/Root` | World-space empty, positioned offscreen (e.g. y = -100), layer 31 |
| `SkinCell.prefab` under `Prefabs/UI/` | RawImage + Button |
| `SkinSelectionHighlight` (Canvas child) | Single Image used as the selection ring |
| Animator controller | Two states (Hidden/Visible), bool `Visible` — copy from SettingsPanel's controller |

**Main camera culling mask:** strip layer 31. Audit layer 31 first — if it's in use, claim a different free layer and update `c_Layer`.

**Lighting:** the atlas camera needs its own light if the existing scene light isn't on layer 31's lighting path. Match the menu spot/directional from the guide; either bake it into `GridRoot` as a child light or add a directional light to the GridRoot prefab.

---

## Step 6 — `MainMenuView` integration (smallest possible edit)

Edit [MainMenuView.cs](Draw.ioTest/Draw.ioTest/Assets/Scripts/UI/MainMenuView.cs):
- Remove the left/right arrow buttons from the scene (or hide them via inspector — they call `LeftButtonBrush()` / `RightButtonBrush()`).
- Add one new button "Select Skin" (icon = `iconSkin.png`) that calls `m_SkinScreen.Show()`.
- Add `[SerializeField] SkinSelectionScreen m_SkinScreen;` to `MainMenuView`.
- After `Hide()` returns control, the next `SetTitleColor()` / `MAIN_MENU` phase already re-applies `BrushMainMenu.Set(...)` using `m_StatsService.FavoriteSkin`, so no extra callback needed.

`ChangeBrush()`, `LeftButtonBrush()`, `RightButtonBrush()` can stay as dead code (we don't delete unused code unless asked) — they just won't be called.

---

## Step 7 — Cleanup

`SkinAtlas.OnDestroy()` handles its own teardown (slots + RT). `Show()`/`Hide()` toggle `m_Atlas.SetActive(bool)` so we don't render the atlas while the panel is closed.

---

## DI

Existing `ManagersInstaller` already binds `IGameService` and `IStatsService` ([ManagersInstaller.cs](Draw.ioTest/Draw.ioTest/Assets/Scripts/Installers/ManagersInstaller.cs)). Scene `SceneContext` auto-injects MonoBehaviours that live in the scene, so the `[Inject] Construct(...)` on `SkinSelectionScreen` works with no installer changes.

---

## File checklist

| File | Action | Location |
|---|---|---|
| `SkinAtlas.cs` | new (decoupled, no UI/service refs) | `Assets/Scripts/UI/` |
| `SkinSelectionScreen.cs` | new | `Assets/Scripts/UI/` |
| `SkinCell.cs` | new | `Assets/Scripts/UI/` |
| `SkinSlot.prefab` (BrushMainMenu + BrushRotation, no new script) | new (editor) | `Assets/Prefabs/UI/` |
| `Skin07.asset` … `Skin12.asset` | new (editor) | `Assets/Resources/Skins/` |
| `SkinCell.prefab` | new (editor) | `Assets/Prefabs/UI/` |
| 7 sprite imports | new (editor) | `Assets/Sprites/UI/SkinSelection/` |
| Animator controller for the panel | new (editor) | `Assets/Animations/` |
| `Game.unity` | edit: add panel + atlas camera + GridRoot, strip layer 31 from main camera, replace arrows with Select Skin button | `Assets/Scenes/` |
| `MainMenuView.cs` | edit: add `m_SkinScreen` field + open button handler | `Assets/Scripts/UI/` |

No new constants, no new installers, no new prefab variants, no new coloring path.

---

## Things explicitly NOT in scope

- Feature flag (`c_DebugSkinSelectionSave`) — dropped. Replace the inline selector outright.
- Menu-variant brush prefabs (`Brush_X-Menu.prefab`) — they're not referenced by `BrushData` and we leave them untouched.
- A second `BrushMainMenu` for the hero preview — we reuse the existing one.
- `View<SkinSelectionScreen>` — wrong base; this is a panel, not a phase view.
- Per-cell `RenderTexture`s, scissor shaders, sprite thumbnails — atlas RT only.
- "Only render visible cells" optimization — 12 cells, trivial.
