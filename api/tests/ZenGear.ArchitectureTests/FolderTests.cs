using Shouldly;
using ZenGear.ArchitectureTests.Infrastructure;

namespace ZenGear.ArchitectureTests;

/// <summary>docs/ARCHITECTURE.md, section "Folder structure": a folder is a business area or a listed technical folder.</summary>
public sealed class FolderTests
{
    private static readonly Dictionary<string, string[]> TechnicalFolders = new(StringComparer.Ordinal)
    {
        ["ZenGear.Api"] = ["Auth", "Errors", "Logging", "OpenApi", "Hosting", "Properties"],
        ["ZenGear.Services"] = ["Integrations", "Jobs"],
        ["ZenGear.Data"] = ["Entities", "Configurations", "Conventions", "Migrations"],
    };

    [TestCase("ZenGear.Api", true)]
    [TestCase("ZenGear.Services", true)]
    [TestCase("ZenGear.Data", false)] // areas only below Entities/ and Configurations/
    public void Top_level_folders_are_listed(string project, bool areasAllowed)
    {
        var unknown = SubFolders(Path.Combine(RepositoryLayout.SourceDirectory, project))
            .Where(folder => !(areasAllowed && RepositoryLayout.Areas.Contains(folder)) && !TechnicalFolders[project].Contains(folder))
            .ToList();

        unknown.ShouldBeEmpty("Move the code, or add the folder to docs/ARCHITECTURE.md (Folder structure) first.");
    }

    [TestCase("Entities")]
    [TestCase("Configurations")]
    public void Data_folders_are_split_by_area(string folder)
    {
        var unknown = SubFolders(Path.Combine(RepositoryLayout.SourceDirectory, "ZenGear.Data", folder))
            .Where(area => !RepositoryLayout.Areas.Contains(area))
            .ToList();

        unknown.ShouldBeEmpty();
    }

    /// <summary>
    /// Folders holding at least one .cs file: git does not track empty folders, so one left behind
    /// locally must not fail the test when a clean checkout (CI) passes it.
    /// </summary>
    private static IEnumerable<string> SubFolders(string path) =>
        Directory.Exists(path)
            ? Directory.GetDirectories(path)
                .Where(folder => Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories).Any())
                .Select(Path.GetFileName)
                .OfType<string>()
                .Where(name => name is not ("bin" or "obj"))
            : Enumerable.Empty<string>();
}
