# Feature Flags — Open Question

## Problem
The debug menu (Task 3) needs to toggle Features 1 & 2 on/off. The project has no explicit feature flag system. We need to decide how the DebugPanel communicates toggle state to consumers.

---

## Existing Patterns Found

### Pattern A: Singleton + Static Gate (Vibration)
```
SettingsPanel.ClickVibrateButton()
  → sets MobileHapticManager.s_Vibrate (static bool)
  → saves PlayerPrefs("Vibration")

HumanPlayer calls m_HapticManager.Vibrate()
  → MobileHapticManager.Vibrate() checks s_Vibrate internally, early-returns if false

Consumers are oblivious to the flag. The service gates itself.
```
Used by: `SettingsPanel` → `MobileHapticManager`

### Pattern B: Zenject DI Services
```
ManagersInstaller binds IGameService, IStatsService, etc.
  → Services injected via [Inject] Construct()
  → Consumers call service methods directly

Services are singletons in Zenject sub-containers.
```
Used by: `GameService`, `StatsService`, `BattleRoyaleService`, `TerrainService`

### Pattern C: Scene-wired Singletons (no DI)
```
MonoBehaviour sits in scene
  → Found via FindObjectOfType / static Instance property
  → Consumers grab Instance directly: MessageView.Instance, BrushMainMenu, etc.
```
Used by: UI components, utilities, `MobileHapticManager`

### Pattern D: Direct PlayerPrefs
```
Writer: PlayerPrefs.SetInt(key, value)
Reader: PlayerPrefs.GetInt(key, default)

No middleman. Used for persistence but also as a simple key-value store.
```
Used by: `StatsService.FavoriteSkin`, all save keys in `Constants.cs`

---

## What Our Feature Flags Need

| Concern | Vibration (existing) | Our flags |
|---------|---------------------|-----------|
| Writers | SettingsPanel only | DebugPanel only |
| Readers | MobileHapticManager only (self-gating) | MainMenuView, GameService, possibly others |
| Reader count | 1 | Multiple |
| When read | Every vibrate call (runtime) | UI setup, game mode init (infrequent) |

Key difference: vibration has one consumer that gates itself. Our flags have multiple consumers across UI and game logic.

---

## Options

### 1. Direct PlayerPrefs reads
- DebugPanel writes PlayerPrefs
- MainMenuView, GameService read PlayerPrefs where needed
- No new class, no singleton
- Simplest. Matches Pattern D
- Downside: PlayerPrefs string keys scattered across files

### 2. Static class with properties
- `FeatureFlags.BoosterModeEnabled` wraps PlayerPrefs read
- DebugPanel sets via property, consumers read via property
- Centralizes the key strings
- New pattern — nothing like this exists in project

### 3. Singleton MonoBehaviour (mirror MobileHapticManager)
- DebugPanel sets static bool on a FeatureFlagManager
- Consumers check the manager
- Closest to Pattern A but stretched — multiple consumers instead of self-gating
- Requires scene GameObject

### 4. Zenject service
- `IDebugService` bound in ManagersInstaller
- DebugPanel and consumers both inject it
- Closest to Pattern B
- Heaviest. Overkill for two booleans?

---

## Decision
**Deferred.** Implement Features 1 & 2 first following the patterns that match each feature's needs. Then see which flag-reading pattern falls out naturally from how consumers are structured. Come back here and pick the option that causes the least friction.

Things to watch during implementation:
- Do the consumers already have Zenject injection? (→ favors option 4)
- Are the checks in MonoBehaviours with no DI? (→ favors option 1 or 3)
- How many files end up reading the flags? (→ if few, option 1 is fine)
- Does the check happen once at setup or repeatedly? (→ once = PlayerPrefs is fine)
