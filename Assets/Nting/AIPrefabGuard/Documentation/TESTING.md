# Testing

## EditMode Tests

Run from the project root:

```powershell
& 'D:\unity6000\Editor\Unity.exe' -batchmode -nographics -projectPath 'E:\凝霆的小创作集合\凝霆的小创作3\tool' -runTests -testPlatform EditMode -testResults 'E:\凝霆的小创作集合\凝霆的小创作3\tool\Logs\AIPrefabGuardEditModeResults.xml' -logFile 'E:\凝霆的小创作集合\凝霆的小创作3\tool\Logs\AIPrefabGuardEditModeTests.log'
```

Current focused coverage:

- Baseline snapshot creation under `Library/AIPrefabGuard/baseline.json`.
- Added, modified, and deleted file detection since baseline.
- Chinese paths and paths with spaces.
- Excluding the imported tool path under `Assets/Nting/AIPrefabGuard/`.
- Accepting current state as a new baseline and clearing future scan results.
- Required high-risk file extension classification.
- Overall risk for Very High asset types.
- Overall risk for only `.asset` and `.asmdef` changes.
- `.meta` paired asset insight.
- Natural-language risk summary for detected high-risk files.
- Markdown report inclusion of baseline status and natural-language summary.
- Conservative Codex prompt boundaries for Unity serialized assets.
- Package hygiene validation for forbidden generated folders and archive files.

## Manual Smoke Test

1. Open `Tools / Nting / AI Prefab Guard`.
2. Confirm the window shows `Baseline ready`.
3. Modify a `.prefab`, `.unity`, `.meta`, `.asset`, or `.asmdef` file under `Assets/`.
4. Click `Scan Changes Since Baseline`.
5. Verify the finding appears with reason, checklist, and output actions.
6. Click `Accept Current State As Baseline`.
7. Scan again and confirm the changed-file count is zero.

## Trust Boundary Checks

- The tool should not create or modify files under `Assets/` during scanning.
- The baseline cache should be under `Library/AIPrefabGuard/`.
- Release packages must not include `Library/AIPrefabGuard/baseline.json`.
