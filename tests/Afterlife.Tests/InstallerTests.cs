using Afterlife.Core;
using Afterlife.Infrastructure;

namespace Afterlife.Tests;


public class RedModTests
{
    [Fact]
    public void Redmods_are_deployed_once_in_load_order_and_again_after_a_change()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("mods/ModA/info.json", "{\"name\":\"ModA\"}"), ("mods/ModA/archives/a.archive", "a"));
        var b = t.ImportZip("B", ("mods/ModB/info.json", "{\"name\":\"ModB\"}"), ("mods/ModB/archives/b.archive", "b"));
        Assert.Equal(RedModOutcome.NoRedMods, t.C.DeployRedMods().Outcome);
        t.C.SetEnabled(b.PackageId, true);
        t.C.SetEnabled(a.PackageId, true);
        t.ApplyOk();
        Assert.Equal(RedModOutcome.ToolMissing, t.C.DeployRedMods().Outcome);
        t.Write("tools/redmod/bin/redMod.exe", "exe");
        var r = t.C.DeployRedMods();
        Assert.Equal(RedModOutcome.Deployed, r.Outcome);
        Assert.True(r.LaunchModded);
        var run = Assert.Single(t.Env.ToolRuns);
        Assert.Equal("deploy", run.Args[0]);
        Assert.Contains(run.Args, x => x.StartsWith("-modlist=", StringComparison.Ordinal));
        Assert.Equal(RedModOutcome.UpToDate, t.C.DeployRedMods().Outcome);
        t.C.SetEnabled(b.PackageId, false);
        t.ApplyOk();
        Assert.Equal(RedModOutcome.Deployed, t.C.DeployRedMods().Outcome);
        Assert.Equal(["ModA"], t.C.DeployedRedMods());
        t.Env.ToolExitCode = 3;
        var f = t.C.DeployRedMods(force: true);
        Assert.Equal(RedModOutcome.Failed, f.Outcome);
        Assert.False(f.LaunchModded);
    }

    [Fact]
    public void Redmod_archives_import_with_or_without_the_mods_folder()
    {
        using var t = new TestEnv();
        var a = t.ImportZip("A", ("mods/ModA/info.json", "{}"), ("mods/ModA/archives/a.archive", "a"), ("mods/ModA/readme.txt", "r"));
        Assert.Equal(["mods/ModA/archives/a.archive", "mods/ModA/info.json"], a.Files.Select(f => f.Target).Order().ToArray());
        var b = t.ImportZip("B", ("ModB/info.json", "{}"), ("ModB/archives/b.archive", "b"));
        Assert.Equal(["mods/ModB/archives/b.archive", "mods/ModB/info.json"], b.Files.Select(f => f.Target).Order().ToArray());
        var c = t.ImportZip("C", ("C v1/mods/ModC/info.json", "{}"), ("C v1/mods/ModC/scripts/x.script", "s"), ("C v1/archive/pc/mod/c.archive", "c"));
        Assert.Contains(c.Files, f => f.Target == "archive/pc/mod/c.archive");
        Assert.Contains(c.Files, f => f.Target == "mods/ModC/scripts/x.script");
    }
}

public class FomodTests
{
    private const string Xml = """
        <?xml version="1.0" encoding="utf-8"?>
        <config xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
          <moduleName>Better Lights</moduleName>
          <requiredInstallFiles><folder source="Core" destination="" /></requiredInstallFiles>
          <installSteps order="Explicit">
            <installStep name="Brightness">
              <optionalFileGroups order="Explicit">
                <group name="Level" type="SelectExactlyOne">
                  <plugins order="Explicit">
                    <plugin name="Soft"><description>Softer</description>
                      <files><file source="Soft\lights.archive" destination="archive\pc\mod\lights.archive" /></files>
                      <conditionFlags><flag name="level">soft</flag></conditionFlags>
                      <typeDescriptor><type name="Optional" /></typeDescriptor></plugin>
                    <plugin name="Bright"><description>Brighter</description>
                      <files><file source="Bright\lights.archive" destination="archive\pc\mod\lights.archive" /></files>
                      <conditionFlags><flag name="level">bright</flag></conditionFlags>
                      <typeDescriptor><type name="Recommended" /></typeDescriptor></plugin>
                  </plugins>
                </group>
              </optionalFileGroups>
            </installStep>
            <installStep name="Extras">
              <visible><flagDependency flag="level" value="bright" /></visible>
              <optionalFileGroups order="Explicit">
                <group name="Extras" type="SelectAny">
                  <plugins order="Explicit">
                    <plugin name="Neon"><description>Neon</description>
                      <files><folder source="Neon" destination="" /></files>
                      <typeDescriptor><type name="Optional" /></typeDescriptor></plugin>
                  </plugins>
                </group>
              </optionalFileGroups>
            </installStep>
          </installSteps>
          <conditionalFileInstalls><patterns>
            <pattern><dependencies operator="And"><flagDependency flag="level" value="soft" /></dependencies>
              <files><file source="Soft\soft.xl" destination="archive\pc\mod\soft.xl" /></files></pattern>
          </patterns></conditionalFileInstalls>
        </config>
        """;

    [Fact]
    public void Parses_steps_defaults_visibility_and_conditional_files()
    {
        var cfg = Fomod.Parse(Xml);
        Assert.Equal("Better Lights", cfg.Name);
        Assert.Equal(2, cfg.Steps.Count);
        // default = recommended "Bright" → Extras step visible
        Assert.Equal(2, cfg.VisibleSteps([]).Count);
        var soft = new[] { new FomodChoice("Brightness", "Level", ["Soft"]) };
        Assert.Single(cfg.VisibleSteps(soft));
        var files = cfg.Resolve(soft);
        Assert.Contains(files, f => f.Source == "Soft\\\\soft.xl".Replace("\\\\", "\\"));
        Assert.Contains(files, f => f.IsFolder && f.Source == "Core");
    }

    [Fact]
    public void Imports_the_chosen_options_and_stores_the_choices()
    {
        using var t = new TestEnv();
        var zip = t.Zip("lights",
            ("Better Lights/fomod/ModuleConfig.xml", Xml),
            ("Better Lights/Core/r6/scripts/BL/core.reds", "core"),
            ("Better Lights/Soft/lights.archive", "soft"),
            ("Better Lights/Soft/soft.xl", "xl"),
            ("Better Lights/Bright/lights.archive", "bright"),
            ("Better Lights/Neon/archive/pc/mod/neon.archive", "neon"));
        var first = t.C.Import(new ImportRequest(zip, "Better Lights", "1.0"));
        Assert.Equal(ImportStatus.NeedsFomod, first.Analysis.Status);
        var cfg = t.C.ReadFomod(zip, first.Analysis.FomodConfigPath!);
        var choices = new[] { new FomodChoice("Brightness", "Level", ["Bright"]), new FomodChoice("Extras", "Extras", ["Neon"]) };
        var r = t.C.Import(new ImportRequest(zip, "Better Lights", "1.0", Options: new ImportOptions { FomodFiles = cfg.Resolve(choices) }) { FomodChoices = choices });
        Assert.True(r.Succeeded, string.Join("; ", r.Analysis.Problems));
        Assert.Equal(["archive/pc/mod/lights.archive", "archive/pc/mod/neon.archive", "r6/scripts/BL/core.reds"], r.Package!.Files.Select(f => f.Target).Order().ToArray());
        Assert.Equal("Bright", t.C.Store.GetPackage(r.Package.PackageId)!.FomodChoices![0].Plugins[0]);
    }
}
