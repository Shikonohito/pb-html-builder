---
name: test-blazor-site
description: Test a local C# Blazor web site or Blazor Web App in this workspace. Use when Codex needs to run the site, inspect it in a browser, verify UI behavior, reproduce frontend bugs, capture browser evidence, or validate changes that require a live Blazor server.
---

# Test Blazor Site

## Workflow

1. Start the local server by running `.\scripts\Start-LocalServer.ps1` from the workspace root outside the sandbox with escalation.
   - Use `sandbox_permissions: "require_escalated"`.
   - Ask for approval with a short justification such as: `Allow starting the local Blazor server outside the sandbox?`
   - Use the URL printed by the script as the browser target. If the script output includes multiple URLs, prefer the HTTPS localhost URL unless the task or output indicates otherwise.

2. Open the site with the Browser plugin / `$browser:browser` skill.
   - Navigate to the server URL.
   - Wait until the Blazor app is fully loaded before interacting.
   - Use browser inspection, screenshots, console output, and network observations as needed for the requested test.
   - Report concrete failures with the page, action, observed result, and any relevant console/network errors.

3. Run the requested site checks.
   - Follow the user's requested scenario first.
   - For general smoke testing, verify the initial page loads, core navigation works, and the changed UI or feature behaves correctly.

4. At the end, stop the local server by running `.\scripts\Stop-LocalServer.ps1` from the workspace root outside the sandbox with escalation.
   - Do this after successful tests, failed tests, or partial startup.
   - Use `sandbox_permissions: "require_escalated"`.
   - Ask for approval with a short justification such as: `Allow stopping the local Blazor server outside the sandbox?`

## Notes

- Keep the server running only for the duration of browser testing.
- Include the tested URL and final pass/fail status in the final response.
