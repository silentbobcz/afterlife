# FAQ

- [Windows says it protected my PC](#windows-says-it-protected-my-pc)
- [Is it free? What do I need Nexus Premium for?](#is-it-free-what-do-i-need-nexus-premium-for)
- [Can I use it together with Vortex or Mod Organizer 2?](#can-i-use-it-together-with-vortex-or-mod-organizer-2)
- [I already have mods installed by hand](#i-already-have-mods-installed-by-hand)
- [Does it change my game files?](#does-it-change-my-game-files)
- [The game crashes after I applied a profile](#the-game-crashes-after-i-applied-a-profile)
- [The power went out while it was applying](#the-power-went-out-while-it-was-applying)
- [Mod Manager Download opens Vortex instead of AFTERLIFE](#mod-manager-download-opens-vortex-instead-of-afterlife)
- [UPDATE only opens a web page](#update-only-opens-a-web-page)
- [A mod says a requirement is missing, but I have it](#a-mod-says-a-requirement-is-missing-but-i-have-it)
- [My REDmods do nothing](#my-redmods-do-nothing)
- [The library takes a lot of space](#the-library-takes-a-lot-of-space)
- [Does it work on Steam Deck, Linux or macOS?](#does-it-work-on-steam-deck-linux-or-macos)
- [Does it send anything about me anywhere?](#does-it-send-anything-about-me-anywhere)
- [How do I report a bug?](#how-do-i-report-a-bug)

## Windows says it protected my PC

`AFTERLIFE.exe` is not code-signed yet, so Microsoft Defender SmartScreen shows *Windows protected your PC* the first time. Click **More info → Run anyway**. You can check that the file is the one from the release: every release lists the SHA-256 of the zip, and the source code is in this repository, built by GitHub Actions.

## Is it free? What do I need Nexus Premium for?

AFTERLIFE is free and open source (MIT). Everything works with a free Nexus Mods account. Nexus allows direct downloads through its API only for Premium members, so:

- **Premium**: UPDATE, Update all and collections download files directly.
- **Free**: AFTERLIFE opens the right Nexus page and you click *Mod Manager Download*; the file then goes straight into the library. For collections, AFTERLIFE opens the next page by itself.

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

Turn on *Open Mod Manager Download links in AFTERLIFE* in **Settings → 03 · NEXUS MODS**. If another manager took the links back later, turn the option off and on again. Turning it off gives the links back to the app that had them before.

## UPDATE only opens a web page

That is how it works on a free Nexus account: Nexus does not allow direct API downloads without Premium. Click *Mod Manager Download* on the page that opened; AFTERLIFE puts the new version in your profile instead of the old one. See [Is it free?](#is-it-free-what-do-i-need-nexus-premium-for).

## A mod says a requirement is missing, but I have it

- If you installed the requirement by hand, **Refresh** (F5) so AFTERLIFE reads the game folder again.
- If it is in the library but switched off, switch it on (the requirement line says *in library · not deployed*).
- Requirements from the Nexus page are what the author listed. If the author lists something you do not really need, you can ignore the mark; it does not block anything.

## My REDmods do nothing

REDmods need the free **REDmod** DLC of the game (Steam, GOG and Epic all have it) and the game has to be started with `-modded`. Start the game with **JACK IN**: AFTERLIFE runs `redMod.exe deploy` first when the REDmod list changed and adds `-modded` for you. Starting the game from the launcher directly skips that.

## The library takes a lot of space

The library keeps every mod version you imported, the downloaded archives and the files it moved out of the game, so you can always go back. **Settings → 04 · KEPT FILES** shows how much each part takes and lets you delete it. Delete old mod versions you do not need with **DELETE EVERYWHERE**.

## Does it work on Steam Deck, Linux or macOS?

Not officially. AFTERLIFE is a Windows (WPF) app and is tested only on Windows 10 and 11.

## Does it send anything about me anywhere?

No telemetry, no analytics, no ads, no self-updater. It talks only to Nexus Mods, and only for things you use. See [PRIVACY.md](../PRIVACY.md).

## How do I report a bug?

Open an issue on [GitHub](https://github.com/silentbobcz/afterlife/issues/new/choose) with:

- **Settings → Copy diagnostics** (paste it into the issue),
- the exported mod list (**Export list**),
- what you did and what happened,
- if possible, the latest log from `%LOCALAPPDATA%\AFTERLIFE\logs`.

Or write to silentbob@silentbob.cz.
