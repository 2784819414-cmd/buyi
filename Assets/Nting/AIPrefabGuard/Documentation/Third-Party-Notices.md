# Third-Party Notices

AI Prefab Guard does not bundle third-party source code, SDKs, fonts, models, or cloud services in the package.

The baseline scanner is local-only. It reads files under `Assets/` to calculate hashes and stores baseline metadata under `Library/AIPrefabGuard/`.

For local development of this project, a project-local MinGit copy may be used under `.aiprefabguard-tools/`. That folder is ignored and must not be included in the Asset Store upload.
