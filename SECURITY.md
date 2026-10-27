# Security policy

## Supported versions

Only the latest release gets fixes. AFTERLIFE is in alpha (1.0.0-alpha).

## Reporting a vulnerability

Please **do not open a public issue** for security problems. Use GitHub's private reporting (**Security → Report a vulnerability** in this repository) or e-mail **silentbob@silentbob.cz** with "AFTERLIFE security" in the subject.

Helpful details: the version, what an attacker can do, and steps or an archive that shows it. You will get an answer within a week. Fixes are released as soon as possible and credited to you if you want.

## In scope

- an archive that makes AFTERLIFE write or delete files outside the game's mod folders or outside the library (path traversal, links, reserved names, alternate data streams),
- an archive or Nexus response that makes AFTERLIFE run code,
- leaking the Nexus API key (logs, diagnostics, files readable by other users),
- `nxm://` links that trigger actions without your confirmation.

## Out of scope

- what a mod does inside the game once you deployed it on purpose: AFTERLIFE installs mods, it does not check what their code does,
- SmartScreen warnings about the unsigned executable.
