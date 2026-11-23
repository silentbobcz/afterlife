# NEW: a place for freshly installed mods (design, no code yet)

Preview: [`new-category.html`](new-category.html) (open by double click).

## The problem

A mod that was just installed lands at the end of the part of MODS without a category. In a library of several hundred mods that is the middle of a long list: the user installs a mod and then has to search for it. The owner asked for a section named **NEW**, on top by default, that every freshly installed mod falls into and from which the user drags mods to where they belong. When NEW is empty it hides; when the next mod arrives it comes back at the place where the user left it.

## How the list works today (what the design builds on)

- The list is split into sections in load order (`BlockLayout.Sections`): CORE MODS first (not part of the layout), then the **blocks** in the saved order (`BlockLayout.Blocks`: every collection and MODS), and inside MODS first the mods without a category, then every category in `BlockLayout.Categories` order.
- The layout (blocks, categories, which mod is in which category) is one record for the whole library (`ui.layout` in the meta table), shared by all profiles. The **order of the mods** is per profile (the profile's entries, `ProfileService.Reorder`), and `modlist.txt` is written from it on APPLY CHANGES.
- The load order always follows the sections: `BlockLayout.Compose` is a stable sort of the profile's order by section. So moving a section moves its mods in the load order, and the other mods keep their order among themselves.
- A mod that is not in the profile yet is shown at the end of its section (`ArrangeRows` in `MainViewModel.LoadOrder.cs`), and with a custom order `SyncSortedOrderAsync` writes that place into the profile. A profile still in the game's default order (no `modlist.txt`) has no order to write: the game loads archives by file name and the numbers in the list are only the order AFTERLIFE would write.
- Blocks can already be moved among themselves and categories among themselves (`MoveBlockAsync`); a category cannot leave MODS and the part of MODS without a category is always first inside MODS.

## The design

**NEW is a block of its own**, next to the collections and MODS, with the key `new` in `BlockLayout.Blocks`.

- **Default place:** first block, right under CORE MODS. `NormalizedBlocks` adds it at the start when the saved layout does not know it yet.
- **Moving it:** like any block today (drag the header, or the arrows): above or below a collection, above or below MODS. The place is saved in `Blocks`, so it survives while NEW is empty.
- **Empty NEW is not shown** (no header, no empty frame). Its place in `Blocks` stays, so the next installed mod brings it back where it was.
- **Membership** is a set of package ids in the layout (`NewIds`), pruned like `CategoryOf` when a mod leaves the library. A mod leaves NEW when the user drags it (or moves it with the bulk bar / MOVE TO CATEGORY) into MODS or a category; then it is an ordinary mod. Switching a mod on or off does not move it.
- **Inside NEW** the rows behave like anywhere else: free order by dragging, numbers, Ctrl+arrows, LOAD BEFORE. The header has no A→Z and no rename/remove; it has one button, **MOVE ALL TO MODS**, which empties NEW in one step by putting its mods at the end of MODS without a category in their current order (exactly where they would have landed before NEW existed).
- **Header text:** `NEW` · `Freshly installed · drag each mod to where it belongs` · count. Colour: the green token (`Ok`) to set it apart from MODS (text colour), collections (cyan) and categories (violet).

## The four questions from the task

### (a) What happens to the load order

A mod in NEW loads **where NEW stands in the list**, like every other section. With NEW on top the new mod loads first after CORE MODS, so it wins file conflicts with everything below it. Installing a mod **never changes the order of the other mods among themselves**: `Compose` is a stable sort, the new mod is added in front (or wherever NEW stands) and the rest keeps its sequence. What the user sees: the new mod is `#1` and every other number grows by one; if the new mod wins or loses a file conflict, the row shows it the same way as after a drag (`FlipText`, the conflict marks).

In a profile that is still in the game's default order nothing is written at all: installing does not create `modlist.txt`, the game keeps loading by file name. Only the first drag in that profile makes the order custom, as today.

This differs from today in one point, and it is the first decision for the owner (below): today a new mod lands *after* every uncategorised mod (it loses conflicts with them and wins against categories); in NEW on top it lands *before* them.

### (b) Mods that arrive through IMPORT ORDER

They do **not** go to NEW. A load list says for every line which category and which place the mod has; the import downloads the missing mods (switched off) and the second load of the same list puts them where the list says. Putting them into NEW first would only make the user move them twice. Rule in code: the load-list download marks its imports as "placed by a list", and `ApplyLayoutAsync` from the import removes every mod it places from `NewIds` anyway.

### (c) Updating a mod

An update is **not** a new mod. The newer version takes over the category of the older one today (`MainViewModel.Updates.cs`) and its place in the profile; with NEW it also takes over the membership: an updated mod that sat in a category stays there, one that still sat in NEW stays in NEW. The same holds for versions merged in the VERSIONS dialog (`MainViewModel.Versions.cs`) and for layers ("└ name"), which follow their mod.

### (d) Many mods at once

- **A collection:** its mods belong to the block of the collection, never to NEW (`SectionOf` already puts a collection member into the collection's block first). A collection of 400 mods does not touch NEW.
- **Several archives dropped or downloaded at once** (30 files into the import): all of them go to NEW, in the order they were imported. NEW is one section with one header, so 30 rows on top are still easier to find than 30 rows scattered at the end of MODS; MOVE ALL TO MODS clears them in one step when the user does not want to sort them.
- **The FIXER drop box:** quests go to the FIXER category as today (`Fixer.cs` assigns it), not to NEW.
- **SCAN MANUAL MODS** (taking over files that already sit in the game): these are not freshly installed, they go to MODS as today.

## Other cases

- **Profiles.** NEW is part of the layout, so it is the same in every profile: a mod installed while profile A is selected is in NEW in profile B too (there it is simply off, like any mod that profile never used). Dragging it out in one profile takes it out everywhere, exactly like category membership today.
- **A mod installed switched off** (for example a download for a load list): NEW shows it switched off; it leaves NEW only by being moved.
- **An existing library after the app update:** `NewIds` is empty, NEW is not shown, `new` is added to `Blocks` without any mod in it, so `Compose` returns the same order as before. Nothing moves and nothing is written to any profile. Mods installed before the update stay where they are.
- **Filters, search, sort for viewing:** NEW is a section like the others; they only change what is shown.
- **EXPORT ORDER / IMPORT ORDER:** the load list writes a mod in NEW as a mod of MODS without a category, placed first or last in MODS by where NEW stands relative to MODS. The format (`load-list-format.md`) does not change; NEW is a working place, not something to share.
- **Settings:** see decision 3.

## Decisions for the owner

### 1. Where a mod in NEW loads

- **A. Where NEW stands in the list (recommended).** One rule for the whole list: higher loads first. A new mod on top wins file conflicts until it is moved; the row shows every conflict it wins. Costs: a new mod can override a file of an older mod right after installing, where today it would lose to the uncategorised mods.
- **B. NEW is shown on top but loads last.** Nothing that is already installed can be overridden by a new mod. Costs: the one place in the list where position and load order disagree; rows in NEW would have no number, and the user has to learn the exception.

### 2. How far NEW can be moved

- **A. Among the blocks (recommended):** above or below collections and MODS, like blocks move today. Small change, reuses the existing block moves, and "on top" really is the top of the list, above collections too.
- **B. Also between categories inside MODS.** Closest to "like any other category". Costs: the part of MODS without a category is fixed as first inside MODS today; NEW between categories needs the section order generalised (`Sections`, `Compose` callers, the load list), roughly twice the work and more to test.

### 3. Switch in Settings

- **A. No switch (recommended):** NEW always works; who does not want it presses MOVE ALL TO MODS or moves NEW to the bottom. One behaviour to explain and test.
- **B. A switch "Put new mods into NEW" in Settings → List,** default on; off means the old behaviour (end of MODS). Costs: a second path through every install to keep working.

## What the code change will touch (for the follow-up package)

1. `Afterlife.Core/BlockLayout.cs`: `NewBlock = "new"`, `NewIds` in the record and in the stored JSON (missing in old layouts = empty), `NormalizedBlocks` adds `new` first when unknown, `AddNew` / `RemoveNew`, `Prune` and `Assign` drop ids from `NewIds`. Tests in `BlockLayoutTests.cs` (old JSON loads, empty NEW changes no order, stable order of the others).
2. `MainViewModel.Blocks.cs` → new file `MainViewModel.NewBlock.cs`: `SectionOf` returns `new` for members that are not in a collection and have no category; chapter for NEW; `UpdateGroupNames` leaves an empty NEW out; MOVE ALL TO MODS.
3. `Rows.cs` (`ChapterKind.New`), the chapter header template in `MainWindow.xaml` and `MainWindow.xaml.cs` (header kind → chapter), `MainViewModel.Collections.cs` only where chapters are built (the file is over the size limit: the NEW chapter is built in the new file and called from there).
4. Every place that adds a package to the library marks it: import (`MainViewModel.Import.cs`), Nexus download and "Mod Manager Download" links; not: updates (`MainViewModel.Updates.cs`), VERSIONS, collections, the load-list download (`MainViewModel.LoadList.cs`), FIXER, SCAN MANUAL MODS.
5. `LoadListPlan.cs` / `MainViewModel.LoadList.cs`: export treats NEW members as MODS; import clears `NewIds` for the mods it places.
6. `docs/USER-GUIDE.md` (the list, installing a mod), `docs/HOW-IT-WORKS.md` ("The list and the load order"), `CHANGELOG.md`.

Not verified by this design: nothing was run; the statements about today's behaviour come from reading the code named above.
