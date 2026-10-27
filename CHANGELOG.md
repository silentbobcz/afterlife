# Changelog

All notable changes. Versions follow [semantic versioning](https://semver.org/); everything before 1.0 is beta.

## 0.5.0 · first public beta

- **Mod details**: taller picture strip; wide pictures fill it, tall and square ones are shown whole over a blurred copy instead of a thin cropped stripe.
- **Library blocks**: the MODS block now has the same framed header as CORE MODS and collections (grey), with fold arrow, `n / n ON` and a switch for all your other mods.
- **Attention marks** in every block header: ▲ updates, ⚠ conflicts, ✕ missing requirements. Click one to unfold the block and jump to the first such mod.
- **Scan manual mods**: the folder bars run the full length and stay filled when done (cyan clean, yellow something found).
- **Window size**: the Mod library header wraps its buttons instead of squeezing the text; minimum size 1240 × 700, also at 150 % display scaling on 1920 × 1080.
- The command line tool is no longer part of the release. AFTERLIFE is one app.
- Public documentation: user guide, FAQ, privacy, security policy, contributing guide, issue templates, release workflow.

## 0.4.3

- Profiles list scrolls; the profile in the game cannot be deleted.
- Rename a mod by double-clicking its name (only in AFTERLIFE; empty name restores the original).
- Search in Download history.
- *Load profile* renamed to **LOAD TO DECK**; one progress bar with the current file.
- Collections never change core mods: core mods you have are used, a new collection profile keeps your enabled core mods, and switching a collection off leaves core mods on.
- Switching a whole collection on or off asks about the mods outside it that it needs.
- Core mods block with its own switch; every block can be folded (remembered).
- Missing requirements: **Open page ↗** or **Search on Nexus ↗**.
- Mod picture in details (cached in the library).

## 0.4.2

- Collections: own block in the library with a switch; the download window stays open with progress, **PAUSE** and **RESUME**.
- Profiles can be deleted; a profile with changes not applied yet shows **PENDING**.
- Removed files list scrolls; selection count fixed.

## 0.4.1

- **Update all** for Premium accounts; **UPDATE** checks the account first, free accounts get the right file page.
- Newer downloads of a mod you have are recognised and can replace the old version in the profile (the old one stays in the library).
- Changed files outside AFTERLIFE are decided once per mod; **USE THIS MOD FOR ALL n FILES** for conflicts.
- **Refresh** (F5). Deleting a mod whose settings files changed moves them to Removed instead of failing.
- GitHub Sponsors link.

## 0.4.0

- **Collections** from Nexus Mods: preview, new profile, downloads (Premium directly, free page by page), locked load order, manage, update to a new revision, remove without losing mods others need.
- **REDmod** deployment with `redMod.exe` at launch and `-modded`.
- **FOMOD** installer wizard, choices remembered.
- **Mod updates** at start and every hour, with one request for all mods.
- Missing requirements marked in the list; switching mods on or off offers to switch related mods.
- **Export list** (copy, .txt, .csv).
- Files tab by folder; Settings **KEPT FILES**, **Copy diagnostics**, **Remove AFTERLIFE from this PC**.

## 0.3.x

- 0.3.3: busier header background, requirements from files and Nexus merged in one list, Download history, smarter Scan manual mods.
- 0.3.2: Nexus key in the first-run window, core mods installed like other mods and their own leftovers tidied away, Nexus summary and link in details, texts without developer jargon.
- 0.3.1: EDDIES-style background, *file in the way* shows the exact path and offers to move the file out.
- 0.3.0: core mods table in the header, core mods block, **Scan manual mods**, **GET NEW MODS**, load order by drag and drop, Settings window.

## 0.2.0

- Multi-select (activate, deactivate, delete), Breach protocol panel while applying, game and framework versions in the header, game folder change, library next to the executable, first scan of manually installed mods.

## 0.1.0

- First beta: library outside the game, import of `.zip`, `.7z`, `.rar` and `.archive`, profiles with **Mods off**, planner, crash-safe apply with journal and recovery, conflicts, Nexus key and `nxm://` downloads.
