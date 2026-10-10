using System.Reflection;
using System.Text.RegularExpressions;

namespace ZenGear.ArchitectureTests.Infrastructure;

/// <summary>
/// The repository as the tests see it: the three production assemblies, the source folders, the business areas
/// read from the table in <c>docs/ARCHITECTURE.md</c> (so the documentation stays the only list of areas), and the
/// <c>README.md</c> of each area of ZenGear.Services.
/// </summary>
internal static partial class RepositoryLayout
{
    public static readonly Assembly Api = typeof(Program).Assembly;
    public static readonly Assembly Services = typeof(ZenGear.Services.Common.IClock).Assembly;
    public static readonly Assembly Data = typeof(ZenGear.Data.ZenGearDbContext).Assembly;

    public static readonly string Root = FindRoot();

    public static readonly string SourceDirectory = Path.Combine(Root, "api", "src");

    public static readonly IReadOnlySet<string> Areas = ReadAreas();

    private static readonly char[] PathSeparators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    /// <summary>Folders that are never source folders: build output and generated migrations.</summary>
    public static bool IsExcluded(string relativePath) =>
        relativePath.Split(PathSeparators)
            .Any(segment => segment is "bin" or "obj" or "Migrations");

    /// <summary>The README.md of an area of ZenGear.Services, or null when it has none.</summary>
    public static string? AreaReadme(string area)
    {
        var path = Path.Combine(SourceDirectory, "ZenGear.Services", area, "README.md");
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    /// <summary>The lines under <c>## heading</c> up to the next <c>## </c> heading, or null when the heading is missing.</summary>
    public static string? Section(string markdown, string heading)
    {
        var lines = markdown.Split('\n').Select(line => line.TrimEnd('\r')).ToList();
        var start = lines.IndexOf($"## {heading}");
        return start < 0
            ? null
            : string.Join('\n', lines.Skip(start + 1).TakeWhile(line => !line.StartsWith("## ", StringComparison.Ordinal)));
    }

    /// <summary>The backticked names that open the bullets of a section: <c>- `Access` — sign-in …</c> gives Access.</summary>
    public static HashSet<string> BulletNames(string? section) =>
        section is null
            ? []
            : BulletName().Matches(section).Select(match => match.Groups["name"].Value).ToHashSet(StringComparer.Ordinal);

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "api", "ZenGear.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException($"Repository root (api/ZenGear.slnx) not found above {AppContext.BaseDirectory}.");
    }

    private static HashSet<string> ReadAreas()
    {
        // Only this section: another table of the file whose rows start with a backticked word must not add areas.
        var architecture = File.ReadAllText(Path.Combine(Root, "docs", "ARCHITECTURE.md"));
        var table = Section(architecture, "Business areas and table ownership")
            ?? throw new InvalidOperationException("No section \"Business areas and table ownership\" in docs/ARCHITECTURE.md.");
        var areas = AreaRow().Matches(table).Select(match => match.Groups["area"].Value).ToHashSet(StringComparer.Ordinal);

        // A changed table format must fail loudly, not silently allow nothing or everything.
        if (areas.Count < 10)
        {
            throw new InvalidOperationException($"Only {areas.Count} areas read from docs/ARCHITECTURE.md; check the table format.");
        }

        return areas;
    }

    // "| `Identity` | `users`, ..." in the section "Business areas and table ownership"
    [GeneratedRegex(@"^\| `(?<area>[A-Za-z]+)` \|", RegexOptions.Multiline)]
    private static partial Regex AreaRow();

    [GeneratedRegex(@"^- `(?<name>[A-Za-z][A-Za-z0-9]*)`", RegexOptions.Multiline)]
    private static partial Regex BulletName();
}
