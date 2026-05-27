# Asset Store Listing Draft

## Product Name

AI Prefab Guard

## Short Description

Local-first Unity Editor tool that flags risky prefab, scene, meta, asset, and asmdef changes after AI-assisted edits.

## Long Description

AI Prefab Guard helps Unity developers review AI-assisted changes before accepting them as the new trusted project state.

When Codex, Cursor, Claude Code, or another AI coding assistant modifies a Unity project, code changes are not the only risk. AI tools can also touch serialized Unity assets such as prefabs, scenes, meta files, ScriptableObject assets, and assembly definitions. Those changes can break references, alter scene setup, change GUIDs, or shift assembly boundaries.

AI Prefab Guard creates a local baseline snapshot of `Assets/`, then scans later changes against that baseline inside the Unity Editor. It explains the risk in plain language, gives you a focused review list, exports a Markdown report, and provides a conservative Codex AI handoff prompt.

## Key Features

- Runs locally inside the Unity Editor.
- Creates a local baseline under `Library/AIPrefabGuard/`.
- Scans `Assets/` changes since the saved baseline.
- Detects `.prefab`, `.unity`, `.meta`, `.asset`, and `.asmdef` changes.
- Excludes the imported tool folder under `Assets/Nting/AIPrefabGuard/`.
- Explains current risk in plain language.
- Shows file-level reason, manual checklist, and semantic notes.
- Copies or exports a Markdown risk report.
- Copies a conservative Codex AI handoff prompt.
- Does not upload project files.
- Does not call any AI API.
- Does not automatically rewrite or repair Unity assets.

## Requirements

- Unity 2022.3 or newer.
- Works as an Editor extension.
- No Git requirement.

## Important Scope Notes

AI Prefab Guard is a risk review assistant, not an automatic repair tool.

It does not:

- Replace manual Unity review.
- Perform full Unity YAML structural diffing.
- Automatically fix prefabs, scenes, meta files, assets, or asmdefs.
- Connect to cloud services.
- Train or call AI models.

## Suggested Keywords

Unity, Editor Tool, AI, Codex, Prefab, Scene, Meta, Review, Risk, Workflow
