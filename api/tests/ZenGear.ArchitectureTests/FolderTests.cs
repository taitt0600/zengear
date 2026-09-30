using Shouldly;
using ZenGear.ArchitectureTests.Infrastructure;

namespace ZenGear.ArchitectureTests;

/// <summary>docs/ARCHITECTURE.md, section "Folder structure": every folder is listed there.</summary>
public sealed class FolderTests
{
    private const string Api = "ZenGear.Api";
    private const string Services = "ZenGear.Services";
    private const string Data = "ZenGear.Data";

    private static readonly string[] ApiFolders =
        ["Controllers", "ExceptionHandling", "Middlewares", "Extensions", "Attributes", "Authorization", "Options", "Constants", "Logging", "Hosting", "Properties"];

    private static readonly string[] DataFolders = ["Common", "Entities", "Configurations", "Externals", "Migrations"];

    /// <summary>Folders by kind inside a service area; any other folder of an area is a mechanism.</summary>
    private static readonly string[] AreaKindFolders = ["Models", "Enums", "Validators"];

    private static readonly string[] ForbiddenFolders = ["Helpers", "Interfaces", "Dtos", "Mappings", "Repositories", "Jobs", "BackgroundServices"];

    private const int MinimumMechanismSize = 5;

    [Test]
    public void Api_has_only_its_technical_folders()
    {
        Unlisted(SubFolders(Api), ApiFolders).ShouldBeEmpty(MoveOrDocument);
    }

    [Test]
    public void Api_folders_are_flat()
    {
        ApiFolders.Where(folder => folder != "Properties")
            .SelectMany(folder => SubFolders(Api, folder).Select(sub => $"{folder}/{sub}"))
            .ShouldBeEmpty();
    }

    [Test]
    public void Services_has_area_folders_and_common()
    {
        Unlisted(SubFolders(Services), [.. RepositoryLayout.Areas, "Common"]).ShouldBeEmpty(MoveOrDocument);
    }

    [Test]
    public void Services_common_has_only_utilities()
    {
        Unlisted(SubFolders(Services, "Common"), ["Utilities"]).ShouldBeEmpty(MoveOrDocument);
    }

    [Test]
    public void Service_area_folders_are_kinds_or_mechanisms()
    {
        var offenders = Areas(Services)
            .SelectMany(area => SubFolders(Services, area).Select(folder => (Area: area, Folder: folder)))
            .Where(x => ForbiddenFolders.Contains(x.Folder)
                || SubFolders(Services, x.Area, x.Folder).Any()
                || (!AreaKindFolders.Contains(x.Folder) && SourceFiles(Services, x.Area, x.Folder) < MinimumMechanismSize))
            .Select(x => $"{x.Area}/{x.Folder}")
            .ToList();

        offenders.ShouldBeEmpty(
            $"A folder of an area is Models, Enums or Validators, or a mechanism of {MinimumMechanismSize} or more types; all are flat.");
    }

    [Test]
    public void Data_has_only_its_technical_folders()
    {
        Unlisted(SubFolders(Data), DataFolders).ShouldBeEmpty(MoveOrDocument);
    }

    [Test]
    public void Data_common_and_externals_systems_are_flat()
    {
        SubFolders(Data, "Common")
            .Concat(SubFolders(Data, "Externals").SelectMany(system => SubFolders(Data, "Externals", system)))
            .ShouldBeEmpty();
    }

    [Test]
    public void Entities_are_split_by_area_with_an_optional_enums_folder()
    {
        var offenders = Unlisted(SubFolders(Data, "Entities"), RepositoryLayout.Areas)
            .Concat(Areas(Data, "Entities").SelectMany(area => Unlisted(SubFolders(Data, "Entities", area), ["Enums"]).Select(sub => $"{area}/{sub}")))
            .ToList();

        offenders.ShouldBeEmpty();
    }

    [Test]
    public void Configurations_are_split_by_area_and_flat()
    {
        var offenders = Unlisted(SubFolders(Data, "Configurations"), RepositoryLayout.Areas)
            .Concat(Areas(Data, "Configurations").SelectMany(area => SubFolders(Data, "Configurations", area).Select(sub => $"{area}/{sub}")))
            .ToList();

        offenders.ShouldBeEmpty();
    }

    private const string MoveOrDocument = "Move the code, or add the folder to docs/ARCHITECTURE.md (Folder structure) first.";

    private static List<string> Unlisted(IEnumerable<string> folders, IEnumerable<string> allowed)
    {
        var set = allowed.ToHashSet(StringComparer.Ordinal);
        return folders.Where(folder => !set.Contains(folder)).ToList();
    }

    private static IEnumerable<string> Areas(params string[] path) =>
        SubFolders(path).Where(RepositoryLayout.Areas.Contains);

    private static int SourceFiles(params string[] path) =>
        Directory.GetFiles(Path.Combine([RepositoryLayout.SourceDirectory, .. path]), "*.cs").Length;

    /// <summary>
    /// Folders holding at least one .cs file: git does not track empty folders, so one left behind
    /// locally must not fail the test when a clean checkout (CI) passes it.
    /// </summary>
    private static IEnumerable<string> SubFolders(params string[] path)
    {
        var directory = Path.Combine([RepositoryLayout.SourceDirectory, .. path]);
        return Directory.Exists(directory)
            ? Directory.GetDirectories(directory)
                .Where(folder => Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories).Any())
                .Select(Path.GetFileName)
                .OfType<string>()
                .Where(name => name is not ("bin" or "obj"))
            : Enumerable.Empty<string>();
    }
}
