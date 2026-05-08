# Task 1 — Booster Game Mode

## Goal
Second game mode with its own progression, infinite levels, and 2 new boosters.

---

## Step 1: Constants & Save Keys

Add to `Assets/Scripts/Gameplay/Constants.cs` (where all save keys and cohorts live):

```csharp
// Booster Mode
public const string c_BoosterCurrentLevelSave = "BoosterCurrentLevel";
public const string c_BoosterHighestLevelSave = "BoosterHighestLevel";
```

Debug toggle keys (`c_DebugBoosterModeSave`, `c_DebugSkinSelectionSave`) belong to Task 3 — added there. Pattern for how flags are read is an open question (see `PATTERNS_FeatureFlags.md`).

Matches existing pattern: `c_LevelSave`, `c_BestScoreSave`, `c_PlayerXPSave`, etc.

---

## Step 2: Level Design System

### Context: the main game has NO level configuration

There is no per-match or per-level config in the existing project. Everything is hardcoded:

| What | Where | Current value | Configurable? |
|------|-------|---------------|---------------|
| Match duration | `Constants.c_MaxTime` | `130f` (const) | No |
| Player count | `Constants.s_PlayerCount` | `8` (static) | Mutable but never changed |
| Power-up spawn rate | `GameService.c_MinPowerUpRate` / `c_MaxPowerUpRate` | `1f` / `2.5f` (private const) | No |
| Brush spawn rate | `GameService.c_BrushRate` | `16f` (private const) | No |
| Power-up pool | `Resources.LoadAll<PowerUpData>("PowerUps")` | All assets in folder | No filtering |
| AI difficulty | `StatsService.GetLevel()` → `IAPlayer.m_Difficulty` | Rolling 5-game win avg (0-1) | Emergent, not configured |
| Terrain | `TerrainService.SetTerrain()` | Random from `Resources/Terrains/` | No per-level mapping |
| In-match growth | `Constants.c_GameplayRequiredPercentPerLevel` | `{2%, 5%, 10%, 15%, 20%, 25%, 25%}` (readonly) | No |

**The test asks us to build a level design system that doesn't exist yet.** We're extracting hardcoded values into configurable ScriptableObjects for the first time.

### BoosterLevelData.cs — goes in `Scripts/Gameplay/Data/`
Follows existing Data naming/location (`PowerUpData`, `SkinData`, `TerrainData`).

Each field maps to a currently-hardcoded value:

```csharp
[CreateAssetMenu(fileName = "BoosterLevelData", menuName = "Data/BoosterLevelData")]
public class BoosterLevelData : ScriptableObject
{
    public int m_LevelIndex;

    [Header("Power-ups")]
    public List<PowerUpData> m_AvailablePowerUps;   // currently: all from Resources/PowerUps/ (no filtering)
    public float m_MinPowerUpRate = 1f;              // currently: GameService.c_MinPowerUpRate (private const 1f)
    public float m_MaxPowerUpRate = 2.5f;            // currently: GameService.c_MaxPowerUpRate (private const 2.5f)
    public float m_BrushSpawnRate = 16f;             // currently: GameService.c_BrushRate (private const 16f)

    [Header("Match")]
    public float m_GameDuration = 130f;              // currently: Constants.c_MaxTime (const 130f)
    public int m_PlayerCount = 8;                    // currently: Constants.s_PlayerCount (static 8)

    [Header("AI")]
    public float m_AiDifficultyMin = 0f;             // currently: derived from StatsService.GetLevel()
    public float m_AiDifficultyMax = 1f;             // currently: always 1f (IAPlayer line 63)
}
```

Assets go in `Resources/BoosterLevels/` (new folder, follows `Resources/Terrains/`, `Resources/PowerUps/` pattern).

### BoosterModeConfig.cs — goes in `Scripts/Configs/`
Follows existing Config naming/location (`GameConfig`, `BattleRoyaleConfig`, `TerrainConfig`).

```csharp
[CreateAssetMenu(fileName = "BoosterModeConfig", menuName = "Config/BoosterModeConfig")]
public class BoosterModeConfig : ScriptableObject
{
    public List<BoosterLevelData> m_AuthoredLevels;
    public BoosterLevelData m_FallbackTemplate;
}
```

Method for infinite levels:
```csharp
public BoosterLevelData GetLevel(int index)
{
    if (index < m_AuthoredLevels.Count) return m_AuthoredLevels[index];
    return GenerateProceduralLevel(index);
}
```

Procedural: clone fallback, scale `m_AiDifficultyMin` and `m_MinPowerUpRate` by level index.

### What this gives us
A designer can create BoosterLevelData assets in the editor and configure each level independently — something impossible in the main game where everything is hardcoded. This is the "level design system" the test asks for.

---

## Step 3: Two New Boosters

Both in `Assets/Scripts/Gameplay/PowerUps/`. Follow existing subclass pattern exactly.

### PowerUp_SpeedBoost.cs

Pattern reference: `PowerUp_SizeUp.cs` (temporary buff with duration + revert).

```csharp
public class PowerUp_SpeedBoost : PowerUp
{
    [SerializeField] float m_SpeedMultiplier = 1.5f;

    // Use [Inject] ChildConstruct() if services needed (matches PowerUp_PaintBomb pattern)

    protected override void OnPlayerTouched(Player _Player)
    {
        base.OnPlayerTouched(_Player);
        _Player.AddSpeedBoost(m_SpeedMultiplier, m_Duration);
    }
}
```

Requires adding `AddSpeedBoost(float multiplier, float duration)` to `Player.cs`:
- Store original speed
- Multiply `m_CurrentSpeed` AND temporarily raise max speed cap
- Coroutine to revert after duration

### PowerUp_ColorBomb.cs

Pattern reference: `PowerUp_PaintBomb.cs` (already uses `ITerrainService.FillCircle()`).

```csharp
public class PowerUp_ColorBomb : PowerUp
{
    [SerializeField] float m_Radius = 8f;

    ITerrainService m_TerrainService;

    [Inject]
    public void ChildConstruct(ITerrainService terrainService)
    {
        m_TerrainService = terrainService;
    }

    protected override void OnPlayerTouched(Player _Player)
    {
        base.OnPlayerTouched(_Player);
        Vector3 randomPos = GetRandomTerrainPosition();
        m_TerrainService.FillCircle(randomPos, m_Radius, _Player.m_Color);
    }
}
```

Uses `[Inject] ChildConstruct()` — same pattern as `PowerUp_PaintBomb` and `PowerUp_DeadMan`.

### CRITICAL: Resource folder separation

`GameService.Init()` loads ALL PowerUpData from `Resources/PowerUps/`:
```csharp
m_PowerUps = new List<PowerUpData>(Resources.LoadAll<PowerUpData>("PowerUps"));
```

**Do NOT put new booster PowerUpData in `Resources/PowerUps/`.** They'd leak into classic mode.

Options (pick one):
- **A) Separate folder**: `Resources/BoosterPowerUps/` — loaded only by BoosterGameMode
- **B) Flag on PowerUpData**: Add `bool m_BoosterOnly` field to PowerUpData, filter in GameService

Option A is cleaner — no existing code changes, matches the folder-per-type pattern.

Prefabs for the PowerUp GameObjects still go in `Assets/Prefabs/` (or wherever existing PowerUp prefabs live).

---

## Step 4: Game Mode Abstraction

### IGameMode.cs — goes in `Scripts/Interfaces/`

```csharp
public interface IGameMode
{
    string ModeName { get; }
    List<PowerUpData> GetPowerUps();
    float GetGameDuration();
    float GetPowerUpSpawnRate();
    float GetBrushSpawnRate();
    int GetPlayerCount();
    float GetAiDifficultyMin();
    float GetAiDifficultyMax();
    void OnGameEnd(int playerRank);
}
```

Note: existing interfaces are all `I{Name}Service` for services. This is intentionally NOT a service — it's a strategy object swapped at runtime. Don't force it into the service pattern.

### ClassicGameMode.cs — `Scripts/Gameplay/`
Returns hardcoded values — preserves current behavior exactly:

| Method | Returns | Mirrors |
|--------|---------|---------|
| `GetPowerUps()` | `Resources.LoadAll<PowerUpData>("PowerUps")` | GameService.Init() line loading |
| `GetGameDuration()` | `130f` | `Constants.c_MaxTime` |
| `GetPowerUpSpawnRate()` | `Random.Range(1f, 2.5f)` | GameService `c_MinPowerUpRate`/`c_MaxPowerUpRate` |
| `GetBrushSpawnRate()` | `16f` | GameService `c_BrushRate` |
| `GetPlayerCount()` | `8` | `Constants.s_PlayerCount` |
| `GetAiDifficultyMin()` | `StatsService.GetLevel() / 2f` | IAPlayer.Start() line 63 |
| `GetAiDifficultyMax()` | `1f` | IAPlayer.Start() line 63 |
| `OnGameEnd()` | delegates to existing StatsService XP/ranking | GameService.ChangePhase(END) |

### BoosterGameMode.cs — `Scripts/Gameplay/`
Reads values from the current `BoosterLevelData`:

| Method | Returns | Source |
|--------|---------|--------|
| `GetPowerUps()` | level-specific list (all 5 boosters) | `BoosterLevelData.m_AvailablePowerUps` |
| `GetGameDuration()` | level-specific | `BoosterLevelData.m_GameDuration` |
| `GetPowerUpSpawnRate()` | `Random.Range(min, max)` per level | `BoosterLevelData.m_MinPowerUpRate/m_MaxPowerUpRate` |
| `GetBrushSpawnRate()` | level-specific | `BoosterLevelData.m_BrushSpawnRate` |
| `GetPlayerCount()` | level-specific | `BoosterLevelData.m_PlayerCount` |
| `GetAiDifficultyMin/Max()` | level-specific | `BoosterLevelData.m_AiDifficultyMin/Max` |
| `OnGameEnd()` | if won → increment level, save to PlayerPrefs | Own progression |

Constructor takes `BoosterModeConfig`. Tracks `m_CurrentLevel` via `PlayerPrefs.GetInt(Constants.c_BoosterCurrentLevelSave)`.

### GameService integration — what changes

Current hardcoded values in GameService that need to read from IGameMode instead:

```csharp
// GameService.cs — current hardcoded values to replace:

// Line ~22-24 (private consts):
private const float c_MinPowerUpRate = 1f;      // → m_CurrentGameMode.GetPowerUpSpawnRate()
private const float c_MaxPowerUpRate = 2.5f;    // → (absorbed into GetPowerUpSpawnRate)
private const float c_BrushRate = 16f;          // → m_CurrentGameMode.GetBrushSpawnRate()

// Init() — power-up loading:
m_PowerUps = new List<PowerUpData>(Resources.LoadAll<PowerUpData>("PowerUps"));
// → m_PowerUps = m_CurrentGameMode.GetPowerUps();

// OnUpdate() — spawn rate:
m_PowerUpRate = Random.Range(c_MinPowerUpRate, c_MaxPowerUpRate);
// → m_PowerUpRate = m_CurrentGameMode.GetPowerUpSpawnRate();

// ChangePhase(END) — progression:
// add: m_CurrentGameMode.OnGameEnd(playerRank);
```

**Timing issue:** Power-ups are loaded in `Init()` (called from `Construct()`), not `ChangePhase(LOADING)`. The game mode must be set BEFORE Init runs, or re-load the list when mode switches.

**IAPlayer integration:** `IAPlayer.Start()` currently reads `StatsService.GetLevel()` directly. In booster mode it should use `IGameMode.GetAiDifficultyMin/Max()`. This means IAPlayer needs access to the current game mode — either through GameService or injected directly.

---

## Step 5: Zenject Wiring

`BoosterModeConfig` needs to be available for injection. Add to `ManagersInstaller.cs`:

```csharp
[SerializeField] BoosterModeConfig m_BoosterModeConfig;

// In InstallGameManager:
subContainer.Bind<BoosterModeConfig>().FromInstance(m_BoosterModeConfig).AsSingle();
```

Follows existing pattern — every config is a serialized field on ManagersInstaller, injected via subcontainer.

---

## Step 6: UI

### MainMenuView
- Add "Booster Mode" button, guarded by debug flag:
  ```csharp
  boosterButton.SetActive(PlayerPrefs.GetInt(Constants.c_DebugBoosterModeSave, 1) == 1);
  ```
- On tap: set `GameService.m_CurrentGameMode = new BoosterGameMode(config)`, then `ChangePhase(LOADING)`
- Show "Level X" text on/near the button

### Gameplay HUD
- When `m_CurrentGameMode is BoosterGameMode`, show level indicator
- Small TextMeshPro overlay, top-left corner

---

## Corrections from original plan

| Original plan | Problem | Fix |
|--------------|---------|-----|
| `FeatureFlags.cs` static class | Foreign pattern — project uses Constants.cs for keys | Use `Constants.c_` keys + `PlayerPrefs` directly |
| `BoosterLevelData` in `Scripts/Configs/` | Wrong folder — it's data, not config | Move to `Scripts/Gameplay/Data/` |
| `BoosterLevelSequence` naming | Doesn't match `{Feature}Config` pattern | Rename to `BoosterModeConfig`, put in `Scripts/Configs/` |
| New PowerUpData in `Resources/PowerUps/` | `GameService.Init()` loads ALL from that folder | Use separate `Resources/BoosterPowerUps/` folder |
| Loose PlayerPrefs strings | All keys defined in `Constants.cs` | Add `c_Booster*Save` and `c_Debug*Save` to Constants |
| "In ChangePhase(LOADING)" load powerups | Actual loading happens in `Init()` | Set game mode before Init, or re-load list on mode switch |
| Constructor injection on PowerUps | PowerUps use `[Inject] ChildConstruct()` | Follow existing `PowerUp_PaintBomb` pattern |
| BoosterLevelData fields were vague | Didn't map to specific hardcoded values | Each field now traces to exact const/line it replaces |
| Assumed level config existed | Main game has ZERO per-match configuration | Documented that this is a new concept — we're extracting hardcoded values into configurable data for the first time |
| AI difficulty not addressed | IAPlayer reads StatsService.GetLevel() directly | IAPlayer needs access to IGameMode for booster-mode difficulty overrides |

---

## File Checklist

| File | Type | Location |
|------|------|----------|
| Constants.cs | Edit existing | `Scripts/Gameplay/` |
| BoosterLevelData.cs | ScriptableObject | `Scripts/Gameplay/Data/` |
| BoosterModeConfig.cs | ScriptableObject | `Scripts/Configs/` |
| IGameMode.cs | Interface | `Scripts/Interfaces/` |
| ClassicGameMode.cs | Class | `Scripts/Gameplay/` |
| BoosterGameMode.cs | Class | `Scripts/Gameplay/` |
| PowerUp_SpeedBoost.cs | MonoBehaviour | `Scripts/Gameplay/PowerUps/` |
| PowerUp_ColorBomb.cs | MonoBehaviour | `Scripts/Gameplay/PowerUps/` |
| GameService.cs | Edit existing | `Scripts/Services/` |
| ManagersInstaller.cs | Edit existing | `Scripts/Installers/` |
| Player.cs | Edit existing (AddSpeedBoost) | `Scripts/Gameplay/Players/` |
| PowerUpData assets (x2) | ScriptableObject | `Resources/BoosterPowerUps/` |
| BoosterLevelData assets | ScriptableObject | `Resources/BoosterLevels/` |
| BoosterModeConfig asset | ScriptableObject | assigned on ManagersInstaller |
