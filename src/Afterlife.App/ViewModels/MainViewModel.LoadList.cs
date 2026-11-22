using System.IO;
using System.Text;
using Afterlife.App.Services;
using Afterlife.Core;
using Afterlife.Infrastructure;
using CommunityToolkit.Mvvm.Input;

namespace Afterlife.App.ViewModels;

/// <summary>
/// EXPORT ORDER and IMPORT ORDER: the load order of the selected profile as a text file (<see cref="LoadList"/>) and
/// back. The import shows its plan first (<see cref="LoadListPlanner"/>), saves the current order into the library's
/// exports folder, then writes on/off through the profile and the order and categories through
/// <see cref="ApplyLayoutAsync"/>. Nothing reaches the game before APPLY.
/// </summary>
public sealed partial class MainViewModel
{
    private const string ModsOffOrderTip = "Mods off cannot be changed. Select another profile.";
    private const int OrderBackupsKept = 10;

    public string ImportOrderTip => IsModsOff ? ModsOffOrderTip : "Load an order saved with EXPORT ORDER into this profile; you see what changes first";

    partial void OnIsModsOffChanged(bool value) => OnPropertyChanged(nameof(ImportOrderTip));

    [RelayCommand]
    private void ExportOrder()
    {
        if (_overview is null) return;
        if (IsModsOff) { Say("Mods off has no load order to save. Select another profile.", Tone.Warn); return; }
        var d = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export order",
            Filter = "Text file|*.txt",
            FileName = LoadList.SuggestedFileName(_overview.Status.Selected.Name, DateTime.Now),
        };
        if (d.ShowDialog() != true) return;
        try
        {
            File.WriteAllText(d.FileName, LoadListText(_overview), new UTF8Encoding(true));
            Say($"Load order saved: {Path.GetFileName(d.FileName)}.", Tone.Ok);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Say("Could not save: " + ex.Message, Tone.Block); }
    }

    [RelayCommand]
    private Task ImportOrder() => RunUiAsync(async () =>
    {
        if (_overview is null) return;
        if (IsModsOff) { Say(ModsOffOrderTip, Tone.Warn); return; }
        var d = new Microsoft.Win32.OpenFileDialog { Title = "Import order", Filter = "Load list (*.txt)|*.txt|All files (*.*)|*.*" };
        if (d.ShowDialog() != true) return;
        await ImportLoadListAsync(d.FileName);
    });

    /// <summary>Reads a list, shows what it changes and imports it. Loading the same list again gives the same result.</summary>
    private async Task ImportLoadListAsync(string path)
    {
        if (_overview is null || IsModsOff) return;
        string text;
        try { text = await File.ReadAllTextAsync(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Say("Could not read the list: " + ex.Message, Tone.Block); return; }
        var read = LoadList.Read(text);
        if (read.Document is not { } doc) { Say(read.Error ?? LoadList.NotAListMessage, Tone.Block); return; }

        var o = _overview;
        var packages = LoadListPackages();
        var collections = _collections.Select(r => new LoadListCollection(r.Slug, r.Name, r.OrderLocked && r.ProfileId == o.Status.Selected.Id)).ToList();
        var layout = _layout;
        var custom = o.OrderIsCustom;
        LoadListPlan PlanFor(LoadListOptions options) => LoadListPlanner.Build(doc, packages, layout, collections, custom, options);

        var plan = PlanFor(new LoadListOptions());
        if (plan.NothingFound) { Say("None of the mods in the list are in your library. Nothing was loaded.", Tone.Warn); return; }
        if (plan.IsEmpty && plan.Missing.Count == 0 && plan.MissingCollections.Count == 0)
        {
            Say("The profile already has this order. Nothing changed.", Tone.Info);
            return;
        }
        // Premium downloads the missing Nexus files directly; other accounts get the pages in the list
        var downloadable = plan.Missing.Any(m => m.NexusModId is not null && m.NexusFileId is not null);
        if (!AppEdition.Offline && downloadable && _nexusAccount is null && NexusKeyStore.Load() is not null) await CheckNexusAsync(null);
        var canDownload = !AppEdition.Offline && downloadable && _nexusAccount is { IsPremium: true };

        var vm = new LoadListPreviewVm(Path.GetFileName(path), doc, o.Status.Selected.Name, PlanFor, !AppEdition.Offline, canDownload);
        var action = await AskAsync(vm, v => ((LoadListPreviewVm)v).Result);
        if (action == LoadListAction.GetFromNexus) { await GetMissingFromNexusAsync(vm.Plan.Missing, path); return; }
        if (action == LoadListAction.AddCollection)
        {
            if (vm.CollectionToAdd is { Slug: { } slug } s) await OpenCollectionAsync(new CollectionLink(NxmLink.Cyberpunk, slug, s.Revision));
            return;
        }
        if (action != LoadListAction.Import) { Say("Load order not imported. Nothing changed.", Tone.Neutral); return; }
        var chosen = vm.Plan;
        if (chosen.IsEmpty) { Say("The profile already has this order. Nothing changed.", Tone.Info); return; }
        await ApplyLoadListAsync(chosen);
    }

    /// <summary>Writes a plan: backup of the current order, collection locks, on/off, then categories and order.</summary>
    private async Task ApplyLoadListAsync(LoadListPlan plan)
    {
        if (_overview is null || IsModsOff) return;
        var profileId = _overview.Status.Selected.Id;
        var backup = await SaveOrderBackupAsync();
        var unlock = plan.Unlock.ToList();
        var on = plan.SwitchOn.ToList();
        var off = plan.SwitchOff.ToList();
        if (unlock.Count > 0 || on.Count > 0 || off.Count > 0)
            await _host.RunAsync(c =>
            {
                foreach (var slug in unlock)
                    if (CollectionStore.Load(c.Store, slug) is { } r) CollectionStore.Save(c.Store, r with { OrderLocked = false });
                if (on.Count > 0) c.SetEnabledMany(profileId, on, true);
                if (off.Count > 0) c.SetEnabledMany(profileId, off, false);
            });
        var message = $"Load order loaded from the list: {plan.Moved.Count} moved, {on.Count} switched on, {off.Count} switched off."
            + (backup ? " The previous order is saved in the library (exports)." : "");
        // a changed order gets the note about conflict winners and APPLY from ApplyLayoutAsync
        var reordered = plan.OrderChanged || plan.TakesOwnOrder;
        await ApplyLayoutAsync(plan.Layout, plan.Order.ToList(), message);
        if (!reordered) Say(message + " Apply to update the game.", Tone.Ok);
    }

    /// <summary>
    /// Missing mods with a Nexus file: Premium downloads them into the library (switched off), then the same list
    /// is loaded again and puts them in place.
    /// </summary>
    private async Task GetMissingFromNexusAsync(IReadOnlyList<LoadListMod> missing, string path)
    {
        if (LockedOffline("Getting mods from Nexus Mods")) return;
        var key = NexusKeyStore.Load();
        if (key is null) { Say("To download from Nexus Mods, paste your personal API key in Settings first.", Tone.Warn); OpenSettings(); return; }
        if (_nexusAccount is null) await CheckNexusAsync(null);
        if (_nexusAccount is not { IsPremium: true })
        {
            Say("Nexus lets free accounts download only on the website: open the pages of the missing mods from IMPORT ORDER, then load the same list again.", Tone.Warn);
            return;
        }
        var files = missing.Where(m => m.NexusModId is not null && m.NexusFileId is not null).ToList();
        var failed = new List<string>();
        var i = 0;
        foreach (var m in files)
        {
            Say($"Downloading {m.Name} ({++i}/{files.Count})…", Tone.Info);
            var link = new NxmLink(NxmLink.Cyberpunk, m.NexusModId!, m.NexusFileId!, null, null);
            var got = await DownloadNexusFileAsync(link, key);
            if (got is null) { failed.Add(m.Name); continue; }
            var imported = await ImportDownloadedAsync(link, got.Value.Dest, got.Value.Info, enable: false, quiet: true);
            if (imported.Count == 0) failed.Add(m.Name);
        }
        await RefreshAsync();
        if (failed.Count > 0) SayLines([$"Not downloaded ({failed.Count}):", .. failed.Select(f => "  · " + f)], Tone.Warn);
        await ImportLoadListAsync(path);
    }

    /// <summary>D6: the current order as a load list in the library (exports/before-import-…txt), the newest ten kept.</summary>
    private async Task<bool> SaveOrderBackupAsync()
    {
        if (_overview is null || _host.Library is not { } lib) return false;
        var text = LoadListText(_overview);
        var folder = Path.Combine(lib.Root, "exports");
        var file = Path.Combine(folder, $"before-import-{DateTime.Now:yyyy-MM-dd-HHmmss}.txt");
        try
        {
            await Task.Run(() =>
            {
                Directory.CreateDirectory(folder);
                File.WriteAllText(file, text, new UTF8Encoding(true));
                foreach (var old in new DirectoryInfo(folder).GetFiles("before-import-*.txt")
                             .OrderByDescending(f => f.Name, StringComparer.Ordinal).Skip(OrderBackupsKept))
                    old.Delete();
            });
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    /// <summary>The library as the planner sees it, in the load order the list shows.</summary>
    private List<LoadListPackage> LoadListPackages() =>
        _loadOrdered.Select(r =>
        {
            var p = r.Overview.Package;
            return new LoadListPackage(p.PackageId, p.ModName, p.Version, r.IsCore ? LoadListPlanner.CoreSection : r.Section,
                r.Overview.Wanted, r.IsCore, p.LayerOfModId is not null, p.NexusModId, p.NexusFileId, p.SourceArchiveName, p.SourceMd5, p.ImportedAt);
        }).ToList();

    /// <summary>
    /// The selected profile as a load list: CORE MODS, then every block and category in load order, each mod on or off
    /// as the list shows it; layers as notes under their mod.
    /// </summary>
    private string LoadListText(InstallOverview o)
    {
        var profile = o.Status.Selected;
        var layers = _loadOrdered
            .Where(r => r.Overview.Package.LayerOfModId is not null && _parentOf.ContainsKey(r.PackageId))
            .GroupBy(r => _parentOf[r.PackageId], StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<LoadListLayer>)g.Select(r => new LoadListLayer(r.Overview.Package.ModName, r.Overview.Wanted)).ToList(),
                StringComparer.Ordinal);
        LoadListMod ModOf(ModRowVm r)
        {
            var p = r.Overview.Package;
            return new LoadListMod(0, r.Overview.Wanted, p.ModName, p.Version, p.NexusModId, p.NexusFileId, p.SourceArchiveName, p.SourceMd5)
            {
                Layers = layers.TryGetValue(p.PackageId, out var own) ? own : Array.Empty<LoadListLayer>(),
            };
        }
        var mains = _loadOrdered.Where(r => r.Overview.Package.LayerOfModId is null).ToList();
        var sections = new List<LoadListSection>();
        var core = mains.Where(r => r.IsCore).Select(ModOf).ToList();
        if (core.Count > 0) sections.Add(new LoadListSection(0, LoadListSectionKind.Core, "", core));
        foreach (var key in _layout.Sections(CollectionSlugs))
        {
            var mods = mains.Where(r => !r.IsCore && r.Section == key).Select(ModOf).ToList();
            if (key == BlockLayout.ModsBlock)
                sections.Add(new LoadListSection(0, LoadListSectionKind.Mods, "", mods));
            else if (key.StartsWith(BlockLayout.CategoryPrefix, StringComparison.Ordinal))
                sections.Add(new LoadListSection(0, LoadListSectionKind.Category, CategoryName(key[BlockLayout.CategoryPrefix.Length..]), mods));
            else if (mods.Count > 0)
            {
                var slug = key[BlockLayout.CollectionPrefix.Length..];
                var record = _collections.FirstOrDefault(c => c.Slug == slug);
                sections.Add(new LoadListSection(0, LoadListSectionKind.Collection, record?.Name ?? slug, mods, slug, record?.Revision.Revision,
                    record is not null && record.OrderLocked && record.ProfileId == profile.Id));
            }
        }
        return LoadList.Write(new LoadListHeader(profile.Name, DateTimeOffset.Now, AppVersion, AppEdition.Offline, !o.OrderIsCustom), sections);
    }
}
