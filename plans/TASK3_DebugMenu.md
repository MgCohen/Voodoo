# Task 3 — Debug Menu

## Goal
Menu accessible from Main Menu that toggles Features 1 (Booster mode) & 2 (Skin Selection screen) on/off, individually. All off → game behaves like the original unmodified project.

Duration target: **1h** per the spec. Assessment criteria: *clarity and ease of use*.

---

## State check (post Tasks 1 & 2)

### What exists
- `SettingsPanel` is the only similar UI artifact in the project. Plain `MonoBehaviour`, Animator-driven (`SetBool("Visible", …)`), Image button + sprite swap for the vibration toggle, writes `PlayerPrefs` directly via a private property.
- `MainMenuView` currently shows: Play button, Booster button + level label (Task 1), Skin-screen button (Task 2). The old `BrushSelect` group (3D brush hero + left/right arrows) is still in the prefab but `m_IsActive: 0`, and its `m_OnClick` bindings were just emptied in the cleanup commit.
- The original arrow-handler methods (`LeftButtonBrush`, `RightButtonBrush`, `ChangeBrush`) and fields (`m_BrushesPrefab`, `m_BrushGroundLight`, `m_IdSkin`) were removed from `MainMenuView` during Task 2 — no longer in code.

### Implication
When skin selection feature is OFF, the disabled state needs to behave **exactly like the original** — meaning the 3D brush hero shows again, arrows cycle skins, `SetTitleColor` updates the brush model. We have to restore the deleted code paths. The Booster feature OFF case is simpler — just hide the entry point; no Classic-mode behavior was changed.

---

## Decisions

### 1. Flag-reading pattern — **Direct PlayerPrefs**
Settled. Two keys, written by `DebugPanel`, read by `MainMenuView` on MAIN_MENU phase entry and after each debug toggle. Mirrors `c_VibrationSave`. No new abstractions.

Why not the other options:
- Static class wrapper: gain is nil for 2 booleans; introduces a new pattern not present elsewhere.
- `IDebugService` via Zenject: overkill — only one consumer.
- Singleton MonoBehaviour: requires scene wiring + Instance plumbing for no gain over PlayerPrefs.

### 2. DebugPanel shape — **Mirror SettingsPanel**
- Plain `MonoBehaviour` (no `View<T>`).
- Animator with `Visible` bool (reuse SettingsPanel's animation controller if structurally compatible, else clone).
- Two Image buttons, sprite swap on/off — reuse the vibration on/off sprites already in the project, or use generic toggle sprites.
- `ClickToggleDebugPanel()` opens/closes (scene-wired from MainMenu's debug button).
- `ClickBoosterToggle()` / `ClickSkinSelectionToggle()` flip the respective flag, save PlayerPrefs, swap sprite, and call `MainMenuView.Instance.RefreshFeatureVisibility()` immediately so the change is visible without closing the panel.

### 3. Defaults
Both features default **ON** (`PlayerPrefs.GetInt(key, 1)`). Reviewer's fresh install sees the candidate's full submission. They toggle OFF to verify the original state.

### 3b. No build-type gating
The spec explicitly asks for a Debug Menu "accessible via the Main Menu" and grades on "clarity and ease of use". Gating on `Debug.isDebugBuild` would force a Development Build APK for the menu to be reachable — a fragile foot-gun for no spec-driven gain. The menu is always accessible. In a real shipping context one would gate it; calling that out in the readme/video is sufficient for this test.

### 4. Refresh flow
- `MainMenuView.Awake()` calls `RefreshFeatureVisibility()` once.
- `DebugPanel` calls `MainMenuView.Instance.RefreshFeatureVisibility()` directly on each toggle. `MainMenuView` is `View<MainMenuView>` (i.e. `SingletonMB`), so `Instance` is available. No event bus needed.

---

## Implementation

### Step A — Constants (5 min)
Add to [Constants.cs](Draw.ioTest/Draw.ioTest/Assets/Scripts/Gameplay/Constants.cs):
```csharp
public const string c_DebugBoosterModeSave    = "DebugBoosterMode";
public const string c_DebugSkinSelectionSave  = "DebugSkinSelection";
```

### Step B — Restore original skin-cycle code in `MainMenuView` (15 min)
Re-add the deleted fields and methods so the feature-OFF state is faithful:

```csharp
public GameObject m_BrushesPrefab;   // 3D brush hero, lives inside BrushSelect group
public int m_IdSkin = 0;

protected override void Awake() {
    base.Awake();
    m_IdSkin = m_StatsService.FavoriteSkin;
    RefreshFeatureVisibility();
}

public void LeftButtonBrush()  { ChangeBrush(m_IdSkin - 1); }
public void RightButtonBrush() { ChangeBrush(m_IdSkin + 1); }

public void ChangeBrush(int _NewBrush) {
    int count = GameService.m_Skins.Count;
    if (count == 0) return;
    m_IdSkin = ((_NewBrush % count) + count) % count;
    GameService.m_PlayerSkinID = m_IdSkin;
    m_StatsService.FavoriteSkin = m_IdSkin;
    if (m_BrushesPrefab != null && m_BrushesPrefab.activeInHierarchy)
        m_BrushesPrefab.GetComponent<BrushMainMenu>().Set(GameService.m_Skins[m_IdSkin]);
    GameService.SetColor(GameService.ComputeCurrentPlayerColor(true, 0));
}
```

In `SetTitleColor`, restore the brush refresh — but gate it on `activeInHierarchy` so it's a no-op when the group is hidden:
```csharp
if (m_BrushesPrefab != null && m_BrushesPrefab.activeInHierarchy) {
    int favoriteSkin = Mathf.Min(m_StatsService.FavoriteSkin, GameService.m_Skins.Count - 1);
    m_BrushesPrefab.GetComponent<BrushMainMenu>().Set(GameService.m_Skins[favoriteSkin]);
}
```

### Step C — Add `RefreshFeatureVisibility()` to `MainMenuView` (10 min)
```csharp
[Header("Feature flag targets")]
public GameObject m_BoosterButton;          // booster entry point + level label container
public GameObject m_SkinScreenButton;       // new skin-screen button
public GameObject m_BrushSelectGroup;       // legacy arrows + 3D brush hero

public void RefreshFeatureVisibility() {
    bool boosterOn   = PlayerPrefs.GetInt(Constants.c_DebugBoosterModeSave, 1) == 1;
    bool skinScreenOn = PlayerPrefs.GetInt(Constants.c_DebugSkinSelectionSave, 1) == 1;

    if (m_BoosterButton != null)     m_BoosterButton.SetActive(boosterOn);
    if (m_SkinScreenButton != null)  m_SkinScreenButton.SetActive(skinScreenOn);
    if (m_BrushSelectGroup != null)  m_BrushSelectGroup.SetActive(!skinScreenOn);
}
```

### Step D — `DebugPanel.cs` (25 min)
[Scripts/UI/DebugPanel.cs](Draw.ioTest/Draw.ioTest/Assets/Scripts/UI/DebugPanel.cs):

```csharp
using UnityEngine;
using UnityEngine.UI;

public class DebugPanel : MonoBehaviour
{
    public Animator m_PanelAnim;
    public Image    m_BoosterButton;
    public Image    m_SkinSelectionButton;
    public Sprite   m_ToggleOnSprite;
    public Sprite   m_ToggleOffSprite;

    private bool m_PanelVisible;

    private bool BoosterEnabled
    {
        get { return PlayerPrefs.GetInt(Constants.c_DebugBoosterModeSave, 1) == 1; }
        set { PlayerPrefs.SetInt(Constants.c_DebugBoosterModeSave, value ? 1 : 0); }
    }

    private bool SkinSelectionEnabled
    {
        get { return PlayerPrefs.GetInt(Constants.c_DebugSkinSelectionSave, 1) == 1; }
        set { PlayerPrefs.SetInt(Constants.c_DebugSkinSelectionSave, value ? 1 : 0); }
    }

    private void Awake()
    {
        m_PanelVisible = false;
        m_PanelAnim.SetBool("Visible", m_PanelVisible);
        RefreshButtonsVisual();
    }

    public void ClickToggleDebugPanel()
    {
        m_PanelVisible = !m_PanelVisible;
        m_PanelAnim.SetBool("Visible", m_PanelVisible);
    }

    public void ClickBoosterToggle()
    {
        BoosterEnabled = !BoosterEnabled;
        RefreshButtonsVisual();
        if (MainMenuView.Instance != null)
            MainMenuView.Instance.RefreshFeatureVisibility();
    }

    public void ClickSkinSelectionToggle()
    {
        SkinSelectionEnabled = !SkinSelectionEnabled;
        RefreshButtonsVisual();
        if (MainMenuView.Instance != null)
            MainMenuView.Instance.RefreshFeatureVisibility();
    }

    private void RefreshButtonsVisual()
    {
        m_BoosterButton.sprite       = BoosterEnabled       ? m_ToggleOnSprite : m_ToggleOffSprite;
        m_SkinSelectionButton.sprite = SkinSelectionEnabled ? m_ToggleOnSprite : m_ToggleOffSprite;
    }
}
```

### Step E — Scene/prefab wiring

#### E.1 — Already done via YAML
- Re-wired RightArrow OnClick → `MainMenuView.RightButtonBrush`
- Re-wired LeftArrow OnClick → `MainMenuView.LeftButtonBrush`
- Assigned MainMenuView serialized fields: `m_BrushesPrefab` (1043197933159812), `m_BoosterButton` (3731833860446700079), `m_SkinScreenButton` (4677167943824637125), `m_BrushSelectGroup` (1054396209444862)

#### E.2 — DebugPanel prefab authored (DOTween-driven)
[Prefabs/UI/DebugPanel.prefab](Draw.ioTest/Draw.ioTest/Assets/Prefabs/UI/DebugPanel.prefab) is a self-contained UI panel. Open/close uses `DOTween` (CanvasGroup fade + inner-panel scale with `OutBack`/`InBack`) instead of an Animator — matches the project's animation house style (`SkinSelectionScreen`, `SkinCell`).

Structure:
- `DebugPanel` root — full-screen RectTransform, CanvasGroup, DebugPanel script
  - `Backdrop` — semi-transparent black full-screen Image, blocks raycasts
  - `Panel` — centered 700×800 dark-grey panel
    - `Title` — "DEBUG MENU"
    - `BoosterRow` — "Booster Mode" label + toggle Image (sprite swap)
    - `SkinRow` — "Skin Selection" label + toggle Image (sprite swap)
    - `CloseButton` — "CLOSE" button → `ClickToggleDebugPanel()`

Toggle sprites reuse the vibration on/off PNGs (`Picto_Vibreation` / `Picto_Vibreation_2`). Cosmetic only — swap for dedicated debug toggle art if desired.

#### E.3 — Debug entry lives inside SettingsPanel (new approach)
The debug entry point is now a second icon button **inside the existing SettingsPanel** (next to the vibration toggle). Settings/Visible.anim slide distance bumped from 122 → 250 so both icons are revealed when the panel slides down.

- `SettingsPanel.cs` gained `m_DebugPanel` (DebugPanel ref) + `ClickDebugButton()` method.
- New `DebugButton` GameObject inside SettingsPanel hierarchy (Cross.png sprite as placeholder) — OnClick already wired to `SettingsPanel.ClickDebugButton`.

#### E.4 — TODO in Unity Editor (~1 min)
1. **Drag `DebugPanel.prefab` into the scene's main Canvas** in `Game.unity` (sibling of `MainMenuView`). Panel is hidden by default (CanvasGroup alpha=0 + Panel scale=0 set in Awake).
2. **Wire the SettingsPanel reference**: select the SettingsPanel inside the MainMenuView scene instance, drag the scene's DebugPanel into the `m_Debug Panel` field.
3. Done. Click the existing gear → panel slides down showing vibration + debug icons → click debug → DebugPanel fades + scales in.

### Step F — Verify (15 min)
Test matrix:
| Booster | Skin | Expected |
|---------|------|----------|
| ON | ON | Candidate's submission (Booster button + new Skin button visible) |
| ON | OFF | Booster button visible. Old arrows + 3D brush hero shown. New Skin button hidden. |
| OFF | ON | No Booster button. New Skin button visible. |
| OFF | OFF | **Original game.** No Booster button. Old arrows + brush hero shown. No new Skin button. |

For each state, also: enter a Classic match, return to menu, re-open debug panel — flags persist via PlayerPrefs.

---

## File Checklist

| File | Type | Notes |
|------|------|-------|
| `Constants.cs` | Edit | Add 2 PlayerPrefs keys |
| `MainMenuView.cs` | Edit | Restore `m_BrushesPrefab` + skin-cycle methods; add `RefreshFeatureVisibility()`; add 3 GameObject refs |
| `DebugPanel.cs` | New | Mirror SettingsPanel shape |
| `Game.unity` | Edit | Add DebugPanel scene object + main-menu Debug button |
| `MainMenuView.prefab` | Edit | Re-wire LeftArrow/RightArrow OnClick; assign new serialized refs |
| Animator controller | New or reuse | Slide-in for DebugPanel |
| Toggle sprites | Reuse | Vibration on/off sprites already in project |

---

## What we are NOT changing

- `GameService` stays untouched. Booster entry is gated by hiding the UI button, not by an internal feature flag check. If the button is hidden, `StartBoosterMode()` is unreachable from the main menu. Defense-in-depth check inside `GameService` would just add coupling for no real safety gain.
- `SkinSelectionScreen` stays untouched. Same reasoning — phase entry is only reachable from the now-hidden `OpenSkinScreen` button.
- No event bus, no static flag class, no service. Two PlayerPrefs keys + a refresh call.

---

## Estimate
~75 min. Slightly over the spec's 1h budget — the over-run is entirely Step B (restoring deleted skin-cycle code), which is necessary because Task 2 removed it from `MainMenuView` rather than gating it.
