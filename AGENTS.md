# HD-2D JRPG

- Read `docs/PROJECT.md` for confirmed game direction and unresolved design decisions. Keep prototype content separate from the historical setting. Read `docs/DEVELOPMENT.md` for development entry points and evidence boundaries.

- Unity 6000.3 LTS, URP 17.3, C#; Windows x64 first.
- Keep gameplay rules deterministic and separate from MonoBehaviour presentation.
- Character, skill and quest definitions are ScriptableObjects; mutable runtime state must not modify assets.
- Preserve .meta files; never commit Library/, Temp/, Logs/, builds, credentials or licenses.
- Use original placeholders. Record every imported asset's source and commercial license in LICENSES.md.
- Run EditMode and PlayMode tests and a Windows build before claiming verified delivery.
- Final builds and large evidence go to physical D:\BuildArtifacts\Codex\hd2d-jrpg\<run-id>.
- Do not alter ../sources/. Do not upgrade Unity or packages without recording the reason.
