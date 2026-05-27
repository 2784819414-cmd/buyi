# Release Checklist

## Package Hygiene

- Confirm the package is under `Packages/com.nting.ai-prefab-guard`.
- Confirm `Library/`, `Logs/`, `Temp/`, `.git/`, `.aiprefabguard-tools/`, and `Library/AIPrefabGuard/baseline.json` are not included in the upload.
- Run `Tools / Nting / AI Prefab Guard / Validate Package Hygiene`.
- Confirm no test verification change is left in `Assets/Scenes/SampleScene.unity`.
- Confirm source code is readable and under `Nting.AIPrefabGuard.Editor`.
- Confirm Editor code stays inside the `Editor` folder.

## Functional Verification

- First open creates a baseline under `Library/AIPrefabGuard/baseline.json`.
- A clean scan shows zero changed files since baseline.
- Modifying a `.unity` file shows a scene risk.
- Modifying a `.prefab` file shows a prefab risk.
- Adding a `.asset` file under a Chinese or space-containing path is detected.
- Deleting a `.meta` file is detected and Open is disabled.
- Files under `Assets/Nting/AIPrefabGuard/` are excluded.
- `Accept Current State As Baseline` clears the next scan.

## Asset Store Page

- Add screenshots showing the main window, risk summary, details panel, Markdown report, and Codex AI prompt.
- Add a 60-90 second demo video hosted outside the package.
- State clearly: local-first, no code upload, no AI API calls, baseline stored under `Library/`.
- Disclose that the tool is for risk review and does not automatically repair Unity assets.

## Documentation

- README complete.
- QuickStart complete.
- SampleRiskReport complete.
- Testing notes complete.
- Third-Party Notices complete.
