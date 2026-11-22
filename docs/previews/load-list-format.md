# Load list: proposed file format, import rules and texts

A proposal, not in the app yet. A **load list** is the load order of one profile written to a text file: to back it up, to move it to another PC, to share it, or to edit it by hand in Notepad. It is not today's EXPORT LIST (a report of what is in the game, with versions, for bug reports); that one stays as it is.

Points marked **Decide** are the open questions for the owner; each has a recommendation.

## 1. What makes the order today, and what goes into the file

| Part | Where it lives | In the file? |
|---|---|---|
| Profile entries: mods in order, on or off | `ProfileState.Entries` (per profile) | **Yes**, this is the core of the file. |
| Blocks: order of collections and MODS | `BlockLayout.Blocks` (one for the whole library) | **Yes**, as the order of the section lines. |
| Categories and which mod is in which | `BlockLayout.Categories`, `CategoryOf` (one for the whole library) | **Yes**, a category line and the mods under it. |
| Collections: which mods belong to one, curator's order, order lock | `CollectionRecord` (from Nexus, bound to one profile) | **Partly**: name, slug, revision and lock as a section line; membership is never changed by a file (it comes from Nexus). |
| Layers ("└ name", a mod's local edits) | a package with `LayerOfModId` | **Yes**, as an indented line under its mod; it only works in the library it was made in. |
| CORE MODS | detected (`IsCore`), always load first | **As information**: listed in their own section so the file shows the whole setup; their order is never imported. |
| `modlist.txt` (archive order) | made from the entries on APPLY (`Reorder`, `ArchiveOrderOf`) | **No.** It is derived; the import writes the entries and APPLY makes `modlist.txt` as today. |
| Conflict winners of single files (`PathWinners`) | profile | **No.** Keys are file paths, the file would be long and fragile. Winners in the library stay as they are. |
| Mods installed by hand, outside the library | game folder | **No.** AFTERLIFE does not manage them. |

Good to know: categories and the block order are one for the whole library, not per profile. An import that creates a category or moves a mod into one changes the list of every profile (the order inside each profile stays its own). See case 9 in the rules.

## 2. The format

One UTF-8 text file, one line per mod, extension `.txt` so it opens in Notepad with a double click. Suggested name: `afterlife-order-<profile>-<yyyy-MM-dd>.txt`.

### Syntax

| Line starts with | Meaning |
|---|---|
| `AFTERLIFE LOAD LIST 1` | First line, required. `1` is the format version. |
| `#` | Comment, ignored. Export writes a few comments at the top (profile, date, edition, how to edit). |
| `@core` | Section CORE MODS (information only). |
| `@collection <name>` | Section of a collection. Optional fields after `|`: `slug=`, `revision=`, `locked`. |
| `@mods` | Section MODS (mods without a category). |
| `@category <name>` | Section of a category. |
| `+ <name>` | Mod switched on. |
| `- <name>` | Mod switched off. |
| `  └ <name>` | Layer of the mod above it (indented). `+`/`-` may follow the `└`. |
| empty line | Ignored. |

A mod line has the name and optional fields, separated by ` | `:

```
+ Better Lighting | 2.1 | nexus=1234/56789 | file=Better Lighting-1234-2-1-1731000000.zip
```

- **Name** is required, the rest is optional, so a hand-written `+ Better Lighting` is a valid line.
- **Version** is the second part when it has no `=`.
- `nexus=<mod id>/<file id>` (or only `<mod id>`) when the mod is linked to Nexus.
- `file=<archive name>` the name of the archive the mod was added from.
- `md5=<hash>` of that archive; written only for mods with no Nexus link, so lines stay short.
- Unknown `key=value` fields are ignored (room for a later version of the format).
- A `|` inside a name is written as `\|`.

**Top of the list loads first and wins**, the same as in the window. Sections go in load order; the mods inside a section in the order of the profile.

### Example

```
AFTERLIFE LOAD LIST 1
# Profile: Main · exported 2026-11-22 18:40 · AFTERLIFE 1.0.0-beta.2 (full)
# Higher in the list loads first and wins conflicts.
# + switched on, - switched off. Lines starting with # are notes.
# You can move lines, change + and -, add @category lines. Load it back with LOAD ORDER.

@core
+ Cyber Engine Tweaks | 1.35.0 | nexus=107/91234
+ redscript | 0.5.28 | nexus=1511/90011
+ ArchiveXL | 1.21.1 | nexus=4198/90377
+ TweakXL | 1.10.8 | nexus=4197/90378

@collection Night City Glow | slug=nc-glow | revision=7 | locked
+ Better Lighting | 2.1 | nexus=1234/56789
+ Neon Signs Restored | 1.4 | nexus=2201/56001
+ Weather Overhaul | 3.0.2 | nexus=3310/57120

@mods
+ Immersive HUD | 1.8 | nexus=4410/60010
+ Better Vehicle Handling | 2.3 | nexus=5120/61002
- Old Map Colours | 0.9 | file=OldMapColours.zip | md5=9b2c0f6a1d3e4b5c6d7e8f9012345678
+ Appearance Menu Mod | 2.6.1 | nexus=790/62233
  └ + Appearance Menu Mod (local edits)

@category Weapons
+ Smart Gun Rework | 1.2 | nexus=6012/63001
+ Mantis Blade Sounds | 1.0 | nexus=6150/63110
- Katana Pack | 4.0 | nexus=6200/63222

@category Clothes
+ Street Cred Jackets | 2.0 | nexus=7001/64000
+ Nomad Boots | 1.1 | nexus=7020/64100
```

That is 4 core mods, a locked collection with 3 mods, 4 mods in MODS (one off, one with a layer) and two categories with 5 mods.

### Readable by other mod managers?

**No, and on purpose.** No other Cyberpunk mod manager reads a list of mods from a text file: Vortex and Mod Organizer 2 keep their order in their own files, and the only shared format, `archive/pc/mod/modlist.txt`, lists `.archive` file names, not mods. It has no categories, no on/off and no way to tell which mod a file belongs to, so it cannot carry what the owner asked for. The game already gets `modlist.txt` from AFTERLIFE on APPLY, so another manager can still read that one. What the format keeps from the plain-list idea: one line per mod with the name first, so a person (or another tool) can read the list without knowing AFTERLIFE.

## 3. How a mod in the file is found in the library

Tried in this order; the first match wins:

1. `nexus=` mod id and file id: the package with that exact Nexus file.
2. `md5=`: the package added from an archive with that checksum.
3. `file=`: the package added from an archive with that name.
4. Name and version: same name (ignoring case and spaces at the ends) and same version.
5. Name only: same name, any version. This is a match with a **different version**: the mod is used and the preview shows it as "other version: 2.1 in the file, 2.2 here".

`nexus=` with only the mod id (no file id) behaves like step 5 within that Nexus mod.

When the library holds several versions of one mod (an update kept the old one), steps 4 and 5 take the version that is in the profile, else the newest.

**Full edition vs. Offline.** Both write and read the same file. Offline never asks Nexus anything; it still uses `nexus=` ids that are already stored with the mods in the library (a local comparison), and otherwise `md5=`, `file=` and the name. A file from the full edition loads in Offline and the other way around; Offline only cannot get missing mods (case 3).

## 4. Import rules

| # | Case | What happens |
|---|---|---|
| 1 | Category in the file, not in the library | Created at the place it has in the file; its mods are put into it. |
| 2 | Collection in the file, not in the library | **Decide** (D1). |
| 3 | Mod in the file, not in the library | **Decide** (D2). |
| 4 | Mod in the library and in the profile, not in the file | **Decide** (D3). |
| 5 | Mod in a collection with locked order | **Decide** (D4). |
| 6 | Mod in the file under `@collection X`, but in the library it is not part of X | Collection membership comes from Nexus and the file never changes it. The mod stays where the library has it (its own collection, or MODS); the preview says so. |
| 7 | Mod in the file is a core mod, or a mod under `@core` | Its place is ignored (core mods always load first). Its `+`/`-` counts like any other mod. A core mod under `@core` that is missing in the library is shown as missing (case 3). |
| 8 | Layer (`└`) | Follows its mod. Found only when the library has a layer of that same mod; otherwise skipped and named in the preview ("local edits exist only on the PC they were made on"). |
| 9 | Mod is in category A in the library and under category B in the file | Moves to B. Categories are one for the whole library, so the mod is in B in every profile; the preview says "Categories are shared by all profiles". |
| 10 | Category in the library, not in the file | Stays, with the mods it still has. Never deleted by an import. |
| 11 | Same mod twice in the file | The first line counts; the later one is named in the preview with its line number. |
| 12 | Category name longer than 32 characters | Cut to 32; the preview shows the new name. |
| 13 | Line that cannot be read (no `+`/`-`, `└` with no mod above, unknown `@` word) | Nothing is imported. Message with the line number: "Line 14: a mod line starts with + or -." |
| 14 | First line missing or format newer than the app knows | Nothing is imported: "This is not an AFTERLIFE load list." / "This list was made by a newer AFTERLIFE (format 2). Update AFTERLIFE to load it." |
| 15 | Selected profile is "Mods off" | LOAD ORDER is greyed out, as other order changes are. |

### Decide

**D1. Collection in the file, not in the library**

- **A (recommended): a category with the collection's name**, e.g. `Night City Glow (collection)`, holding the mods that are found. In the full edition the preview also offers *Add collection from Nexus…* (the usual add-collection dialog, with the slug from the file), done separately. Costs: the mods are grouped, but without the curator's order lock until the collection is really added.
- **B: skip the section**; its mods go to MODS. Simple, but the grouping from the file is lost.
- **C: only offer adding the collection from Nexus** and stop the import until it is there. Exact, but makes the import depend on a large download and does not work in Offline.

**D2. Mod in the file, not in the library**

- **A (recommended): import what is there, list what is missing.** The preview lists missing mods; in the full edition, mods with `nexus=` get *Get from Nexus* (Premium downloads directly, other accounts get the download pages one by one, as UPDATE ALL does). After downloading, the same file is loaded again and puts the new mods in place (loading a list twice gives the same result). Offline: only the list, with the Nexus link as text for the browser.
- **B: skip silently**, with only a count in the result. Cheaper, but the user does not learn what is missing.
- **C: keep a placeholder** for the missing mod in the profile so it lands in its place once added. Exact, but a new kind of entry in the profile; more work and risk in O15.

**D3. Mod in the library and in the profile, not in the file**

- **A (recommended): stays in its section, at the end of it, on or off as it was.** Option in the preview: *Switch off mods that are not in the list* (unticked by default; tick it to restore a backup exactly).
- **B: always switched off** and put at the end of MODS. Exact restore, but loading a list from someone else would switch off half of the library.
- **C: stays exactly where it is** among the imported mods. Hard to predict where "where it is" ends up once everything around it moved.

**D4. Mod in a collection with locked order**

- **A (recommended): the collection keeps the curator's order**; the order of its lines in the file is ignored, `+`/`-` still counts. The preview says "Night City Glow: order locked, kept as the curator set it".
- **B: unlock the collection** and use the order from the file. Option in the preview: *Unlock collections and use the order from the list*.

**D5. Does the import switch mods on and off?**

- **A (recommended): yes**, `+` and `-` count, with the option *Also switch mods on and off as in the list* (ticked by default). Untick it to take only the order and the categories.
- **B: never**, only order and categories. Safer, but a shared list then does not give the same setup.

**D6. Backup before import**

- **A (recommended): yes, automatic.** Before an import, AFTERLIFE saves the current order of the profile as a load list into the library (`exports/before-import-<date>.txt`, the newest 10 kept) and the result message says where.
- **B: no**; the user exports first if they want.

## 5. What the import changes, and the preview

An import changes only the **selected profile** (order of its mods, on/off when D5 = A) and the **library's categories and block order** (case 1, 9). It never touches the game: as after a drag in the list, the game changes only with APPLY.

Before anything changes, a preview dialog (the same overlay style as EXPORT LIST):

```
LOAD ORDER FROM A LIST
afterlife-order-Main-2026-11-22.txt · 16 mods · made by AFTERLIFE 1.0.0-beta.2

Into profile: Main
  12 mods change place
   2 switched on, 1 switched off
   2 new categories: Weapons, Clothes
   1 other version: Better Lighting (2.1 in the list, 2.2 here)
   3 missing in the library: Katana Pack, Nomad Boots, Neon Signs Restored   [Get from Nexus]
   1 skipped: Appearance Menu Mod (local edits) – exists only on the PC it was made on
   5 mods here are not in the list – they stay where they are

[x] Also switch mods on and off as in the list
[ ] Switch off mods that are not in the list
[ ] Unlock collections and use the order from the list

Categories are shared by all profiles. Nothing changes in the game until you press APPLY.

[ LOAD ]   Cancel
```

Every line with a number opens the names (the full list of moved mods can be long at 500 mods, so it is folded).

## 6. In the window, texts

Two new buttons next to EXPORT LIST, in the same style (`SmallButton`):

| Button | Tooltip |
|---|---|
| SAVE ORDER | Save the load order of this profile to a text file: a backup, for another PC, or to edit by hand |
| LOAD ORDER | Load an order saved with SAVE ORDER into this profile; you see what changes first |

EXPORT LIST keeps its name and gets the tooltip "Active mods with versions, for a bug report or a forum post (not loadable)", so the two exports are not mixed up. RESET ORDER stays where it is.

**SAVE ORDER** opens the Windows save dialog straight away, with no overlay and no options. Every mod of the profile is written, on and off. Result: "Load order saved: afterlife-order-Main-2026-11-22.txt."

Messages:

| When | Text |
|---|---|
| After LOAD | "Load order loaded from the list: 12 moved, 2 switched on, 1 switched off. Apply to update the game." |
| … with backup (D6 = A) | append " The previous order is saved in the library (exports)." |
| Nothing to change | "The profile already has this order. Nothing changed." |
| Line error | "Line 14: a mod line starts with + or -. Nothing was loaded." |
| Not a load list | "This is not an AFTERLIFE load list. Nothing was loaded." |
| Newer format | "This list was made by a newer AFTERLIFE (format 2). Update AFTERLIFE to load it." |
| Nothing found | "None of the mods in the list are in your library. Nothing was loaded." |
| Mods off | tooltip on the greyed button: "Mods off cannot be changed. Select another profile." |

## 7. For O15

- Reading and writing as pure logic in `Afterlife.Core` (a parser that returns the sections and lines or an error with a line number; a writer), tests: write and read back gives the same; every case of section 4.
- Matching (section 3) and the plan of changes (what the preview shows) as pure logic too, so the preview and the import use the same result.
- Writing: categories through `BlockLayout.WithCategories` / `Assign` / `WithBlocks`, order through the existing `ApplyLayoutAsync` (it composes sections and calls `Reorder`), on/off through the profile entries.
- Guide: new part under section 10 (load order) and a line in section 16 (export) saying what the difference is; FAQ "How do I move my load order to another PC?".
