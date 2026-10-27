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
- AFTERLIFE is a beta. It is careful with your files, but it is still new: if you have a heavily modded install you care about, copy your save games first (`%USERPROFILE%\Saved Games\CD Projekt Red\Cyberpunk 2077`).
- **Use one mod manager per install.** If Vortex or Mod Organizer 2 already manages the game, either keep using it or remove its mods first (its own *purge* or *disable all*). AFTERLIFE will then find any leftovers in *Scan manual mods*.
- Close the game before you apply changes. AFTERLIFE refuses to write while the game is running.

## 2. First run

1. Unzip `AFTERLIFE.exe` to any folder you like, for example `C:\Tools\AFTERLIFE`. Do not put it inside the game folder.
2. Start it. Windows may show *Windows protected your PC* because the file is not code-signed yet: click **More info → Run anyway**.
3. **Game folder**: AFTERLIFE suggests the usual Steam, GOG and Epic locations. If yours is elsewhere, **BROWSE** to the folder that contains `bin\x64\Cyberpunk2077.exe`.
4. **Library folder**: where AFTERLIFE keeps its copy of every mod. Pick a drive with free space; the library holds every version you ever imported, so it grows. It must not be inside the game folder.
5. **Nexus Mods key** (optional, you can add it later): see [section 11](#11-nexus-mods-key-downloads-updates).
6. **START AFTERLIFE**.

If the game already contains mods installed by hand, the *Scan manual mods* card shows how many it found. They keep working; AFTERLIFE does not touch them until you ask (see [section 14](#14-scan-manual-mods)).

## 3. The window

| Area | What it shows |
|---|---|
| **Header, top right** | Game version and build, and the core mods table: version and source of each framework (AFTERLIFE, manual, in library, missing). **GET ›** opens the Nexus page of a missing one. |
| **Profiles (left)** | Your profiles. The one in the game is marked **DEPLOYED**; if it has changes you have not applied yet it shows **PENDING**. |
| **Download history, Scan manual mods, Settings (left)** | Cards that open their own windows. |
| **JACK IN (left, bottom)** | Starts the game. Above it, a line tells you whether the game matches the selected profile. |
| **Mod library (middle)** | Every mod in the library, in blocks: **CORE MODS** (yellow), each **COLLECTION** (cyan) and **MODS** (grey). |
| **Mod details (right)** | The selected mod: picture and description from Nexus, status, and the tabs Overview, Files, Requirements, Conflicts. |
| **Footer** | Progress of the current operation (Preparing, Applying, Verifying), contact and support links. |

### The mod list

Each block header has:

- an **arrow** to fold the block (the state is remembered; a search unfolds everything),
- small **marks** when something inside needs you: **▲** updates, **⚠** conflicts, **✕** missing requirements. Click a mark to jump to the first such mod,
- the number of mods switched on, for example `7 / 7 ON`,
- a **switch** for the whole block (all core mods, the whole collection, or all your other mods). It asks before it also switches related mods.

Each row has:

- a checkbox for selecting several mods (Shift and Ctrl work like in Explorer),
- the name (double-click to rename it, see [section 8](#8-mod-details)), tags and a pink line under the name when a requirement is missing,
- the version, with **UPDATE vX** when Nexus has a newer one,
- the **DEPLOYED** column: what is really in the game (**DEPLOYED**, **+ PENDING · APPLY**, **− PENDING · APPLY**, **! CONFLICT · DECIDE** and so on),
- the **IN PROFILE** switch: whether the mod is on in the selected profile.

When several mods are selected, the bar at the bottom offers **ACTIVATE**, **DEACTIVATE** and **DELETE EVERYWHERE** for all of them.

Above the list: **Refresh** (F5) reads the game folder and the library again, **Check updates** asks Nexus now, **Update all** (Premium, see section 11), **Export list** ([section 16](#16-export-the-mod-list)) and **Preview changes**.

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
- **Newer version of a mod you have?** · the file is a newer download of a mod already in the library. **REPLACE OLD VERSION** puts it in the profile instead of the old one; the old version stays in the library, switched off. Cancel adds it as a separate mod.
- **Not accepted** · the archive is damaged, empty, or its layout is unknown. The message says why.

## 5. Profiles

A profile is a list of which mods are on, which mod wins each conflict and the archive load order.

- **+ New profile** creates one as a copy of the selected profile (an empty one when *Mods off* is selected).
- Clicking a profile **selects** it. Selecting changes nothing in the game; the list just shows that profile. Applying makes it the one in the game.
- **Mods off** is built in: applying it takes out every file AFTERLIFE put in the game. Manually installed mods stay. Handy to test whether a problem comes from a mod at all.
- **Delete profile** appears for profiles you can delete. You cannot delete *Mods off* or the profile that is in the game (switch to another one and apply first). The mods stay in the library.

## 6. Apply changes: load to the deck

**APPLY CHANGES** (or **Preview changes** to just look) shows what will happen: how many mods go in and out, the load order, and files that need a decision. **LOAD TO DECK** writes it.

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

Switching a mod **on** offers to also switch on what it needs (if it is in the library). Switching a mod **off** offers to also switch off mods that need it. The same questions appear for whole blocks.

**Core mods** are the frameworks other mods build on. They have their own yellow block and are never changed by collections. Turning all core mods off asks about every mod that depends on them, because most mods will not run without them.

## 10. Conflicts and load order

Two mods can bring the same file. AFTERLIFE shows **! CONFLICT · DECIDE** and the Conflicts tab lists the files with the mod that wins each one.

- **USE THIS MOD** makes the selected mod win one file.
- **USE THIS MOD FOR ALL n FILES** makes it win every conflict it has, in one click.

Archive mods (`archive/pc/mod`) are loaded by the game in an order. Drag rows by the **⋮⋮** handle to change it; mods higher in the list load first and win. AFTERLIFE writes the order to `modlist.txt` when you apply. **Reset order** returns to the default. Mods of a collection keep the curator's order unless you unlock it (section 12).

## 11. Nexus Mods: key, downloads, updates

### Your key

AFTERLIFE uses your personal Nexus Mods API key. Get it on [nexusmods.com → Site preferences → API Keys](https://www.nexusmods.com/settings/api-keys) (*Personal API Key*), paste it in **Settings → 03 · NEXUS MODS** and **SAVE KEY**. AFTERLIFE checks it and shows your account type. The key is stored encrypted for your Windows user; **Remove key** deletes it.

*Log in with Nexus Mods* (single sign-on) will replace the copy-paste once Nexus Mods has registered AFTERLIFE as an application.

### Mod Manager Download

Turn on *Open Mod Manager Download links in AFTERLIFE* in Settings. AFTERLIFE registers itself for `nxm://` links for your Windows user (the previous handler, for example Vortex, is remembered and given back when you turn it off). Then **Mod Manager Download** on any Cyberpunk 2077 mod page downloads the file straight into the library.

**GET NEW MODS** opens the Cyberpunk 2077 section on Nexus Mods. **Download history** lists everything downloaded through AFTERLIFE, with search; your full history is on the Nexus website.

### Updates

With a key, AFTERLIFE looks for newer versions when it starts and then every hour. It sends one request for all mods ("which mods changed in the last day/week/month") and reads the file list only of the mods that changed, so it stays well inside the Nexus limits. **Check updates** asks right away.

A mod with a newer version shows **UPDATE vX** in the list and an UPDATE line in its details.

- **Premium accounts**: **UPDATE** downloads the new file directly and puts it in the profile instead of the old one. **Update all (n)** does it for every mod with an update, after one question.
- **Free accounts**: Nexus allows downloads only through the website. **UPDATE** opens the file page; click *Mod Manager Download* there (or drop the downloaded file into the window) and AFTERLIFE replaces the old version in the profile.

The old version always stays in the library, switched off, so you can go back.

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

Files that mods installed by AFTERLIFE create on their own (logs, settings, caches) are not reported, and identical leftovers from an earlier manual install of the same mod are ignored.

## 15. Files changed outside AFTERLIFE

Mods often change their own settings files after you play. When AFTERLIFE is about to replace or remove such a file, it asks once **per mod**:

- **Keep my edits** · the changed files are saved in the library as *my edits* and stay with the mod,
- **Discard** · they are replaced or removed as planned.

**Keep all** and **Discard all** decide for every mod at once. When you delete a mod everywhere, its changed files are moved to *Removed* as *Changed files of …* instead of blocking the deletion.

## 16. Export the mod list

**Export list** (above the list) gives the active mods with their versions and Nexus links:

- **In game now** or **Selected profile after apply**,
- **Copy** to the clipboard, or save as **.txt** or **.csv**.

Attach it to bug reports, or use it to find the mod that breaks something (switch half of them off, test, repeat).

## 17. Settings

| Section | |
|---|---|
| **01 · GAME** | The game folder. **CHANGE…** moves AFTERLIFE to another install (it asks to take the mods out of the old one first). |
| **02 · LIBRARY** | Where the library is; **Open in Explorer**. |
| **03 · NEXUS MODS** | Key, account type, `nxm://` links. |
| **04 · KEPT FILES** | Space used by *Removed* files, downloaded archives and temporary files, each with **Delete**, plus **DELETE ALL KEPT FILES**. Deleted kept files can no longer be restored. |
| **Copy diagnostics** | Copies versions, paths and the state of the install to the clipboard for a bug report. Nothing is sent anywhere. |
| **Open log folder** | Logs are in `%LOCALAPPDATA%\AFTERLIFE\logs`. |
| **Remove AFTERLIFE from this PC…** | See [section 21](#21-removing-afterlife). |

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
- **Reporting a bug**: [GitHub Issues](https://github.com/silentbobcz/afterlife/issues) with *Copy diagnostics*, the exported list and, if possible, the log from `%LOCALAPPDATA%\AFTERLIFE\logs`.

More in the [FAQ](FAQ.md).

## 20. Where AFTERLIFE keeps its data

| What | Where |
|---|---|
| Settings | `%APPDATA%\AFTERLIFE` |
| Library (default) | `%LOCALAPPDATA%\AFTERLIFE\Library`, or the folder you chose. A folder named `AFTERLIFE Library` next to `AFTERLIFE.exe` is used automatically (portable use). |
| Inside the library | `afterlife.db` (database), `packages` (mods), `downloads`, `removed`, `staging`, `backups`, `locks`, `cache/images` |
| Logs | `%LOCALAPPDATA%\AFTERLIFE\logs` |
| Nexus key | encrypted with Windows DPAPI for your user, in the settings folder |

## 21. Removing AFTERLIFE

1. Apply **Mods off** if you want the game without AFTERLIFE's mods.
2. **Settings → Remove AFTERLIFE from this PC…**: gives `nxm://` links back to the previous app, deletes the saved key and the settings, and the library too if you tick it.
3. Delete `AFTERLIFE.exe`.

## 22. Keyboard and mouse

| | |
|---|---|
| Ctrl+O | Add mod |
| F5 | Refresh |
| Double-click a mod name | Rename |
| Shift+click, Ctrl+click | Select several mods |
| Drag ⋮⋮ | Change the load order |
