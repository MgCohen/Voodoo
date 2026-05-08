# Draw.io — Technical Test (Core Studios / Voodoo)

## What this is
Technical assignment for a Game Developer position. Implement 3 features for Draw.io, a mobile battle royale drawing game. Features must be independently toggleable. ~8 hours total.

Submission: essential files + Android APK + playthrough video → victor.mutuale@voodoo.io

---

## The Game
8 players (1 human, 7 AI) control brushes that paint terrain. Percentage of terrain painted = score. Lowest-ranked player is eliminated every 12 seconds. Last player standing wins.

- **Engine**: Unity 2022.3.54f1
- **DI**: Zenject (services bound as singletons via `ManagersInstaller`)
- **Entry point**: `Game.unity` (only scene) → `ProjectContext` (Zenject) → `SceneLoadObject` bridges Unity lifecycle to services
- **Game loop**: `SceneLoadObject.Update()` → `SceneEventsService.TriggerOnUpdate()` → `GameService.OnUpdate()`
- **Phases**: `MAIN_MENU → LOADING → GAME → PRE_END → END` → scene reload

---

## Existing Architecture

### Services (Zenject DI, `BindInterfacesAndSelfTo<>().FromSubContainerResolve().AsSingle()`)
| Service | Interface | Config | Purpose |
|---------|-----------|--------|---------|
| GameService | IGameService | GameConfig | Core game loop, player spawning, power-up spawning, phase transitions |
| BattleRoyaleService | IBattleRoyaleService | BattleRoyaleConfig | Elimination timer, ranking, save mechanic |
| TerrainService | ITerrainService | TerrainConfig | Terrain selection, painting, color tracking |
| StatsService | IStatsService | StatsConfig | XP, level, difficulty (rolling 5-game avg), FavoriteSkin |
| RankingService | IRankingService | RankingConfig | Rankings |
| MapService | IMapService | — | Spatial grid for collision queries |
| SceneEventsService | ISceneEventsService | — | Bridges Unity Awake/Start/Update to services |

### Singletons (no DI, `FindObjectOfType` or static Instance)
UI views, `MobileHapticManager`, `BrushMainMenu`, `ScreenShaker`, `PoolSingleton`

### Data/Config split
- **Configs** → `Scripts/Configs/`, named `{Feature}Config`, injected via Zenject subcontainers
- **Data** → `Scripts/Gameplay/Data/`, named `{Type}Data`, loaded from `Resources/` via `Resources.LoadAll<T>()`
- **Resources folders**: `Brushs/`, `Skins/`, `Colors/`, `PowerUps/`, `Terrains/`, `PlayerNames/`

### UI patterns
- **View\<T>** — `SingletonMB<T>`, CanvasGroup alpha fade, responds to `onGamePhaseChanged`
- **SettingsPanel** — plain MonoBehaviour, Animator `SetBool("Visible")`, scene-wired buttons, no DI
- **3D in UI** — world-space 3D objects rendered by same camera (no RenderTextures)
- **Brush display** — `BrushMainMenu.Set(SkinData)` instantiates prefab, parents, applies color. `BrushRotation` auto-rotates at 90deg/s

### Key: NO per-match configuration exists
Every match is identical. Duration, player count, power-up rates, AI difficulty — all hardcoded as consts in `Constants.cs` and `GameService`. No level concept in the main game.

---

## Coding Conventions

### Naming
| Prefix | Meaning | Example |
|--------|---------|---------|
| `m_` | Member variable | `m_Players`, `m_TerrainService` |
| `c_` | Const | `c_MinSpeed`, `c_MaxPowerUpRate` |
| `s_` | Static | `s_PlayerCount`, `s_Vibrate` |
| `_PascalCase` | Method parameter | `_Player`, `_GamePhase`, `_InOrOut` |

- Classes/methods: PascalCase
- Local variables: camelCase
- Public fields exposed directly (not `[SerializeField] private` — the codebase uses `public` fields for inspector exposure)
- One class per file, `#region` markers for logical grouping

### DI Pattern
- Services use `[Inject] public void Construct(...)` — never constructor injection
- MonoBehaviour children needing DI use `[Inject] public void ChildConstruct(...)` (see `PowerUp_PaintBomb`)
- ScriptableObjects use `[CreateAssetMenu(fileName = "...", menuName = "...")]`

### Event Pattern
- C# `event Action<T>` delegates on services (e.g. `onGamePhaseChanged`, `onEndGame`, `onScoresCalculated`)
- Subscribers register in `Awake()`, unregister in `OnDestroy()`

---

## Implementation Reference Patterns

### PowerUp Recipe
Base class: `PowerUp : MappedObject` — handles spawn animation (scale lerp), map registration, collision via `OnPlayerTouched`, auto-destruction after particles finish.

**Simple pattern** (`PowerUp_SizeUp`):
```
override OnPlayerTouched → call base → invoke one method on Player
```

**DI pattern** (`PowerUp_PaintBomb`):
```
[Inject] ChildConstruct for service access → override OnPlayerTouched → does NOT call base (handles cleanup manually) → uses ITerrainService.FillCircle
```

**Spawning pipeline**: `GameService.OnUpdate()` → timer check → picks random `PowerUpData` from `m_PowerUps` → `PopObjectRandomly(prefab)` → `DiContainer.InstantiatePrefab` at random map position.

**To add a new PowerUp**: create C# class + prefab (with MeshRenderer, 2 ParticleSystems, shadow GO) + `PowerUpData` asset in `Resources/PowerUps/`.

### Player Bonus API
- `AddSizeUp(factor, duration)` — sets `m_SizeFactor`, runs `BonusCoroutine` to reset after duration
- `EBonus` enum has `SIZE_UP` — add `SPEED_UP` for SpeedBoost
- Speed: `m_Speed` field, clamped between `c_MinSpeed` (50) and `c_MaxSpeed` (100)
- NO `AddSpeedBoost()` exists yet — must be added following the same coroutine pattern as `AddSizeUp`

### PaintBomb vs ColorBomb (Key Gotcha)
`PowerUp_PaintBomb` does NOT call `base.OnPlayerTouched()` — it handles its own cleanup (UnregisterMap, disable model, play particles, hide shadow) because it needs to defer destruction until `FillCircle` completes via callback. `PowerUp_ColorBomb` (random position variant) must follow this same manual-cleanup pattern.

---

## Existing Data Inventory

| Resource folder | Assets | Details |
|----------------|--------|---------|
| `Skins/` | Skin01–Skin06 (6) | Each = `ColorData` + `BrushData` ref |
| `Brushs/` | Brush_1–Brush_4 (4) | Each = `GameObject` prefab ref |
| `Colors/` | 8 assets | Blue, Cyan, DarkPurple, Green, Magenta, Orange, Pink, Purple, Yellow |
| `PowerUps/` | SizeUp, PaintBomb (2) | Each = probability int + prefab ref |
| `Terrains/` | Terrain0 (1) | |
| `Patterns/` | Pattern_1, Pattern_2 (2) | |
| `Zonings/` | Zoning_1–Zoning_4 (4) | |

Task 2 needs 12 skins total (2 brush models × 6 colors) — requires 6 new `SkinData` assets.

---

## Evaluation Criteria (from PDF)

| Task | What they assess | What they look at |
|------|-----------------|-------------------|
| Task 1 — Booster Mode | Adaptability with existing codebase, game feel, code structure | **Extensibility and stability** |
| Task 2 — Skin Screen | Understanding of existing codebase, save/load preferences, 3D visual integration | **Functionality, device optimization, UI integration, code clarity** |
| Task 3 — Debug Menu | — | **Clarity and ease of use** |

They will test on a **mobile device**. Treat it as a fully released game.

---

## The 3 Tasks

### Task 1 — Booster Game Mode (~3h)
Second game mode with own progression, infinite levels, 2 new boosters.
- Build level design system (new — extracting hardcoded values into ScriptableObjects)
- `PowerUp_SpeedBoost` (temp speed buff, follows `PowerUp_SizeUp` pattern)
- `PowerUp_ColorBomb` (random position fill, follows `PowerUp_PaintBomb` pattern)
- `IGameMode` interface, `ClassicGameMode` (wraps current hardcoded values), `BoosterGameMode` (reads from `BoosterLevelData`)
- Detailed plan: `TASK1_BoosterGameMode.md`

### Task 2 — Skin Selection Screen (~4h)
Scrollable screen with 12 skins (2 models × 6 colors), 3D rotating previews.
- Follow world-space 3D pattern (no RenderTextures) for hero preview
- Follow SettingsPanel pattern (MonoBehaviour + Animator), not View\<T>
- Grid with 2D thumbnails + ScrollRect (first in this project)
- Reuse `BrushMainMenu.Set()` for hero preview, `StatsService.FavoriteSkin` for persistence
- Detailed plan: `TASK2_SkinSelectionScreen.md`

### Task 3 — Debug Menu (~1h)
Toggle Features 1 & 2 on/off. All off = original game.
- Follow SettingsPanel pattern (MonoBehaviour + Animator + Image buttons with sprite swap)
- How consumers read the flags is an open question: `PATTERNS_FeatureFlags.md`
- Detailed plan: `TASK3_DebugMenu.md`

---

## Open Questions
- **Feature flag pattern** — how DebugPanel communicates toggle state to consumers. Four options analyzed, decision deferred until Features 1 & 2 reveal consumer structure. See `PATTERNS_FeatureFlags.md`

---

## Documents
| File | Purpose |
|------|---------|
| `PROJECT.md` | This file — canonical reference |
| `TASK1_BoosterGameMode.md` | Implementation plan for Task 1 |
| `TASK2_SkinSelectionScreen.md` | Implementation plan for Task 2 |
| `TASK3_DebugMenu.md` | Implementation plan for Task 3 |
| `PATTERNS_FeatureFlags.md` | Open question on flag-reading pattern |

---

## Build Order
1. Constants.cs — save keys (5 min)
2. DebugPanel — UI + script + animator (45 min)
3. MainMenuView — debug button + feature visibility (30 min)
4. PowerUp_SpeedBoost + PowerUp_ColorBomb (45 min)
5. BoosterLevelData + BoosterModeConfig (45 min)
6. IGameMode + ClassicGameMode + BoosterGameMode (1h)
7. GameService integration (30 min)
8. SkinData assets — create 6 new (15 min)
9. SkinSelectionScreen + SkinCell (2h)
10. MainMenuView — skin button + flag wiring (30 min)
11. Polish + device testing (1h)
12. Record video + build APK (30 min)
