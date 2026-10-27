using Afterlife.Core;
using Afterlife.Infrastructure;

namespace Afterlife.Tests;

public class GamePathTests
{
    [Theory]
    [InlineData("../evil.archive")]
    [InlineData("archive/../../evil")]
    [InlineData("/etc/passwd")]
    [InlineData("C:/Windows/evil.dll")]
    [InlineData("archive/pc/mod/a.archive:hidden")]
    [InlineData("r6/scripts/CON.reds")]
    [InlineData("r6/scripts/lpt1")]
    [InlineData("r6/scripts/trailing.")]
    [InlineData("r6/scripts/trailing ")]
    [InlineData("r6//double")]
    [InlineData("r6/scr\u0001ipt")]
    public void Unsafe_paths_are_rejected(string raw) => Assert.False(GamePath.TryCreate(raw, out _, out _));

    [Fact]
    public void Paths_compare_case_insensitively_and_normalize_separators()
    {
        Assert.Equal(GamePath.Parse("Archive\\PC\\mod\\A.archive"), GamePath.Parse("archive/pc/mod/a.ARCHIVE"));
        Assert.Equal("Archive/PC/mod/A.archive", GamePath.Parse("Archive\\PC\\mod\\A.archive").Value);
    }
}

public class ImportAnalyzerTests
{
    private static ImportAnalysis A(params string[] paths) =>
        ImportAnalyzer.Analyze(paths.Select(p => new ArchiveEntryInfo(p, 10)).ToList());

    [Fact]
    public void Wrapped_root_is_detected_and_docs_are_ignored()
    {
        var r = A("MyMod/archive/pc/mod/x.archive", "MyMod/readme.txt", "MyMod/r6/scripts/x/x.reds");
        Assert.Equal(ImportStatus.Ok, r.Status);
        Assert.Contains(r.Mapped, m => m.Target.Value == "archive/pc/mod/x.archive");
        Assert.Contains("MyMod/readme.txt", r.Ignored);
        Assert.Contains(r.Requirements, q => q.Capability == Capabilities.Redscript);
    }

    [Fact]
    public void Loose_archives_go_to_archive_pc_mod()
    {
        var r = A("x.archive", "x.xl", "preview.png");
        Assert.Equal(ImportStatus.Ok, r.Status);
        Assert.Equal(["archive/pc/mod/x.archive", "archive/pc/mod/x.xl"], r.Mapped.Select(m => m.Target.Value).ToArray());
        Assert.Contains(r.Requirements, q => q.Capability == Capabilities.ArchiveXl);
    }

    [Fact]
    public void Several_variants_need_a_choice_and_never_install_together()
    {
        var r = A("Option A/archive/pc/mod/a.archive", "Option B/archive/pc/mod/b.archive");
        Assert.Equal(ImportStatus.NeedsChoice, r.Status);
        Assert.Equal(["Option A", "Option B"], r.RootCandidates.ToArray());
        var chosen = ImportAnalyzer.Analyze(new[] { "Option A/archive/pc/mod/a.archive", "Option B/archive/pc/mod/b.archive" }
            .Select(p => new ArchiveEntryInfo(p, 1)).ToList(), new ImportOptions("Option B"));
        Assert.Equal(ImportStatus.Ok, chosen.Status);
        Assert.Single(chosen.Mapped);
        Assert.Equal("archive/pc/mod/b.archive", chosen.Mapped[0].Target.Value);
    }

    [Fact]
    public void Traversal_and_case_collisions_reject_the_whole_archive()
    {
        Assert.Equal(ImportStatus.Rejected, A("archive/pc/mod/a.archive", "../../evil.dll").Status);
        Assert.Equal(ImportStatus.Rejected, A("archive/pc/mod/A.archive", "archive/pc/mod/a.archive").Status);
    }

    [Fact]
    public void Redmod_imports_and_fomod_asks_for_options_R2_R3()
    {
        Assert.Equal(ImportStatus.Ok, A("mods/mymod/info.json", "mods/mymod/archives/x.archive").Status);
        Assert.Equal(ImportStatus.NeedsChoice, A("mods/mymod/info.json", "legacy/archive/pc/mod/x.archive").Status);
        var f = A("fomod/ModuleConfig.xml", "a/archive/pc/mod/x.archive");
        Assert.Equal(ImportStatus.NeedsFomod, f.Status);
        Assert.Equal("fomod/ModuleConfig.xml", f.FomodConfigPath);
    }

    [Fact]
    public void Frameworks_import_like_any_mod_P36()
    {
        var r = A("red4ext/plugins/ArchiveXL/ArchiveXL.dll");
        Assert.Equal(ImportStatus.Ok, r.Status);
        Assert.Contains(Capabilities.ArchiveXl, r.Provides);
        Assert.Contains(r.Requirements, q => q.Capability == Capabilities.Red4ext);
        Assert.Equal(ImportStatus.Ok, A("red4ext/plugins/input_loader/input_loader.dll").Status);
    }

    [Fact]
    public void Requirement_inference_covers_yml_and_input_loader()
    {
        var r = A("r6/tweaks/x/x.yml", "r6/input/x.xml");
        Assert.Contains(r.Requirements, q => q.Capability == Capabilities.TweakXl);
        Assert.Contains(r.Requirements, q => q.Capability == Capabilities.InputLoader);
    }

    [Fact]
    public void Modlist_inside_archive_is_ignored()
    {
        var r = A("archive/pc/mod/x.archive", "archive/pc/mod/modlist.txt");
        Assert.DoesNotContain(r.Mapped, m => m.Target.FileName == "modlist.txt");
    }
}

public class RequirementTests
{
    [Fact]
    public void Disabling_a_provider_excludes_its_dependents_transitively()
    {
        using var t = new TestEnv();
        var fw = t.C.Import(new ImportRequest(t.Zip("RED4ext", ("red4ext/RED4ext.dll", "dll")), "RED4ext", "1.31",
            Options: new ImportOptions(AllowUnverifiedFrameworks: true))).Package!;
        var axl = t.C.Import(new ImportRequest(t.Zip("ArchiveXL", ("red4ext/plugins/ArchiveXL/ArchiveXL.dll", "axl")), "ArchiveXL", "1.28",
            Options: new ImportOptions(AllowUnverifiedFrameworks: true))).Package!;
        var mod = t.ImportZip("Visuals", ("archive/pc/mod/v.archive", "v"), ("archive/pc/mod/v.xl", "xl"));
        t.C.UpdateSelected(p => p.WithEntry(fw.PackageId, true).WithEntry(axl.PackageId, true).WithEntry(mod.PackageId, true));
        t.ApplyOk();
        Assert.Equal("v", t.Read("archive/pc/mod/v.archive"));

        t.C.SetEnabled(fw.PackageId, false);
        var r = t.ApplyOk();
        Assert.Contains(axl.PackageId, r.Preview!.Snapshot.ExcludedPackages);
        Assert.Contains(mod.PackageId, r.Preview!.Snapshot.ExcludedPackages);
        Assert.Null(t.Read("archive/pc/mod/v.archive"));
    }

    [Fact]
    public void Unmanaged_framework_install_satisfies_requirements()
    {
        using var t = new TestEnv();
        t.Write("red4ext/RED4ext.dll", "manual");
        t.Write("red4ext/plugins/ArchiveXL/ArchiveXL.dll", "manual");
        var mod = t.ImportZip("Visuals", ("archive/pc/mod/v.archive", "v"), ("archive/pc/mod/v.xl", "xl"));
        t.C.SetEnabled(mod.PackageId, true);
        t.ApplyOk();
        Assert.Equal("v", t.Read("archive/pc/mod/v.archive"));
        Assert.Equal("manual", t.Read("red4ext/plugins/ArchiveXL/ArchiveXL.dll"));
    }
}

public class ModlistTests
{
    [Fact]
    public void Custom_order_writes_modlist_and_leaving_it_restores_absence()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("archive/pc/mod/a.archive", "a"));
        var z = t.ImportZip("Z", ("archive/pc/mod/z.archive", "z"));
        t.C.UpdateSelected(p => p.WithEntry(a.PackageId, true).WithEntry(z.PackageId, true)
            .WithArchiveOrder(new ArchiveOrderSettings(true, ["z.archive"], false, false)));
        t.ApplyOk();
        Assert.Equal("z.archive\r\na.archive\r\n", t.Read("archive/pc/mod/modlist.txt"));
        t.C.UpdateSelected(p => p.WithArchiveOrder(ArchiveOrderSettings.Default));
        t.ApplyOk();
        Assert.Null(t.Read("archive/pc/mod/modlist.txt"));
    }

    [Fact]
    public void Foreign_modlist_needs_takeover_and_is_restored_byte_for_byte()
    {
        using var t = new TestEnv();
        t.Write("archive/pc/mod/modlist.txt", "my.archive\n");
        t.Write("archive/pc/mod/my.archive", "mine");
        var a = t.ImportZip("A", ("archive/pc/mod/a.archive", "a"));
        t.C.UpdateSelected(p => p.WithEntry(a.PackageId, true).WithArchiveOrder(new ArchiveOrderSettings(true, [], false, false)));
        var r = t.C.Apply();
        Assert.Contains(r.Preview!.Snapshot.Issues.Concat(r.Preview.Plan.Blockers), i => i.Kind == IssueKind.UnmanagedArchivesInCustomOrder);

        t.C.UpdateSelected(p => p.WithArchiveOrder(new ArchiveOrderSettings(true, [], true, false)));
        r = t.C.Apply();
        Assert.Contains(r.Preview!.Plan.Blockers, i => i.Kind == IssueKind.ModlistForeignNeedsTakeover);

        t.C.UpdateSelected(p => p.WithArchiveOrder(new ArchiveOrderSettings(true, [], true, true)));
        t.ApplyOk();
        Assert.Equal("a.archive\r\nmy.archive\r\n", t.Read("archive/pc/mod/modlist.txt"));

        t.C.SelectProfile(ProfileState.ModsOffId);
        t.ApplyOk();
        Assert.Equal("my.archive\n", t.Read("archive/pc/mod/modlist.txt"));
        Assert.Equal("mine", t.Read("archive/pc/mod/my.archive"));
    }

    [Fact]
    public void Externally_changed_modlist_is_not_restored_blindly()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("archive/pc/mod/a.archive", "a"));
        t.C.UpdateSelected(p => p.WithEntry(a.PackageId, true).WithArchiveOrder(new ArchiveOrderSettings(true, [], false, false)));
        t.ApplyOk();
        t.Write("archive/pc/mod/modlist.txt", "edited by hand\n");
        t.C.SelectProfile(ProfileState.ModsOffId);
        var r = t.C.Apply();
        Assert.Contains(r.Preview!.Plan.Blockers, i => i.Kind == IssueKind.ModlistChangedExternally);
        Assert.Equal("edited by hand\n", t.Read("archive/pc/mod/modlist.txt"));
    }
}

public class LibraryTests
{
    [Fact]
    public void Packages_in_use_cannot_be_deleted()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("archive/pc/mod/a.archive", "a"));
        t.C.SetEnabled(a.PackageId, true);
        t.ApplyOk();
        Assert.Throws<InvalidOperationException>(() => t.C.Packages.Delete(a.PackageId));
    }

    [Fact]
    public void Library_inside_game_folder_is_refused()
    {
        var root = Path.Combine(Path.GetTempPath(), "afterlife-tests", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(root);
        try
        {
            Assert.Throws<InvalidOperationException>(() => InstallCoordinator.Initialize(root, Path.Combine(root, "lib"), env: new FakeGameEnvironment()));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Imported_files_match_their_manifest_hashes()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("archive/pc/mod/a.archive", "content"));
        var f = a.Files.Single();
        Assert.Equal(ContentHash.Of("content"), f.Hash);
        Assert.True(File.Exists(t.C.Library.PackageFile(a.PackageId, f.StoredPath)));
        Assert.NotNull(a.SourceMd5);
    }
}

public class FileNameGuessTests
{
    [Theory]
    [InlineData("Night City Visuals-1234-1-2-3-1700000000.zip", "Night City Visuals", "1.2.3", "1234")]
    [InlineData("NightCity-1.2.zip", "NightCity", "1.2", null)]
    [InlineData("Better Lights v2.0.1.7z", "Better Lights", "2.0.1", null)]
    [InlineData("JustAName.zip", "JustAName", "unknown", null)]
    public void Name_and_version_are_guessed_from_download_names(string file, string name, string version, string? nexusId)
    {
        var g = PackageLibrary.GuessFromFileName(file);
        Assert.Equal(name, g.Name);
        Assert.Equal(version, g.Version);
        Assert.Equal(nexusId, g.NexusModId);
    }
}
