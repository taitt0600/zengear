using Shouldly;
using ZenGear.ArchitectureTests.Infrastructure;

namespace ZenGear.ArchitectureTests;

/// <summary>docs/ARCHITECTURE.md, section "Folder structure": every folder is one it allows (DEC-106).</summary>
public sealed class FolderTests
{
    private const string Api = "ZenGear.Api";
    private const string Services = "ZenGear.Services";
    private const string Data = "ZenGear.Data";

    private static readonly string[] ApiFolders =
        ["Controllers", "ExceptionHandling", "Middlewares", "Extensions", "Attributes", "Authorization", "Options", "Constants", "Logging", "Hosting", "Resources", "Properties"];

    private static readonly string[] DataFolders = ["Common", "Entities", "Configurations", "Externals", "Migrations"];

    /// <summary>Never a module or a mechanism: the name of a type says its kind, so an area has no folder by kind.</summary>
    private static readonly string[] ForbiddenFolders =
        ["Helpers", "Interfaces", "Dtos", "Mappings", "Repositories", "Jobs", "BackgroundServices", "Models", "Validators", "Enums"];

    private static readonly string[] ReadmeSections = ["Purpose", "Modules", "Public contract", "Dependencies"];

    private const int MinimumMechanismSize = 5;

    [Test] // a technical folder of the API is listed in docs/ARCHITECTURE.md before it exists
    public void Api_has_only_its_technical_folders()
    {
        Unlisted(SubFolders(Api), ApiFolders).ShouldBeEmpty(MoveOrDocument);
    }

    [Test] // controllers are sorted by area; the other technical folders stay flat
    public void Api_folders_are_flat_except_controllers_by_area()
    {
        var offenders = ApiFolders.Where(folder => folder is not ("Properties" or "Controllers"))
            .SelectMany(folder => SubFolders(Api, folder).Select(sub => $"{folder}/{sub}"))
            .Concat(Unlisted(SubFolders(Api, "Controllers"), RepositoryLayout.Areas).Select(sub => $"Controllers/{sub}"))
            .Concat(SubFolders(Api, "Controllers").SelectMany(area => SubFolders(Api, "Controllers", area).Select(sub => $"Controllers/{area}/{sub}")))
            .Concat(SourceFiles(Api, "Controllers") > 0 ? ["Controllers: a controller outside an area folder"] : [])
            .ToList();

        offenders.ShouldBeEmpty("A controller sits in Controllers/<Area>/; the other API folders are flat.");
    }

    [Test] // the services are sorted by the areas of docs/ARCHITECTURE.md, plus Common
    public void Services_has_area_folders_and_common()
    {
        Unlisted(SubFolders(Services), [.. RepositoryLayout.Areas, "Common"]).ShouldBeEmpty(MoveOrDocument);
    }

    [Test] // Common is shared code, not an area: only the shared list types, their validators and utilities
    public void Services_common_has_models_validators_and_utilities_only()
    {
        Unlisted(SubFolders(Services, "Common"), ["Models", "Validators", "Utilities"]).ShouldBeEmpty(MoveOrDocument);
        SubFolders(Services, "Common").SelectMany(folder => SubFolders(Services, "Common", folder)).ShouldBeEmpty();
    }

    [Test] // DEC-106 (1): an area holds modules; a module is flat but for mechanisms of five or more types
    public void Service_areas_hold_modules_with_flat_mechanisms()
    {
        var areas = Areas(Services).ToList();
        areas.ShouldNotBeEmpty("No area folder found in ZenGear.Services: the test no longer reads the code.");
        var offenders = areas.Where(area => SourceFiles(Services, area) > 0).Select(area => $"{area}: a file outside a module")
            .Concat(areas.SelectMany(area => SubFolders(Services, area)
                .Where(ForbiddenFolders.Contains)
                .Select(module => $"{area}/{module}: a folder by kind")))
            .Concat(areas.SelectMany(area => SubFolders(Services, area).SelectMany(module => SubFolders(Services, area, module)
                .Where(mechanism => ForbiddenFolders.Contains(mechanism)
                    || SubFolders(Services, area, module, mechanism).Any()
                    || SourceFiles(Services, area, module, mechanism) < MinimumMechanismSize)
                .Select(mechanism => $"{area}/{module}/{mechanism}: not a flat mechanism of {MinimumMechanismSize} or more types"))))
            .ToList();

        offenders.ShouldBeEmpty("ZenGear.Services/<Area>/<Module>/, with a mechanism folder of five or more types inside a module.");
    }

    [Test] // DEC-106 (7): the README of an area says what it holds and which of its services other areas may call
    public void Every_service_area_has_a_readme_listing_its_modules()
    {
        Areas(Services).SelectMany(ReadmeProblems).ShouldBeEmpty(
            $"Each area has a README.md with the sections {string.Join(", ", ReadmeSections)}, and a bullet per module under Modules.");
    }

    [Test] // the README reader must find the bullets of one section only, or the README tests pass on nothing
    public void The_readme_reader_finds_the_bullets_of_one_section()
    {
        const string readme = "# Sample\n\n## Modules\n\n- `Access` — sign-in\n- `Account` — my account\n  - `Nested` is not a module\n\n## Public contract\n\nNone.\n";

        RepositoryLayout.BulletNames(RepositoryLayout.Section(readme, "Modules")).ShouldBe(["Access", "Account"], ignoreOrder: true);
        RepositoryLayout.BulletNames(RepositoryLayout.Section(readme, "Public contract")).ShouldBeEmpty();
        RepositoryLayout.Section(readme, "Dependencies").ShouldBeNull();
    }

    [Test] // the data access layer keeps its technical folders (docs/ARCHITECTURE.md)
    public void Data_has_only_its_technical_folders()
    {
        Unlisted(SubFolders(Data), DataFolders).ShouldBeEmpty(MoveOrDocument);
    }

    [Test] // shared building blocks and the client of one external system have no sub-folder
    public void Data_common_and_externals_systems_are_flat()
    {
        SubFolders(Data, "Common")
            .Concat(SubFolders(Data, "Externals").SelectMany(system => SubFolders(Data, "Externals", system)))
            .ShouldBeEmpty();
    }

    [Test] // the modules of an area share its entities, so entities are sorted by area only
    public void Entities_are_split_by_area_with_an_optional_enums_folder()
    {
        var offenders = Unlisted(SubFolders(Data, "Entities"), RepositoryLayout.Areas)
            .Concat(Areas(Data, "Entities").SelectMany(area => Unlisted(SubFolders(Data, "Entities", area), ["Enums"]).Select(sub => $"{area}/{sub}")))
            .ToList();

        offenders.ShouldBeEmpty();
    }

    [Test] // the configurations follow the entities they configure
    public void Configurations_are_split_by_area_and_flat()
    {
        var offenders = Unlisted(SubFolders(Data, "Configurations"), RepositoryLayout.Areas)
            .Concat(Areas(Data, "Configurations").SelectMany(area => SubFolders(Data, "Configurations", area).Select(sub => $"{area}/{sub}")))
            .ToList();

        offenders.ShouldBeEmpty();
    }

    private const string MoveOrDocument = "Move the code, or add the folder to docs/ARCHITECTURE.md (Folder structure) first.";

    private static IEnumerable<string> ReadmeProblems(string area)
    {
        var readme = RepositoryLayout.AreaReadme(area);
        if (readme is null)
        {
            return [$"{area}: no README.md"];
        }

        var modules = RepositoryLayout.BulletNames(RepositoryLayout.Section(readme, "Modules"));
        return ReadmeSections.Where(section => RepositoryLayout.Section(readme, section) is null).Select(section => $"{area}: no section {section}")
            .Concat(SubFolders(Services, area).Where(module => !modules.Contains(module)).Select(module => $"{area}: module {module} is not under Modules"));
    }

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
