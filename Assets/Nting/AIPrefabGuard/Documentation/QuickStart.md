# QuickStart

## 1. Install

Choose one installation style:

- UPM/local package: copy `com.nting.ai-prefab-guard` into the Unity project's root `Packages` folder.
- Traditional Unity package: use `Assets / Import Package / Custom Package...` and select the Asset Import `.unitypackage`.

Do not install both styles into the same project at the same time.

## 2. Open

Open the Unity menu:

`Tools / Nting / AI Prefab Guard`

The first open creates a local baseline snapshot at:

`Library/AIPrefabGuard/baseline.json`

This file is local cache metadata. It should not be uploaded as part of the package.

## 3. Scan After AI Edits

After AI or another tool changes the project, click:

`Scan Changes Since Baseline`

The scan compares current `Assets/` files against the saved baseline. It does not modify assets, call AI APIs, or upload project files.

## 4. Review

Check the natural-language summary first, then review:

- `Risk Compared With Baseline`
- `High-risk Unity files found`
- `Very High count`
- `High count`
- `Files changed since baseline`

Select each listed file to see the reason, manual verification checklist, and semantic notes.

Use `Ping` to locate an asset in the Project window.

Use `Open` when available to open the asset directly. Deleted or missing files cannot be opened and should be reviewed through the previous baseline, project history, or backup context.

## 5. Accept New Baseline

After you manually confirm the current changes are safe, click:

`Accept Current State As Baseline`

This records the current `Assets/` state as the new trusted baseline. A new scan should then show zero changed files.

Use `Reset Baseline` only when you want to discard the saved baseline and recreate it from the current project state.

## 6. Export

Use:

- `Copy Markdown Risk Report` to copy a Markdown report.
- `Export Markdown Report` to choose a `.md` destination.
- `Copy Codex AI Rework Prompt` to ask Codex AI to explain or redo risky asset edits conservatively.

## Common States

`Baseline ready`

The current project has a saved baseline. Scan after AI edits.

`Current project Assets match the local baseline`

No changed files were found since the baseline.

`No high-risk Unity asset files are currently listed`

Changes may exist since the baseline, but no `.prefab`, `.unity`, `.meta`, `.asset`, or `.asmdef` high-risk files were detected.
