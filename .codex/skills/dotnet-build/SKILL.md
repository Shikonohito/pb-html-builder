---
name: dotnet-build
description: Build this repository's .NET solution through the project-standard PowerShell script. Use when Codex is asked to build, compile, validate compilation, check whether the solution builds, or run a .NET build for PbHtmlBuilder.
---

# Dotnet Build

Run all build commands from the workspace root.

For any request to build or verify compilation, run the project script:

```powershell
.\scripts\Build.ps1
```

Prefer this script over invoking `dotnet build` directly, so every agent uses the same build entry point. The script currently runs:

```powershell
dotnet build PbHtmlBuilder.sln -nr:false
```

If sandboxing blocks the script or build output, request escalation and rerun the same script. Report whether the build succeeded, and include the first relevant errors if it failed.
