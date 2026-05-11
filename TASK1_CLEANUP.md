# Task 1 — Cleanup Plan

Punch list to close the gaps found in the Booster Game Mode review. Ordered by severity. Estimated total: ~2h.

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

### 2. Restore Classic AI rubber-band difficulty (~25 min)

**Problem:** Original [IAPlayer.cs:63](Draw.ioTest/Draw.ioTest/Assets/Scripts/Gameplay/Players/IAPlayer.cs:63) was:
```csharp
m_Difficulty = Random.Range(Mathf.Clamp01(StatsService.GetLevel() / 2f), 1f);
```
After refactor it reads static `MatchSettings.m_AIDifficultyMin/Max`. [ClassicMode.asset](Draw.ioTest/Draw.ioTest/Assets/Scripts/Configs/ClassicMode.asset) hardcodes `min=0, max=1` → uniform random. The rubber band is dead in Classic mode.

**Fix (recommended):** Make `AIDifficultyMin` a virtual on `GameMode`, override in `ClassicMode` to call into stats.

```csharp
// GameMode.cs
public abstract float GetAIDifficultyMin(IStatsService _Stats);
public virtual float GetAIDifficultyMax(IStatsService _Stats) => 1f;
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

**Acceptance:** Play 5 losing classic matches → next match's AI floor is `0`. Play 5 winning classic matches → AI floor is `0.5`. Matches original behavior.

---

## 🟡 Should-fix — close before sign-off

### 3. Decouple `IStatsService` from `GameMode` (~20 min)

**Problem:** `IStatsService.SetActiveMode(GameMode)` lets `StatsService.XPToNextLevel()` call back into `m_ActiveMode.GetXPForLevel(...)`. `GameMode.OnPreEndGame/OnPostEndGame` also take `IStatsService`. Mutual coupling that didn't exist pre-refactor.

**Fix:** Pass the prefix only, and move the XP curve lookup to the call site.

```csharp
// IStatsService.cs
void SetActiveStatsPrefix(string _Prefix);
int  XPToNextLevel(int _XPForCurrentLevel);   // takes the threshold as arg
```

```csharp
// StatsService.GainXP
int threshold = m_ActiveMode.GetXPForLevel(GetPlayerLevel() - 1);  // ← move OUT
// caller now passes the threshold in
```

Cleanest version: keep `GainXP()` parameterless but have `GameService` set the threshold via an injected `Func<int,int>` callback on `SetGameMode`. Or just keep `SetActiveMode` but rename to make the coupling explicit and document why.

**Acceptance:** `StatsService` compiles with zero references to the `GameMode` type. `using` for `GameMode` removed.

---

### 4. Leave a feature-flag hook for Task 3 (~15 min)

**Problem:** PDF: *"features should function independently and it should be possible to individually enable/disable them."* Task 1 didn't leave a toggle hook. `OnBoosterButton` always works regardless of whether the feature is "on".

**Fix (lightweight, defer the storage to Task 3):**

```csharp
// Constants.cs
public const string c_BoosterModeEnabledSave = "Feature_BoosterMode";

// MainMenuView.cs — button visibility
m_BoosterButton.gameObject.SetActive(
    PlayerPrefs.GetInt(Constants.c_BoosterModeEnabledSave, 1) == 1);

// MainMenuView.cs — defensive guard
public void OnBoosterButton()
{
    if (PlayerPrefs.GetInt(Constants.c_BoosterModeEnabledSave, 1) == 0) return;
    if (GameService.currentPhase == GamePhase.MAIN_MENU)
        GameService.StartBoosterMode();
}
```

Task 3's debug menu just flips this prefs key. Default `1` (on) so nothing changes today.

**Acceptance:** Setting `Feature_BoosterMode=0` in PlayerPrefs hides the button on next menu entry; setting `=1` restores it.

---

## 🟢 Nice-to-have — polish

### 5. Document the SpeedBoost ≤ 2× ceiling (~2 min)

`Player.GetSpeed()` clamps at `c_MaxSpeed = 100` with base `m_Speed = 50`. Any `m_SpeedMultiplier > 2` silently no-ops.

```csharp
// PowerUp_SpeedBoost.cs
[Tooltip("Effective multiplier is capped at 2x by Player.c_MaxSpeed.")]
public float m_SpeedMultiplier = 1.5f;
```

Or raise `c_MaxSpeed` if higher boosts are wanted by design.

### 6. Restore the StatsService regression tests (~5 min)

Commit `722cb782` ("Add regression tests for StatsService before Phase 1 refactor") added them; they're not in the final tree. Recover them — the parallel-prefix pattern is exactly the kind of invariant that breaks silently. `git show 722cb782 -- '**/*Test*.cs'` to find what was deleted.

### 7. Author distinct booster XP thresholds (~5 min)

All 5 `BoosterLevel_*.asset` files have `m_XPToNextLevel: 100`. If progression is meant to feel like climbing, set them to e.g. `100, 150, 220, 320, 480, 700` (fallback). Otherwise drop the per-level field and store a single curve on `BoosterMode`.

### 8. Gate the editor `m_DebugLevel` overwrite (~5 min)

[GameService.cs:128-135](Draw.ioTest/Draw.ioTest/Assets/Scripts/Services/GameService.cs:128) unconditionally writes `PlayerPrefs.SetInt(Constants.c_LevelSave, m_DebugLevel)` on every editor entry. With booster progression, this masks the current `Booster_Lvl` during editor testing. Either:
- Only write when `m_DebugLevel >= 0` (sentinel for "off"), or
- Move the override behind a separate `m_OverrideLevel` bool.

---

## 🧹 Cleanup tickets (do not block resubmission)

These were flagged in the candidate's own plan as out-of-scope. Tracking them here so they don't get lost.

- **`RankingService` is dead/half-built.** `IRankingService` declares zero methods; the class duplicates `StatsService`'s XP/level reads/writes; 25 `RankData` assets in `Resources/Ranks/` are loaded but no consumer reads them (`RankingView` pulls labels from `MainMenuView.m_Ratings[]`). Either delete or finish wiring.
- **`MainMenuView.m_Ratings[]`** is shared across modes. If booster wants distinct rank vocabulary, lift to a config.
- **Stray planning artifacts in repo root** (`PROTOTYPE_00`, `_06`, `_12`, `_13`, `PATTERNS_FeatureFlags.md`) — keep `TASK1_*.md` if useful as a design doc, move/delete the rest before code review.

---

## Definition of Done

- [ ] Booster button visible in main menu, wired to `OnBoosterButton`, APK rebuilt
- [ ] Classic match: AI floor scales with `StatsService.GetLevel()` (verified by 5-loss / 5-win cycles)
- [ ] `StatsService` has no `using` for `GameMode`; `IStatsService` exposes prefix-only API
- [ ] Booster button respects `Feature_BoosterMode` prefs flag (default on)
- [ ] PlayerPrefs inspector: `BestScore` and `Booster_BestScore` are distinct after playing both modes
- [ ] APK demo: open menu → tap Booster → match runs with `BoosterLevel_1` → win → return → tap Booster → `BoosterLevel_2` loads
