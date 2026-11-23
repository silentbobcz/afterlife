# FAQ

- [Windows says it protected my PC](#windows-says-it-protected-my-pc)
- [What is AFTERLIFE Offline and why do some buttons have a lock?](#what-is-afterlife-offline-and-why-do-some-buttons-have-a-lock)
- [How do I update AFTERLIFE without losing my mods?](#how-do-i-update-afterlife-without-losing-my-mods)
- [Why is the download a folder and not one exe?](#why-is-the-download-a-folder-and-not-one-exe)
- [How do I load one mod before another?](#how-do-i-load-one-mod-before-another)
- [How do I move my load order to another PC?](#how-do-i-move-my-load-order-to-another-pc)
- [AFTERLIFE says the request limit of Nexus Mods is reached](#afterlife-says-the-request-limit-of-nexus-mods-is-reached)
- [Is it free? What do I need Nexus Premium for?](#is-it-free-what-do-i-need-nexus-premium-for)
- [Can I use it together with Vortex or Mod Organizer 2?](#can-i-use-it-together-with-vortex-or-mod-organizer-2)
- [I already have mods installed by hand](#i-already-have-mods-installed-by-hand)
- [Does it change my game files?](#does-it-change-my-game-files)
- [The game crashes after I applied a profile](#the-game-crashes-after-i-applied-a-profile)
- [The power went out while it was applying](#the-power-went-out-while-it-was-applying)
- [Mod Manager Download opens Vortex instead of AFTERLIFE](#mod-manager-download-opens-vortex-instead-of-afterlife)
- [UPDATE only opens a web page](#update-only-opens-a-web-page)
- [Input Loader shows CHANGED OUTSIDE](#input-loader-shows-changed-outside)
- [How do I report a problem?](#how-do-i-report-a-problem)
- [A mod says a requirement is missing, but I have it](#a-mod-says-a-requirement-is-missing-but-i-have-it)
- [My REDmods do nothing](#my-redmods-do-nothing)
- [The library takes a lot of space](#the-library-takes-a-lot-of-space)
- [Does it work on Steam Deck, Linux or macOS?](#does-it-work-on-steam-deck-linux-or-macos)
- [Does it send anything about me anywhere?](#does-it-send-anything-about-me-anywhere)
- [How do I report a bug?](#how-do-i-report-a-bug)

## Windows says it protected my PC

`AFTERLIFE.exe` is not code-signed yet, so Microsoft Defender SmartScreen shows *Windows protected your PC* the first time. Click **More info → Run anyway**. You can check that the file is the one from the release: every release lists the SHA-256 of the zip, and the source code is in this repository, built by GitHub Actions.

## What is AFTERLIFE Offline and why do some buttons have a lock?

AFTERLIFE Offline is the edition published on Nexus Mods. It is the same app, but it never reads an API key and never connects to Nexus Mods. A program that uses the Nexus Mods API has to be registered by Nexus Mods first, and that registration is still open; until then the download on Nexus Mods is the edition that does not use the API at all. The full edition, which downloads, updates and adds collections with your Nexus key, is on [GitHub Releases](https://github.com/silentbobcz/afterlife/releases) as an installer or a zip. Both editions use the same library and settings.

Everything that works with files on your disk is there. What needs Nexus Mods (Mod Manager Download, pictures and descriptions, updates, collections, download history) stays visible with a lock; a click explains what is waiting and what works, with links to the pages of every core mod.

## How do I update AFTERLIFE without losing my mods?

Your mods are in the library, and with a copy from a zip the library is usually the folder `AFTERLIFE Library` inside the AFTERLIFE folder. So do not delete the old folder: close AFTERLIFE and unzip the new version over it, or run the installer. Settings, profiles, load order and the Nexus key stay. The steps for every case are in [Updating AFTERLIFE](USER-GUIDE.md#updating-afterlife) in the guide.

## Why is the download a folder and not one exe?

Until 1.0.0-alpha.8 AFTERLIFE shipped as one executable with the whole app packed inside. Nexus Mods sends self-extracting executables to manual review and held one upload back for it, although no antivirus found anything. Since 1.0.0-alpha.9 the zip holds a plain folder `AFTERLIFE` with `AFTERLIFE.exe` and its libraries side by side. Unzip the whole folder, keep the files together and run `AFTERLIFE.exe` from it. The full edition on GitHub also comes as a classic installer; Nexus Mods gets only the zip.

## How do I load one mod before another?

Only archive mods (`.archive`) have a load order; the top of the list loads first and wins. Drag the mod by its **⋮⋮** handle above the other one (inside MODS, inside a category or from one into another), then **APPLY CHANGES**. The number at the start of a row (#1, #2 …) is its place in the load order. Categories stay a way to group mods; **A→Z** in a header sorts that section by name once. The steps are in the [user guide, section 10](USER-GUIDE.md#load-order).

## How do I move my load order to another PC?

**EXPORT ORDER** (in the **LIST & ORDER** menu below the list) saves the load order of the selected profile to a text file. On the other PC, with the mods in its library, select a profile and press **IMPORT ORDER**: a preview shows what changes before anything is written, including mods the other library does not have and mods found only by their name. Categories are shared by all profiles, so categories the list creates appear in every profile there. Nothing changes in the game until **APPLY CHANGES**. The file is plain text you can also edit in Notepad; the format is in the [user guide, section 10](USER-GUIDE.md#save-and-load-the-order-export-order-import-order). EXPORT LIST is a report for bug reports and cannot be loaded back.

## AFTERLIFE says the request limit of Nexus Mods is reached

Nexus Mods allows every key 20,000 requests a day and 500 an hour, for all programs that use the key together. AFTERLIFE stops a check a little before the limit so that downloads still work, waits until the quota is back and then goes on by itself; you do not have to do anything. If it happens often, raise the number of days between full reads in **Settings → Updates** (the line under it shows what your choice costs), or switch off the check every hour.

## Is it free? What do I need Nexus Premium for?

AFTERLIFE is free and open source (MIT). Everything works with a free Nexus Mods account. Nexus allows direct downloads through its API only for Premium members, so:

- **Premium**: UPDATE, UPDATE ALL and collections download files directly, at full speed. Updating a few hundred mods or adding a collection is one click instead of one click per mod. This is where AFTERLIFE works best.
- **Free**: AFTERLIFE opens the right Nexus download page and you click *Slow download* (or *Mod Manager Download*); the file then goes straight into the library. For UPDATE ALL and collections, AFTERLIFE opens the next page by itself when the previous file has arrived.

Without any Nexus key AFTERLIFE still works with archives you drop into the window, it just cannot show mod pages, requirements from Nexus or updates.

## Can I use it together with Vortex or Mod Organizer 2?

Not on the same game install at the same time. Two managers writing to one game folder will fight over files. AFTERLIFE never overwrites or deletes files it did not install, so it will not break Vortex's files, but it also cannot manage them.

To switch: in Vortex use *Purge* (or disable all mods and deploy), close it, then start AFTERLIFE. Files Vortex leaves behind show up in **Scan manual mods**. Import your mods again into AFTERLIFE from the archives (Vortex keeps them in its downloads folder).

## I already have mods installed by hand

They keep working. AFTERLIFE shows them in **Scan manual mods** and in the core mods table (as *manual*), and counts manually installed frameworks as present when it checks requirements. If you want AFTERLIFE to manage them, remove them there (they are moved into the library, not deleted) and import their archives.

## Does it change my game files?

Only what you apply, and only inside the mod folders (`archive/pc/mod`, `bin/x64/plugins`, `r6/scripts`, `r6/tweaks`, `red4ext`, `mods` and the other usual places). AFTERLIFE does not patch the game executable and does not change original game archives. Every file it puts in the game is recorded with its hash, so it can always take exactly those files out again. Applying **Mods off** returns the game to the state without AFTERLIFE's mods.

## The game crashes after I applied a profile

1. Apply **Mods off** and start the game. If it still crashes, the problem is not in AFTERLIFE's mods: verify the game files in your launcher.
2. If it runs, check the core mods table: after a game patch, CET, RED4ext and their plugins often need a new version.
3. Look for **✕** (missing requirement) and **⚠** (conflict) marks in the list.
4. Switch half of the mods off, apply, test, repeat. *Export list* helps you keep notes.

## The power went out while it was applying

Start AFTERLIFE again. It reads its journal and offers **FINISH** (complete the apply) or **RETURN TO PREVIOUS** (put back the previous state). Files are only ever renamed into place after they were written and checked, so you will not end up with half-written files.

## Mod Manager Download opens Vortex instead of AFTERLIFE

Turn on *Open Mod Manager Download links in AFTERLIFE* in **Settings → Nexus Mods**. If another manager took the links back later, turn the option off and on again. Turning it off gives the links back to the app that had them before.

## UPDATE only opens a web page

That is how it works on a free Nexus account: Nexus does not allow direct API downloads without Premium. Click *Slow download* on the page that opened; AFTERLIFE replaces the old version, applies when the profile is in the game and deletes the old version from the library. See [Is it free?](#is-it-free-what-do-i-need-nexus-premium-for).

## Input Loader shows CHANGED OUTSIDE

Fixed in 1.0.0-alpha.2. Input Loader ships `r6/cache/inputContexts.xml`, `inputUserMappings.xml` and `input_loader.ini` only as a starting copy and rewrites them every time the game runs. AFTERLIFE now knows these files: they are not reported as changed, they are removed with Input Loader and replaced when it is updated. The same applies to `user.ini` of Mod Settings.

## How do I report a problem?

Turn on **Settings → Diagnostics → Log what AFTERLIFE does**, repeat what went wrong, then use **Open log folder** and attach `activity-YYYYMMDD.log` together with **Copy diagnostics** to your report on GitHub or the Nexus Mods page. The log holds what AFTERLIFE did: mod and profile names, files and results. Your Windows user name, the computer name, the folders of the game, the library and your profile, e-mail addresses and the secrets of download links are replaced by marks such as `<user>` and `<game>` before a line is written.

## A mod says a requirement is missing, but I have it

- If you installed the requirement by hand, **Refresh** (F5) so AFTERLIFE reads the game folder again.
- If it is in the library but switched off, switch it on (the requirement line says *in library · not deployed*).
- Requirements from the Nexus page are what the author listed. If the author lists something you do not really need, you can ignore the mark; it does not block anything.

## My REDmods do nothing

REDmods need the free **REDmod** DLC of the game (Steam, GOG and Epic all have it) and the game has to be started with `-modded`. Start the game with **JACK IN**: AFTERLIFE runs `redMod.exe deploy` first when the REDmod list changed and adds `-modded` for you. Starting the game from the launcher directly skips that.

## The library takes a lot of space

The library keeps every mod version you imported, the downloaded archives and the files it moved out of the game, so you can always go back. **Settings → Kept files** shows how much each part takes, opens its folder with **Show** and lets you delete it. Delete old mod versions you do not need with **DELETE EVERYWHERE**.

## Can I move the library to another drive?

Yes: **Settings → 01 · Game & library → MOVE…**. Choose an empty or new folder; AFTERLIFE moves everything it keeps there and uses the new folder from then on. The game is not touched and your mods keep working. If the move is interrupted, the next start offers to continue it or to keep the library where it was. Do not move the folder by hand in Explorer: AFTERLIFE would not find it.

## Does it work on Steam Deck, Linux or macOS?

Not officially. AFTERLIFE is a Windows (WPF) app and is tested only on Windows 10 and 11.

## Does it send anything about me anywhere?

No telemetry, no analytics, no ads, no self-updater. It talks only to Nexus Mods, and only for things you use. See [PRIVACY.md](../PRIVACY.md).

## How do I report a bug?

Open an issue on [GitHub](https://github.com/silentbobcz/afterlife/issues/new/choose) with:

- **Settings → Diagnostics → Copy diagnostics** (paste it into the issue),
- the exported mod list (**Export list**),
- what you did and what happened,
- if possible, the latest log from `%LOCALAPPDATA%\AFTERLIFE\logs`.

Or write to silentbob@silentbob.cz.
