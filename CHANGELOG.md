# Changelog

All notable changes. Versions follow [semantic versioning](https://semver.org/); everything before 1.0 is beta.

## 1.0.0-beta.2

- **Mod Manager Download no longer opens a new window**: when the running AFTERLIFE could not be reached (for example because it runs as administrator, or was still starting), each click on Mod Manager Download opened another AFTERLIFE window. Now the link always goes to the window that is open, also as administrator, and several quick clicks each become their own download; if it really cannot get there, a short message says why instead.
- **Update check no longer skipped at start**: when Nexus Mods answered the requirements question for one mod with an empty reply, reading mod pages stopped with an error in the log, and the update check at start and the hourly check never ran. Such a mod is now read without requirements and the rest goes on.
- **NEW: freshly added mods on top**: a mod you add from a file or download from Nexus yourself goes into the green block NEW, right under CORE MODS, instead of the end of MODS. Drag each mod to where it belongs or press MOVE ALL TO MODS; NEW loads where it stands, can be moved like any block, and hides when empty. Updates, collections, IMPORT ORDER downloads and FIXER quests stay out of it, and an existing library looks and loads exactly as before.
- **Texts**: messages that said "mod(s)", "file(s)" and similar now use the right singular or plural form for the number they print; the guide and the window call ticked rows "checked" (Check all, REMOVE CHECKED FROM GAME) and keep "selected" for the selected profile and the highlighted row.
- **Buttons stay locked while any action runs**: when two actions overlapped, the first one to finish unlocked the window while the other was still working; now the window waits for the last one. Talking to Nexus Mods reuses one connection instead of opening a new one for every download and check.
- **Script mods did not load? AFTERLIFE says why**: after you return from the game (and at start) a banner tells you when redscript could not compile, once per log; ANALYZE (or LIST & ORDER → ANALYZE REDSCRIPT ERROR) names the mods behind it, switches the checked ones off through the normal switch-off (nothing is deleted or applied) and has a text to COPY for an AI chat.
- **Redscript errors traced to mods (groundwork, no window yet)**: AFTERLIFE reads `r6/logs/redscript_rCURRENT.log`, ties each error to the mod of the library that deployed the script (or marks it as installed by hand), says whether that mod is on in the profile, and writes a question for an AI chat with the paths filtered like the logs.
- **IMPORT ORDER for free Nexus accounts**: missing mods that name a Nexus file are fetched page by page, as with collections: AFTERLIFE opens each file page, you click Mod Manager Download, the file comes in switched off and the next page opens; the window shows waiting, open, in library and skipped, and at the end offers to load the same list again.
- **Move the library**: **Settings → Game & library → MOVE…** takes the whole library to another folder or drive without touching the game. To another drive every file is copied and checked before the old folder is emptied, and a move that was interrupted can be continued or undone at the next start.
- A game folder written with a backslash at its end no longer slips past the check that keeps the library and the game apart, and a game folder inside the library is refused too.

## 1.0.0-beta.1

The first beta: the features are together and from here on they are mostly tuned. Everything below is new since alpha.9.

- **Optional requirements from Nexus**: a requirement whose note on Nexus Mods calls it optional anywhere, not only at the start (RedFileSystem lists RedData as "v0.9.0+ (optional, only required for JSON)"), is no longer shown as missing.
- **APPLY stays while mods are checked**: checking a mod used to replace the whole bar under the list, so APPLY CHANGES was gone and the game could not be started with changes waiting. Now only the row of small tools gives way to the bulk actions (**MOVE TO CATEGORY**, **ACTIVATE**, **DEACTIVATE**, **DELETE EVERYWHERE**); the status line, PREVIEW and APPLY CHANGES stay. The row shows how many mods are checked and how many of them the search, the filters or a folded section hide (with the reason in a tooltip), and **Esc** or **Clear (Esc)** unchecks everything.
- **LIST & ORDER menu**: the row under the list keeps **REFRESH**, **CHECK UPDATES** and **UPDATE ALL**; **EXPORT LIST**, **EXPORT ORDER**, **IMPORT ORDER** and **RESET ORDER** moved into one menu next to them, where each line says what it does. RESET ORDER is greyed out while the profile is in the game's default order.
- **EXPORT ORDER and IMPORT ORDER**: save the load order of a profile to a text file you can read and edit in Notepad (CORE MODS, collections, MODS and categories, each mod on or off) and load it back, on this PC or another. The import shows first what changes (moved, switched on and off, new categories, other versions, missing mods, mods found only by name), keeps locked collections in the curator's order unless you unlock them, and saves the previous order into the library. Categories are shared by all profiles. EXPORT LIST stays the report for bug reports.
- **Guide on the web**: the user guide, FAQ, changelog and privacy notes are now also a website at [afterlife.silentbob.cz](https://afterlife.silentbob.cz). **Guide & website ↗** in the footer of the window, a **WEBSITE** button in **About** and a link at the top of the quick guide open it. The quick guide also covers EXPORT / IMPORT ORDER, LOAD BEFORE and the Ctrl+arrow moves.
- **FILTERS and SORT buttons**: violet instead of cyan so they no longer pass for links, lit when a filter is on or the list is sorted; the sort button now reads **SORT · LOAD ORDER**, **SORT · NAME** and so on.
- **Free load order**: drag any mod up or down inside MODS and inside a category (several checked mods move together), not only into another category; local edits stay below their mod. Every row shows its place in the load order (#1, #2 …), **A→Z** in a header sorts that section by name once, new mods go to the end of MODS and each profile keeps its own order. Existing lists keep the order they had.
- **Keyboard moves, LOAD BEFORE and dimmed numbers**: **Ctrl+Up** / **Ctrl+Down** move the checked mods (or the selected row) by one place in the load order inside their section, **Ctrl+Home** / **Ctrl+End** to its start or end. In the Conflicts tab a conflicting `.archive` file that another mod wins gets **LOAD BEFORE ‹mod›**, which puts the mod just before that one. The number of a mod without any `.archive` in `archive/pc/mod` is dimmed: the load order does not affect it.
- **Nothing personal in the logs**: the activity log and the error log are filtered before a line is written. The Windows user name, the computer name, the folders of the game, the library and your profile, network shares, e-mail addresses and the secrets of a download link are replaced by marks such as `<user>` and `<game>`. The logs hold what AFTERLIFE did, so they are safe to attach to a report.
- **Update checks for large libraries**: a check now reads every mod that is due instead of 60 per hour, core mods and the mods in the profile first, and stops only when Nexus Mods says its request limit is reached. **CHECK UPDATES** asks Nexus which mods changed and reads only those, instead of every mod. As a safety net each mod is read in full once in a while, the mods taking turns so about the same number is read every day.
- **Request limit of Nexus Mods**: AFTERLIFE knows the limit of your key (20,000 requests a day, 500 an hour, for every program together), stops a check a little before it so downloads still work, rests until the quota is back and then goes on by itself, also with a *Check every mod now* that did not fit into one hour. Settings show what the chosen days cost for your library and warn when it is too often, and the message at the limit says where to change it.
- **Settings → UPDATES**: switch the check at start and the check every hour on or off separately (core mods are always checked), limit it to the mods switched on in the profile, set the days between full reads (7 by default, 0 = never), and *Check every mod now*.
- **Sort the list for viewing**: the button next to FILTERS walks through load order, name, newest, state and size. Sorting stays inside each block, core mods keep their place and a line above the list says that the load order is unchanged. Dragging mods is off while the list is sorted; *Back to load order* returns.
- **Draw only the visible rows** (Settings → 05 · DISPLAY, experimental, off by default): with hundreds of mods the list opens and scrolls faster because rows off screen are not drawn.
- **Mods added from files find their Nexus page**: a mod you add from an archive on disk is linked to its page on Nexus Mods when Nexus confirms it, first by the checksum of the archive, then by its file name. It then shows its description, picture, requirements and updates like a mod that came through Mod Manager Download. *Link all unlinked mods…* in the details and in Settings → Nexus Mods does the same for every mod already in the library: one list of what Nexus confirmed, one question. A mod Nexus does not confirm is left as it is. Needs a Nexus key; locked in AFTERLIFE Offline.
- **Filter panel next to the mod list**: filter by state (on, off, not applied yet), by what needs attention (update, conflict, missing requirement), by block (core mods, collections, my mods) and by tags, each with a count. A click means "must match", a second click "must not match", a third turns it off; MATCH ALL or MATCH ANY. The panel can be open, collapsed to a strip with quick filters, or hidden (the FILTERS button next to the search brings it back); AFTERLIFE remembers the choice. Filtering never changes the load order, and Select all takes only what the filter shows.
- **Overlays use a bigger window**: lists in overlays (Scan manual mods, download history, collection management, plan preview, FOMOD, settings, guide and others) keep their size at the smallest window and grow with it, so a maximised window shows more rows instead of empty space.
- **Switching a mod off shows both directions**: besides the switched-on mods that need it, the dialog lists the mods only it needed (with their own checkbox, unticked by default) and, to read only, what it needs that stays on because another mod uses it. Core mods are never offered. A mod without such links still goes off without a question.
- **Clearer switch-off dialog**: it is titled **SWITCH OFF ‹mod›?** and has three parts, each with a sentence under its title: **WILL STOP WORKING** (the mods that need it, ticked to go off too), **NOT NEEDED ANY MORE** (unticked) and **STAYS ON**. The last two now follow what really goes off: untick the mods that need it and what they still use moves to STAYS ON. A collection asks **SWITCH OFF UNUSED MODS?** with the box unticked, and its Cancel leaves the collection on.
- **Start position on small screens**: the window is fitted to the work area before it is shown, so it starts centred instead of sticking out of a 1536 × 824 work area (1920 × 1080 at 125 %).
- **Settings in tabs**: the settings are no longer one long column. Tabs on the left (Game & library, Nexus Mods, Updates, Display, Kept files, Diagnostics, FIXER) and the chosen tab on the right, in one fixed size; the settings open on the tab used last. The tabs show short facts: the size of the kept files and a green dot when the Nexus key works. *Remove AFTERLIFE from this PC…* sits apart at the bottom. New: **Show** opens the folder of each kind of kept file before you delete it, and the FIXER tab says what FIXER is and that it is not out yet. In AFTERLIFE Offline the Updates tab is visible with a lock.
- **Documentation for the beta**: the user guide, the FAQ and the README describe the two editions, the folder in the zip, the filter panel, sorting, the settings in tabs, linking mods to Nexus Mods, the update checks with the request limit and what Nexus Mods Premium changes. The quick guide in the window explains FILTERS and ⇅ SORT. The folder `site` builds the same texts into a small website.
- **The full edition and an installer on GitHub**: a release on GitHub now carries three files, each with its SHA-256. `AFTERLIFE-<version>-win-x64-Setup.exe` installs the full edition for your Windows user without admin rights, with a Start menu shortcut and an uninstaller; `AFTERLIFE-<version>-win-x64.zip` is the same full edition as a folder; `AFTERLIFE-Offline-<version>-win-x64.zip` is AFTERLIFE Offline, the only edition on Nexus Mods. Neither the installer nor the uninstaller touches your library or settings, and an installed copy never suggests a library inside its own folder.
- **Updating AFTERLIFE** in the user guide and the FAQ: how to move to a newer version from a zip or with the installer, and why the old folder must stay when the library is inside it.
- **A tidier folder**: the zip no longer carries thirteen language folders (`cs`, `de`, `fr` …). They held translations of .NET itself, not of AFTERLIFE, which is in English.

## 1.0.0-alpha.9

- **AFTERLIFE Offline only, as a plain folder**: the release carries just the Offline edition, the one published on Nexus Mods. The zip holds a folder `AFTERLIFE` with `AFTERLIFE.exe` and its libraries side by side. Unzip the whole folder and run `AFTERLIFE.exe` from it. No change in the app itself.
- **Why**: Nexus Mods quarantined the previous upload. No antivirus found anything: VirusTotal reports 0 of 66 engines for the alpha.8 zip ([report](https://www.virustotal.com/gui/file/7a24dbc1ed7614d3e9a7f183baba5a202e5c0c69ec76fb1c9d9c9466f34e27cd/detection)). The hold comes from an internal rule of Nexus Mods that sends self-extracting executables to manual review, and until now AFTERLIFE shipped as one executable with the whole app bundled inside. The plain folder has no such bundle. The source code is public and every release is built and tested by GitHub Actions from this repository.

## 1.0.0-alpha.8

- **Plainer packaging** for antivirus scanners: `AFTERLIFE.exe` is no longer compressed and no longer unpacks its native libraries to a temporary folder at start; the few libraries it needs ship next to it in the zip. Keep them together in one folder. No change in the app itself.

## 1.0.0-alpha.7

- **FIXER drop box** (Settings → 06 · FIXER, off by default, in both editions): FIXER, a quest editor for Cyberpunk 2077, can hand its quests to AFTERLIFE instead of copying them into the game. A quest left in `fixer\inbox` of the library is imported like any other mod, lands in the category FIXER switched on in the selected profile, and goes into the game with APPLY CHANGES. A new build takes the place of the previous one, a withdrawn quest is switched off in every profile, and FIXER reads what happened from `fixer\outbox`.

## 1.0.0-alpha.6

- **AFTERLIFE Offline**: a second edition built from the same code (`-p:Edition=Offline`), published on Nexus Mods while Nexus Mods has not registered AFTERLIFE as an app. It never reads an API key, never registers for Mod Manager Download links and never connects to Nexus Mods. The features that need Nexus Mods (Mod Manager Download, updates, collections, the Nexus page in the details, download history) stay visible with a lock; a click explains what is waiting and what works, with links to the pages of every core mod. Each release now carries both zips.

## 1.0.0-alpha.5

Fixes and speed-ups from a review of the whole code.

- **Check updates** on request reads every mod, not only the first 60, and says how many could not be checked instead of reporting that everything is up to date.
- **Delete everywhere** leaves every profile as it was when the game cannot let the mods go (a file in the way, a load order file edited by hand). Before, the mods stayed in the game and the library but dropped out of every profile.
- **FOMOD installers** that copy one file of the archive to two places import instead of failing.
- **Restore** of removed files that fails half-way remembers which files already went back, so the batch can be finished later.
- **Faster APPLY**: the check for a running game and a changed executable no longer runs for every single file.
- **Faster refresh**: packages and the record of deployed files are read from the database once, not several times per click; files a mod keeps rewriting (settings, databases) are not read in full on every refresh; a refresh after a mere profile change does not ask the disk about every file of the profile again.
- **Faster start and import**: the scripts of a mod are read for its requirements once, not on every start, import and refresh of the whole library.
- **Quiet in the background**: the background animation rests while the window is minimized or not in front; a scan no longer writes to the database when nothing changed.

## 1.0.0-alpha.4

- **Files a mod changes in its own folder** (settings, AMM's database, caches) no longer block anything: they are left alone while the mod stays, and a copy goes to the library as *Changed files of …* when the mod is switched off, deleted or updated. An edit directly in a shared folder shows CHANGED OUTSIDE on its row without holding other mods back; APPLY asks Keep / Discard only when that file is about to be replaced or removed. Before, one changed file could block APPLY and every deletion.
- **APPLY CHANGES** stays available when the only blockers are changed files; the keep/discard dialog opens from it.

## 1.0.0-alpha.3

- **Faster refresh**: the window no longer walks the game folder and reads every mod's Nexus rows from the database after every click. The last scan is reused while only profiles change (a full scan still runs on start, REFRESH, *Scan manual mods*, after apply or launch and when the window comes back to the front); the overview builds its per-mod data from one-time lookups.
- **Steadier list**: rows are updated in place, so the selection, checkboxes and scroll position survive a refresh; the mod picture is not decoded again when the selected mod did not change.
- **Address Library** database (`bin/x64/plugins/address_library/version-….bin`), written by native plugins when the game starts, no longer shows up in *Scan manual mods*; AFTERLIFE never touches it.
- **Leftovers are not mods**: a mod folder with nothing its loader would load (for example only a `pending.lock` left by a removed CET mod) is no longer listed in *Scan manual mods*.
- **Select all while searching** takes exactly the mods the search shows (list header, block and category headers, Ctrl+A), so found mods can be moved into a category in one go.
- **Clear search**: a ✕ in the search box (or Esc) removes the filter.
- **More core mods**: Audioware and Virtual Atelier are recognised as optional core mods (in the library, installed by hand and as requirements on Nexus pages or in scripts).
- Leaner sources: the coordinator is a facade over five services, the main view model is a set of partial files, overlay view models and their templates have their own files; no change in behaviour. Tests are organised by feature; comments no longer carry patch numbers.

## 1.0.0-alpha.2

Fixes and small additions after the first alpha on a real install.

- **Runtime files of core mods** (Input Loader's `r6/cache/inputContexts.xml`, `inputUserMappings.xml`, `input_loader.ini`; Mod Settings `user.ini`) are no longer reported as CHANGED OUTSIDE after the game rewrote them. Switching the mod off removes them, an update replaces them, a copy left by a manual install is taken over.
- **Update** on a free Nexus account opens the mod-manager download page straight away (one click on *Slow download* and the file comes into AFTERLIFE). **UPDATE ALL** works for every account: Premium downloads directly, others get the pages one after another; the header button shows how many are left and can skip or stop.
- **Core mods** show a small arrow next to the version when a newer version is on Nexus; one click updates.
- **Rename and Delete** are offered for every own profile. Deleting the profile that is in the game first switches the game to Mods off, then deletes it.
- **Several updates dropped at once** are confirmed as one list (REPLACE ALL or add all as separate mods), not file by file.
- **Another version is on** has a Cancel that leaves the mod off (Escape does the same).
- **Blocked apply** shows a SHOW MOD button that jumps to the mod the blocker belongs to.
- **Header checkboxes** in CORE MODS, collections, MODS and categories select all mods of the block for bulk actions (three states).
- **Preview** groups the changes by mod with the reason (switched off, held back because its framework is off, update); the summary and the Apply dialog count mods held back, so a long remove list after switching ArchiveXL off is explained.
- **Update replaces the old version completely**: the new version takes its place in every profile, collection, category and conflict rule, the game is updated at once (when the profile is the one in the game) and the old version is deleted from the library. UPDATE ALL and REPLACE ALL apply once at the end. Old versions left from earlier updates are offered for deletion once after start.
- **Alphabetical order**: mods inside MODS and every category, and the profiles, are sorted by name (a layer stays below its mod); collections keep their curator's order. With a custom load order the profile follows the sorted list (blocks and categories keep their order). Dragging inside a category no longer reorders; to load a mod earlier, put it in a category placed higher.
- **Manual mods without noise**: `red4ext/config.ini`, lock and cache files that a mod in the library wrote into its own folder (also while it is switched off) are no longer listed. A core-mod group with only its own settings or logs says so instead of "installed manually"; the Address Library database (`bin/x64/plugins/address_library`) is named and explained.
- **Activity log** (Settings): writes switches, imports, every planned file change with its reason and the results into the log folder, one file per day.

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
