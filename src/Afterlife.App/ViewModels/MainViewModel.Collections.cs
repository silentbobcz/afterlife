using System.IO;
using Afterlife.App.Services;
using Afterlife.Core;
using Afterlife.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace Afterlife.App.ViewModels;

// P50/P51: Nexus Mods collections (AFTERLIFE-souhrn-0.4.md, chapter 4).
public sealed partial class MainViewModel
{
    /// <summary>The collection whose missing mods are being downloaded through the website (free accounts).</summary>
    private string? _collectionDownloading;

    public const string CollectionChapterPrefix = "COLLECTION · ";

    [RelayCommand]
    private Task AddCollection() => RunUiAsync(async () =>
    {
        var text = await AskAsync(new TextPromptVm("ADD COLLECTION", "Link to a Cyberpunk 2077 collection on nexusmods.com", ""));
        if (text is null) return;
        if (!CollectionLink.TryParse(text, out var link)) { Say("This is not a link to a Nexus Mods collection.", Tone.Block); return; }
        await OpenCollectionAsync(link);
    });

    /// <summary>From the ADD COLLECTION button or an nxm://…/collections/… link.</summary>
    internal async Task OpenCollectionAsync(CollectionLink link)
    {
        if (!link.Game.Equals(NxmLink.Cyberpunk, StringComparison.OrdinalIgnoreCase)) { Say($"This collection is for {link.Game}, not Cyberpunk 2077.", Tone.Block); return; }
        var key = NexusKeyStore.Load();
        if (key is null) { Say("Collections come from Nexus Mods: add your personal API key in Settings first.", Tone.Warn); OpenSettings(); return; }
        if (_overview is null) return;
        var existing = await _host.RunAsync(c => c.Collections().FirstOrDefault(r => r.Slug == link.Slug));
        if (existing is not null) { ManageCollection(CollectionChapterPrefix + existing.Name); return; }

        Say("Reading the collection from Nexus Mods…", Tone.Info);
        var rev = await FetchRevisionAsync(link, key);
        if (rev is null) return;
        var packages = _overview.Mods.Select(m => m.Package).ToList();
        var states = CollectionLogic.States(rev, packages);
        var preview = new CollectionPreviewVm(rev, states, GameVersion, _nexusAccount is { IsPremium: true });
        if (!await AskAsync(preview, v => ((CollectionPreviewVm)v).Result)) { Say("Collection not added.", Tone.Neutral); return; }

        // target profile
        string profileId;
        if (preview.NewProfile)
        {
            // P80/P81: the core stays as it is: core mods switched on now are on in the new profile too
            var coreOn = _overview.Mods.Where(m => m.IsCore && !IsModsOff && _overview.Status.Selected.IsEnabled(m.Package.PackageId)).Select(m => m.Package.PackageId).ToList();
            var p = await _host.RunAsync(c => c.CreateProfile(rev.Name));
            profileId = p.Id;
            await _host.RunAsync(c =>
            {
                c.SelectProfile(p.Id);
                if (coreOn.Count > 0) c.SetEnabledMany(p.Id, coreOn, true);
            });
        }
        else profileId = _overview.Status.Selected.Id;

        var matched = states.Where(s => s.Status == ItemStatus.InLibrary && s.PackageId is not null)
            .Where(s => !s.Item.Optional || preview.ChosenOptional.Contains(s.Item.Key))
            .ToDictionary(s => s.Item.Key, s => s.PackageId!);
        var record = new CollectionRecord(rev, profileId, matched, preview.ChosenOptional, matched.Values.Distinct().ToList(), preview.NewProfile, DateTimeOffset.UtcNow);
        await _host.RunAsync(c =>
        {
            CollectionStore.Save(c.Store, record);
            if (matched.Count > 0) c.SetEnabledMany(profileId, matched.Values.Distinct().ToList(), true);
        });

        // mods that came inside the collection
        var bundle = BundlePath(rev);
        foreach (var item in record.Missing.Where(i => i.Source == CollectionSource.Bundle))
        {
            var file = File.Exists(bundle) ? await _host.RunAsync(c => c.ExtractBundled(bundle, item)) : null;
            if (file is null) continue;
            var imported = await ImportFilesCoreAsync([file], null, item.Choices, enable: false, quiet: true);
            if (imported.Count == 1) await AttachAsync(rev.Slug, item, imported[0], file);
        }
        await RefreshAsync();
        await ContinueCollectionAsync(rev.Slug);
    }

    private string BundlePath(CollectionRevisionInfo rev) =>
        Path.Combine(AppSettings.ResolveLibrary(null), "downloads", "collections", $"{NexusClient.SafeFileName(rev.Slug)}-r{rev.Revision}.7z");

    private async Task<CollectionRevisionInfo?> FetchRevisionAsync(CollectionLink link, string key)
    {
        using var client = new NexusClient(key, AppVersion);
        CollectionRevisionInfo rev;
        try { rev = await client.CollectionRevisionAsync(link); }
        catch (Exception ex) when (ex is NexusException or System.Net.Http.HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            Say("Nexus Mods: " + ex.Message, Tone.Block);
            return null;
        }
        // the bundle (collection.json) has the installer choices and load rules; best effort
        if (rev.BundleLink is { } dl)
        {
            var file = BundlePath(rev);
            try
            {
                if (!File.Exists(file) && !await client.CollectionBundleAsync(dl, file)) return rev;
                return await _host.RunAsync(c => c.ReadCollectionBundle(rev, file));
            }
            catch (Exception ex) when (ex is NexusException or System.Net.Http.HttpRequestException or TaskCanceledException or IOException or ImportException) { }
        }
        return rev;
    }

    // ---------------- P69/P70: download progress ----------------
    private CollectionProgressVm? _collectionProgress;

    private void ShowCollectionWindow(CollectionRecord r, IReadOnlyList<CollectionItemState> states)
    {
        var progress = _collectionProgress?.Slug == r.Slug ? _collectionProgress : null;
        if (progress is not null) progress.Hidden = false;
        Overlay = new ManageCollectionVm(r, states, this)
        {
            Progress = progress,
            CloseCommand = new RelayCommand(() => { if (progress is not null) progress.Hidden = true; Overlay = null; }),
        };
    }

    /// <summary>The collection window stays in front while its mods download (unless the user closed it).</summary>
    private void KeepCollectionWindow(string slug, CollectionRecord r)
    {
        if (_collectionProgress is not { Hidden: false } p || p.Slug != slug || _overview is null) return;
        if (Overlay is ManageCollectionVm m && m.Record.Slug == slug && m.Progress == p) return;
        if (Overlay is not null && Overlay is not ManageCollectionVm) return; // a question is open; it puts the window back
        ShowCollectionWindow(r, CollectionLogic.States(r.Revision, _overview.Mods.Select(x => x.Package).ToList()));
    }

    private CollectionProgressVm ProgressFor(CollectionRecord r)
    {
        if (_collectionProgress?.Slug != r.Slug)
            _collectionProgress = new CollectionProgressVm(r.Slug, r.Name)
            {
                PauseCommand = new RelayCommand(PauseCollection),
                ResumeCommand = new AsyncRelayCommand(() => ResumeCollectionAsync(r.Slug)),
            };
        var p = _collectionProgress;
        var wanted = r.Wanted.ToList();
        p.Total = wanted.Count;
        p.Done = wanted.Count(i => r.PackageOf.ContainsKey(i.Key));
        var known = p.Items.ToDictionary(i => i.Key);
        p.Items.Clear();
        foreach (var i in wanted)
        {
            var vm = known.TryGetValue(i.Key, out var old) ? old : new CollectionProgressItemVm(i.Key, i.Name);
            if (r.PackageOf.ContainsKey(i.Key)) { vm.Status = "in library"; vm.Tone = Tone.Ok; }
            else if (i.Source != CollectionSource.Nexus) { vm.Status = "add by hand"; vm.Tone = Tone.Warn; }
            else if (vm.Tone != Tone.Block) { vm.Status = "waiting"; vm.Tone = Tone.Neutral; }
            p.Items.Add(vm);
        }
        return p;
    }

    private void PauseCollection()
    {
        if (_collectionProgress is not { } p) return;
        p.PauseRequested = true;
        // free account: stop opening further pages; a page already open may still hand its file over
        if (_nexusAccount is not { IsPremium: true }) { p.IsPaused = true; p.IsRunning = false; _collectionWaiting = null; p.StatusText = "Paused. Resume opens the next page."; }
        else p.StatusText = "Pausing after the file in progress…";
    }

    private Task ResumeCollectionAsync(string slug)
    {
        if (_collectionProgress is { } p)
        {
            p.PauseRequested = false;
            p.IsPaused = false;
            p.Failed = 0;
            foreach (var row in p.Items.Where(x => x.Tone == Tone.Block)) { row.Status = "waiting"; row.Tone = Tone.Neutral; }
        }
        return ContinueCollectionAsync(slug);
    }

    /// <summary>Downloads what is still missing: Premium directly, otherwise page by page through Mod Manager Download.</summary>
    public Task ContinueCollectionAsync(string slug) => RunUiAsync(async () =>
    {
        var r = await _host.RunAsync(c => CollectionStore.Load(c.Store, slug));
        if (r is null) return;
        var progress = ProgressFor(r);
        KeepCollectionWindow(slug, r);
        var missing = r.Missing.Where(i => i.Source == CollectionSource.Nexus && i.ModId is not null && i.FileId is not null).ToList();
        if (missing.Count == 0)
        {
            await ApplyCollectionOrderAsync(slug, false);
            var outside = r.Missing.Count;
            progress.IsRunning = false;
            progress.IsPaused = false;
            progress.Current = null;
            progress.CurrentPercent = 0;
            progress.StatusText = outside == 0 ? "Complete. Apply to put the collection in the game."
                : $"Everything from Nexus is in. {outside} item(s) outside Nexus need adding by hand.";
            Say(outside == 0 ? $"Collection {r.Name} is complete in the library. Apply to put it in the game."
                : $"Collection {r.Name}: everything from Nexus is in. {outside} item(s) outside Nexus need adding by hand (see the collection).", outside == 0 ? Tone.Ok : Tone.Warn);
            _collectionDownloading = null;
            _collectionWaiting = null;
            return;
        }
        var key = NexusKeyStore.Load();
        if (key is null) { OpenSettings(); return; }
        progress.PauseRequested = false;
        progress.IsPaused = false;
        progress.IsRunning = true;
        if (_nexusAccount is { IsPremium: true })
        {
            var n = 0;
            foreach (var item in missing)
            {
                if (progress.PauseRequested) break;
                n++;
                PhaseTitle = $"COLLECTION · {n}/{missing.Count}";
                var row = progress.Item(item.Key);
                if (row is not null) { row.Status = "downloading"; row.Tone = Tone.Info; }
                progress.Current = item.Name;
                progress.CurrentPercent = 0;
                progress.StatusText = $"Downloading {item.Name}…";
                var link = new NxmLink(NxmLink.Cyberpunk, item.ModId!, item.FileId!, null, null);
                var got = await DownloadNexusFileAsync(link, key, pct => progress.CurrentPercent = pct);
                if (got is null)
                {
                    if (row is not null) { row.Status = "failed"; row.Tone = Tone.Block; }
                    progress.Failed++;
                    continue;
                }
                if (row is not null) { row.Status = "installing"; row.Tone = Tone.Info; }
                progress.CurrentPercent = 100;
                await ImportCollectionItemAsync(slug, item, got.Value.Dest);
                var now = await _host.RunAsync(c => CollectionStore.Load(c.Store, slug));
                if (now is not null) { ProgressFor(now); KeepCollectionWindow(slug, now); }
            }
            PhaseIdle();
            if (progress.PauseRequested)
            {
                progress.IsRunning = false;
                progress.IsPaused = true;
                progress.Current = null;
                progress.StatusText = "Paused. Resume goes on with the next mod; the ones already downloaded stay.";
                Say($"Collection {r.Name}: download paused ({progress.Done}/{progress.Total}).", Tone.Warn);
                return;
            }
            if (progress.Failed > 0)
            {
                // no endless retry: stop, Resume tries the failed ones again
                progress.IsRunning = false;
                progress.Current = null;
                progress.StatusText = $"{progress.Failed} mod(s) could not be downloaded. Resume tries them again.";
                Say($"Collection {r.Name}: {progress.Failed} mod(s) could not be downloaded. Open the collection and press Resume to try again.", Tone.Warn);
                return;
            }
            await ContinueCollectionAsync(slug);
            return;
        }
        // free account: the website hands each file over with a key
        _collectionDownloading = slug;
        var waitingFor = missing.ToList();
        _collectionWaiting = (link, dest, info) =>
        {
            var item = waitingFor.FirstOrDefault(i => i.FileId == link.FileId && i.ModId == link.ModId);
            if (item is null) return false;
            waitingFor.Remove(item);
            if (progress.Item(item.Key) is { } row) { row.Status = "installing"; row.Tone = Tone.Info; }
            _ = TakeCollectionDownloadAsync(slug, item, dest);
            return true;
        };
        var first = missing[0];
        if (progress.Item(first.Key) is { } fr) { fr.Status = "open on Nexus"; fr.Tone = Tone.Info; }
        progress.Current = first.Name;
        progress.CurrentPercent = 0;
        progress.StatusText = $"Click Mod Manager Download on the page for {first.Name}. AFTERLIFE opens the next one by itself.";
        AppLinks.Open(first.PageUrl!);
        Say($"Collection {r.Name}: click Mod Manager Download on the page ({r.Wanted.Count() - missing.Count + 1}/{r.Wanted.Count()}: {first.Name}). AFTERLIFE opens the next one by itself.", Tone.Info);
    });

    private async Task TakeCollectionDownloadAsync(string slug, CollectionItem item, string dest)
    {
        await ImportCollectionItemAsync(slug, item, dest);
        if (_collectionProgress is { Slug: var s, PauseRequested: true } p && s == slug)
        {
            if (await _host.RunAsync(c => CollectionStore.Load(c.Store, slug)) is { } now) ProgressFor(now);
            p.IsPaused = true;
            p.IsRunning = false;
            return;
        }
        await ContinueCollectionAsync(slug);
    }

    /// <summary>Imports one downloaded file of a collection with the curator's installer choices and links it to the item.</summary>
    private async Task ImportCollectionItemAsync(string slug, CollectionItem item, string file)
    {
        var imported = await ImportFilesCoreAsync([file], new NexusSource(item.ModId!, item.FileId!, item.Name, item.Version), item.Choices, enable: false, quiet: true);
        if (imported.Count == 1) await AttachAsync(slug, item, imported[0], file);
    }

    private async Task AttachAsync(string slug, CollectionItem item, PackageManifest p, string file)
    {
        var order = InstallCoordinator.ModlistInArchive(file);
        var warn = item.Md5 is not null && p.SourceMd5 is not null && !string.Equals(item.Md5, p.SourceMd5, StringComparison.OrdinalIgnoreCase);
        await _host.RunAsync(c =>
        {
            var r = CollectionStore.Load(c.Store, slug);
            if (r is null) return;
            var map = new Dictionary<string, string>(r.PackageOf) { [item.Key] = p.PackageId };
            var rev = order.Count > 0 ? r.Revision with { ArchiveOrder = order } : r.Revision;
            CollectionStore.Save(c.Store, r with { PackageOf = map, Revision = rev });
            if (r.ProfileId is { } pid && c.Store.GetProfile(pid) is { } prof)
            {
                // another version of the same mod in this profile goes off; the collection's version wins
                var others = c.Store.GetPackages().Where(x => x.PackageId != p.PackageId && x.NexusModId is not null && x.NexusModId == p.NexusModId && prof.IsEnabled(x.PackageId))
                    .Select(x => x.PackageId).ToList();
                if (others.Count > 0) c.SetEnabledMany(pid, others, false);
                c.SetEnabledMany(pid, [p.PackageId], true);
            }
        });
        if (warn) Say($"{item.Name}: the downloaded file differs from the one the curator used (checksum). It was added anyway.", Tone.Warn);
        await RefreshAsync();
    }

    /// <summary>The collection's order: its mods first, in the curator's order; with reset also after the user dragged them.</summary>
    public Task ApplyCollectionOrderAsync(string slug, bool reset) => RunUiAsync(async () =>
    {
        await _host.RunAsync(c =>
        {
            var r = CollectionStore.Load(c.Store, slug);
            if (r?.ProfileId is not { } pid || c.Store.GetProfile(pid) is not { } prof) return;
            if (!r.OrderLocked && !reset) return;
            var packages = c.Store.GetPackages();
            var order = CollectionLogic.Order(r, packages);
            var rest = prof.Entries.Select(e => e.PackageId).Where(id => !order.Contains(id)).ToList();
            c.Reorder(pid, order.Concat(rest).ToList());
            if (reset && !r.OrderLocked) CollectionStore.Save(c.Store, r with { OrderLocked = true });
        });
        if (Overlay is ManageCollectionVm) Overlay = null;
        await RefreshAsync();
        if (reset) Say("Load order set back to the collection's.", Tone.Ok);
    });

    public Task SetCollectionLockAsync(string slug, bool locked) => RunUiAsync(async () =>
    {
        if (!locked && !await AskAsync(new ConfirmVm("UNLOCK LOAD ORDER?",
                "The curator tested this order. If you drag its mods, crashes or missing items can be your change, not the collection's. You can go back with Reset to collection order.",
                "UNLOCK"))) return;
        await _host.RunAsync(c =>
        {
            if (CollectionStore.Load(c.Store, slug) is { } r) CollectionStore.Save(c.Store, r with { OrderLocked = locked });
        });
        Overlay = null;
        if (locked) await ApplyCollectionOrderAsync(slug, false);
        await RefreshAsync();
    });

    // ---------------- P68: collection chapters ----------------
    [ObservableProperty] private IReadOnlyDictionary<string, CollectionChapterVm> _collectionChapters = new Dictionary<string, CollectionChapterVm>();
    private IReadOnlyList<CollectionRecord> _collections = [];

    private async Task LoadCollectionChaptersAsync()
    {
        try
        {
            (_collections, var folded) = await _host.RunAsync(c => (c.Collections(), _collapsed is null ? c.Store.GetMeta("ui.collapsed") : null));
            if (_collapsed is null) _collapsed = (folded ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        }
        catch (Exception ex) when (IsExpected(ex)) { _collections = []; }
        BuildCollectionChapters();
    }

    private HashSet<string>? _collapsed;

    private void BuildCollectionChapters()
    {
        if (_overview is null) { CollectionChapters = new Dictionary<string, CollectionChapterVm>(); return; }
        _collapsed ??= new HashSet<string>(StringComparer.Ordinal);
        var selected = _overview.Status.Selected;
        var searching = !string.IsNullOrWhiteSpace(SearchText); // P83: a search shows what it finds, folded or not
        var canEdit = !IsModsOff && _overview.Status.Unfinished is null;
        bool Folded(string key) => !searching && _collapsed.Contains(key);
        ICommand Fold(string key) => new RelayCommand(() => ToggleFold(key));
        var rows = Mods.ToList();
        // P89: marks in the header; the click unfolds the chapter and selects the first such mod
        CollectionChapterVm Marks(CollectionChapterVm vm, string key)
        {
            var inChapter = rows.Where(r => r.Chapter == key).ToList();
            ModRowVm? First(Func<ModRowVm, bool> f) => inChapter.FirstOrDefault(f);
            ICommand Show(Func<ModRowVm, bool> f) => new RelayCommand(() => ShowInChapter(key, First(f)));
            static bool Upd(ModRowVm r) => r.Overview.Update is not null;
            static bool Con(ModRowVm r) => r.Overview.State == ModState.Conflict || r.Overview.Conflicts.Any(c => !c.ThisModWins);
            static bool Mis(ModRowVm r) => r.Overview.Missing.Count > 0;
            return new CollectionChapterVm(vm.Kind, vm.Slug, vm.Label, vm.Name, vm.On, vm.Total, vm.CanToggle, vm.IsCollapsed, vm.ToggleCommand, vm.ManageCommand, vm.FoldCommand)
            {
                Updates = inChapter.Count(Upd), Conflicts = inChapter.Count(Con), Missing = inChapter.Count(Mis),
                ShowUpdateCommand = Show(Upd), ShowConflictCommand = Show(Con), ShowMissingCommand = Show(Mis),
            };
        }
        var map = new Dictionary<string, CollectionChapterVm>(StringComparer.Ordinal);

        // P82: core mods as one block with a switch
        var core = _overview.Mods.Where(m => m.IsCore && m.Package.LayerOfModId is null).Select(m => m.Package.PackageId).ToList();
        var coreOn = core.Count(selected.IsEnabled);
        map[CoreChapter] = Marks(new CollectionChapterVm(ChapterKind.Core, CoreChapter, "CORE MODS", "Frameworks the other mods build on", coreOn, core.Count,
            canEdit && core.Count > 0, Folded(CoreChapter), new AsyncRelayCommand(() => SetCoreEnabledAsync(coreOn == 0)), null, Fold(CoreChapter)), CoreChapter);

        foreach (var r in _collections)
        {
            var ids = MembersOf(r);
            var on = ids.Count(selected.IsEnabled);
            var slug = r.Slug;
            var key = CollectionChapterPrefix + r.Name;
            map[key] = Marks(new CollectionChapterVm(ChapterKind.Collection, slug, "COLLECTION", r.Name, on, ids.Count,
                canEdit && ids.Count > 0, Folded(key),
                new AsyncRelayCommand(() => SetCollectionEnabledAsync(slug, on == 0)),
                new RelayCommand(() => ManageCollection(key)), Fold(key)), key);
        }
        // P88: the other mods get the same kind of block and a switch
        var plain = rows.Where(r => r.Chapter == PlainChapter && r.Overview.Package.LayerOfModId is null).Select(r => r.PackageId).ToList();
        var plainOn = plain.Count(selected.IsEnabled);
        map[PlainChapter] = Marks(new CollectionChapterVm(ChapterKind.Plain, PlainChapter, "MODS", "Your mods outside collections", plainOn, plain.Count,
            canEdit && plain.Count > 0, Folded(PlainChapter), new AsyncRelayCommand(() => SetPlainEnabledAsync(plainOn == 0)), null, Fold(PlainChapter)), PlainChapter);
        CollectionChapters = map;
    }

    private void ShowInChapter(string key, ModRowVm? row)
    {
        if (_collapsed?.Remove(key) == true)
        {
            var text = string.Join("\n", _collapsed);
            _ = _host.RunAsync(c => c.Store.SetMeta("ui.collapsed", text));
            BuildCollectionChapters();
        }
        if (row is not null) { SelectedMod = row; ScrollToSelected?.Invoke(); }
    }

    /// <summary>Asks the window to bring the selected row into view (P89).</summary>
    public event Action? ScrollToSelected;

    /// <summary>P88: all mods outside core and collections on or off; asks about requirements and dependents.</summary>
    private Task SetPlainEnabledAsync(bool on) => RunUiAsync(async () =>
    {
        if (_overview is null || IsModsOff) return;
        var selected = _overview.Status.Selected;
        var ids = Mods.Where(r => r.Chapter == PlainChapter && r.Overview.Package.LayerOfModId is null && selected.IsEnabled(r.PackageId) != on)
            .Select(r => r.PackageId).ToList();
        if (ids.Count == 0) return;
        var all = await WithRelatedAsync(ids, on);
        if (all is null) { await RefreshAsync(); return; }
        await _host.RunAsync(c => c.SetEnabledMany(c.SelectedProfile.Id, all, on));
        await RefreshAsync();
        Say(on ? $"{all.Count} mod(s) switched on in the profile. Apply to put them in the game."
               : $"{all.Count} mod(s) switched off in the profile. Apply to remove them from the game.", Tone.Info);
    });

    public const string CoreChapter = "CORE MODS";
    public const string PlainChapter = "MODS";

    private void ToggleFold(string key)
    {
        _collapsed ??= new HashSet<string>(StringComparer.Ordinal);
        if (!_collapsed.Add(key)) _collapsed.Remove(key);
        var text = string.Join("\n", _collapsed);
        _ = _host.RunAsync(c => c.Store.SetMeta("ui.collapsed", text));
        BuildCollectionChapters();
    }

    /// <summary>P82: all core mods on or off; off asks about the mods that need them (P54).</summary>
    private Task SetCoreEnabledAsync(bool on) => RunUiAsync(async () =>
    {
        if (_overview is null || IsModsOff) return;
        var selected = _overview.Status.Selected;
        var ids = _overview.Mods.Where(m => m.IsCore && m.Package.LayerOfModId is null && selected.IsEnabled(m.Package.PackageId) != on)
            .Select(m => m.Package.PackageId).ToList();
        if (ids.Count == 0) return;
        var all = await WithRelatedAsync(ids, on);
        if (all is null) { await RefreshAsync(); return; }
        await _host.RunAsync(c => c.SetEnabledMany(c.SelectedProfile.Id, all, on));
        await RefreshAsync();
        Say(on ? $"{all.Count} mod(s) switched on in the profile. Apply to put them in the game."
               : $"{all.Count} mod(s) switched off in the profile. Apply to remove them from the game.", Tone.Info);
    });

    /// <summary>Packages of the collection that are in the library (required and the optional ones chosen); never core mods (P81).</summary>
    private List<string> MembersOf(CollectionRecord r)
    {
        var have = _overview?.Mods.Where(m => !m.IsCore).Select(m => m.Package.PackageId).ToHashSet(StringComparer.Ordinal) ?? [];
        return r.PackageOf.Values.Distinct(StringComparer.Ordinal).Where(have.Contains).ToList();
    }

    /// <summary>
    /// P68: the whole collection on or off in the selected profile. Off keeps mods another switched-on collection
    /// uses, and asks about mods outside the collection that need it (P54); on asks about missing requirements (P53).
    /// </summary>
    public Task SetCollectionEnabledAsync(string slug, bool on) => RunUiAsync(async () =>
    {
        if (_overview is null || IsModsOff) return;
        var r = _collections.FirstOrDefault(x => x.Slug == slug);
        if (r is null) return;
        var selected = _overview.Status.Selected;
        var ids = MembersOf(r);
        if (!on)
        {
            var otherOn = _collections.Where(x => x.Slug != slug && MembersOf(x).Any(selected.IsEnabled))
                .SelectMany(MembersOf).ToHashSet(StringComparer.Ordinal);
            ids = ids.Where(id => selected.IsEnabled(id) && !otherOn.Contains(id)).ToList();
        }
        else ids = ids.Where(id => !selected.IsEnabled(id)).ToList();
        if (ids.Count == 0) { await RefreshAsync(); return; }
        var all = await WithRelatedAsync(ids, on);
        if (all is null) { await RefreshAsync(); return; }
        if (!on)
        {
            // P81: mods outside the collection it needed (not core) that nothing else switched on needs any more
            var extra = OverviewBuilder.UnneededRequirements(_overview, all);
            if (extra.Count > 0)
            {
                var ask = new ConfirmVm("ALSO SWITCH OFF WHAT IT NEEDED?",
                    $"{r.Name} used {extra.Count} mod(s) outside the collection that nothing else switched on needs. Core mods stay on.",
                    "SWITCH OFF", false, extra.Select(x => $"{x.Name}   · needed by {x.NeededBy}").ToList(),
                    $"Also switch off the {extra.Count} mod(s)", true, "NEEDED BY THE COLLECTION");
                if (!await AskAsync(ask)) { await RefreshAsync(); return; }
                if (ask.OptionChecked) all = all.Concat(extra.Select(x => x.PackageId)).Distinct().ToList();
            }
        }
        await _host.RunAsync(c => c.SetEnabledMany(c.SelectedProfile.Id, all, on));
        await RefreshAsync();
        Say(on ? $"{r.Name}: {all.Count} mod(s) switched on in the profile. Apply to put them in the game."
               : $"{r.Name}: {all.Count} mod(s) switched off in the profile. Apply to remove them from the game.", Tone.Info);
    });

    [RelayCommand]
    private void ManageCollection(string? chapter)
    {
        if (chapter is null || !chapter.StartsWith(CollectionChapterPrefix, StringComparison.Ordinal) || _overview is null) return;
        var name = chapter[CollectionChapterPrefix.Length..];
        _ = RunUiAsync(async () =>
        {
            var r = await _host.RunAsync(c => c.Collections().FirstOrDefault(x => x.Name == name));
            if (r is null) return;
            var states = CollectionLogic.States(r.Revision, _overview.Mods.Select(m => m.Package).ToList());
            ShowCollectionWindow(r, states);
        });
    }

    /// <summary>Remove a collection without losing mods that other mods or collections still need (4.6).</summary>
    public Task RemoveCollectionAsync(string slug) => RunUiAsync(async () =>
    {
        if (_overview is null) return;
        var all = await _host.RunAsync(c => c.Collections());
        var r = all.FirstOrDefault(x => x.Slug == slug);
        if (r is null) return;
        var (remove, keep) = CollectionLogic.RemovalPlan(r, all, _overview);
        var lines = remove.Select(x => $"− {x.Name}").Concat(keep.Select(x => $"= {x.Name}   · stays: {x.KeptBecause}")).ToList();
        var vm = new ConfirmVm($"REMOVE COLLECTION {r.Name.ToUpperInvariant()}?",
            $"{remove.Count} mod(s) only this collection uses leave the game and the library (their downloaded archives stay, so Import again works). "
            + $"{keep.Count} mod(s) stay because something else needs them."
            + (r.ProfileId is null ? "" : " The profile of the collection stays; you can switch away from it."),
            "REMOVE", danger: true, lines, "Keep all mods, remove only the collection (its chapter and labels)", false, "WHAT HAPPENS");
        if (!await AskAsync(vm)) return;
        Overlay = null;
        var report = new List<string>();
        if (!vm.OptionChecked && remove.Count > 0)
        {
            var outcome = await _host.RunAsync(c => c.DeleteEverywhere(remove.Select(x => x.PackageId).ToList()));
            report.AddRange(outcome.Deleted.Select(d => "  · removed " + d));
            report.AddRange(outcome.Kept.Select(k => $"  · kept {k.Name}: {k.Reason}"));
        }
        await _host.RunAsync(c => CollectionStore.Delete(c.Store, slug));
        if (_collectionDownloading == slug) { _collectionDownloading = null; _collectionWaiting = null; }
        await RefreshAsync();
        SayLines([$"Collection {r.Name} removed."], Tone.Ok);
        if (report.Count > 0) SayLines([$"Collection {r.Name} removed:", .. report], Tone.Ok);
    });

    /// <summary>New revision: added / updated / removed, then the same download and removal rules (4.7).</summary>
    public Task UpdateCollectionAsync(string slug) => RunUiAsync(async () =>
    {
        var key = NexusKeyStore.Load();
        if (key is null || _overview is null) { OpenSettings(); return; }
        var all = await _host.RunAsync(c => c.Collections());
        var r = all.FirstOrDefault(x => x.Slug == slug);
        if (r is null) return;
        var rev = await FetchRevisionAsync(new CollectionLink(NxmLink.Cyberpunk, slug, null), key);
        if (rev is null) return;
        if (rev.Revision <= r.Revision.Revision) { Say($"Collection {r.Name} is on the newest revision ({r.Revision.Revision}).", Tone.Ok); return; }
        var diff = CollectionLogic.Diff(r.Revision, rev);
        var lines = diff.Added.Select(i => $"+ {i.Name} {i.Version}")
            .Concat(diff.Updated.Select(u => $"~ {u.New.Name} {u.Old.Version} → {u.New.Version}"))
            .Concat(diff.Removed.Select(i => $"− {i.Name}")).ToList();
        if (!await AskAsync(new ConfirmVm($"UPDATE TO REVISION {rev.Revision}?",
                $"{diff.Added.Count} added · {diff.Updated.Count} updated · {diff.Removed.Count} removed · {diff.Unchanged} unchanged. Old versions stay in the library while something else uses them.",
                "UPDATE", lines: lines, linesTitle: "CHANGES"))) return;
        Overlay = null;
        var keepKeys = rev.Items.Select(i => i.Key).ToHashSet();
        var map = r.PackageOf.Where(kv => keepKeys.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
        // packages only the dropped/updated items used follow the removal rules
        var dropped = r with { PackageOf = r.PackageOf.Where(kv => !keepKeys.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value) };
        var (remove, _) = CollectionLogic.RemovalPlan(dropped, all.Where(x => x.Slug != slug).Append(r with { PackageOf = map }).ToList(), _overview);
        if (remove.Count > 0) await _host.RunAsync(c => c.DeleteEverywhere(remove.Select(x => x.PackageId).ToList()));
        var packages = await _host.RunAsync(c => c.Store.GetPackages());
        foreach (var item in rev.Items.Where(x => !map.ContainsKey(x.Key) && (!x.Optional || r.ChosenOptional.Contains(x.Key))))
        {
            var found = CollectionLogic.Match(item, packages);
            if (found is not null) map[item.Key] = found;
        }
        var updated = r with { Revision = rev, PackageOf = map };
        await _host.RunAsync(c =>
        {
            CollectionStore.Save(c.Store, updated);
            if (updated.ProfileId is { } pid) c.SetEnabledMany(pid, map.Values.Distinct().ToList(), true);
        });
        await RefreshAsync();
        await ContinueCollectionAsync(slug);
    });
}
