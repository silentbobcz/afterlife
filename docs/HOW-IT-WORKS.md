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

## Plan

`Planner` builds the target state from the profile (enabled mods, conflict winners, load order) and compares it with the **ledger**, the record of every file AFTERLIFE put in the game. The result is a list of steps: add, replace, delete, and the generated `archive/pc/mod/modlist.txt`.

Before anything is written, the plan is checked against reality:

- a file in the way that AFTERLIFE did not install → the plan stops and asks (it never overwrites foreign files),
- a deployed file that changed since (a mod saved its settings) → you decide per mod: keep your edits or discard,
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
- Update checks use one request to the *updated in the last day/week/month* feed for all mods, then read the file list only of mods that changed. Results are cached. If Nexus answers *rate limit reached*, AFTERLIFE stops and tells you instead of retrying.
- `nxm://` links are registered per Windows user (`HKCU\Software\Classes\nxm`); the previous handler is saved and restored.
- Mod pictures are downloaded once into `cache/images`.

## REDmod

REDmods are deployed into `mods/<name>`. Before launch AFTERLIFE runs the game's own `tools/redmod/bin/redMod.exe deploy` when the list of REDmods changed since the last run, then starts the game with `-modded`.

## Projects

| Project | Target | Contents |
|---|---|---|
| `Afterlife.Core` | `net10.0` | Pure logic, no I/O: model, game paths, import analysis, FOMOD, frameworks, planner, snapshot builder. |
| `Afterlife.Infrastructure` | `net10.0` | SQLite store, library, file operations, deployment and recovery, coordinator (the single entry point for the app), Nexus client, collections, overview, export. |
| `Afterlife.App` | `net10.0-windows` | WPF window (MVVM with CommunityToolkit.Mvvm), DPAPI key storage, `nxm://` registration, passing `nxm://` links to an AFTERLIFE window that is already open. |
| `Afterlife.Tests` | `net10.0` | xUnit tests on a fake game install, including crash injection at every deployment step. |
