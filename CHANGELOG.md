# Changelog

All notable changes. Versions follow [semantic versioning](https://semver.org/); everything before 1.0 is beta.

## 1.0.0-alpha

First alpha of 1.0: everything since 0.5.0 in one release.

- **Categories in MODS**: your own categories (+ CATEGORY), shown as smaller violet dividers inside MODS. Drag mods onto a category or check several and use MOVE TO CATEGORY; rename by a double-click, removing never deletes mods.
- **Blocks set the load order**: collections, MODS and categories move by their icon or the small arrows; CORE MODS always stay on top. AFTERLIFE tells you when a move gives a conflicting file another winner.
- **Block icons** in their colour: chip (CORE MODS, with a lock), layers (collection), cube (MODS), bookmark (category).
- **No duplicates**: the same files are never imported twice, older duplicates are found after start and merged with MERGE.
- **New version of a collection mod**: into the collection (old one off), replace (old one deleted) or keep it outside; UPDATE ALL asks once.
- **Two versions on**: offered to switch the other one off, rows show 2 VERSIONS ON, Apply lists them.
- **Rename profiles** (Rename or double-click), and categories by a double-click.
- **Bottom bar** with small bevelled buttons; the legend moved to the **GUIDE** tab next to CORE MODS (short part, SHOW EVERYTHING for the rest).
- Attention marks in block headers are small tags with icons; the MODS header is always shown.
- A click into the column header unchecks all mods; mods moved into a category are unchecked afterwards.
- Minimum window size 1612 × 910, cut to the screen on small or zoomed displays.
- Release zip is now named `AFTERLIFE-<version>-win-x64.zip`.

## 0.5.4

- New category icon (a bookmark instead of the tag).
- The GUIDE shows the block icons and covers categories, two versions, duplicates, renaming and unchecking; it scrolls when SHOW EVERYTHING is open.

## 0.5.3

- **No duplicates**: an archive with exactly the same files as a mod already in the library does not make a second copy; the existing mod is used (and switched on, or put into the collection that wanted it). Duplicates made by older versions are found after start and can be merged in one step: one copy keeps its place in profiles, collections, categories and the load order, the others are deleted.
- **New version of a collection mod**: AFTERLIFE asks whether the new version goes into the collection with the old one switched off, replaces the old one (deleted from the library), or stays outside the collection. UPDATE ALL asks once and can use the answer for the rest. The new version keeps the category of the old one.
- **Two versions on**: switching on another version of a mod that is already on offers to switch the other one off; rows show **2 VERSIONS ON** and Apply lists them.
- **Rename profiles**: Rename next to Delete profile, or double-click the profile name. Categories are renamed by a double-click on their name too.
- **Block icons**: every block header starts with an icon in its colour (chip for CORE MODS, layers for a collection, cube for MODS, tag for a category); it is also the drag handle.
- Mods moved into a category are unchecked afterwards.

## 0.5.2

- A click into the column header of the list (MOD · TAGS, VERSION, DEPLOYED, IN PROFILE) unchecks all mods.

## 0.5.1

- **Categories in MODS**: split your mods into your own categories (+ CATEGORY in the MODS header). A category is a smaller violet divider inside MODS with its own count, marks, fold arrow and switch; rename or remove it in its header (removing never deletes mods).
- **Into a category**: drag mods onto a category (a row or its header), or check several and use **MOVE TO CATEGORY**. Checked mods are dragged together.
- **Blocks can be moved**: collections, MODS and categories have a drag handle and small arrows. The order of the blocks is the load order: CORE MODS always first, then the blocks from top to bottom; inside MODS the mods without a category load first, then the categories. A move tells you when a file conflict gets another winner.
- Mods of a collection stay inside their collection; CORE MODS cannot be moved.
- **MODS header always shown**, also when it is empty, and in a light frame that is easier to see; a mod whose collection is gone falls back to MODS.
- **Attention marks** are small bevelled tags with icons instead of ▲ ⚠ ✕ characters.
- **Bottom bar**: small bevelled buttons (REFRESH, CHECK UPDATES, UPDATE ALL, EXPORT LIST, RESET ORDER, PREVIEW) under the status, APPLY CHANGES on the right. The legend moved to a **GUIDE** tab next to CORE MODS: a short visual guide, **SHOW EVERYTHING** for the rest.
- **Window size**: minimum 1612 × 910 (30 % more), cut to the screen's work area on small or zoomed screens so the window always fits.

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
