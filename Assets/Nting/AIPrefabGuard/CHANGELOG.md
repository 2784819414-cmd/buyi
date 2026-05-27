# Changelog

## 0.1.0

Initial implementation for the baseline snapshot product direction.

- Added UPM-ready package structure.
- Added `Tools / Nting / AI Prefab Guard` Editor window.
- Added local baseline storage under `Library/AIPrefabGuard/baseline.json`.
- Added `Assets/` snapshot scanning with path, size, timestamp, and SHA-256 content hash.
- Added baseline diff detection for added, modified, and deleted files.
- Excluded imported tool files under `Assets/Nting/AIPrefabGuard/` from risk scans.
- Added classification for `.prefab`, `.unity`, `.meta`, `.asset`, and `.asmdef`.
- Added risk summary, Markdown report generation, and conservative AI handoff prompt generation.
- Added asset `Ping` and conditional `Open` actions.
- Added natural-language current risk summaries in English and Chinese.
- Added `Accept Current State As Baseline` and `Reset Baseline` workflows.
- Added a package hygiene validator and Unity menu command for pre-release checks.
- Added EditMode tests for baseline scanning, risk classification, summaries, reports, prompts, and package hygiene.
