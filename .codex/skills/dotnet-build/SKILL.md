---
name: dotnet-build
description: Build this repository's .NET solution directly with dotnet build. Use when Codex is asked to build, compile, validate compilation, check whether the solution builds, or run a .NET build for PbHtmlBuilder.
---

# Dotnet Build

Run all build commands from the workspace root.

For any request to build or verify compilation, run:

```powershell
dotnet build PbHtmlBuilder.sln -nr:false
```

If sandboxing blocks the build output, request escalation and rerun the same command. Report whether the build succeeded, and include the first relevant errors if it failed.
