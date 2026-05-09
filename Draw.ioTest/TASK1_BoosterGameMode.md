# Task 1 — Booster Game Mode

## Goal
Second game mode with its own progression, infinite levels, and 2 new boosters.

---

## Architecture Decisions

### What we learned from auditing the codebase

1. **There is no game mode concept.** All match parameters are hardcoded consts on GameService (`c_MinPowerUpRate`, `c_MaxPowerUpRate`, `c_BrushRate`) and Constants (`s_PlayerCount`, `c_MaxTime`). Power-ups are loaded via `Resources.LoadAll<PowerUpData>("PowerUps")` with no filtering.

2. **BattleRoyaleService is not a "mode" — it's a rule layer.** It handles the elimination timer, crown/skull visuals, save mechanic, and win detection. It runs on top of GameService via events. Both classic and booster modes use it identically.

3. **The only behavioral difference between modes is end-game progression.** Classic does `StatsService.AddGameResult()`, `GainXP()`, `TryToSetBestScore()`. Booster increments a level counter. The actual game loop (painting, spawning, eliminating) is identical.

4. **Everything else that differs is just data** — which power-ups, spawn rates, AI difficulty range, player count.

5. **The event system already provides lifecycle hooks.** `onGamePhaseChanged`, `onEndGame`, `onScoresCalculated`, `onElimination` — all exist and are used by services and UI. A game mode can subscribe to these.

### Design: IGameMode = data + hooks

IGameMode is a plain object (not a Zenject service). It provides:
- **Data properties** — GameService reads these instead of its hardcoded consts
- **Lifecycle hooks** — called by GameService at key moments, the mode can react or do nothing

GameService holds `IGameMode m_CurrentGameMode`, defaults to `ClassicGameMode` in Init(). Classic mode returns the exact same values as today's consts — zero behavior change. Booster mode reads from `BoosterLevelData`.

BattleRoyaleService is **untouched**. It keeps driving eliminations for both modes.

---

## Phased Implementation

### Phase 1 — Decouple: Extract IGameMode from hardcoded values
**Goal:** Classic mode works exactly as before, but through the IGameMode abstraction. No new content yet.

### Phase 2 — New game mode: BoosterGameMode + data
**Goal:** Booster mode exists, selectable from main menu, with its own progression and level data. Uses existing classic power-ups initially.

### Phase 3 — New content: SpeedBoost + ColorBomb power-ups
**Goal:** Two new power-ups for booster mode. Booster levels can configure them.

---

## Phase 1 — Decouple

Extract the hardcoded values from GameService into an IGameMode interface. After this phase, the game runs identically but GameService reads from `m_CurrentGameMode` instead of consts.

### Step 1.1: IGameMode interface

New file: `Scripts/Interfaces/IGameMode.cs`

```csharp
public interface IGameMode
{
    List<PowerUpData> PowerUps { get; }
    float PowerUpSpawnRate { get; }
    float BrushSpawnRate { get; }
    int PlayerCount { get; }
    float AiDifficultyMin { get; }
    float AiDifficultyMax { get; }

    void OnGameEnd(int playerRank, int playerScore);
}
```

Each property maps to a hardcoded value:

| Property | Replaces | Current value |
|----------|----------|---------------|
| `PowerUps` | `Resources.LoadAll<PowerUpData>("PowerUps")` in `Init()` line 133 |  All assets in folder |
| `PowerUpSpawnRate` | `Random.Range(c_MinPowerUpRate, c_MaxPowerUpRate)` in `OnUpdate()` line 371 | 1–2.5s |
| `BrushSpawnRate` | `c_BrushRate` in `OnUpdate()` line 378 | 16s |
| `PlayerCount` | `Constants.s_PlayerCount` in `PopPlayers()` line 273 | 8 |
| `AiDifficultyMin` | `Mathf.Clamp01(StatsService.GetLevel() / 2f)` in `IAPlayer.Start()` line 63 | 0–0.5 |
| `AiDifficultyMax` | Hardcoded `1f` in `IAPlayer.Start()` line 63 | 1 |
| `OnGameEnd()` | Inline StatsService calls in `ChangePhase(END)` lines 226–243 | XP, difficulty, best score |

### Step 1.2: ClassicGameMode

New file: `Scripts/Gameplay/ClassicGameMode.cs`

Returns the exact same values as today's hardcoded consts. `OnGameEnd` contains the progression logic currently inline in `GameService.ChangePhase(END)`.

```csharp
public class ClassicGameMode : IGameMode
{
    private IStatsService m_StatsService;
    private List<PowerUpData> m_PowerUps;
    private List<int> m_XPByRank;

    public ClassicGameMode(IStatsService _StatsService, List<int> _XPByRank)
    {
        m_StatsService = _StatsService;
        m_XPByRank = _XPByRank;
        m_PowerUps = new List<PowerUpData>(Resources.LoadAll<PowerUpData>("PowerUps"));
    }

    public List<PowerUpData> PowerUps => m_PowerUps;
    public float PowerUpSpawnRate => Random.Range(1f, 2.5f);
    public float BrushSpawnRate => 16f;
    public int PlayerCount => 8;
    public float AiDifficultyMin => Mathf.Clamp01(m_StatsService.GetLevel() / 2f);
    public float AiDifficultyMax => 1f;

    public void OnGameEnd(int _PlayerRank, int _PlayerScore)
    {
        m_StatsService.TryToSetBestScore(_PlayerScore);

        int rankingScore = -1;
        if (_PlayerRank == 0)
            rankingScore = 1;
        else if (_PlayerRank >= 2)
            rankingScore = 0;

        m_StatsService.AddGameResult(rankingScore);
        m_StatsService.SetLastXP(m_XPByRank[_PlayerRank]);
        m_StatsService.GainXP();
    }
}
```

### Step 1.3: GameService changes

Minimal edits to `GameService.cs`:

**Add field:**
```csharp
public IGameMode m_CurrentGameMode;
```

**In Init() — create default mode, load power-ups from it:**
```csharp
m_CurrentGameMode = new ClassicGameMode(m_StatsService, m_XPByRank);
m_PowerUps = m_CurrentGameMode.PowerUps;
```
Replaces: `m_PowerUps = new List<PowerUpData>(Resources.LoadAll<PowerUpData>("PowerUps"));`

**In ChangePhase(LOADING) — read spawn rate from mode:**
```csharp
m_PowerUpRate = m_CurrentGameMode.PowerUpSpawnRate;
```
Replaces: `m_PowerUpRate = Random.Range(c_MinPowerUpRate, c_MaxPowerUpRate);`

**In OnUpdate() — spawn rates from mode:**
```csharp
m_PowerUpRate = m_CurrentGameMode.PowerUpSpawnRate;
// ...
if (Time.time - m_LastBrushTime > m_CurrentGameMode.BrushSpawnRate)
```
Replaces: `Random.Range(c_MinPowerUpRate, c_MaxPowerUpRate)` and `c_BrushRate`

**In ChangePhase(END) — delegate progression:**
```csharp
case GamePhase.END:
    int playerScore = Mathf.RoundToInt(m_Players[0].percent * 100.0f);
    int playerRank = m_BattleRoyaleService.GetHumanPlayer().m_Rank;
    m_CurrentGameMode.OnGameEnd(playerRank, playerScore);
    PreEndView.Instance.LaunchPreEnd();
    break;
```
Replaces: all the inline StatsService calls currently there.

**In PopPlayers() — player count from mode:**
```csharp
m_Players = new List<Player>(m_CurrentGameMode.PlayerCount);
for (int i = 0; i < m_CurrentGameMode.PlayerCount; ++i)
```
Replaces: `Constants.s_PlayerCount`

**Consts `c_MinPowerUpRate`, `c_MaxPowerUpRate`, `c_BrushRate` become unused.** Can delete or leave — ClassicGameMode returns their values.

### Step 1.4: Verify

After this phase: run the game, play classic mode. Everything should behave identically. GameService reads from ClassicGameMode which returns the same hardcoded values. No new UI, no new content.

**Files touched:**

| File | Action |
|------|--------|
| `IGameMode.cs` | New — `Scripts/Interfaces/` |
| `ClassicGameMode.cs` | New — `Scripts/Gameplay/` |
| `GameService.cs` | Edit — add m_CurrentGameMode, delegate reads + OnGameEnd |

---

## Phase 2 — Booster Game Mode

With the IGameMode abstraction in place, add the booster mode with its own data and progression.

### Step 2.1: Constants save keys

Add to `Constants.cs`:
```csharp
public const string c_BoosterCurrentLevelSave = "BoosterCurrentLevel";
public const string c_BoosterHighestLevelSave = "BoosterHighestLevel";
```
Matches existing pattern: `c_LevelSave`, `c_BestScoreSave`, etc.

### Step 2.2: BoosterLevelData

New file: `Scripts/Gameplay/Data/BoosterLevelData.cs`
Follows existing Data location/naming (`PowerUpData`, `SkinData`, `TerrainData`).

```csharp
[CreateAssetMenu(fileName = "BoosterLevelData", menuName = "Data/BoosterLevelData")]
public class BoosterLevelData : ScriptableObject
{
    public int m_LevelIndex;

    [Header("Power-ups")]
    public List<PowerUpData> m_AvailablePowerUps;
    public float m_MinPowerUpRate = 1f;
    public float m_MaxPowerUpRate = 2.5f;
    public float m_BrushSpawnRate = 16f;

    [Header("Match")]
    public int m_PlayerCount = 8;

    [Header("AI")]
    public float m_AiDifficultyMin = 0f;
    public float m_AiDifficultyMax = 1f;
}
```

Assets go in `Resources/BoosterLevels/`.

### Step 2.3: BoosterModeConfig

New file: `Scripts/Configs/BoosterModeConfig.cs`
Follows existing Config location/naming (`GameConfig`, `BattleRoyaleConfig`).

```csharp
[CreateAssetMenu(fileName = "BoosterModeConfig", menuName = "Config/BoosterModeConfig")]
public class BoosterModeConfig : ScriptableObject
{
    public List<BoosterLevelData> m_AuthoredLevels;
    public BoosterLevelData m_FallbackTemplate;

    public BoosterLevelData GetLevel(int _Index)
    {
        if (_Index < m_AuthoredLevels.Count)
            return m_AuthoredLevels[_Index];
        return m_FallbackTemplate;
    }
}
```

### Step 2.4: BoosterGameMode

New file: `Scripts/Gameplay/BoosterGameMode.cs`

```csharp
public class BoosterGameMode : IGameMode
{
    private BoosterModeConfig m_Config;
    private BoosterLevelData m_CurrentLevel;

    public BoosterGameMode(BoosterModeConfig _Config)
    {
        m_Config = _Config;
        int levelIndex = PlayerPrefs.GetInt(Constants.c_BoosterCurrentLevelSave, 0);
        m_CurrentLevel = m_Config.GetLevel(levelIndex);
    }

    public List<PowerUpData> PowerUps => m_CurrentLevel.m_AvailablePowerUps;
    public float PowerUpSpawnRate => Random.Range(m_CurrentLevel.m_MinPowerUpRate, m_CurrentLevel.m_MaxPowerUpRate);
    public float BrushSpawnRate => m_CurrentLevel.m_BrushSpawnRate;
    public int PlayerCount => m_CurrentLevel.m_PlayerCount;
    public float AiDifficultyMin => m_CurrentLevel.m_AiDifficultyMin;
    public float AiDifficultyMax => m_CurrentLevel.m_AiDifficultyMax;

    public void OnGameEnd(int _PlayerRank, int _PlayerScore)
    {
        if (_PlayerRank == 0)
        {
            int level = PlayerPrefs.GetInt(Constants.c_BoosterCurrentLevelSave, 0) + 1;
            PlayerPrefs.SetInt(Constants.c_BoosterCurrentLevelSave, level);
        }
    }
}
```

### Step 2.5: Zenject wiring

Edit `ManagersInstaller.cs` — add BoosterModeConfig to GameService's sub-container:

```csharp
[SerializeField] private BoosterModeConfig m_BoosterModeConfig;

private void InstallGameManager(DiContainer subContainer)
{
    subContainer.Bind<GameService>().AsSingle();
    subContainer.Bind<GameConfig>().FromInstance(m_GameConfig).AsSingle();
    subContainer.Bind<BoosterModeConfig>().FromInstance(m_BoosterModeConfig).AsSingle();
}
```

GameService.Construct receives it:
```csharp
[Inject]
public void Construct(GameConfig gameConfig, BoosterModeConfig boosterModeConfig, ...)
```

### Step 2.6: MainMenuView — mode selection

The game starts via `MainMenuView.OnPlayButton()` → `GameService.ChangePhase(LOADING)`. The default mode is ClassicGameMode, set in `GameService.Init()`. No change needed for classic play.

Add a booster button that swaps the mode before starting:
```csharp
public void OnBoosterButton()
{
    if (GameService.currentPhase == GamePhase.MAIN_MENU)
    {
        GameService.SetGameMode(new BoosterGameMode(boosterModeConfig));
        GameService.ChangePhase(GamePhase.LOADING);
    }
}
```

GameService resets to ClassicGameMode on scene reload / `ChangePhase(MAIN_MENU)` so the default is always classic.

### Step 2.7: Reload power-ups on mode switch

Power-ups are loaded in `Init()` which runs once at construction. When the mode switches before `ChangePhase(LOADING)`, GameService needs to reload:

```csharp
public void SetGameMode(IGameMode _GameMode)
{
    m_CurrentGameMode = _GameMode;
    m_PowerUps = m_CurrentGameMode.PowerUps;
}
```

### Step 2.8: Create initial assets

- 3–5 `BoosterLevelData` assets in `Resources/BoosterLevels/` with escalating difficulty
- 1 `BoosterModeConfig` asset assigned on `ManagersInstaller` prefab
- Initial booster levels can use the same classic power-ups (`SizeUp`, `PaintBomb`) — new power-ups come in Phase 3

### Step 2.9: Verify

Play classic mode — still works identically. Tap booster button — plays a match using BoosterLevelData values. Win → level increments. Reload → next level's data is used.

**Files touched:**

| File | Action |
|------|--------|
| `Constants.cs` | Edit — 2 save keys |
| `BoosterLevelData.cs` | New — `Scripts/Gameplay/Data/` |
| `BoosterModeConfig.cs` | New — `Scripts/Configs/` |
| `BoosterGameMode.cs` | New — `Scripts/Gameplay/` |
| `ManagersInstaller.cs` | Edit — add BoosterModeConfig field + binding |
| `GameService.cs` | Edit — add SetGameMode(), inject BoosterModeConfig |
| `MainMenuView.cs` | Edit — add booster button |

---

## Phase 3 — New Power-Ups

With both modes working, add the two new power-ups for booster mode.

### Step 3.1: Player.cs — AddSpeedBoost

Add speed boost support following the existing `AddSizeUp` pattern:

```csharp
private float m_SpeedFactor = 1f;
private Coroutine m_SpeedPowerUpCoroutine;

public virtual void AddSpeedBoost(float _Factor, float _Duration)
{
    m_SpeedFactor = _Factor;
    if (m_SpeedPowerUpCoroutine != null)
        StopCoroutine(m_SpeedPowerUpCoroutine);
    m_SpeedPowerUpCoroutine = StartCoroutine(BonusCoroutine(EBonus.SPEED_UP, _Duration));
}
```

Add `SPEED_UP` case to `BonusCoroutine` (the enum value already exists but is unimplemented):
```csharp
case EBonus.SPEED_UP:
    m_SpeedFactor = 1f;
    break;
```

Update `GetSpeed()` to use the factor:
```csharp
protected float GetSpeed()
{
    return Mathf.Clamp(m_Speed * m_SpeedFactor, c_MinSpeed, c_MaxSpeed);
}
```

### Step 3.2: PowerUp_SpeedBoost

New file: `Scripts/Gameplay/PowerUps/PowerUp_SpeedBoost.cs`
Follows `PowerUp_SizeUp` pattern — call base, invoke player method:

```csharp
public class PowerUp_SpeedBoost : PowerUp
{
    public float m_SpeedMultiplier = 1.5f;

    public override void OnPlayerTouched(Player _Player)
    {
        base.OnPlayerTouched(_Player);
        _Player.AddSpeedBoost(m_SpeedMultiplier, m_Duration);
    }
}
```

### Step 3.3: PowerUp_ColorBomb

New file: `Scripts/Gameplay/PowerUps/PowerUp_ColorBomb.cs`
Follows `PowerUp_PaintBomb` pattern — does NOT call base (deferred destruction via FillCircle callback), uses `[Inject] ChildConstruct` for ITerrainService:

```csharp
public class PowerUp_ColorBomb : PowerUp
{
    public float m_Radius = 8f;
    public float m_FillDuration = 0.3f;

    private ITerrainService m_TerrainService;

    [Inject]
    public void ChildConstruct(ITerrainService _TerrainService)
    {
        m_TerrainService = _TerrainService;
    }

    public override void OnPlayerTouched(Player _Player)
    {
        // Manual cleanup — same as PowerUp_PaintBomb (no base call)
        UnregisterMap();
        m_Model.enabled = false;
        m_ParticleSystem.Play(true);
        m_IdleParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        m_Shadow.SetActive(false);

        Vector3 randomPos = GetRandomTerrainPosition();
        m_TerrainService.FillCircle(_Player, randomPos, m_Radius, m_FillDuration, SelfDestroy);
    }

    private Vector3 GetRandomTerrainPosition()
    {
        // Same random position logic as GameService.PopObjectRandomly
        float halfW = m_TerrainService.WorldHalfWidth;
        float halfH = m_TerrainService.WorldHalfHeight;
        float padding = 13f;
        return new Vector3(
            Random.Range(-halfW + padding, halfW - padding),
            0f,
            Random.Range(-halfH + padding, halfH - padding)
        );
    }

    private void SelfDestroy()
    {
        Destroy(gameObject);
    }
}
```

### Step 3.4: Resource folder separation

Create `Resources/BoosterPowerUps/` for the new PowerUpData assets (SpeedBoost + ColorBomb).

**Do NOT put them in `Resources/PowerUps/`** — ClassicGameMode loads all assets from that folder via `Resources.LoadAll<PowerUpData>("PowerUps")`. New assets there would leak into classic mode.

BoosterLevelData's `m_AvailablePowerUps` list references these assets explicitly — no folder loading, just direct references in the ScriptableObject.

### Step 3.5: Create assets

- PowerUp_SpeedBoost prefab (MeshRenderer + 2 ParticleSystems + shadow GO — matches existing PowerUp prefab structure)
- PowerUp_ColorBomb prefab (same structure)
- SpeedBoost PowerUpData asset in `Resources/BoosterPowerUps/`
- ColorBomb PowerUpData asset in `Resources/BoosterPowerUps/`
- Update BoosterLevelData assets to reference the new power-ups in `m_AvailablePowerUps`

### Step 3.6: Verify

Play booster mode — new power-ups spawn based on level config. SpeedBoost gives temporary speed buff. ColorBomb paints a circle at a random position. Classic mode still only spawns SizeUp and PaintBomb.

**Files touched:**

| File | Action |
|------|--------|
| `Player.cs` | Edit — AddSpeedBoost, m_SpeedFactor, SPEED_UP case, GetSpeed update |
| `PowerUp_SpeedBoost.cs` | New — `Scripts/Gameplay/PowerUps/` |
| `PowerUp_ColorBomb.cs` | New — `Scripts/Gameplay/PowerUps/` |
| PowerUpData assets (x2) | New — `Resources/BoosterPowerUps/` |
| PowerUp prefabs (x2) | New — follow existing PowerUp prefab structure |
| BoosterLevelData assets | Edit — add new power-ups to `m_AvailablePowerUps` |

---

## Full File Summary

| File | Phase | Action |
|------|-------|--------|
| `IGameMode.cs` | 1 | New — `Scripts/Interfaces/` |
| `ClassicGameMode.cs` | 1 | New — `Scripts/Gameplay/` |
| `GameService.cs` | 1+2 | Edit — m_CurrentGameMode, delegate data + OnGameEnd, SetGameMode |
| `Constants.cs` | 2 | Edit — 2 save keys |
| `BoosterLevelData.cs` | 2 | New — `Scripts/Gameplay/Data/` |
| `BoosterModeConfig.cs` | 2 | New — `Scripts/Configs/` |
| `BoosterGameMode.cs` | 2 | New — `Scripts/Gameplay/` |
| `ManagersInstaller.cs` | 2 | Edit — add BoosterModeConfig field + binding |
| `MainMenuView.cs` | 2 | Edit — booster button |
| `Player.cs` | 3 | Edit — AddSpeedBoost, m_SpeedFactor, SPEED_UP case |
| `PowerUp_SpeedBoost.cs` | 3 | New — `Scripts/Gameplay/PowerUps/` |
| `PowerUp_ColorBomb.cs` | 3 | New — `Scripts/Gameplay/PowerUps/` |

---

## Open Questions

- **IAPlayer difficulty:** Currently reads `StatsService.GetLevel()` directly. In booster mode should use `IGameMode.AiDifficultyMin/Max`. Simplest: GameService sets a difficulty range on each IAPlayer after spawning, based on `m_CurrentGameMode`. Avoids injecting IGameMode into IAPlayer.
- **Scene reload reset:** When the scene reloads after a match, GameService.Init() creates a new ClassicGameMode as default. If the player was in booster mode, we need to restore it. Options: check a PlayerPrefs flag, or have MainMenuView re-set it before each match.
