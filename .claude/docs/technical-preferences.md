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

- **Target Platforms**: PC (Windows x64, Mono scripting backend)
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

- **Target Framerate**: [TO BE CONFIGURED]
- **Frame Budget**: [TO BE CONFIGURED]
- **Draw Calls**: [TO BE CONFIGURED]
- **Memory Ceiling**: [TO BE CONFIGURED]

## Testing

- **Framework**: Unity Test Framework (EditMode: `Assets/Tests/EditMode/`, PlayMode: `Assets/Tests/PlayMode/`)
- **Minimum Coverage**: [TO BE CONFIGURED]
- **Required Tests**: Balance formulas, gameplay systems, networking (if applicable)

## Forbidden Patterns

<!-- Add patterns that should never appear in this project's codebase -->
- DOTS / ECS (Entities), Netcode, Unity Gaming Services, IAP, ads SDKs — out of scope
- Gameplay rules inside MonoBehaviours — keep rules deterministic and separate from presentation
- Mutating ScriptableObject definition assets at runtime — copy into runtime state

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
- [No ADRs yet — use /architecture-decision to create one]

## Engine Specialists

<!-- Written by /setup-engine when engine is configured. -->
<!-- Read by /code-review, /architecture-decision, /architecture-review, and team skills -->
<!-- to know which specialist to spawn for engine-specific validation. -->

- **Primary**: unity-specialist
- **Language/Code Specialist**: unity-specialist
- **Shader Specialist**: unity-shader-specialist
- **UI Specialist**: unity-ui-specialist
- **Additional Specialists**: unity-addressables-specialist (DOTS not used — no unity-dots-specialist)
- **Routing Notes**: Code root is `Assets/`; route by file type per the table below

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
