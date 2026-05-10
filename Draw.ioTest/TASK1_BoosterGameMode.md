# Task 1 — Booster Game Mode

## Goal
Second game mode with its own progression (parallel to classic), infinite levels via authored content, and 2 new boosters.

---

## Architecture Decisions

### What we learned from auditing the codebase

1. **There is no game mode concept.** All match parameters are hardcoded consts on GameService (`c_MinPowerUpRate`, `c_MaxPowerUpRate`, `c_BrushRate`) and Constants (`s_PlayerCount`). Power-ups are loaded via `Resources.LoadAll<PowerUpData>("PowerUps")` with no filtering.

2. **BattleRoyaleService is not a "mode" — it's a rule layer.** It owns elimination timer, crown/skull visuals, save mechanic, and win detection. Both modes use it identically. Untouched.

3. **The only behavioral difference between modes is end-game progression.** Classic does `TryToSetBestScore`, `AddGameResult`, `SetLastXP`, `GainXP`. Booster does the same calls — they just write into a parallel set of PlayerPrefs keys via an active-prefix system (see Stats Parallelism below). The actual game loop (painting, spawning, eliminating) is identical.

4. **Everything else that differs is just data** — power-ups list, spawn rates, AI difficulty range, player count, XP-per-rank curve.

5. **The event system already provides lifecycle hooks.** `onGamePhaseChanged`, `onEndGame`, `onScoresCalculated`, `onElimination`. A game mode subscribes via the new `OnPreEndGame`/`OnPostEndGame` hooks GameService calls explicitly.

6. **Three different "level" concepts coexist** in the existing code; we need to be precise about which one we touch.
   - **In-match growth** (`Player.m_Level`, `Player.cs:71`) — visual size growth during a match, ephemeral, mode-agnostic. **Untouched.**
   - **Rubber-band difficulty** (`StatsService.GetLevel()` at `StatsService.cs:54`) — float 0..1 = average of last 5 results in `c_GameResultSave_*`. Drives `IAPlayer.cs:63`. **Becomes parallel** via prefix.
   - **Player rank/XP** (`c_PlayerLevelSave`, `c_PlayerXPSave`) — meta progression shown in main menu, animated by PreEndView. **Becomes parallel** via prefix; in booster mode this same value doubles as the "current authored level" cursor (same stat, different lookup).

7. **`RankingService` is half-built dead code.** `IRankingService.cs` is empty; the class duplicates StatsService's XP/level reads/writes; the 25 `RankData` assets in `Resources/Ranks/` are loaded but no consumer reads them (`RankingView` pulls labels from `MainMenuView.m_Ratings[]` instead). **Out of scope** — flagged as cleanup ticket below.

### Design: IGameMode = data + hooks

`IGameMode` is a plain object (not a Zenject service). It exposes:
- **Data properties** — GameService reads them instead of hardcoded consts.
- **Lifecycle hooks** — `OnPreEndGame` / `OnPostEndGame` invoked either side of `PreEndView.LaunchPreEnd()`.
- **Stats namespace** — `StatsKeyPrefix` string that scopes all per-mode persistence.

GameService holds `IGameMode m_CurrentGameMode`, defaults to `ClassicGameMode`, switches via a single `SetGameMode` call that also notifies StatsService. Classic returns `""` for `StatsKeyPrefix` (preserves existing PlayerPrefs keys → zero migration). Booster returns `"Booster_"`.

### Stats parallelism: active-prefix pattern

Constraint: progression must be parallel and **constrained to the active mode by construction** — classic and booster cannot read or write each other's stats, even by mistake.

Mechanism: `StatsService` holds an `m_ActivePrefix` field, set by `GameService.SetGameMode` whenever the mode changes. Every per-mode method on StatsService prepends the prefix to its PlayerPrefs key:

| Stat | Classic key | Booster key |
|------|-------------|-------------|
| Best score | `BestScore` | `Booster_BestScore` |
| Game results history (rubber band) | `GameResult_0..4` | `Booster_GameResult_0..4` |
| Player XP | `XP` | `Booster_XP` |
| Player Level | `Lvl` | `Booster_Lvl` |

Mode-agnostic stats stay un-prefixed: `c_PlayerNameSave` (nickname), `FavoriteSkin`, `c_VibrationSave` (settings).

This builds on the existing key-suffix pattern (`StatsService.cs:33` already does `c_GameResultSave + "_" + i`) — same shape, plus one more concatenation. UI code (`PreEndView`, `RankingView`, `MainMenuView`, `EndView`) does **not** change. It keeps calling `m_StatsService.GetXP()` etc. and gets the active mode's value back.

### Player level = booster cursor

In booster mode, `GetPlayerLevel()` (with `Booster_` prefix → `Booster_Lvl` key) doubles as the booster level cursor. `BoosterModeConfig.GetLevel(int)` indexes into the authored level list using this value. **No separate booster-level field, no separate methods.**

Progression mechanism is XP-driven and **identical to classic**: `OnPreEndGame` → `SetLastXP` → `OnPostEndGame` → `GainXP` → cascades into `LevelUp` at threshold. The only differences are the active prefix and the XP-per-rank curve, both per-mode data. Same OnGameEnd shape, both modes get the PreEndView XP-bar animation for free.

`m_XPByRank` lives on each config: classic on `GameConfig.m_XPByRank` (existing), booster on `BoosterModeConfig.m_XPByRank` (new). Booster's curve is tuned to match the desired progression cadence — e.g., for "win = next level," set rank-0 XP to one full level threshold and rank ≥ 1 XP to 0.

PlayerLevel is **1-indexed** in existing code (defaults to 1 in `StatsService.cs:121`; `RankingView.cs:29` does `GetPlayerLevel() - 1`). Booster inherits this: `BoosterModeConfig.GetLevel(level)` does `m_AuthoredLevels[level - 1]`.

---

## Phased Implementation

### Phase 1 — Decouple
Extract IGameMode + active-prefix stats. Classic mode runs through the new abstraction with **zero behavior change**.

### Phase 2 — Booster mode
Add BoosterGameMode + content + UI entry point. Parallel stats just work because of Phase 1.

### Phase 3 — New power-ups
SpeedBoost + ColorBomb in a separate Resources folder so they don't leak into classic.

---

## Phase 1 — Decouple

### Step 1.1: IGameMode interface

New file: `Scripts/Gameplay/IGameMode.cs` (next to ClassicGameMode/BoosterGameMode and existing `IDrawLine` — `Scripts/Interfaces/` is reserved for service interfaces).

```csharp
public interface IGameMode
{
    string StatsKeyPrefix { get; }

    List<PowerUpData> PowerUps { get; }
    float MinPowerUpRate { get; }
    float MaxPowerUpRate { get; }
    float BrushSpawnRate { get; }
    int PlayerCount { get; }
    float AIDifficultyMin { get; }
    float AIDifficultyMax { get; }

    void OnPreEndGame(int _PlayerRank, int _PlayerScore);
    void OnPostEndGame();
}
```

Notes on shape:
- `MinPowerUpRate` / `MaxPowerUpRate` are exposed separately (not a single getter that internally calls `Random.Range`) so properties stay pure data — GameService keeps owning the randomization.
- `OnPreEndGame` runs **before** `PreEndView.LaunchPreEnd()` (TryToSetBestScore, AddGameResult, SetLastXP). `OnPostEndGame` runs **after** (GainXP). Splitting preserves the original ordering at `GameService.cs:226-243` where PreEndView captures pre-gain XP/level for the bar animation (`PreEndView.cs:69-70`).
- Param naming `_PascalCase` matches the gameplay layer (Player, IAPlayer, PowerUp).

| Property | Replaces | Current value (Classic) |
|----------|----------|-------------------------|
| `PowerUps` | `Resources.LoadAll<PowerUpData>("PowerUps")` (`GameService.cs:133`) | All assets in folder |
| `MinPowerUpRate` | `c_MinPowerUpRate` | 1f |
| `MaxPowerUpRate` | `c_MaxPowerUpRate` | 2.5f |
| `BrushSpawnRate` | `c_BrushRate` | 16f |
| `PlayerCount` | `Constants.s_PlayerCount` | 8 |
| `AIDifficultyMin` | `Mathf.Clamp01(StatsService.GetLevel() / 2f)` (`IAPlayer.cs:63`) | 0..0.5 |
| `AIDifficultyMax` | hardcoded `1f` (`IAPlayer.cs:63`) | 1 |
| `OnPreEndGame`+`OnPostEndGame` | inline StatsService calls in `ChangePhase(END)` | XP, difficulty, best score |

### Step 1.2: StatsService gets the active prefix

Edit `Scripts/Interfaces/Services/IStatsService.cs`:

```csharp
void SetActiveStatsPrefix(string _Prefix);
int GetBestScore();   // promote from concrete class to interface
```

Edit `Scripts/Services/StatsService.cs`. Add the field and setter:

```csharp
private string m_ActivePrefix = "";

public void SetActiveStatsPrefix(string _Prefix)
{
    m_ActivePrefix = _Prefix ?? "";
}
```

Prefix-prepend every per-mode key:

```csharp
private int GetGameResult(int _Index)
{
    string key = m_ActivePrefix + Constants.c_GameResultSave + "_" + _Index;
    return PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : 0;
}

public void AddGameResult(int _WinScore)
{
    for (int i = Constants.c_SavedGameCount - 1; i >= 0; --i)
    {
        string key = m_ActivePrefix + Constants.c_GameResultSave + "_" + i;
        PlayerPrefs.SetInt(key, GetGameResult(i - 1));
    }
    PlayerPrefs.SetInt(m_ActivePrefix + Constants.c_GameResultSave + "_0", _WinScore);
}

public float GetLevel() { /* loop over GetGameResult, unchanged shape */ }

public int GetBestScore() =>
    PlayerPrefs.GetInt(m_ActivePrefix + Constants.c_BestScoreSave, 0);

public void TryToSetBestScore(int _Score)
{
    if (GetBestScore() < _Score)
        PlayerPrefs.SetInt(m_ActivePrefix + Constants.c_BestScoreSave, _Score);
}

public int GetXP() =>
    PlayerPrefs.GetInt(m_ActivePrefix + Constants.c_PlayerXPSave, 0);

public int GetPlayerLevel() =>
    PlayerPrefs.GetInt(m_ActivePrefix + Constants.c_PlayerLevelSave, 1);

void LevelUp() =>
    PlayerPrefs.SetInt(m_ActivePrefix + Constants.c_PlayerLevelSave, GetPlayerLevel() + 1);

void LevelDown() =>
    PlayerPrefs.SetInt(m_ActivePrefix + Constants.c_PlayerLevelSave, GetPlayerLevel() - 1);

public void GainXP()
{
    int xp = m_LastGain + GetXP();
    while (xp >= XPToNextLevel())
    {
        xp -= XPToNextLevel();
        LevelUp();
    }
    PlayerPrefs.SetInt(m_ActivePrefix + Constants.c_PlayerXPSave, xp);
}
```

Mode-agnostic methods unchanged: `SetNickname`/`GetNickname`, `FavoriteSkin`, `SetLastXP` (in-memory only), `XPToNextLevel` (reads `m_XPForLevel` from StatsConfig — same threshold curve for both modes; promote to per-mode later if design needs distinct curves).

Default prefix is `""`. Until `SetGameMode` runs, StatsService behaves identically to today — no migration of existing PlayerPrefs.

### Step 1.3: ClassicGameMode

New file: `Scripts/Gameplay/ClassicGameMode.cs`

```csharp
public class ClassicGameMode : IGameMode
{
    private readonly IStatsService m_StatsService;
    private readonly List<int> m_XPByRank;
    private readonly List<PowerUpData> m_PowerUps;

    public ClassicGameMode(IStatsService _StatsService, List<int> _XPByRank)
    {
        m_StatsService = _StatsService;
        m_XPByRank = _XPByRank;
        m_PowerUps = new List<PowerUpData>(Resources.LoadAll<PowerUpData>("PowerUps"));
    }

    public string StatsKeyPrefix => "";

    public List<PowerUpData> PowerUps => m_PowerUps;
    public float MinPowerUpRate => 1f;
    public float MaxPowerUpRate => 2.5f;
    public float BrushSpawnRate => 16f;
    public int PlayerCount => 8;
    public float AIDifficultyMin => Mathf.Clamp01(m_StatsService.GetLevel() / 2f);
    public float AIDifficultyMax => 1f;

    public void OnPreEndGame(int _PlayerRank, int _PlayerScore)
    {
        m_StatsService.TryToSetBestScore(_PlayerScore);

        int rankingScore = -1;
        if (_PlayerRank == 0)        rankingScore = 1;
        else if (_PlayerRank >= 2)   rankingScore = 0;

        m_StatsService.AddGameResult(rankingScore);
        m_StatsService.SetLastXP(m_XPByRank[_PlayerRank]);
    }

    public void OnPostEndGame()
    {
        m_StatsService.GainXP();
    }
}
```

### Step 1.4: IGameService extends

Edit `Scripts/Interfaces/Services/IGameService.cs`. Add:

```csharp
void SetGameMode(IGameMode _Mode);
int PlayerCount { get; }
float GetAIDifficultyMin();
float GetAIDifficultyMax();
```

`PlayerCount` and `GetAIDifficulty*` delegate to the current mode. Adding them to the interface lets `LoadingView`, `EndView`, and `IAPlayer` read mode-dependent values without leaking the IGameMode type or mutating `Constants.s_PlayerCount`.

### Step 1.5: GameService changes

Edit `Scripts/Services/GameService.cs`.

Add:
```csharp
private IGameMode m_CurrentGameMode;
public IGameMode CurrentGameMode => m_CurrentGameMode;

public int PlayerCount => m_CurrentGameMode.PlayerCount;
public float GetAIDifficultyMin() => m_CurrentGameMode.AIDifficultyMin;
public float GetAIDifficultyMax() => m_CurrentGameMode.AIDifficultyMax;

public void SetGameMode(IGameMode _Mode)
{
    m_CurrentGameMode = _Mode;
    m_StatsService.SetActiveStatsPrefix(_Mode.StatsKeyPrefix);
}
```

`Init()` — replace the `Resources.LoadAll<PowerUpData>` line and create the default mode:
```csharp
SetGameMode(new ClassicGameMode(m_StatsService, m_XPByRank));
```
Drop the `m_PowerUps` field — read `m_CurrentGameMode.PowerUps` directly at the two call sites (`OnUpdate` and `PickPowerUp`). Avoids the two-source-of-truth trap.

`ChangePhase(LOADING)`:
```csharp
m_PowerUpRate = Random.Range(m_CurrentGameMode.MinPowerUpRate, m_CurrentGameMode.MaxPowerUpRate);
```

`OnUpdate()`:
```csharp
if (Time.time - m_LastPowerUpTime > m_PowerUpRate)
{
    m_PowerUpRate = Random.Range(m_CurrentGameMode.MinPowerUpRate, m_CurrentGameMode.MaxPowerUpRate);
    m_LastPowerUpTime = Time.time;
    var ups = m_CurrentGameMode.PowerUps;
    PowerUpData powerUpData = ups[Random.Range(0, ups.Count)];
    PopObjectRandomly(powerUpData.m_Prefab);
}

if (Time.time - m_LastBrushTime > m_CurrentGameMode.BrushSpawnRate) { ... }
```

`PickPowerUp()`:
```csharp
var ups = m_CurrentGameMode.PowerUps;
return ups[Random.Range(0, ups.Count)].m_Prefab;
```

`PopPlayers()`:
```csharp
m_Players = new List<Player>(m_CurrentGameMode.PlayerCount);
for (int i = 0; i < m_CurrentGameMode.PlayerCount; ++i) { ... }
```

`ChangePhase(END)` — split into the two hooks around LaunchPreEnd:
```csharp
case GamePhase.END:
    int playerScore = Mathf.RoundToInt(m_Players[0].percent * 100.0f);
    int playerRank = m_BattleRoyaleService.GetHumanPlayer().m_Rank;
    m_CurrentGameMode.OnPreEndGame(playerRank, playerScore);
    PreEndView.Instance.LaunchPreEnd();
    m_CurrentGameMode.OnPostEndGame();
    break;
```

`ChangePhase(MAIN_MENU)` — reset to classic so the menu always lands in classic mode (resolves the "stuck in booster after one match" bug):
```csharp
case GamePhase.MAIN_MENU:
    SetGameMode(new ClassicGameMode(m_StatsService, m_XPByRank));
    Randomize();
    SetColor(ComputeCurrentPlayerColor(true, 0));
    break;
```

Drop unused fields/consts: `m_PowerUps` (field), `c_MinPowerUpRate`, `c_MaxPowerUpRate`, `c_BrushRate` — they live in `ClassicGameMode` now.

### Step 1.6: Update direct consumers of mode-dependent state

`Scripts/UI/LoadingView.cs` — five reads of `Constants.s_PlayerCount` (lines 42, 105, 106, 118, 122). Replace with `GameService.PlayerCount` (LoadingView is a `View<>`, so `GameService` is already injected via the base class at `View.cs:15`).

`Scripts/UI/EndView.cs:137` — same swap.

`Scripts/Gameplay/Players/IAPlayer.cs:63` — replace:
```csharp
m_Difficulty = Random.Range(GameService.GetAIDifficultyMin(), GameService.GetAIDifficultyMax());
```
(IAPlayer inherits the `GameService` reference from `Player.cs:47`.)

Order-of-operations note: `PopPlayer` instantiates the IAPlayer prefab synchronously (`GameService.cs:323`). Unity defers `Start()` to the next frame, by which time `m_CurrentGameMode` is already set (set in `Init()` at construction or in `SetGameMode` before `ChangePhase(LOADING)`). `Start()` reads correct values.

### Step 1.7: Verify

After Phase 1: run the game, play classic.
- AI difficulty scales the same (rubber-band still feeds off classic `GetLevel()`, prefix is `""` → reads same keys → same data).
- Best score, XP, player level all read/write the same PlayerPrefs keys (no migration).
- Power-ups, spawn rates, player count, brush rate — all the same values via ClassicGameMode.
- PreEndView XP-bar animation works (Pre/Post hook split preserves ordering).

**Files touched:**

| File | Action |
|------|--------|
| `Scripts/Gameplay/IGameMode.cs` | New |
| `Scripts/Gameplay/ClassicGameMode.cs` | New |
| `Scripts/Services/StatsService.cs` | Edit — `m_ActivePrefix`, `SetActiveStatsPrefix`, prefix every per-mode key |
| `Scripts/Interfaces/Services/IStatsService.cs` | Edit — `SetActiveStatsPrefix`, `GetBestScore` |
| `Scripts/Services/GameService.cs` | Edit — `m_CurrentGameMode`, `SetGameMode`, `PlayerCount`/`GetAIDifficulty*`, delegate reads, split end hooks, reset to classic on MAIN_MENU |
| `Scripts/Interfaces/Services/IGameService.cs` | Edit — `SetGameMode`, `PlayerCount`, `GetAIDifficultyMin`/`Max` |
| `Scripts/UI/LoadingView.cs` | Edit — `Constants.s_PlayerCount` → `GameService.PlayerCount` (5 sites) |
| `Scripts/UI/EndView.cs` | Edit — same swap (1 site) |
| `Scripts/Gameplay/Players/IAPlayer.cs` | Edit — line 63 reads from GameService |

---

## Phase 2 — Booster Game Mode

With Phase 1 in place, booster mode is mostly content + a small mode class.

### Step 2.1: BoosterLevelData

New file: `Scripts/Gameplay/Data/BoosterLevelData.cs`

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
    public float m_AIDifficultyMin = 0f;
    public float m_AIDifficultyMax = 1f;
}
```

Assets go in `Resources/BoosterLevels/`. Folder location matches existing data conventions (`Resources/Skins/`, `Resources/Brushs/`, `Resources/Terrains/`).

### Step 2.2: BoosterModeConfig

New file: `Scripts/Configs/BoosterModeConfig.cs`

```csharp
[CreateAssetMenu(fileName = "BoosterModeConfig", menuName = "Config/BoosterModeConfig")]
public class BoosterModeConfig : ScriptableObject
{
    public List<BoosterLevelData> m_AuthoredLevels;
    public BoosterLevelData m_FallbackTemplate;
    public List<int> m_XPByRank;

    public BoosterLevelData GetLevel(int _OneIndexedLevel)
    {
        int index = _OneIndexedLevel - 1;
        if (index >= 0 && index < m_AuthoredLevels.Count)
            return m_AuthoredLevels[index];
        return m_FallbackTemplate;
    }
}
```

`m_XPByRank` is booster's own XP-per-rank curve (mirrors `GameConfig.m_XPByRank` for classic). Independently tunable.

### Step 2.3: BoosterGameMode

New file: `Scripts/Gameplay/BoosterGameMode.cs`

```csharp
public class BoosterGameMode : IGameMode
{
    private readonly IStatsService m_StatsService;
    private readonly BoosterModeConfig m_Config;
    private BoosterLevelData m_CurrentLevel;

    public BoosterGameMode(IStatsService _StatsService, BoosterModeConfig _Config)
    {
        m_StatsService = _StatsService;
        m_Config = _Config;
        // m_CurrentLevel resolved lazily on first property access — by then, the active
        // prefix is already "Booster_" (set inside SetGameMode), so GetPlayerLevel returns
        // the booster-scoped value.
    }

    private BoosterLevelData CurrentLevel =>
        m_CurrentLevel ??= m_Config.GetLevel(m_StatsService.GetPlayerLevel());

    public string StatsKeyPrefix => "Booster_";

    public List<PowerUpData> PowerUps => CurrentLevel.m_AvailablePowerUps;
    public float MinPowerUpRate => CurrentLevel.m_MinPowerUpRate;
    public float MaxPowerUpRate => CurrentLevel.m_MaxPowerUpRate;
    public float BrushSpawnRate => CurrentLevel.m_BrushSpawnRate;
    public int PlayerCount => CurrentLevel.m_PlayerCount;
    public float AIDifficultyMin => CurrentLevel.m_AIDifficultyMin;
    public float AIDifficultyMax => CurrentLevel.m_AIDifficultyMax;

    public void OnPreEndGame(int _PlayerRank, int _PlayerScore)
    {
        m_StatsService.TryToSetBestScore(_PlayerScore);

        int rankingScore = -1;
        if (_PlayerRank == 0)        rankingScore = 1;
        else if (_PlayerRank >= 2)   rankingScore = 0;

        m_StatsService.AddGameResult(rankingScore);
        m_StatsService.SetLastXP(m_Config.m_XPByRank[_PlayerRank]);
    }

    public void OnPostEndGame()
    {
        m_StatsService.GainXP();
    }
}
```

Construction order: `StartBoosterMode()` calls `SetGameMode(new BoosterGameMode(...))`. The `new` evaluates first (constructor stores refs but does not read PlayerLevel), then `SetGameMode` flips the prefix to `"Booster_"` on StatsService. First property access during `ChangePhase(LOADING)` triggers the lazy `CurrentLevel` resolution, which reads `Booster_Lvl` correctly. Each `StartBoosterMode` call constructs a fresh BoosterGameMode, so `m_CurrentLevel` is always recomputed — no stale cache between matches.

### Step 2.4: ManagersInstaller wiring

Edit `Scripts/Installers/ManagersInstaller.cs`:

```csharp
[SerializeField] private BoosterModeConfig m_BoosterModeConfig;

private void InstallGameManager(DiContainer subContainer)
{
    subContainer.Bind<GameService>().AsSingle();
    subContainer.Bind<GameConfig>().FromInstance(m_GameConfig).AsSingle();
    subContainer.Bind<BoosterModeConfig>().FromInstance(m_BoosterModeConfig).AsSingle();
}
```

`GameService.Construct` adds the parameter:
```csharp
[Inject]
public void Construct(GameConfig gameConfig, BoosterModeConfig boosterModeConfig, ...)
{
    m_BoosterModeConfig = boosterModeConfig;
    // ...
}
```

### Step 2.5: GameService.StartBoosterMode

Add to `GameService`:

```csharp
public void StartBoosterMode()
{
    SetGameMode(new BoosterGameMode(m_StatsService, m_BoosterModeConfig));
    ChangePhase(GamePhase.LOADING);
}
```

Add `void StartBoosterMode()` to `IGameService`. The booster config dependency stays inside GameService — call sites (MainMenuView) don't need to inject it.

### Step 2.6: MainMenuView booster button

Edit `Scripts/UI/MainMenuView.cs`:

```csharp
public void OnBoosterButton()
{
    if (GameService.currentPhase == GamePhase.MAIN_MENU)
        GameService.StartBoosterMode();
}
```

Wire the new button in the `MainMenuView` prefab to `OnBoosterButton`. Classic Play button stays unchanged — the default mode after `ChangePhase(MAIN_MENU)` is classic (set in Step 1.5).

### Step 2.7: Initial assets

- 3–5 `BoosterLevelData` assets in `Resources/BoosterLevels/` with escalating difficulty.
- 1 `BoosterModeConfig` asset assigned on the `ManagersInstaller` ScriptableObject; set `m_XPByRank` to a curve matching design's progression cadence.
- Initial booster levels can use the same classic power-ups (`SizeUp`, `PaintBomb`) — new power-ups land in Phase 3.

### Step 2.8: Verify

- Play classic — still works identically (regression).
- Tap Booster button — match runs with `BoosterLevelData[Booster_Lvl - 1]` values (player count, spawn rates, AI range, power-up list all from the level).
- End match → `OnPreEndGame` writes `Booster_BestScore` / `Booster_GameResult_*` / sets last XP → LaunchPreEnd animates the booster-scoped XP bar → `OnPostEndGame` commits gain to `Booster_XP` → cascades into `Booster_Lvl` at threshold.
- Return to MAIN_MENU → mode resets to classic → classic best score / rank intact, untouched.
- Tap Booster again → BoosterGameMode reads the new `Booster_Lvl` → loads `BoosterLevelData[Booster_Lvl - 1]`.
- Verify in PlayerPrefs (Editor): `BestScore` and `Booster_BestScore` are distinct, never overlap.

**Files touched:**

| File | Action |
|------|--------|
| `Scripts/Gameplay/Data/BoosterLevelData.cs` | New |
| `Scripts/Configs/BoosterModeConfig.cs` | New |
| `Scripts/Gameplay/BoosterGameMode.cs` | New |
| `Scripts/Installers/ManagersInstaller.cs` | Edit — `BoosterModeConfig` field + binding |
| `Scripts/Services/GameService.cs` | Edit — inject `BoosterModeConfig`, `StartBoosterMode` |
| `Scripts/Interfaces/Services/IGameService.cs` | Edit — `StartBoosterMode` |
| `Scripts/UI/MainMenuView.cs` | Edit — `OnBoosterButton` |

---

## Phase 3 — New Power-Ups

### Step 3.1: Player.cs — AddSpeedBoost

`m_SpeedPowerUpCoroutine` field is **already declared** at `Player.cs:57`. The `EBonus.SPEED_UP` enum value is already declared at line 28 but the case is unhandled in `BonusCoroutine`. **Do not redeclare the field.**

Add:
```csharp
private float m_SpeedFactor = 1f;

public virtual void AddSpeedBoost(float _Factor, float _Duration)
{
    m_SpeedFactor = _Factor;
    if (m_SpeedPowerUpCoroutine != null)
        StopCoroutine(m_SpeedPowerUpCoroutine);
    m_SpeedPowerUpCoroutine = StartCoroutine(BonusCoroutine(EBonus.SPEED_UP, _Duration));
}
```

Add the SPEED_UP case to `BonusCoroutine` (line 490):
```csharp
case EBonus.SPEED_UP:
    m_SpeedFactor = 1f;
    break;
```

Update `GetSpeed()` (line 471):
```csharp
protected float GetSpeed()
{
    return Mathf.Clamp(m_Speed * m_SpeedFactor, c_MinSpeed, c_MaxSpeed);
}
```

**Speed clamp ceiling note:** `c_MinSpeed = 50f`, `c_MaxSpeed = 100f`, default `m_Speed = c_MinSpeed = 50`. Factor 2× → 100 (at ceiling). Anything beyond 2× is a no-op. Tune authoring to ≤ 2×, or raise `c_MaxSpeed` if larger boosts are wanted.

### Step 3.2: PowerUp_SpeedBoost

New file: `Scripts/Gameplay/PowerUps/PowerUp_SpeedBoost.cs`. Calls `base.OnPlayerTouched` → standard cleanup pattern (sets `m_Alive = false`, particles fade, GameObject destroyed in `PowerUp.Update`).

```csharp
public sealed class PowerUp_SpeedBoost : PowerUp
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

New file: `Scripts/Gameplay/PowerUps/PowerUp_ColorBomb.cs`. Mirrors `PowerUp_PaintBomb`'s pattern: **does NOT call `base.OnPlayerTouched`**, manual cleanup, deferred destruction via `FillCircle` callback (matches `PowerUp_PaintBomb.cs:25-41`).

```csharp
public sealed class PowerUp_ColorBomb : PowerUp
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
        const float c_Padding = 13f;   // matches GameService.c_PowerUpPadding
        float halfW = m_TerrainService.WorldHalfWidth;
        float halfH = m_TerrainService.WorldHalfHeight;
        return new Vector3(
            Random.Range(-halfW + c_Padding, halfW - c_Padding),
            0f,
            Random.Range(-halfH + c_Padding, halfH - c_Padding));
    }

    private void SelfDestroy()
    {
        Destroy(gameObject);
    }
}
```

### Step 3.4: Resource folder separation

Create `Resources/BoosterPowerUps/` for the new `PowerUpData` assets.

**Do not put them in `Resources/PowerUps/`.** `ClassicGameMode` loads everything in that folder via `Resources.LoadAll<PowerUpData>("PowerUps")`. New assets there would leak into classic.

`BoosterLevelData.m_AvailablePowerUps` references these assets by direct reference — no folder loading required.

### Step 3.5: Create assets

- PowerUp_SpeedBoost prefab (MeshRenderer + 2 ParticleSystems + shadow GO — match the existing PowerUp_SizeUp prefab structure).
- PowerUp_ColorBomb prefab (same structure).
- SpeedBoost `PowerUpData` asset in `Resources/BoosterPowerUps/`.
- ColorBomb `PowerUpData` asset in `Resources/BoosterPowerUps/`.
- Update `BoosterLevelData` assets to reference the new power-ups in `m_AvailablePowerUps`.

### Step 3.6: Verify

- Play booster mode — new power-ups spawn based on per-level config.
- SpeedBoost gives temporary speed buff (visible: brush moves faster); after duration, speed returns to normal.
- ColorBomb fills a colored circle at a random terrain position.
- Classic mode still only spawns SizeUp and PaintBomb (folder isolation working).

**Files touched:**

| File | Action |
|------|--------|
| `Scripts/Gameplay/Players/Player.cs` | Edit — `m_SpeedFactor`, `AddSpeedBoost`, SPEED_UP case, `GetSpeed` |
| `Scripts/Gameplay/PowerUps/PowerUp_SpeedBoost.cs` | New |
| `Scripts/Gameplay/PowerUps/PowerUp_ColorBomb.cs` | New |
| `Resources/BoosterPowerUps/SpeedBoost.asset` | New |
| `Resources/BoosterPowerUps/ColorBomb.asset` | New |
| Power-up prefabs (×2) | New |
| `BoosterLevelData` assets | Edit — reference new power-ups |

---

## Full File Summary

| File | Phase | Action |
|------|-------|--------|
| `Scripts/Gameplay/IGameMode.cs` | 1 | New |
| `Scripts/Gameplay/ClassicGameMode.cs` | 1 | New |
| `Scripts/Services/StatsService.cs` | 1 | Edit — active prefix on every per-mode key |
| `Scripts/Interfaces/Services/IStatsService.cs` | 1 | Edit — `SetActiveStatsPrefix`, `GetBestScore` |
| `Scripts/Services/GameService.cs` | 1 + 2 | Edit — `m_CurrentGameMode`, `SetGameMode`/`StartBoosterMode`, delegate reads, split end hooks, MAIN_MENU reset |
| `Scripts/Interfaces/Services/IGameService.cs` | 1 + 2 | Edit — `SetGameMode`/`StartBoosterMode`/`PlayerCount`/`GetAIDifficulty*` |
| `Scripts/UI/LoadingView.cs` | 1 | Edit — `Constants.s_PlayerCount` → `GameService.PlayerCount` (5 sites) |
| `Scripts/UI/EndView.cs` | 1 | Edit — same swap (1 site) |
| `Scripts/Gameplay/Players/IAPlayer.cs` | 1 | Edit — line 63 reads from GameService |
| `Scripts/Gameplay/Data/BoosterLevelData.cs` | 2 | New |
| `Scripts/Configs/BoosterModeConfig.cs` | 2 | New |
| `Scripts/Gameplay/BoosterGameMode.cs` | 2 | New |
| `Scripts/Installers/ManagersInstaller.cs` | 2 | Edit — `BoosterModeConfig` field + binding |
| `Scripts/UI/MainMenuView.cs` | 2 | Edit — `OnBoosterButton` |
| `Scripts/Gameplay/Players/Player.cs` | 3 | Edit — `AddSpeedBoost`, `m_SpeedFactor`, SPEED_UP case, `GetSpeed` |
| `Scripts/Gameplay/PowerUps/PowerUp_SpeedBoost.cs` | 3 | New |
| `Scripts/Gameplay/PowerUps/PowerUp_ColorBomb.cs` | 3 | New |

---

## Out of Scope / Cleanup Tickets

- **`RankingService` is dead/half-built code.** `IRankingService.cs` declares zero methods; the class duplicates StatsService's XP/level reads/writes (`RankingService.cs:32-54` uses the same `c_PlayerXPSave` / `c_PlayerLevelSave` keys); `m_RankData` (25 RankData assets in `Resources/Ranks/`) is loaded but no consumer reads it (`RankingView` pulls labels from `MainMenuView.m_Ratings[]` instead). Either delete service + empty interface, or finish the wiring and remove the duplicate XP/level logic on StatsService. **Do not touch in this task.**
- **`Constants.s_PlayerCount`** can become a true `const` (or be removed) once Phase 1 lands — no consumer left after the LoadingView / EndView / GameService swaps.
- **`MainMenuView.m_Ratings[]` (rank label strings)** is a `string[]` field on the prefab. Currently shared across modes; if booster wants distinct rank vocabulary, lift to a config. Defer.
- **PreEndView dual-mode UI** is not needed — the active-prefix system makes the same XP-bar template show the right mode's data automatically. Single bar, two contexts.

---

## Done When

- Classic mode plays identically to today's behavior. Regression checks: best score, XP, player level, AI difficulty, power-up roster, player count, brush rate, end-of-match XP-bar animation — all unchanged.
- Booster button on the main menu starts a booster match using `BoosterLevelData[Booster_Lvl - 1]`.
- Booster wins accumulate XP in `Booster_XP`; level-up cascades into `Booster_Lvl`; the next booster match loads the next level's data.
- Best scores, XP, levels, and recent-results history are completely separate between modes — playing one never reads or writes the other's PlayerPrefs keys.
- Two new power-ups (SpeedBoost, ColorBomb) spawn in booster mode only — never leak into classic.
