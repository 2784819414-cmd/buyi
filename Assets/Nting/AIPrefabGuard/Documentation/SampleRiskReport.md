# AI Prefab Guard Risk Report

- Generated At: 2026-05-26 12:00:00
- Project Path: E:/Example/UnityProject
- Baseline Path: E:/Example/UnityProject/Library/AIPrefabGuard/baseline.json
- Baseline Created: 2026-05-26T03:00:00.0000000Z
- Baseline Updated: 2026-05-26T03:00:00.0000000Z
- Baseline File Count: 240
- Scan Status: Baseline scan completed. 3 changed file(s) found since baseline.
- Overall Risk: High
- Files Changed Since Baseline: 3
- High-risk Unity Files: 3
- Very High Count: 2
- High Count: 1

## Natural Language Summary

Current risk is High. 3 file(s) changed since the local baseline, including 3 high-risk Unity asset file(s) that should be manually reviewed before accepting the new baseline.

## Review Scope

- This report is generated locally inside the Unity Editor.
- The scan compares current Assets/ files against a local baseline stored under Library/AIPrefabGuard.
- No project files are uploaded.
- No AI API is called by AI Prefab Guard.
- The tool does not automatically fix, rewrite, or regenerate Unity assets.

## High-risk Files

### Assets/Characters/Hero.prefab

- Risk: VeryHigh
- Type: Prefab
- Baseline Status: M
- Change Kind: Modified
- Reason: Prefab serialization changed. Hierarchy, components, references, and overrides may have been altered.

Checklist:
- [ ] Open Prefab Mode and verify hierarchy, components, serialized references, and overrides.
- [ ] Check for Missing Script warnings and broken object references.
- [ ] Run the smallest relevant Play Mode flow that uses this prefab.

### Assets/Scenes/Main.unity

- Risk: VeryHigh
- Type: Scene
- Baseline Status: M
- Change Kind: Modified
- Reason: Scene serialization changed. Scene objects, cameras, lights, UI, and logic objects may have been altered.

Checklist:
- [ ] Open the scene and verify key GameObjects, cameras, lights, UI, and bootstrap objects.
- [ ] Check Console for missing scripts, missing references, or import warnings.
- [ ] Enter Play Mode and verify the scene starts without unexpected behavior.

### Assets/Scripts/Runtime.asmdef

- Risk: High
- Type: AssemblyDefinition
- Baseline Status: M
- Change Kind: Modified
- Reason: Assembly definition changed. Compilation boundaries, platform filters, and dependencies may have been altered.

Checklist:
- [ ] Review references, includePlatforms, excludePlatforms, and autoReferenced.
- [ ] Confirm Editor-only and Runtime assemblies are still separated correctly.
- [ ] Wait for Unity compilation and resolve all Console errors before accepting the new baseline.

## AI-readable Summary

```text
overall_risk=High
changed_since_baseline_count=3
high_risk_file_count=3
baseline_updated_at=2026-05-26T03:00:00.0000000Z
file=Assets/Characters/Hero.prefab;risk=VeryHigh;type=Prefab;change=Modified
file=Assets/Scenes/Main.unity;risk=VeryHigh;type=Scene;change=Modified
file=Assets/Scripts/Runtime.asmdef;risk=High;type=AssemblyDefinition;change=Modified
```
