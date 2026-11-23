# How AFTERLIFE works

A short tour under the hood, for the curious and for contributors.

## Three places

| Place | What lives there | Who writes it |
|---|---|---|
| **Library** | every imported mod (files plus manifest with hashes), profiles, decisions, journal, downloads, removed files | AFTERLIFE only |
| **Profile** | a list: which mods are on, which mod wins each conflicting file, archive load order | you, through the app |
| **Game folder** | what is really deployed | AFTERLIFE only on *Apply*, and only its own files |

The game folder is never the source of truth for a mod. Mods are imported into the library first; applying a profile turns the profile into a plan, and the plan into files in the game.

## Import

1. The archive (`.zip`, `.7z`, `.rar` via SharpCompress, or a single `.archive`) is extracted into `staging` in the library.
2. Every path is validated: no absolute or drive paths, no `.` or `..` segments, no control or invalid characters, no alternate data streams, no reserved Windows names (`CON`, `NUL`, …), length limits. While deploying, AFTERLIFE also refuses to write through symbolic links or junctions.
3. The layout is recognised by `ImportAnalyzer` (archive mods, CET, RED4ext, redscript, TweakXL, ArchiveXL, REDmod, `bin/x64` plugins, FOMOD and so on). Archives with several variants or with a FOMOD installer ask you first.
4. Files are stored under `packages/<id>` with an XXH128 hash each. Executables and scripts inside archives are stored as data and never run.

## Scan

`InstallCoordinator.Scan` walks the mod folders of the game and compares what it finds with the ledger and the library: manual mods, files in the way of a library mod, identical copies, runtime files of core mods. The walk is the expensive part, so the coordinator keeps the last result and reuses it (`CachedScan`) while only profiles change; it walks again as soon as packages or the ledger changed (`AfterlifeStore.ScanVersion`), the game root changed, or AFTERLIFE itself touched the game folder (apply, launch, REDmod deploy). The window asks for a full scan on start, on REFRESH, in *Scan manual mods* and when it comes back to the front.

## What is kept in memory

Three things are remembered between refreshes of the window, each with a clear moment when it is dropped:

- **Packages and the ledger** (`AfterlifeStore.GetPackages`, `GetLedger`): kept as read; dropped by a write to their table, by a rolled back transaction and when another connection committed to the database (`PRAGMA data_version`). `GetLedger` hands out a read-only view shared by all callers.
- **What the planner saw of each game file** (`ScanService.SeenFiles`, size and time of the last write): lives exactly as long as the cached scan, so a preview after a profile change asks the disk only about files it has not seen yet.
- **Hashes of game files** (`HashCache`, by path, size and time of the last write): spares reading a file a mod keeps rewriting on every refresh. A file written in the last two seconds is never remembered. Used for previews only: `Apply`, recovery and *Delete everywhere* empty it first, and the deployment checks every file it touches against the disk anyway.

The requirements of a package are derived from its scripts once per `FrameworkCatalog.RulesVersion` (stored in the package as `RequirementRules`); raise the version when a rule changes.

## Editions

`dotnet publish -p:Edition=Offline` builds **AFTERLIFE Offline** (`AppEdition.Offline`, compile symbol `OFFLINE`): `NexusKeyStore.Load` always returns nothing, nxm:// is never registered, mod pictures are never downloaded, and every entry point that needs the Nexus Mods API calls `LockedOffline` first, which explains the lock (`OfflineInfoVm`). The workflow builds both editions and fails when the two executables are identical. A release carries three files: the zip and the installer of the full edition (`installer/AFTERLIFE.iss`, per user, GitHub only) and the Offline zip for Nexus Mods. A copy with an uninstaller next to it never suggests a library inside its own folder (`AppSettings.SuggestLibrary`).

## The FIXER drop box

FIXER (a quest editor) hands its quests to AFTERLIFE through a folder in the library instead of copying them into the game. `FixerService` (`Fixer.cs`) owns it; it is off until switched on in Settings (`fixer.enabled` in the meta table) and a library that never used it gets no folder.

| Path in the library | Written by | What |
|---|---|---|
| `fixer/status.json` | AFTERLIFE | `schema`, `enabled`, edition, version. Written on start and when the switch changes. |
| `fixer/inbox/<id>-<version>.zip` | FIXER | Game folder layout plus `fixer.json` (`schema`, `kind` quest or tool, `id`, `name`, `version`, `requires`). Written as `.zip.part` and renamed; a `.part` is never read. |
| `fixer/inbox/remove-<id>-<time>.json` | FIXER | `kind: remove`: the quest is switched off in every profile. |
| `fixer/outbox/<file>.result.json` | AFTERLIFE | `status`: `imported`, `applied`, `removed` or `rejected`, with a message. |
| `fixer/done/` | AFTERLIFE | Files taken from the inbox; the newest 20 are kept (the same in `outbox`). |

`Process` imports a package through the ordinary import (so paths are validated the same way), gives it the mod id `fixer-<id>`, adds the requirements FIXER listed (origin `fixer`, kept by `RefreshRequirements`), puts it into the category FIXER and switches it on in the selected profile. A new build replaces the previous one with `ReplaceVersion`; the old build is deleted once the game no longer has it. Nothing here touches the game: that is still APPLY, after which `AfterApply` rewrites the result to `applied`. The window (`MainViewModel.Fixer.cs`) looks into the inbox every three seconds while the switch is on and never takes anything over a running operation, an open dialog or a pending recovery. The contract is schema 1; the same description lives in the FIXER repository, and both sides raise `schema` together.

## The list and the load order

The window keeps the mod rows twice (`MainViewModel.Sorting.cs`): `_loadOrdered` in load order, which every reordering, block move and `SyncSortedOrderAsync` reads through `CurrentOrder()`, and `Mods` as shown. Sorting for viewing (`ModSort`, pure) only changes the second, inside each block, so a sort can never reach the profile or `modlist.txt`; rows cannot be dragged while it is on. The filter panel (`ModFilter`, `FilterPanelVm`) and the search are conditions of the view and do not touch the order either. Inside MODS and every category the rows follow the profile's entries (`MainViewModel.LoadOrder.cs` over the pure `LoadOrder`: `Arrange`, `MoveGroup`, `SortSection`); layers ("└ name") follow their mod and have no place of their own. A profile in the game's default order (no `modlist.txt`) is shown by name until a mod is dragged, and every profile of a library from before the free order is shown by name once more (`order.takeover` in the meta table) before its own order counts, so nothing moves after an update. The number on a row counts `CurrentOrder()` after CORE MODS, the same list `Reorder` turns into `modlist.txt`. The grouped list draws every row unless `AppSettings.VirtualizeList` is on (bound to `VirtualizingPanel.IsVirtualizingWhenGrouping`).

EXPORT ORDER and IMPORT ORDER (`MainViewModel.LoadList.cs`, preview `LoadListPreviewVm`) write and read a load list (`LoadList` in Core, format in `docs/previews/load-list-format.md`). `LoadListPlanner` (pure) finds each line in the library (Nexus file, archive MD5, archive name, name and version, Nexus mod, name only) and builds one `LoadListPlan` that the preview shows and the import writes: on/off through `SetEnabledMany`, categories, blocks and order through `ApplyLayoutAsync`. Before writing, the current order is saved as `exports/before-import-<time>.txt` in the library (newest 10 kept).

Switching mods off (`MainViewModel.Library.cs`, `WithRelatedOffAsync`) asks in one `ConfirmVm` with three parts: `OverviewBuilder.ActiveDependents`, then `SwitchOffPlanner.Parts` (Infrastructure, pure) for NOT NEEDED ANY MORE and STAYS ON, computed from the set that really goes off (with or without the dependents) and recomputed through `ConfirmVm.OptionChanged` when the first box changes.

## Logs

`ActivityLog` (optional, Settings) and the error log of the window both pass every line through `LogPrivacy.Scrub` before it is written: user name, computer name, the folders of the game, the library and the profile, network shares, e-mail addresses and link secrets become marks. New log output goes through the same two writers, never straight to a file.

## Plan

`Planner` builds the target state from the profile (enabled mods, conflict winners, load order) and compares it with the **ledger**, the record of every file AFTERLIFE put in the game. The result is a list of steps: add, replace, delete, and the generated `archive/pc/mod/modlist.txt`.

Before anything is written, the plan is checked against reality:

- a file in the way that AFTERLIFE did not install → the plan stops and asks (it never overwrites foreign files),
- a deployed file that changed since (a mod saved its settings) → you decide per mod: keep your edits or discard,
- files a core mod rewrites by design while the game runs (listed per core mod in `FrameworkCatalog`, for example Input Loader's `r6/cache` XMLs) → not a change: removed with the mod, replaced by its update,
- the game is running, or the install is locked by another AFTERLIFE window → nothing happens.

## Apply (the journal)

`DeploymentExecutor` uses a write-ahead protocol. For every step:

1. copy the library file to a temporary file next to the target,
2. hash the temporary file and compare with the manifest,
3. record the step as *prepared* in the database (durable),
4. rename into place (or delete),
5. record the step as *applied* and update the ledger, in one transaction.

At the end every file is verified against its hash. Only then the operation is marked as verified and a new *generation* (what is in the game) is stored.

If the process dies at any point, the next start finds an unfinished operation and offers **FINISH** (continue from the last durable step) or **RETURN TO PREVIOUS** (undo the applied steps from the recorded baseline). The test suite kills the apply at every single step of a fake install and checks that both ways end in a consistent state.

## Requirements and core mods

`FrameworkCatalog` knows the common frameworks (CET, RED4ext, redscript, ArchiveXL, TweakXL, Codeware, Input Loader and others): how to recognise them in the game and in a mod's files, and their Nexus mod IDs. Requirements come from two sources, the files (`.reds` → redscript, `.xl` → ArchiveXL, and so on) and the Nexus page of the mod, and are merged. A framework counts as present when it is deployed by AFTERLIFE, installed by hand, or in the library.

## Nexus Mods

- **REST v1** (`api.nexusmods.com/v1`): key validation, mod info, file lists, the *updated mods* feed for update checks, download links (Premium) and download links from `nxm://` keys (all accounts).
- **GraphQL v2** (`api.nexusmods.com/v2/graphql`): collections and their revisions, requirements listed on mod pages.
- Update checks use one request to the *updated in the last day/week/month* feed for all mods, then read the file list only of mods that changed. Which mods are read and in which order is decided by `UpdatePlan` (pure): never checked, changed in the feed, older than the feed reaches, or due for the full read every mod gets once in `AppSettings.FullCheckDays` days (each package on its own point of the period, from `nexus:fullread:<package>`); core mods and the mods of the profile first. The automatic check (start, hourly) follows the scope in Settings, CHECK UPDATES covers all mods through the feed, *Check every mod now* reads all. `NexusLimits` reads the remaining quota from the answers of Nexus (`x-rl-hourly-remaining`, `x-rl-daily-remaining`); a check stops at a reserve of 50 or at *rate limit reached*, stores a pause (`nexus:paused-until`, next full hour or midnight GMT) and no check asks anything until it is over. An unfinished *Check every mod now* is remembered (`nexus:fullrun`) and finished by the following checks.
- A mod added from a file is linked to its Nexus page only when Nexus confirms it (`NexusLinker`, `NexusLinking.cs`): the MD5 of the archive through `md5_search`, otherwise the file list of the mod guessed from the archive name must hold a file with that name or that upload time. The link also stores the file id, so update checks compare the exact file. A package without a match is not asked about again for a day (`nexus:linktry:<package>` in the meta table). The window does this for all unlinked mods on request and for each import from disk (`MainViewModel.Linking.cs`).
- `nxm://` links are registered per Windows user (`HKCU\Software\Classes\nxm`); the previous handler is saved and restored.
- Mod pictures are downloaded once into `cache/images`.

## REDmod

REDmods are deployed into `mods/<name>`. Before launch AFTERLIFE runs the game's own `tools/redmod/bin/redMod.exe deploy` when the list of REDmods changed since the last run, then starts the game with `-modded`.

## Projects

| Project | Target | Contents |
|---|---|---|
| `Afterlife.Core` | `net10.0` | Pure logic, no I/O: model, game paths, import analysis, FOMOD, frameworks, planner, snapshot builder. |
| `Afterlife.Infrastructure` | `net10.0` | SQLite store, library, file operations, deployment and recovery, Nexus client, collections, overview, export. `InstallCoordinator` is the single entry point for the app; it is a thin facade over five services that share one `InstallContext`: `ProfileService` (profiles, wants, order), `ScanService` (walk of the game folder, cached result), `LibraryService` (import, remove, rename, Nexus rows), `DeployService` (preview, apply, recovery, launch) and `BundleService` (collections). |
| `Afterlife.App` | `net10.0-windows` | WPF window (MVVM with CommunityToolkit.Mvvm), DPAPI key storage, `nxm://` registration, passing `nxm://` links to an AFTERLIFE window that is already open. `MainViewModel` is split into partial files by feature (`Updates`, `Import`, `Settings`, `Library`, `Apply`, `Ui`, `Blocks`, `Collections`, `Versions`); overlay view models live in `ViewModels/Overlays`; the overlay templates are resource dictionaries in `Views/` (`Blocks`, `Dialogs`, `CollectionViews`, `Panels`, `SettingsView`, `FilterPanel`) merged by `MainWindow.xaml`, which keeps only the layout and the one template that needs code-behind. The filter panel of the mod list is `FilterPanelVm` over the pure `ModFilter` (Infrastructure): each row has facet keys, the panel a state per key. Settings are `SettingsVm` (tab choice and kept-files folders in `SettingsVm.Tabs.cs`) with the template in `Views/SettingsView.xaml`: tabs on the left, one panel per tab shown by `Is…Tab`; a new option goes into the tab it belongs to. |
| `Afterlife.Tests` | `net10.0` | xUnit tests on a fake game install, including crash injection at every deployment step. |

- **Guide website** (`site/`): `build.mjs` turns `docs/USER-GUIDE.md`, `docs/FAQ.md`, `CHANGELOG.md`, `PRIVACY.md` and `site/home.md` into static pages in `site/dist` (not committed; built by Cloudflare Pages, see `site/README.md`). No app code depends on it.
