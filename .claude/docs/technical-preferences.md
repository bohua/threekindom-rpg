# Technical Preferences

<!-- project.yaml at the repo root is the machine-readable source of truth for
     engine, specialists, naming, performance, platform, and testing.framework.
     This file is the human-readable LEGACY FALLBACK: agents and skills resolve
     each key from project.yaml first and fall back here only when the
     project.yaml key is absent. /setup-engine dual-writes both.
     Forbidden patterns and allowed libraries are NOT migrated — they live only
     in this file. Populated by /setup-engine; updated as decisions are made. -->

## Engine & Language

- **Engine**: Unity 6.3 LTS (6000.3)
- **Language**: C#
- **Rendering**: URP (Universal Render Pipeline 17.3)
- **Physics**: PhysX (Unity built-in 3D physics)

## Input & Platform

<!-- Written by /setup-engine. Read by /ux-design, /ux-review, /test-setup, /team-ui, and /dev-story -->
<!-- to scope interaction specs, test helpers, and implementation to the correct input methods. -->

- **Target Platforms**: PC (Windows 10/11 x64). Dev builds Mono (`Win64-Dev` build profile), release builds IL2CPP (`Win64-Release`) — ADR-0001. Steam Deck (Proton) is a post-V1 compatibility target; consoles, macOS, mobile and Web are out of scope until a new ADR
- **Input Methods**: Keyboard/Mouse, Gamepad (via Input System)
- **Primary Input**: Mixed
- **Gamepad Support**: Full
- **Touch Support**: None
- **Platform Notes**: Single-player, offline. No certification tier (cert_tier: none).

## Naming Conventions

- **Classes**: PascalCase
- **Variables**: camelCase
- **Signals/Events**: PascalCase On<Event> (e.g. `OnTurnStarted`)
- **Files**: PascalCase (one public type per file, file name = type name)
- **Scenes/Prefabs**: PascalCase
- **Constants**: [TO BE CONFIGURED]

## Performance Budgets

- **Target Framerate**: 60 fps at 1920×1080 (provisional, ADR-0001; minimum-spec validation deferred to ADR-0005)
- **Frame Budget**: 16.6 ms
- **Draw Calls**: [TO BE CONFIGURED]
- **Memory Ceiling**: 2 GB process memory (provisional, ADR-0001). Baseline 2026-10-19: ~190 MB working set, see `production/migration/adr-0001-baseline.md`

## Testing

- **Framework**: Unity Test Framework (EditMode: `Assets/Tests/EditMode/`, PlayMode: `Assets/Tests/PlayMode/`)
- **Minimum Coverage**: [TO BE CONFIGURED]
- **Required Tests**: Balance formulas, gameplay systems, networking (if applicable)

## Forbidden Patterns

<!-- Add patterns that should never appear in this project's codebase -->
- DOTS / ECS (Entities), Netcode, Unity Gaming Services, IAP, ads SDKs — out of scope
- Gameplay rules inside MonoBehaviours — keep rules deterministic and separate from presentation
- Mutating ScriptableObject definition assets at runtime — copy into runtime state
- Legacy `UnityEngine.Input` class (`Input.GetKey*`, `Input.GetAxis*`, `Input.mousePosition` …) — use Input System actions (`Assets/_Project/Settings/GameControls.inputactions`); `StandaloneInputModule` — use `InputSystemUIInputModule` (ADR-0001)
- `UnityEngine` references in `ThreeKingdoms.Core` (asmdef has `noEngineReferences`); assembly references against the ADR-0001 dependency graph (ADR-0001)
- Game code in the `Assets/` root or `Assembly-CSharp`; third-party code without its own asmdef (ADR-0001)
- Custom URP render passes without RenderGraph (`RecordRenderGraph`); URP compatibility mode (ADR-0001)
- `System.Diagnostics.Process` and other APIs unsupported under IL2CPP in runtime code (found by the ADR-0001 IL2CPP smoke build)
- Save files outside `Application.persistentDataPath` (ADR-0001)

## Allowed Libraries / Addons

<!-- Add approved third-party dependencies here -->
Unity packages (installed in `Packages/manifest.json` unless noted):
- URP (`com.unity.render-pipelines.universal`) and Shader Graph (bundled with URP)
- Input System (`com.unity.inputsystem`)
- Cinemachine 3 (`com.unity.cinemachine`)
- Timeline (`com.unity.timeline`)
- 2D Sprite (`com.unity.2d.sprite`), 2D Aseprite Importer (`com.unity.2d.aseprite`)
- Test Framework (`com.unity.test-framework`)
- Newtonsoft Json (`com.unity.nuget.newtonsoft-json`)

Runtime UI: UI Toolkit (fallback uGUI + TextMeshPro only where UI Toolkit is insufficient)

Third-party (approved, not yet installed — record source and license in LICENSES.md when added):
- Yarn Spinner — dialogue
- DOTween — tweening
- UniTask — async/await

## Architecture Decisions Log

<!-- Quick reference linking to full ADRs in docs/architecture/ -->
- [ADR-0001](../../docs/architecture/adr-0001-engine-platform-project-structure.md) — Engine (Unity 6.3 LTS, URP Universal Renderer, Input System only), platform targets (Windows x64 first, Steam Deck later), build profiles, `Assets/_Project` layout and `ThreeKingdoms.*` assemblies. Accepted 2026-10-18; migration steps 1–5 done 2026-10-19
- [ADR-0002](../../docs/architecture/adr-0002-battle-core.md) — Battle rules core: pure C# in `ThreeKingdoms.Core`, SO → spec via `ToRuntime()`, `CombatantInput` in / event list + `BattleResult` out (no out-of-battle writes), data-driven troop table incl. enemy-only types, `IEnemyAi` policies, integer fixed-point damage. Accepted 2026-10-19

## Engine Specialists

<!-- Written by /setup-engine when engine is configured. -->
<!-- Read by /code-review, /architecture-decision, /architecture-review, and team skills -->
<!-- to know which specialist to spawn for engine-specific validation. -->

- **Primary**: unity-specialist
- **Language/Code Specialist**: unity-specialist
- **Shader Specialist**: unity-shader-specialist
- **UI Specialist**: unity-ui-specialist
- **Additional Specialists**: unity-addressables-specialist (DOTS not used — no unity-dots-specialist)
- **Routing Notes**: Code root is `Assets/_Project/Scripts/` (one folder per `ThreeKingdoms.*` assembly), tests in `Assets/Tests/`; route by file type per the table below. Unity 6 APIs post-date model training data — verify against `docs/engine-reference/unity/` and the Unity 6.3 docs (ADR-0001)

### File Extension Routing

<!-- Skills use this table to select the right specialist per file type. -->
<!-- If a row says [TO BE CONFIGURED], fall back to Primary for that file type. -->

| File Extension / Type | Specialist to Spawn |
|-----------------------|---------------------|
| Game code (`.cs`, `.asmdef`) | unity-specialist |
| Shader / material files (`.shadergraph`, `.shader`, `.hlsl`, `.mat`) | unity-shader-specialist |
| UI / screen files (`.uxml`, `.uss`, UI prefabs) | unity-ui-specialist |
| Scene / prefab / level files (`.unity`, `.prefab`) | unity-specialist |
| Addressables groups / asset bundles | unity-addressables-specialist |
| General architecture review | Primary |
