# AI Prefab Guard for Unity

AI Prefab Guard is a local-first Unity Editor tool for reviewing high-risk Unity asset changes after AI-assisted work.

It creates a local baseline snapshot of the current `Assets/` folder, then scans later changes against that baseline:

- `.prefab`
- `.unity`
- `.meta`
- `.asset`
- `.asmdef`

The tool does not upload code, call AI APIs, modify assets, repair files, or rewrite project content.

## Install

AI Prefab Guard supports two installation styles:

- UPM/local package: copy `com.nting.ai-prefab-guard` into your Unity project's root `Packages` folder.
- Traditional Unity package: import the Asset Import build with `Assets / Import Package / Custom Package...`.

Do not install both styles into the same Unity project at the same time, because that would compile the same Editor code twice.

## Open The Tool

In Unity, open:

`Tools / Nting / AI Prefab Guard`

When the window opens, it creates or loads a local baseline at:

`Library/AIPrefabGuard/baseline.json`

After AI or another tool changes your project, click `Scan Changes Since Baseline`.

If the reviewed changes are correct, click `Accept Current State As Baseline` so future scans compare against the accepted state.

## What It Checks

The v1 scope is intentionally small:

- Scan `Assets/` only.
- Compare current files against a local baseline snapshot.
- Detect added, modified, and deleted files.
- Exclude the imported AI Prefab Guard tool folder under `Assets/Nting/AIPrefabGuard/`.
- Identify high-risk Unity asset files.
- Show risk level, file type, baseline status, reason, and review notes.
- Copy or export a Markdown risk report.
- Copy a conservative AI rework prompt.
- Ping or open assets in Unity when possible.

## Risk Rules

- No high-risk Unity files: `Low`
- Only `.asset` or `.asmdef`: `Medium`
- Any `.prefab`, `.unity`, or `.meta`: `High`

Per-file risk levels:

- `.prefab`: `VeryHigh`
- `.unity`: `VeryHigh`
- `.meta`: `VeryHigh`
- `.asset`: `High`
- `.asmdef`: `High`

## Local-first Promise

AI Prefab Guard runs locally in the Unity Editor.

It does not:

- Upload project files.
- Connect to cloud analysis services.
- Call OpenAI, Claude, Cursor, or Codex APIs.
- Automatically modify Unity serialized assets.
- Automatically fix or roll back changes.

## Trust Boundary

AI Prefab Guard is designed as a local baseline risk review assistant.

- It reads files under `Assets/` to build hashes for comparison.
- It writes only baseline metadata under `Library/AIPrefabGuard/` and reports when you explicitly export them.
- It never writes to `.prefab`, `.unity`, `.asset`, `.meta`, or `.asmdef` files.
- It never uploads project files.
- It never calls an AI API.
- It never trains a model.

## Current Limitations

- v1 scans `Assets/` only.
- It does not scan `ProjectSettings/` or `Packages/manifest.json`.
- It does not perform full Unity YAML structure parsing.
- It does not visualize complete diffs.
- It does not replace manual Unity review.
