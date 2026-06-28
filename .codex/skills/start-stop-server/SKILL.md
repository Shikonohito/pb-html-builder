---
name: start-stop-server
description: Start, stop, or restart only the local workspace server for this project. Use when Codex is asked to bring the server up, shut it down, restart it, or manage the local server lifecycle without browser testing, UI inspection, report writing, or broader application validation.
---

# Start Stop Server

## Scope

Use this skill only for local server lifecycle control.

Run all commands from the workspace root.

## Start

Start the server by running:

```powershell
.\scripts\Start-LocalServer.ps1
```

Use `sandbox_permissions: "require_escalated"` when the command needs to run outside the sandbox. Report the URL or status printed by the script.

## Stop

Stop the server by running:

```powershell
.\scripts\Stop-LocalServer.ps1
```

Use `sandbox_permissions: "require_escalated"` when the command needs to run outside the sandbox. Report the stop status printed by the script.

## Restart

For restart requests, stop first, then start. If the stop command reports that no server is running, continue with the start command unless the user asked only to stop.
