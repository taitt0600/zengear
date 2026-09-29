using System.Reflection;
using System.Text.RegularExpressions;

namespace ZenGear.ArchitectureTests.Infrastructure;

/// <summary>
/// The repository as the tests see it: the three production assemblies, the source folders, and the business
/// areas read from the table in <c>docs/ARCHITECTURE.md</c>, so the documentation stays the only list of areas.
/// </summary>
internal static partial class RepositoryLayout
{
    public static readonly Assembly Api = typeof(Program).Assembly;
    public static readonly Assembly Services = typeof(ZenGear.Services.Common.IClock).Assembly;
    public static readonly Assembly Data = typeof(ZenGear.Data.ZenGearDbContext).Assembly;

    public static readonly string Root = FindRoot();

    public static readonly string SourceDirectory = Path.Combine(Root, "api", "src");

    public static readonly IReadOnlySet<string> Areas = ReadAreas();

    /// <summary>Folders that are never source folders: build output and generated migrations.</summary>
    public static bool IsExcluded(string relativePath) =>
        relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment is "bin" or "obj" or "Migrations");

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
        var architecture = File.ReadAllText(Path.Combine(Root, "docs", "ARCHITECTURE.md"));
        var areas = AreaRow().Matches(architecture).Select(match => match.Groups["area"].Value).ToHashSet(StringComparer.Ordinal);

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
}
