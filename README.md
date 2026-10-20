# Safe Haven

Unity game project. Open the project using the Unity version recorded in
[ProjectVersion.txt](ProjectSettings/ProjectVersion.txt).

## Restore this backup

Install Git LFS before cloning:

```powershell
git lfs install
git clone https://github.com/AndersMausethagen/Safe_Haven.git
Set-Location Safe_Haven
git lfs pull
```

Add the cloned folder to Unity Hub and open it with the matching Editor version.
Unity regenerates its Library and restores dependencies from the package manifest
and lock file. Git-based package dependencies require network access.

The backup includes saved assets, scenes, prefabs, scripts, `.meta` GUID files,
Packages, ProjectSettings, project documentation and shared VS Code configuration.
Binary art and audio assets are stored using Git LFS.

Generated caches, builds, logs, machine-specific UserSettings, local Unity Version
Control databases, assistant state and credential files are excluded. Unsaved
Editor state is not part of a filesystem backup. Third-party assets retain their
original licenses; this repository is private and is not a redistribution license.

## Later backups

Save intended changes in Unity and VS Code first, then review before committing:

```powershell
git status --short
git add --all
git diff --cached --stat
git commit -m "Back up project changes"
git push
```

This is a manual backup workflow, not a scheduled automatic backup.
