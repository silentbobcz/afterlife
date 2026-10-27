using System.Globalization;
using System.Text;
using Afterlife.Core;

namespace Afterlife.Infrastructure;

public enum ExportScope { InGameNow, SelectedAfterApply }

/// <summary>
/// P52: list of the active mods with versions, for finding a problem mod (forum, Discord, mod author, bug report).
/// Never contains the Nexus key, the account name or paths outside the game folder.
/// </summary>
public static class ModListExport
{
    public sealed record Row(int Order, string Name, string Version, string? NexusId, string? Archive, string Kinds, string State, string? Collection);

    public static IReadOnlyList<Row> Rows(InstallOverview o, ExportScope scope, bool includeOff)
    {
        var list = new List<Row>();
        var n = 0;
        foreach (var m in o.Mods)
        {
            var active = scope == ExportScope.InGameNow ? m.InGame : m.Included;
            if (!active && !(includeOff && !m.Wanted)) continue;
            var state = active ? (scope == ExportScope.InGameNow ? "in game" : "after apply") : "off";
            if (active && m.Missing.Count > 0) state += " · missing " + string.Join(", ", m.Missing);
            if (m.State == ModState.HeldBack) state += " · held back";
            list.Add(new Row(active ? ++n : 0, m.Package.ModName, m.Package.Version, NexusInfo.IdOf(m.Package).Id, m.Package.SourceArchiveName,
                Kinds(m.Package), state, m.Collections.Count == 0 ? null : string.Join(", ", m.Collections)));
        }
        return list;
    }

    public static string Kinds(PackageManifest p)
    {
        var k = new List<string>();
        var t = p.Files.Select(f => f.Target.ToLowerInvariant()).ToList();
        if (t.Any(x => x.StartsWith("archive/", StringComparison.Ordinal))) k.Add("Archive");
        if (t.Any(x => x.EndsWith(".reds", StringComparison.Ordinal))) k.Add("redscript");
        if (t.Any(x => x.StartsWith("bin/x64/plugins/cyber_engine_tweaks/mods/", StringComparison.Ordinal))) k.Add("CET");
        if (t.Any(x => x.StartsWith("red4ext/plugins/", StringComparison.Ordinal))) k.Add("RED4ext plugin");
        if (t.Any(x => x.StartsWith("r6/tweaks/", StringComparison.Ordinal))) k.Add("Tweak");
        if (t.Any(x => x.StartsWith("mods/", StringComparison.Ordinal))) k.Add("REDmod");
        if (t.Any(x => x.StartsWith("r6/audioware/", StringComparison.Ordinal))) k.Add("Audioware");
        return k.Count == 0 ? "Other" : string.Join(", ", k);
    }

    public static string StoreOf(string gameRoot)
    {
        bool Has(string rel) => File.Exists(Path.Combine(gameRoot, rel.Replace('/', Path.DirectorySeparatorChar)));
        if (Has("bin/x64/steam_api64.dll")) return "Steam";
        if (Has("bin/x64/GameServicesGOG.dll") || Has("bin/x64/Galaxy64.dll")) return "GOG";
        if (Has("bin/x64/EOSSDK-Win64-Shipping.dll") || Has("bin/x64/GameServicesEpic.dll")) return "Epic";
        return "unknown store";
    }

    public static string Text(InstallOverview o, ExportScope scope, bool includeOff, string appVersion, DateTimeOffset now)
    {
        var rows = Rows(o, scope, includeOff);
        var fp = o.Status.Fingerprint;
        var sb = new StringBuilder();
        var active = rows.Count(r => r.Order > 0);
        sb.Append(CultureInfo.InvariantCulture, $"AFTERLIFE {appVersion} · {now:yyyy-MM-dd HH:mm} · profile {o.Status.Selected.Name}");
        sb.Append(CultureInfo.InvariantCulture, $" · Cyberpunk 2077 {fp?.GameVersion ?? "?"} (build {fp?.FileVersion ?? "?"}, {StoreOf(o.Status.GameRoot)})");
        sb.Append(CultureInfo.InvariantCulture, $" · {active} mods {(scope == ExportScope.InGameNow ? "in game" : "after apply")}");
        if (o.Status.BuildChanged) sb.Append(" · GAME UPDATED SINCE LAST APPLY");
        sb.AppendLine();
        var core = o.CoreMods.Where(c => c.Source != CoreSource.Missing).ToList();
        if (core.Count > 0)
            sb.AppendLine("CORE  " + string.Join(" · ", core.Select(c => $"{c.Name} {c.Version ?? "?"}{(c.Source == CoreSource.Manual ? " (by hand)" : "")}")));
        var w = Math.Clamp(rows.Count == 0 ? 10 : rows.Max(r => r.Name.Length), 10, 44);
        foreach (var r in rows)
        {
            var name = r.Name.Length > w ? r.Name[..(w - 1)] + "…" : r.Name.PadRight(w);
            var order = r.Order > 0 ? r.Order.ToString("00", CultureInfo.InvariantCulture) : "--";
            sb.Append(CultureInfo.InvariantCulture, $"{order}  {name}  {r.Version,-10}  {(r.NexusId is null ? "-" : "nexus " + r.NexusId),-12}  {r.Kinds}");
            if (r.Collection is not null) sb.Append("  [" + r.Collection + "]");
            if (r.State is not ("in game" or "after apply")) sb.Append("  · " + r.State);
            sb.AppendLine();
        }
        var manual = o.Detected.Where(d => d.Removable).ToList();
        if (manual.Count > 0)
        {
            sb.AppendLine("MANUAL (not managed by AFTERLIFE)");
            foreach (var d in manual) sb.AppendLine(CultureInfo.InvariantCulture, $"    {d.Name} ({d.Files.Count} file{(d.Files.Count == 1 ? "" : "s")})");
        }
        return sb.ToString();
    }

    public static string Csv(InstallOverview o, ExportScope scope, bool includeOff)
    {
        static string Q(string? s) => s is null ? "" : "\"" + s.Replace("\"", "\"\"") + "\"";
        var sb = new StringBuilder("order,name,version,nexus_id,archive,kind,state,collection\r\n");
        foreach (var r in Rows(o, scope, includeOff))
            sb.Append(CultureInfo.InvariantCulture, $"{(r.Order > 0 ? r.Order : "")},{Q(r.Name)},{Q(r.Version)},{r.NexusId},{Q(r.Archive)},{Q(r.Kinds)},{Q(r.State)},{Q(r.Collection)}\r\n");
        foreach (var c in o.CoreMods.Where(c => c.Source != CoreSource.Missing))
            sb.Append(CultureInfo.InvariantCulture, $",{Q(c.Name)},{Q(c.Version)},,,core,{Q(c.SourceLabel)},\r\n");
        foreach (var d in o.Detected.Where(d => d.Removable))
            sb.Append(CultureInfo.InvariantCulture, $",{Q(d.Name)},,,,manual,{Q($"{d.Files.Count} files, not managed")},\r\n");
        return sb.ToString();
    }
}
