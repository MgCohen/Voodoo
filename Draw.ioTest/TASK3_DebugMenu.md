# Task 3 — Debug Menu

## Goal
Menu accessible from Main Menu to toggle Features 1 & 2 on/off. All off = original game state.

---

## Step 1: Understand existing patterns

### SettingsPanel (the closest reference)
- **NOT a View\<T>** — plain MonoBehaviour
- Opens/closes via **Animator** with `SetBool("Visible", value)`
- Vibration toggle is an **Image button with sprite swap**, NOT a Unity Toggle component
- Directly writes to PlayerPrefs, no event broadcast
- Accessed from MainMenuView via button

### Toggle pattern (vibration)
```csharp
// SettingsPanel.cs
public bool Vibration
{
    get { return PlayerPrefs.GetInt(Constants.c_VibrationSave, 1) == 1; }
    set {
        PlayerPrefs.SetInt(Constants.c_VibrationSave, value ? 1 : 0);
        m_VibrationButton.sprite = value ? m_VibrationOnSprite : m_VibrationOffSprite;
    }
}

public void ClickVibration() { Vibration = !Vibration; }
```

Button click → flip bool → swap sprite → save PlayerPrefs. No events. Follow this.

### Save keys
All defined in `Constants.cs`. `"FavoriteSkin"` in StatsService is the one exception (inconsistency in the codebase).

---

## Step 2: Add constants

Add to `Assets/Scripts/Gameplay/Constants.cs`:

```csharp
public const string c_DebugBoosterModeSave   = "DebugBoosterMode";
public const string c_DebugSkinSelectionSave  = "DebugSkinSelection";
```

---

## Step 3: DebugPanel

`Assets/Scripts/UI/DebugPanel.cs`

Follow SettingsPanel pattern: plain MonoBehaviour, Animator for show/hide, Image buttons with sprite swap.

Note: SettingsPanel is NOT a SingletonMB — it's a bare `MonoBehaviour`. But SettingsPanel is never accessed from code (100% scene-wired). Our DebugPanel might need to be accessed from code depending on which flag-reading pattern we pick (see `PATTERNS_FeatureFlags.md`). Decide during implementation.

```csharp
// Minimum shape — details TBD based on flag pattern decision
public class DebugPanel : MonoBehaviour
{
    [SerializeField] Animator m_Animator;
    [SerializeField] Image m_BoosterModeButton;
    [SerializeField] Image m_SkinSelectionButton;
    [SerializeField] Sprite m_ToggleOnSprite;
    [SerializeField] Sprite m_ToggleOffSprite;

    bool m_Visible;

    // Toggle panel open/close (scene-wired button)
    public void ClickTogglePanel()
    {
        m_Visible = !m_Visible;
        m_Animator.SetBool("Visible", m_Visible);
    }

    // Toggle feature flags (scene-wired buttons)
    public void ClickBoosterMode() { /* write toggle state — pattern TBD */ }
    public void ClickSkinSelection() { /* write toggle state — pattern TBD */ }
}
```

### Layout
```
┌─────────────────────────┐
│      Debug Menu         │
│                         │
│  [Image btn] Booster    │  ← sprite swaps on/off (like vibration)
│  [Image btn] Skin Sel.  │  ← sprite swaps on/off
│                         │
│      [ Close ]          │
└─────────────────────────┘
```

Animator controller with "Visible" bool parameter — slide in/out, matching SettingsPanel.

---

## Step 4: MainMenuView integration

### Reading flags
How MainMenuView reads the toggle state depends on the pattern chosen (see `PATTERNS_FeatureFlags.md`). Regardless of pattern, the effect is the same:

```csharp
void RefreshFeatureVisibility()
{
    bool boosterOn = /* read flag — pattern TBD */;
    bool skinScreenOn = /* read flag — pattern TBD */;

    m_BoosterModeButton.SetActive(boosterOn);

    m_SelectSkinButton.SetActive(skinScreenOn);
    m_LegacyArrowLeft.SetActive(!skinScreenOn);
    m_LegacyArrowRight.SetActive(!skinScreenOn);
}
```

Call `RefreshFeatureVisibility()`:
- In `OnGamePhaseChanged(MAIN_MENU)` (when returning to menu)
- When DebugPanel closes (add a callback or just refresh on MainMenuView becoming visible again)

### Debug button placement
Add a small button in a corner of MainMenuView. Scene-wired to DebugPanel's `ClickTogglePanel()` — same way the existing settings button is wired to SettingsPanel.

---

## Step 5: Verify "original state"

When both toggles are off:
- No "Booster Mode" button on main menu
- Original left/right arrow skin cycling is shown
- No booster-only power-ups spawn
- Game is identical to unmodified project

Test this by toggling both off and playing a full round.

---

## Open Decision: Flag-reading pattern

See `PATTERNS_FeatureFlags.md` for full analysis. Decision deferred until Features 1 & 2 are implemented — we'll see what the consumers look like and pick the pattern that causes least friction.

---

## Corrections from original plan

| Original plan | Problem | Fix |
|--------------|---------|-----|
| `FeatureFlags.cs` static class with events | Foreign pattern — no precedent in project. See `PATTERNS_FeatureFlags.md` | Deferred. Pattern decided after Features 1 & 2 are built |
| `View<DebugMenuView>` inheriting View\<T> | SettingsPanel (closest pattern) is NOT a View, uses Animator + MonoBehaviour | Follow SettingsPanel: MonoBehaviour + Animator |
| Unity `Toggle` components | No Toggle used anywhere in the project. Vibration uses Image + sprite swap | Use Image buttons with on/off sprites (matches vibration toggle) |
| `FeatureFlags.OnFlagsChanged` event | No event pattern for settings. SettingsPanel writes PlayerPrefs directly | Deferred — may or may not need events |
| Named "DebugMenuView" | Naming inconsistency with SettingsPanel | Renamed to `DebugPanel` (matches SettingsPanel naming) |
| `boosterToggle.onValueChanged.AddListener` | Toggle-based API doesn't exist here | Button click methods: `ClickBoosterMode()`, `ClickSkinSelection()` |

---

## File Checklist

| File | Type | Location |
|------|------|----------|
| Constants.cs | Edit existing | `Scripts/Gameplay/` |
| `DebugPanel.cs` | SingletonMB\<T> | `Scripts/UI/` |
| DebugPanel | Scene GameObject | Canvas hierarchy in Game.unity |
| Animator controller | Animation | `Assets/Animations/` |
| Toggle on/off sprites | Sprite | `Assets/Sprites/` or reuse existing |
| MainMenuView.cs | Edit existing | `Scripts/UI/` |

---

## Build Order (all 3 tasks)

1. **Constants.cs** — add save keys (5 min)
2. **DebugPanel** — UI + script + animator (45 min)
3. **MainMenuView** — debug button + `RefreshFeatureVisibility()` (30 min)
4. **PowerUp_SpeedBoost + PowerUp_ColorBomb** (45 min)
5. **BoosterLevelData + BoosterModeConfig** (45 min)
6. **IGameMode + ClassicGameMode + BoosterGameMode** (1h)
7. **GameService integration** for game modes (30 min)
8. **SkinData assets** — create 6 new ones (15 min)
9. **SkinSelectionScreen + SkinCell** (2h)
10. **MainMenuView** — skin button + feature flag wiring (30 min)
11. **Polish + device testing** (1h)
12. **Record video + build APK** (30 min)

Total: ~8 hours
