# Moonlit Glade — HD-2D JRPG

The game direction is now a Three Kingdoms historical HD-2D JRPG, with Liu Bei's storyline beginning at his early military beginnings and multiple faction viewpoints planned later. The main story follows historical sources, with fictional side stories. Exact starting dates, locations and whether historical outcomes can change remain undecided. The formal game title is undecided; Moonlit Glade remains the technical prototype name.

Read [the game project document](docs/PROJECT.md) for confirmed setting and open questions, [the development handoff](docs/DEVELOPMENT.md) for the verified baseline, and [CLAUDE.md](CLAUDE.md) for the Claude Code entry point. Current placeholder art and the slime encounter are not the final historical setting.

Unity 6.3 LTS (6000.3.26f1), URP 3D, Windows x64. Original pixel placeholders, orthographic camera, 3D collision and lighting. This is a first vertical prototype, not a production-ready RPG.

## First launch

1. In Unity Hub, sign in and activate a free Personal license (financial eligibility required).
2. Add this directory as a project and open with the pinned Editor. Allow package import and compilation.
3. Choose **JRPG → Create First Scene (only if absent)**. Open `Assets/Game/Scenes/Glade.unity` and press Play.
4. WASD/arrows move. Approach the green slime and press E. Click Attack to play a deterministic turn-based encounter; victory completes the quest.

The setup method creates URP settings, PNG sprites, materials, ScriptableObject data and a saved scene inside Unity. Until it runs successfully, these generated assets do not exist. Existing scenes are preserved.

## Automation (PowerShell)

```powershell
.\tools\Invoke-Unity.ps1 -Action Setup
.\tools\Invoke-Unity.ps1 -Action EditMode
.\tools\Invoke-Unity.ps1 -Action PlayMode
.\tools\Invoke-Unity.ps1 -Action Build
```

Pass `-Editor` if installed elsewhere. Each run uses a unique physical D drive output directory. Tests verify damage/asset isolation, turn ordering, quest counts and actual CharacterController collision. No test success is claimed until Unity emits passing results.

## Architecture

- `CharacterDefinition.cs`, `SkillDefinition.cs`, `QuestDefinition.cs`: static authoring data; `Definitions.cs`: separate mutable combat and quest state.
- `TurnBattle`: stable speed-based turn queue, skipping defeated combatants.
- `PlayerMover`, `FollowCamera`: movement and orthographic presentation.
- `DemoEncounter`: prototype-only input/UI bridge into combat and quest rules.
- `ProjectSetup`: reproducible, non-overwriting scene generation and Windows build entry point.

The sprite shader is unlit for crisp readable pixels; the 3D environment receives URP lighting. This is a visual foundation; animation, dialogue, persistence, content pipelines and full pixel-grid presentation are future work.

## Commercial use

Read LICENSES.md. Original placeholders have MIT permission. Unity Personal and Microsoft Unity/C# Dev Kit extensions have eligibility limits; the stack is free only while those conditions hold. No paid assets/services are required. Windows Mono is the first build target; IL2CPP and console delivery are not configured.
