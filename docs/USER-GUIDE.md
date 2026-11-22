# AFTERLIFE user guide

This guide covers everything in AFTERLIFE (`AFTERLIFE.exe`), from the first run to removing it again.

- [1. Before you start](#1-before-you-start)
- [2. First run](#2-first-run)
- [3. The window](#3-the-window)
- [4. Adding mods](#4-adding-mods)
- [5. Profiles](#5-profiles)
- [6. Apply changes: load to the deck](#6-apply-changes-load-to-the-deck)
- [7. Jack in: starting the game](#7-jack-in-starting-the-game)
- [8. Mod details](#8-mod-details)
- [9. Requirements and core mods](#9-requirements-and-core-mods)
- [10. Conflicts and load order](#10-conflicts-and-load-order)
- [11. Nexus Mods: key, downloads, updates](#11-nexus-mods-key-downloads-updates)
- [12. Collections](#12-collections)
- [13. FOMOD installers and REDmods](#13-fomod-installers-and-redmods)
- [14. Scan manual mods](#14-scan-manual-mods)
- [15. Files changed outside AFTERLIFE](#15-files-changed-outside-afterlife)
- [16. Export the mod list](#16-export-the-mod-list)
- [17. Settings](#17-settings)
- [18. After a game update](#18-after-a-game-update)
- [19. If something goes wrong](#19-if-something-goes-wrong)
- [20. Where AFTERLIFE keeps its data](#20-where-afterlife-keeps-its-data)
- [21. Removing AFTERLIFE](#21-removing-afterlife)
- [22. Keyboard and mouse](#22-keyboard-and-mouse)

---

## 1. Before you start

- Windows 10 or 11, 64-bit. Cyberpunk 2077 2.x from Steam, GOG or Epic.
- AFTERLIFE is a beta of 1.0. It is careful with your files, but it is still new: if you have a heavily modded install you care about, copy your save games first (`%USERPROFILE%\Saved Games\CD Projekt Red\Cyberpunk 2077`).
- **Use one mod manager per install.** If Vortex or Mod Organizer 2 already manages the game, either keep using it or remove its mods first (its own *purge* or *disable all*). AFTERLIFE will then find any leftovers in *Scan manual mods*.
- Close the game before you apply changes. AFTERLIFE refuses to write while the game is running.

### Two editions

AFTERLIFE comes in two editions built from the same code:

- **AFTERLIFE Offline** is the one you download today, on Nexus Mods and on GitHub. It never reads an API key and never connects to Nexus Mods. Everything that works with files on your disk is there: the library, profiles, apply, conflicts, load order, FOMOD, REDmod, scan of manual mods, export.
- **The full edition** adds what needs Nexus Mods: Mod Manager Download, pictures and descriptions, requirements from the mod page, update checks and collections. It is published once Nexus Mods has registered AFTERLIFE as an application.

In AFTERLIFE Offline the features of the full edition stay visible with a **lock**. A click on one opens *WAITING FOR NEXUS MODS*, which says what is waiting and what works now, with links to the pages of every core mod. In this guide, sections 11 and 12 and everything that mentions a Nexus key describe the full edition.

## 2. First run

1. Unzip the whole folder `AFTERLIFE` from the zip to any place you like, for example `C:\Tools\AFTERLIFE`. Keep its files together and do not put it inside the game folder.
2. Start `AFTERLIFE.exe` in that folder. Windows may show *Windows protected your PC* because the file is not code-signed yet: click **More info → Run anyway**.
3. **Game folder**: AFTERLIFE suggests the usual Steam, GOG and Epic locations. If yours is elsewhere, **BROWSE** to the folder that contains `bin\x64\Cyberpunk2077.exe`.
4. **Library folder**: where AFTERLIFE keeps its copy of every mod. Pick a drive with free space; the library holds every version you ever imported, so it grows. It must not be inside the game folder.
5. **Nexus Mods key** (full edition only; optional, you can add it later): see [section 11](#11-nexus-mods-key-downloads-updates).
6. **START AFTERLIFE**.

If the game already contains mods installed by hand, the *Scan manual mods* card shows how many it found. They keep working; AFTERLIFE does not touch them until you ask (see [section 14](#14-scan-manual-mods)).

## 3. The window

| Area | What it shows |
|---|---|
| **Header, top right** | Game version and build, and the core mods table: version and source of each framework (AFTERLIFE, manual, in library, missing). **GET ›** opens the Nexus page of a missing one. |
| **Profiles (left)** | Your profiles. The one in the game is marked **DEPLOYED**; if it has changes you have not applied yet it shows **PENDING**. |
| **Download history, Scan manual mods, Settings (left)** | Cards that open their own windows. |
| **JACK IN (left, bottom)** | Starts the game. Above it, a line tells you whether the game matches the selected profile. |
| **Filters (between the profiles and the list)** | Filter the list by state, by what needs attention, by block and by tag. Open, collapsed to a strip, or hidden. |
| **Mod library (middle)** | Every mod in the library, in blocks: **CORE MODS** (yellow), each **COLLECTION** (cyan) and **MODS** (grey). |
| **Mod details (right)** | The selected mod: picture and description from Nexus, status, and the tabs Overview, Files, Requirements, Conflicts. |
| **Footer** | Progress of the current operation (Preparing, Applying, Verifying), contact and support links. |

### The mod list

Each block header has:

- a **checkbox** that selects every mod of the block or category for the bulk actions below (it shows a dash when only some are selected); while you search, it takes only the mods the search shows,
- an **arrow** to fold the block (the state is remembered; a search unfolds everything),
- small **marks** when something inside needs you: **▲** updates, **⚠** conflicts, **✕** missing requirements. Click a mark to jump to the first such mod,
- the number of mods switched on, for example `7 / 7 ON`,
- a **switch** for the whole block (all core mods, the whole collection, or all your other mods). It asks before it also switches related mods.

Each row has:

- a checkbox for selecting several mods (Shift and Ctrl work like in Explorer),
- its place in the load order (**#1**, **#2** …, only while the list is in load order) and the **⋮⋮** handle for dragging it,
- the name (double-click to rename it, see [section 8](#8-mod-details)), tags and a pink line under the name when a requirement is missing,
- the version, with **UPDATE vX** when Nexus has a newer one (core mods also show a small cyan arrow next to their version in the CORE MODS table at the top; one click updates),
- the **DEPLOYED** column: what is really in the game (**DEPLOYED**, **+ PENDING · APPLY**, **− PENDING · APPLY**, **! CONFLICT · DECIDE** and so on),
- the **IN PROFILE** switch: whether the mod is on in the selected profile.

The **search** box above the list filters by name and tag; **✕** or Esc clears it. **Select all** (the checkbox above the list, Ctrl+A) selects exactly the mods the search shows, so you can, for example, move all found mods into a category at once.

**Filters.** The panel next to the list filters by state (on, off, not applied yet), by what needs attention (update available, conflict, missing requirement), by block (core mods, collections, your mods) and by tag; each line shows how many mods it matches. One click on a line means *must match*, a second click *must not match*, a third turns it off. **MATCH ALL** shows mods that fit every chosen line, **MATCH ANY** mods that fit at least one. **«** collapses the panel to a strip with the quick filters, **✕** hides it, and **⧩ FILTERS** next to the search brings it back; AFTERLIFE remembers how you left it. Filtering never changes the load order, and *Select all* takes only what the filter shows.

**Sorting for viewing.** The **SORT** button next to FILTERS walks through **LOAD ORDER**, **NAME**, **NEWEST**, **STATE** and **SIZE**. Sorting stays inside each block and core mods keep their place. It changes only what you see: a line above the list says that the load order is unchanged, dragging mods is off while the list is sorted, and **Back to load order** returns.

When several mods are selected, the bar at the bottom offers **MOVE TO CATEGORY**, **ACTIVATE**, **DEACTIVATE** and **DELETE EVERYWHERE** for all of them.

The list is split into blocks, each with its own icon in its colour: **CORE MODS** (yellow chip with a lock, always on top), every **COLLECTION** (cyan layers) and **MODS** (light grey cube) for the rest. Drag a block by its icon or use the small arrows next to it. **+ CATEGORY** in the MODS header adds your own category: a smaller violet divider with a bookmark icon inside MODS; double-click its name to rename it. Moved mods are unchecked afterwards; a click into the column header unchecks all mods. Drag mods onto a category (a row or its header) or select them and use **MOVE TO CATEGORY**; **Rename** and **Remove** are in the category header, and removing a category never deletes its mods. Collections, MODS and categories can be moved by their **⋮⋮** handle or the small arrows; see [section 10](#10-conflicts-and-load-order) for what that does to the load order and how to load one mod before another. Inside MODS and every category you set the order yourself: drag a row up or down (local edits stay right below their mod), and **A→Z** in the header sorts that section by name once; collections keep the curator's order. Profiles are sorted by name, with *Mods off* first.

Below the list: **REFRESH** (F5) reads the game folder and the library again, **CHECK UPDATES** asks Nexus now, **UPDATE ALL** (see section 11), **EXPORT LIST** ([section 16](#16-export-the-mod-list)), **RESET ORDER**, **PREVIEW** and **APPLY CHANGES**. The **GUIDE** tab next to CORE MODS explains columns, colours and marks; **SHOW EVERYTHING** opens the rest.

## 4. Adding mods

Three ways, all ending up in the library:

1. **Drag and drop** `.zip`, `.7z`, `.rar` or `.archive` files into the window, or use **+ ADD MOD** (Ctrl+O).
2. **Mod Manager Download** on a Nexus Mods page, once AFTERLIFE handles those links (Settings → Nexus Mods). The file is downloaded and imported; you see it in *Download history*.
3. **+ ADD COLLECTION** for a whole Nexus collection ([section 12](#12-collections)).

Every archive is checked before it is accepted: paths are validated, the layout is recognised (archives, CET, RED4ext, redscript, TweakXL, REDmod, `bin/x64` plugins and so on) and the files are stored with their hashes. AFTERLIFE never runs `.exe`, `.bat` or script files from an archive.

What you may see after an import:

- **Imported** · the mod is in the library and switched on in the selected profile. Apply to put it in the game.
- **Choose a variant** · the archive contains several variants (for example different colours). Pick one and **INSTALL THIS VARIANT**.
- **FOMOD wizard** · the archive has an installer with options ([section 13](#13-fomod-installers-and-redmods)).
- **Newer version of a mod you have?** · the file is a newer download of a mod already in the library. **REPLACE OLD VERSION** puts it in place of the old one (see *Updates* in [section 11](#11-nexus-mods-key-downloads-updates)); **Add as a separate mod** keeps both. Several such files dropped at once are shown as one list: **REPLACE ALL** or **Add all as separate mods**.
- **Another version is on** · switching on a second version of a mod offers to switch the other one off (**SWITCH THE OTHER OFF**), to **Keep both**, or to **Cancel** (the new one stays off; Escape does the same). Rows show **2 VERSIONS ON** while both are on.
- **Old versions in the library** · older versions left from updates made before 1.0.0-alpha.2, switched off everywhere, are offered for deletion once after start.
- **Duplicate mods in the library** · copies with exactly the same files (made by older AFTERLIFE versions) are found after start; **MERGE** keeps one with its place in profiles, collections, categories and the load order.
- **Already in the library** · the archive has exactly the same files as a mod you have; no second copy is made, the existing mod is used.
- **Not accepted** · the archive is damaged, empty, or its layout is unknown. The message says why.

**Mods added from files find their Nexus page** (full edition, needs a key). A mod you add from an archive on disk is linked to its page on Nexus Mods when Nexus confirms it, first by the checksum of the archive, then by its file name. From then on it shows its description, picture, requirements and updates like a mod that came through Mod Manager Download. A mod Nexus does not confirm is left as it is; AFTERLIFE never guesses from the name. **Link all unlinked mods…** in the mod details and in **Settings → Nexus Mods** does the same for every mod already in the library: one list of what Nexus confirmed, one question.

## 5. Profiles

A profile is a list of which mods are on, which mod wins each conflict and the archive load order.

- **+ New profile** creates one as a copy of the selected profile (an empty one when *Mods off* is selected).
- Clicking a profile **selects** it. Selecting changes nothing in the game; the list just shows that profile. Applying makes it the one in the game.
- **Mods off** is built in: applying it takes out every file AFTERLIFE put in the game. Manually installed mods stay. Handy to test whether a problem comes from a mod at all.
- **Rename** and **Delete profile** are under the list for every own profile (double-click a name also renames it). *Mods off* cannot be renamed or deleted. Deleting the profile that is in the game first switches the game to *Mods off* (**MODS OFF, THEN DELETE**), then deletes the profile. The mods stay in the library.

## 6. Apply changes: load to the deck

**APPLY CHANGES** (or **Preview changes** to just look) shows what will happen: how many mods go in and out, mods held back because a core mod they need is off, the load order, and files that need a decision. **LOAD TO DECK** writes it.

**Preview changes** lists the changes **by mod** (how many files each mod adds, replaces or removes, and why: switched off, held back because ArchiveXL or another core mod is off, new content); **BY FILE ›** shows every file. When something blocks the apply, the reason is shown above APPLY CHANGES and **SHOW MOD ›** jumps to the mod it concerns.

How the write works:

1. every file is written to a temporary name next to its target,
2. its hash is checked against the library,
3. the intent is recorded in a journal,
4. the file is renamed into place,
5. at the end every file is verified.

Files that AFTERLIFE did not put there are never overwritten or deleted. If a step fails, nothing is left half done: the game stays in the previous state, and a crash or power cut in the middle is offered as **FINISH** or **RETURN TO PREVIOUS** the next time you start AFTERLIFE.

When an apply takes a core mod out of the game, the files that mod created itself (logs, settings) are moved into the library as *Leftovers of …*, so they can be restored.

## 7. Jack in: starting the game

**JACK IN** starts Cyberpunk 2077 with `--launcher-skip`. If the profile contains REDmods, AFTERLIFE first runs the game's `redMod.exe deploy` (only when the REDmod list changed) and starts the game with `-modded`. If the REDmod DLC is missing, the game starts without the REDmods and AFTERLIFE tells you how to add it.

JACK IN asks you to apply first when the game does not match the selected profile.

## 8. Mod details

- **Picture and description** come from the Nexus page (needs a Nexus key). Click the picture to open the page.
- **Rename**: double-click the name in the list. The name changes only in AFTERLIFE; files in the game stay the same. An empty name restores the original.
- **Overview** · status, version, where the files go (install map), source archive and import date. **UPDATE** when Nexus has a newer version.
- **Files** · every file grouped by folder; click a folder to open it in the game (or in the library when the mod is not deployed).
- **Requirements** · see the next section.
- **Conflicts** · see section 10.
- **View files** opens the library copy; **DELETE EVERYWHERE** switches the mod off in every profile, takes it out of the game and deletes it from the library (it asks first and lists mods that need it).

## 9. Requirements and core mods

AFTERLIFE combines two sources:

- **from files** · what the mod's files need (for example a CET Lua mod needs CET, a `.reds` file needs redscript, an `.xl` file needs ArchiveXL),
- **from Nexus page** · what the author lists on the Nexus page.

Each requirement shows its state: **deployed** (green), **in library · not deployed**, **installed by hand**, **not in library**. For a missing one, **Open page ↗** opens its Nexus page (or **Search on Nexus ↗** by name), and **Copy name** copies it for searching. Requirements the author marked as *optional* or *recommended* do not raise warnings.

Switching a mod **on** offers to also switch on what it needs (if it is in the library). Switching a mod **off** opens a dialog that shows both directions: the switched-on mods that need it (offered to go off with it), the mods **only it needed** (with their own checkbox, unticked by default) and, to read only, what it needs that stays on because another mod uses it. Core mods are never offered. A mod without such links goes off without a question. The same questions appear for whole blocks.

**Core mods** are the frameworks other mods build on: Cyber Engine Tweaks, RED4ext, redscript, ArchiveXL, TweakXL, Codeware, Input Loader and, optional, Mod Settings, Native Settings UI, RedFileSystem, Audioware and Virtual Atelier. They have their own yellow block and are never changed by collections. Turning all core mods off asks about every mod that depends on them, because most mods will not run without them.

## 10. Conflicts and load order

Two mods can bring the same file. AFTERLIFE shows **! CONFLICT · DECIDE** and the Conflicts tab lists the files with the mod that wins each one.

- **USE THIS MOD** makes the selected mod win one file.
- **USE THIS MOD FOR ALL n FILES** makes it win every conflict it has, in one click.
- **LOAD BEFORE ‹mod›** appears next to a conflicting `.archive` file (in `archive/pc/mod`) that another mod wins, when both mods are in MODS or a category. It moves the selected mod to just before that mod in the load order, so it wins the file; if the other mod is in another category, the mod moves into it, as with a drag onto that row.

### Load order

**Where it matters.** Only archive mods (`.archive` files in `archive/pc/mod`) have a load order. When two of them change the same game asset, the one that loads first wins. The order (written to `modlist.txt`) does not affect scripts, CET, RED4ext, TweakXL and other mods; for them only the file conflicts above count.

**How the list maps to the order.** The top of the list loads first and wins:

1. CORE MODS (always first, cannot be moved),
2. then the blocks from top to bottom (every COLLECTION and MODS),
3. inside MODS: the mods without a category first, then each category from top to bottom,
4. inside MODS and inside every category the mods load in the order you give them; inside a collection they keep the curator's order.

The number at the start of a row (**#1**, **#2** …) is its place in that order, counted after CORE MODS; it is shown only while the list is in load order (not while it is sorted with **SORT**).
A number shown dimmer belongs to a mod without any `.archive` file in `archive/pc/mod`: the load order does not affect it (its tooltip says so).

**To load one mod before another** (for example a texture fix that must win over a bigger texture pack):

1. Drag the mod by its **⋮⋮** handle above the other one, inside MODS, inside a category or from one into another. A line shows where it lands. Several checked mods move together and keep their order.
2. **APPLY CHANGES**. The preview shows the new order before anything is written.

From the keyboard (only in the load order view, not while sorted with **SORT**): **Ctrl+Up** / **Ctrl+Down** move the checked mods (or the selected row) by one row inside their section (MODS, one category or an unlocked collection), **Ctrl+Home** / **Ctrl+End** to its start or end. They never cross into another section.

A mod's local edits (**└ name**) always stay right below the mod and move with it. Categories still group mods, and a whole category moves by its handle or the small arrows. **A→Z** in the header of MODS or a category sorts that section by name once, after a question; nothing changes in the game before you apply. New mods go to the end of MODS, without a category; an updated mod keeps the place of the old version. The order belongs to the profile: another profile can order the same mods differently (categories are shared by all profiles).

Inside an unlocked collection you can drag rows the same way ([section 12](#12-collections)). Moving a mod, a block or a category changes the load order; AFTERLIFE tells you when a conflicting file gets another winner, and nothing changes in the game before you apply. AFTERLIFE writes the order to `modlist.txt` when you apply. Until you change the order of a profile, the game uses its default (alphabetical by archive name) and the list shows the mods of MODS by name. **Reset order** returns to that default. Sorting the list (**SORT**) changes only the view, never the order.

### Save and load the order (EXPORT ORDER, IMPORT ORDER)

**EXPORT ORDER** (above the list, next to EXPORT LIST) saves the load order of the selected profile to a text file, `afterlife-order-<profile>-<date>.txt`: a backup, a way to move the order to another PC, to share it, or to edit it in Notepad. Every mod of the list is written, switched on or off, with CORE MODS, every collection, MODS and every category in load order. A mod's local edits are written as notes only: they exist just in the library they were made in.

The file is plain text. The first line is `AFTERLIFE LOAD LIST 1`; lines starting with `#` are notes. `@core`, `@collection <name>`, `@mods` and `@category <name>` start a section, `+ <name>` is a mod switched on and `- <name>` one switched off. After the name a line can have the version and fields separated by ` | ` (`nexus=`, `file=`, `md5=`); a hand-written `+ Better Lighting` is enough. Top loads first and wins, as in the window. You can move lines, change `+` and `-` and add `@category` lines.

**IMPORT ORDER** loads such a file into the selected profile. First a preview shows what changes: mods that change place, switched on and off, new categories, other versions, mods missing in the library, and every mod found only by its name on its own line (two different mods can share a name, so check those). A mod is found by its Nexus file, then the checksum or name of its archive, then its name and version, then its name alone. Options in the preview:

- **Also switch mods on and off as in the list** (on by default; untick it to take only the order and the categories),
- **Switch off mods that are not in the list** (off by default; tick it to restore a backup exactly). Without it those mods stay at the end of their section, on or off as they were,
- **Unlock collections and use the order from the list** (only when the list has a locked collection; otherwise the curator's order stays).

Good to know:

- **Categories are shared by all profiles.** A category the list creates, or a mod it moves into another category, changes how every profile is grouped; the order inside each profile stays its own. An import never deletes a category.
- Mods missing in the library are listed; with Nexus Mods Premium **Get from Nexus** downloads those with a Nexus file and loads the list again, other accounts get a link to each page. Loading the same list twice gives the same result. AFTERLIFE Offline only lists them.
- A collection your library does not have becomes a category named "<name> (collection)" with the mods that are found; **Add collection from Nexus…** adds the real one (not in AFTERLIFE Offline). Which mods belong to a collection always comes from Nexus, never from the file.
- Core mods always load first; the list only switches them on or off.
- Before an import AFTERLIFE saves the current order into the library (`exports/before-import-<date>.txt`, the newest 10 are kept).
- Nothing changes in the game until you press **APPLY CHANGES**. IMPORT ORDER is greyed out in Mods off.

## 11. Nexus Mods: key, downloads, updates

### Your key

AFTERLIFE uses your personal Nexus Mods API key. Get it on [nexusmods.com → Site preferences → API Keys](https://www.nexusmods.com/settings/api-keys) (*Personal API Key*), paste it in **Settings → Nexus Mods** and **SAVE KEY**. AFTERLIFE checks it and shows your account type. The key is stored encrypted for your Windows user; **Remove key** deletes it.

*Log in with Nexus Mods* (single sign-on) will replace the copy-paste once Nexus Mods has registered AFTERLIFE as an application.

### Mod Manager Download

Turn on *Open Mod Manager Download links in AFTERLIFE* in Settings. AFTERLIFE registers itself for `nxm://` links for your Windows user (the previous handler, for example Vortex, is remembered and given back when you turn it off). Then **Mod Manager Download** on any Cyberpunk 2077 mod page downloads the file straight into the library.

**GET NEW MODS** opens the Cyberpunk 2077 section on Nexus Mods. **Download history** lists everything downloaded through AFTERLIFE, with search; your full history is on the Nexus website.

### Updates

With a key, AFTERLIFE looks for newer versions when it starts and then every hour. It asks Nexus which mods changed and reads only those, core mods and the mods of the selected profile first. As a safety net every mod is also read in full once in a while (every 7 days by default), the mods taking turns so about the same number is read each day. **CHECK UPDATES** asks right away.

**Settings → Updates** has the switches: the check at start and the check every hour (separately; core mods are always checked), *check only the mods switched on in the selected profile*, the number of days between full reads (0 = never) and **Check every mod now…**.

**The request limit of Nexus Mods.** Nexus allows every key 20,000 requests a day and 500 an hour, for all programs that use the key together. AFTERLIFE knows the limit, stops a check a little before it so downloads still work, rests until the quota is back and then goes on by itself. Settings show what your choice of days costs for your library and warn when it is too often.

A mod with a newer version shows **UPDATE vX** in the list and an UPDATE line in its details.

- **Premium accounts**: **UPDATE** downloads the new file directly. **UPDATE ALL (n)** does it for every mod with an update, after one question.
- **Free accounts**: Nexus allows downloads only through the website. **UPDATE** opens the mod-manager download page of the new file; click *Slow download* there and the file comes straight into AFTERLIFE (needs *Mod Manager Download* links on, see above; without them the files page opens and you can drop the downloaded file into the window). **UPDATE ALL (n)** opens these pages one after another: the next one opens when the previous file has arrived. The button then shows **UPDATES · n LEFT**; click it to skip one or stop.

### With Nexus Mods Premium

AFTERLIFE works with a free Nexus Mods account, and it is at its best with **Premium**, because Nexus allows direct downloads through its API only for Premium members:

| | Premium | Free account |
|---|---|---|
| **UPDATE** | downloads the new file directly | opens the download page; one click on *Slow download* |
| **UPDATE ALL** | one question, then every update downloads and installs by itself | opens the pages one after another; one click for each mod |
| **Collections** | the whole collection downloads by itself | one click on *Mod Manager Download* for each mod |
| **Download speed** | uncapped | capped by Nexus |

With a few hundred mods the difference is one click instead of a few hundred. Premium also supports the mod authors and the site that hosts their work. You can get it on [nexusmods.com](https://www.nexusmods.com/); AFTERLIFE itself stays free either way and locks nothing behind it.

### What an update does

An update replaces the old version completely: the new one takes its place in every profile (same position, on or off as before), in its collections and category and as the winner of its file conflicts. When the profile is the one in the game, AFTERLIFE applies right away, then deletes the old version from the library. UPDATE ALL and REPLACE ALL apply once at the end. If the game cannot be changed at that moment (for example it is running), the old version is kept until you apply.

## 12. Collections

**+ ADD COLLECTION** accepts a collection link (`https://next.nexusmods.com/…/collections/…` or `nxm://…/collections/…`). The preview shows:

- the curator, revision and size,
- every mod: **in library**, **other version in library**, **missing**, **outside Nexus (add by hand)**,
- optional mods you can tick,
- where to put it: a **new profile** (recommended) or the current profile.

**ADD COLLECTION** then downloads what is missing:

- **Premium**: one after another, directly.
- **Free**: AFTERLIFE opens the page of the next mod; click *Mod Manager Download* and it opens the following one by itself.

The collection window stays open while it downloads: one progress bar for the whole collection, the mod in progress, the state of every mod, **PAUSE** (stops after the current file) and **RESUME** (continues and retries failed ones). Closing the window does not stop the download.

In the library a collection is its own cyan block, with **Manage ›**, a count of mods on and a switch for the whole collection:

- **switching it on** offers to also switch on mods outside the collection that its mods need,
- **switching it off** offers to also switch off mods outside the collection (never core mods) that nothing else switched on needs any more.

Mods you add later stay outside the collection; the collection always matches the curator's version on Nexus.

**Manage collection**:

- **Load order** is set by the collection and locked. **Unlock** lets you drag; **Reset to collection order** goes back.
- **Check for new revision** compares with the latest revision and shows what was added, removed and updated before you update.
- **Remove** takes the collection out. Mods that another collection, another mod or your own profile still needs are kept; the window lists what goes and what stays and why.

Core mods are never changed by a collection: if the collection lists a core mod you already have, your version is used.

## 13. FOMOD installers and REDmods

**FOMOD**: archives with `fomod/ModuleConfig.xml` open a wizard with the author's steps, groups, descriptions and pictures. Your choices are saved with the mod and reused when you import a newer version. AFTERLIFE reads the XML only; it never runs C# scripts.

**REDmod**: archives with a `mods/<name>/info.json` layout are recognised. They are deployed with the game's own `tools\redmod\bin\redMod.exe` before the game starts ([section 7](#7-jack-in-starting-the-game)). The REDmod DLC must be installed (free on every store).

## 14. Scan manual mods

The card on the left shows how many files in the mod folders AFTERLIFE does not manage, by folder (archive, redscript, tweaks, CET, RED4ext, REDmod, other). **Scan manual mods** opens the list:

- mods installed by hand, grouped by name and folder, core mods marked,
- **RESCAN** reads the folders again,
- tick mods and **REMOVE SELECTED FROM GAME** moves their files into the library (into *Removed*), so they can always be restored,
- **Removed earlier · kept in the library** lists what was moved out before, with **RESTORE**.

Files that mods in the library create on their own (logs, settings, caches, lock files in their own folder, `red4ext/config.ini`) are not reported, also while the mod is switched off, and identical leftovers from an earlier manual install of the same mod are ignored. A core-mod group that holds only such files says so instead of *installed manually*. The **Address Library** database (`bin/x64/plugins/address_library/version-….bin`), which native plugins write when the game starts, belongs to the game: it is never reported and never moved. A mod folder that holds nothing its loader would load (no `init.lua`, `.reds`, `.dll`, tweak, archive or REDmod `info.json`), only files a removed mod left behind (lock files, logs, saved settings), is not a mod and is not listed either.

## 15. Files changed outside AFTERLIFE

Mods often change their own files after you play: settings, databases (AMM's `db.sqlite3`), caches. AFTERLIFE tells the two cases apart by where the file is:

- **In the mod's own folder** (a CET mod folder, a RED4ext plugin folder, a redscript module folder): the mod is at work. Nothing is reported and nothing blocks. When the mod is switched off, deleted or updated, a copy of the changed files goes to *Removed* as *Changed files of …* (restorable in *Scan manual mods*) before they are removed or replaced.
- **Directly in a shared folder** (`archive/pc/mod`, `r6/scripts`, `r6/tweaks`, …): that is your edit. While the mod stays, the row says **CHANGED OUTSIDE** and nothing else is held back. When AFTERLIFE is about to replace or remove the file, **APPLY** asks once **per mod**: **Keep my edits** saves the changed files in the library as *my edits* that stay with the mod; **Discard** replaces or removes them as planned. **Keep all** and **Discard all** decide for every mod at once. When you delete a mod everywhere, its changed files are moved to *Removed* instead of blocking the deletion.

Files a core mod rewrites by design every time the game runs (Input Loader's `r6/cache/inputContexts.xml`, `inputUserMappings.xml` and `input_loader.ini`, Mod Settings' `user.ini`) are treated like files in the mod's own folder.

## 16. Export the mod list

**Export list** (above the list) gives the active mods with their versions and Nexus links:

- **In game now** or **Selected profile after apply**,
- **Copy** to the clipboard, or save as **.txt** or **.csv**.

Attach it to bug reports, or use it to find the mod that breaks something (switch half of them off, test, repeat).

EXPORT LIST is a report and cannot be loaded back. To save the load order and load it again (a backup, another PC), use **EXPORT ORDER** and **IMPORT ORDER** ([section 10](#save-and-load-the-order-export-order-import-order)).

## 17. Settings

The settings have tabs on the left and the chosen tab on the right. They open on the tab you used last. Under the tabs are the version and **Remove AFTERLIFE from this PC…** ([section 21](#21-removing-afterlife)).

| Tab | |
|---|---|
| **01 · Game & library** | The game folder: **CHANGE…** moves AFTERLIFE to another install (it asks to take the mods out of the old one first). Where the library is, with **Open in Explorer**. |
| **02 · Nexus Mods** | *Account*: your key and account type (a green dot on the tab means the key works). *Link mods*: **Link all unlinked mods…** ([section 4](#4-adding-mods)). *Download links*: open `nxm://` links in AFTERLIFE. Locked in AFTERLIFE Offline. |
| **03 · Updates** | When AFTERLIFE looks for newer versions and what it reads, and the cost in requests to Nexus Mods ([section 11](#11-nexus-mods-key-downloads-updates)). Visible with a lock in AFTERLIFE Offline. |
| **04 · Display** | **Reduce motion** stops the background, glitches, scans and the breach animation. **Draw only the visible rows** (experimental, off by default) makes a list of hundreds of mods open and scroll faster; switch it off again if the list jumps while scrolling or dragging. |
| **05 · Kept files** | Copies AFTERLIFE keeps so you can undo: *Removed* files, downloaded archives and temporary files. Each line shows its size and has **Show** (opens the folder, so you can look first) and **Delete**; **DELETE ALL KEPT FILES** clears all three. The tab shows the total. Deleted kept files can no longer be restored; the game, the mods in the library and the profiles stay. |
| **06 · Diagnostics** | **Log what AFTERLIFE does** (off by default) writes switches, imports, every planned file change with its reason, blockers and results into `activity-YYYYMMDD.log`, one file per day; **Open log folder** shows `%LOCALAPPDATA%\AFTERLIFE\logs`. **Copy diagnostics** copies the mod list, versions and the last errors to the clipboard for a bug report, without your key or paths. Nothing is sent anywhere. |
| **07 · FIXER** | Off by default. FIXER is not released yet, so for now this switch has nothing to receive. For FIXER, a quest editor for Cyberpunk 2077: while this is on, AFTERLIFE takes the quests FIXER leaves in the `fixer\inbox` folder of the library. They arrive in the category **FIXER**, switched on in the selected profile, and go into the game with **APPLY CHANGES** like any other mod. A new build of a quest takes the place of the previous one; a quest FIXER withdraws is switched off in every profile. |

## 18. After a game update

When the game version or build changes, AFTERLIFE shows a **game updated** banner. Nothing is switched off, but JACK IN asks before a modded launch (**Launch anyway** or **Switch to Mods off**). Many mods, especially RED4ext plugins and CET, need their own update after a game patch:

1. check the core mods table and the authors' pages for versions that support the new build,
2. update them (section 11),
3. click **REVIEW** on the banner and apply.

## 19. If something goes wrong

- **The game crashes after applying**: apply **Mods off** and test. If the game runs, switch half of your mods on, apply, test, and repeat. *Export list* helps you keep track.
- **An apply was interrupted** (crash, power cut): start AFTERLIFE; it offers **FINISH** or **RETURN TO PREVIOUS**.
- **Something looks wrong in the list**: **Refresh** (F5).
- **The game itself is damaged**: verify the game files in Steam, GOG Galaxy or Epic, then apply again.
- **Reporting a bug**: [GitHub Issues](https://github.com/silentbobcz/afterlife/issues) with *Copy diagnostics*, the exported list and, if possible, the log from `%LOCALAPPDATA%\AFTERLIFE\logs`. The logs are safe to attach: before a line is written, your Windows user name, the computer name, the folders of the game, the library and your profile, e-mail addresses and the secrets of download links are replaced by marks such as `<user>` and `<game>`.

More in the [FAQ](FAQ.md).

## 20. Where AFTERLIFE keeps its data

| What | Where |
|---|---|
| Settings | `%APPDATA%\AFTERLIFE` |
| Library (default) | `%LOCALAPPDATA%\AFTERLIFE\Library`, or the folder you chose. A folder named `AFTERLIFE Library` next to `AFTERLIFE.exe` is used automatically (portable use). |
| Inside the library | `afterlife.db` (database), `packages` (mods), `downloads`, `removed`, `staging`, `backups`, `locks`, `cache/images`, `exports` (the order saved before each IMPORT ORDER), and `fixer` once the FIXER drop box was switched on |
| Logs | `%LOCALAPPDATA%\AFTERLIFE\logs` |
| Nexus key | encrypted with Windows DPAPI for your user, in the settings folder |

## 21. Removing AFTERLIFE

1. Apply **Mods off** if you want the game without AFTERLIFE's mods.
2. **Settings → Remove AFTERLIFE from this PC…**: gives `nxm://` links back to the previous app, deletes the saved key and the settings, and the library too if you tick it.
3. Delete the folder `AFTERLIFE`.

## 22. Keyboard and mouse

| | |
|---|---|
| Ctrl+O | Add mod |
| F5 | Refresh |
| Ctrl+A | Select all mods the search and the filters show |
| Esc | Clear the search |
| Double-click a mod, profile or category name | Rename |
| Shift+click, Ctrl+click | Select several mods |
| Ctrl+Up, Ctrl+Down | Move the checked mods (or the selected row) one row up or down in the load order, inside their section |
| Ctrl+Home, Ctrl+End | Move them to the start or end of their section |
| Drag ⋮⋮ | Move a mod up or down inside MODS, a category or an unlocked collection, or into another category; move a block or category (load order) |
