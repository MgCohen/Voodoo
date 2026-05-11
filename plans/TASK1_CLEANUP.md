# Task 1 — Cleanup Plan

Punch list to close the gaps found in the Booster Game Mode review. Ordered by severity.

---

## 🔴 Blockers — must fix before resubmission

### 1. Wire the Booster button in the main menu (~20 min)

**Problem:** Last commit is titled *"task 1 functional, missing Ui"*. `Game.unity` is unchanged from the initial commit. `MainMenuView.OnBoosterButton()` exists in code but nothing in the scene calls it. The APK cannot demonstrate the feature.

**Fix:**
- Open `Draw.ioTest/Draw.ioTest/Assets/Scenes/Game.unity` in Unity
- Locate the `MainMenu` canvas → find the `PLAY` button group
- Duplicate the Play button next to it; rename to `BoosterButton`, relabel to **"BOOSTER MODE"**
- Wire its `OnClick` → `MainMenuView.OnBoosterButton`
- Optional: add a small "Lvl XX" label bound to `m_StatsService.GetPlayerLevel()` (matches the mockup in the PDF)
- Rebuild APK

**Acceptance:** Tapping the button on device transitions to LOADING in booster mode, match runs using `BoosterLevel_<N>` data, end of match advances `Booster_Lvl`.

---

### 2. Restore Classic AI rubber-band difficulty + regression tests (~45 min)

**Problem:** Original [IAPlayer.cs:63](Draw.ioTest/Draw.ioTest/Assets/Scripts/Gameplay/Players/IAPlayer.cs:63) was:
```csharp
m_Difficulty = Random.Range(Mathf.Clamp01(StatsService.GetLevel() / 2f), 1f);
```
After refactor it reads static `MatchSettings.m_AIDifficultyMin/Max`. [ClassicMode.asset](Draw.ioTest/Draw.ioTest/Assets/Scripts/Configs/ClassicMode.asset) hardcodes `min=0, max=1` → uniform random. The rubber band is dead in Classic mode.

**Fix:** Make `AIDifficultyMin/Max` virtuals on `GameMode`, override in `ClassicMode` to call into stats.

```csharp
// GameMode.cs
public abstract float GetAIDifficultyMin(IStatsService _Stats);
public virtual  float GetAIDifficultyMax(IStatsService _Stats) => 1f;
```

```csharp
// ClassicMode.cs
public override float GetAIDifficultyMin(IStatsService _Stats)
    => Mathf.Clamp01(_Stats.GetLevel() / 2f);
```

```csharp
// BoosterMode.cs
public override float GetAIDifficultyMin(IStatsService _Stats)
    => GetLevelData(_Stats.GetPlayerLevel() - 1).m_Match.m_AIDifficultyMin;
public override float GetAIDifficultyMax(IStatsService _Stats)
    => GetLevelData(_Stats.GetPlayerLevel() - 1).m_Match.m_AIDifficultyMax;
```

Update `GameService.GetAIDifficultyMin/Max()` to pass `m_StatsService`. Drop `m_AIDifficultyMin/Max` from Classic's `MatchSettings` (still useful for booster, since per-level AI floor is a designer knob).

**Regression tests** (`Assets/Tests/Editor/AIDifficultyTests.cs`):
- `ClassicMode_AIDifficultyMin_ScalesWithStatsLevel`
  - Inject a fake `IStatsService` returning `GetLevel() = 0` → floor `= 0`
  - Same fake returning `0.5` → floor `= 0.25`
  - Same fake returning `1.0` → floor `= 0.5`
- `BoosterMode_AIDifficultyMin_UsesCurrentLevelData`
  - 2 `BoosterLevelData` SOs with distinct `m_AIDifficultyMin` values
  - Fake stats returns `GetPlayerLevel() = 1` → reads level 0's value
  - Fake stats returns `GetPlayerLevel() = 2` → reads level 1's value
- `BoosterMode_AIDifficulty_FallsBackBeyondAuthoredLevels`
  - Authored count = 2, fake stats returns `GetPlayerLevel() = 99` → returns fallback's value
- (Recover the dropped StatsService tests from commit `722cb782` while we're here — see item 6.)

**Acceptance:** Play 5 losing classic matches → next match's AI floor is `0`. Play 5 winning classic matches → AI floor is `0.5`. Tests above all green.

---

## 🟡 Should-fix — close before sign-off

### 3. ~~Decouple `IStatsService` from `GameMode`~~ — **dropped**

Reviewed: passing `GameMode` is cleaner than the alternatives (`Func<int,int>` callback or threshold-as-arg). The mutual reference is intentional and readable. Keep as is.

### 4. Leave a feature-flag hook for Task 3 — **deferred to Task 3**

Will be designed alongside the Debug Menu so the flag-reading pattern is consistent across both features. See `PATTERNS_FeatureFlags.md` for the open question.

---

## 🟢 Nice-to-have — polish

### 5. Add tooltip on `PowerUp_SpeedBoost.m_SpeedMultiplier` (~2 min)

The `Mathf.Clamp(m_Speed * m_SpeedFactor, c_MinSpeed, c_MaxSpeed)` cap predates this task — `c_MinSpeed = 50`, `c_MaxSpeed = 100` were in the original `Player.cs`. But SpeedBoost is the first surface that lets a designer push the multiplied speed past the ceiling, so a tooltip is worth the 30 seconds.

```csharp
// PowerUp_SpeedBoost.cs
[Tooltip("Effective multiplier is capped at 2x by Player.c_MaxSpeed (base speed 50, ceiling 100).")]
public float m_SpeedMultiplier = 1.5f;
```

### 6. Restore the StatsService regression tests (~5 min)

Commit `722cb782` ("Add regression tests for StatsService before Phase 1 refactor") added them; they're not in the final tree. Recover them — the parallel-prefix pattern is exactly the kind of invariant that breaks silently.

```bash
git show 722cb782 --stat | grep Test
git checkout 722cb782 -- <test file paths>
```

### 7. Replace per-level XP threshold list with a formula (~15 min)

**Problem:** All 5 `BoosterLevel_*.asset` files hand-author `m_XPToNextLevel: 100`. The task asks for **infinite levels**, so a per-level list is the wrong shape — every new level needs an asset edit, and the fallback level's threshold has to silently substitute for everything past the authored range.

**Fix:** Move XP threshold to a formula on `BoosterMode`. Drop `m_XPToNextLevel` from `BoosterLevelData`.

```csharp
// BoosterMode.cs
[Header("XP curve")]
public int   m_BaseXPThreshold      = 100;
public int   m_XPIncrementPerLevel  = 50;    // linear: level N needs Base + N*Increment
// or, exponential variant:
// public float m_XPGrowthFactor    = 1.15f;

public override int GetXPForLevel(int _LevelIndex)
{
    return m_BaseXPThreshold + _LevelIndex * m_XPIncrementPerLevel;
    // exponential: Mathf.RoundToInt(m_BaseXPThreshold * Mathf.Pow(m_XPGrowthFactor, _LevelIndex));
}
```

`BoosterLevelData` keeps everything else (`MatchSettings` per level, since AI range / power-up roster / spawn rates are designer-tuned per level by design). The fallback level keeps doing its job for *match settings* past the authored range. XP just scales smoothly forever.

ClassicMode keeps its hand-authored `m_XPThresholdPerLevel` list — it's a meta-progression curve, finite-ish, designer wants explicit control. The asymmetry is fine since the two modes have different progression shapes.

### 8. Delete dead `c_LevelSave` debug write (~3 min)

Inherited cruft, not a regression — confirmed both `c_LevelSave = "Level"` and `c_PlayerLevelSave = "Lvl"` existed in the initial commit, and `c_LevelSave` was already written-once / read-by-nobody before the candidate touched anything. Cleanup is just opportunistic.

`Constants.c_LevelSave = "Level"` is written once in [GameService.cs:134](Draw.ioTest/Draw.ioTest/Assets/Scripts/Services/GameService.cs:134) and read by nobody. The actually-used rank-level key is `c_PlayerLevelSave = "Lvl"`. Vestigial.

**Fix:** Delete the dead write *and* `Constants.c_LevelSave`, *and* the unused `m_DebugLevel` / `m_SaveCleared` fields. If a debug level-override is genuinely useful for testing booster progression, add a proper one later that writes to the active mode's prefixed `Lvl` key behind an explicit `m_OverrideLevel` bool.

```csharp
// Remove from GameService.Init():
#if UNITY_EDITOR
    if (m_SaveCleared == false) { m_SaveCleared = true; }
    PlayerPrefs.SetInt(Constants.c_LevelSave, m_DebugLevel);
#endif

// Remove fields:
#if UNITY_EDITOR
    public  int  m_DebugLevel = 1;
    private static bool m_SaveCleared = false;
#endif

// Remove from Constants.cs:
public const string c_LevelSave = "Level";
```

### 9. Move planning markdowns to `/plans` (~5 min)

Currently scattered at `Draw.ioTest/*.md`. They're not Unity-project files and clutter the project root.

Move to `plans/` at repo root via `git mv` (preserves history):
- `Draw.ioTest/PROJECT.md` → `plans/PROJECT.md`
- `Draw.ioTest/TASK1_BoosterGameMode.md` → `plans/TASK1_BoosterGameMode.md`
- `Draw.ioTest/TASK2_SkinSelectionScreen.md` → `plans/TASK2_SkinSelectionScreen.md`
- `Draw.ioTest/TASK3_DebugMenu.md` → `plans/TASK3_DebugMenu.md`
- `Draw.ioTest/PATTERNS_FeatureFlags.md` → `plans/PATTERNS_FeatureFlags.md`
- `Draw.ioTest/PROTOTYPE_00_BenchmarkHarness.md` → `plans/PROTOTYPE_00_BenchmarkHarness.md`
- `Draw.ioTest/PROTOTYPE_06_AtlasCamera.md` → `plans/PROTOTYPE_06_AtlasCamera.md`
- `Draw.ioTest/PROTOTYPE_12_Direct3DonUI.md` → `plans/PROTOTYPE_12_Direct3DonUI.md`
- `Draw.ioTest/PROTOTYPE_13_RuntimeFlipbook.md` → `plans/PROTOTYPE_13_RuntimeFlipbook.md`
- `TASK1_CLEANUP.md` → `plans/TASK1_CLEANUP.md` (this file)

---

## 🧹 Inherited cleanup tickets (not blocking resubmission)

Flagged in the candidate's own plan as out-of-scope. Tracking so they don't get lost.

- **`RankingService` is dead/half-built.** `IRankingService` declares zero methods; the class duplicates `StatsService`'s XP/level reads/writes; 25 `RankData` assets in `Resources/Ranks/` are loaded but no consumer reads them (`RankingView` pulls labels from `MainMenuView.m_Ratings[]`). Either delete or finish wiring.
- **`MainMenuView.m_Ratings[]`** is shared across modes. If booster wants distinct rank vocabulary, lift to a config.

---

## Definition of Done

- [ ] Booster button visible in main menu, wired to `OnBoosterButton`, APK rebuilt
- [ ] Classic match: AI floor scales with `StatsService.GetLevel()` (verified manually + by tests)
- [ ] Regression tests for Classic AI rubber-band + Booster AI per-level + fallback all green
- [ ] StatsService regression tests recovered from `722cb782`
- [ ] `SpeedMultiplier` tooltip in place
- [ ] Booster XP threshold computed by formula on `BoosterMode`, `m_XPToNextLevel` field removed from `BoosterLevelData`
- [ ] Dead `c_LevelSave` / `m_DebugLevel` / `m_SaveCleared` removed
- [ ] All planning markdowns under `/plans` via `git mv`
- [ ] APK demo: open menu → tap Booster → match runs with `BoosterLevel_1` → win → return → tap Booster → `BoosterLevel_2` loads
