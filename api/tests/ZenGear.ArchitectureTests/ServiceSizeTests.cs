using System.Reflection;
using Shouldly;
using ZenGear.ArchitectureTests.Infrastructure;

namespace ZenGear.ArchitectureTests;

/// <summary>docs/ARCHITECTURE.md, section "Folder structure": services are split before they grow.</summary>
public sealed class ServiceSizeTests
{
    private const int MaxLinesOfCode = 400;
    private const int MaxPublicMethods = 10;
    private const int MaxExceptions = 3;

    /// <summary>Type (file) name → why it may exceed a limit. More than <see cref="MaxExceptions"/> call for a Decision (Evolution thresholds).</summary>
    private static readonly Dictionary<string, string> Exceptions = new(StringComparer.Ordinal);

    private static readonly string ServicesDirectory = Path.Combine(RepositoryLayout.SourceDirectory, "ZenGear.Services");

    [Test] // a file that a person cannot read in one sitting holds more than one family of use cases
    public void Services_files_have_at_most_400_lines_of_code()
    {
        Directory.EnumerateFiles(ServicesDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(file => !RepositoryLayout.IsExcluded(Path.GetRelativePath(ServicesDirectory, file)))
            .Select(file => (Name: Path.GetFileNameWithoutExtension(file), Lines: LinesOfCode(file)))
            .Where(file => file.Lines > MaxLinesOfCode && !Exceptions.ContainsKey(file.Name))
            .Select(file => $"{file.Name}: {file.Lines} lines of code")
            .ShouldBeEmpty("Split the type by family of use cases (docs/ARCHITECTURE.md, Folder structure).");
    }

    [Test] // a service with many use cases is split by family before it grows
    public void Services_have_at_most_10_public_methods()
    {
        RepositoryLayout.Services.GetTypes()
            .Where(type => type is { IsClass: true, IsNested: false } && type.Name.EndsWith("Service", StringComparison.Ordinal))
            .Select(type => (type.Name, Methods: type
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Count(method => !method.IsSpecialName)))
            .Where(service => service.Methods > MaxPublicMethods && !Exceptions.ContainsKey(service.Name))
            .Select(service => $"{service.Name}: {service.Methods} public methods")
            .ShouldBeEmpty("Split the service by family of use cases: <Entity><UseCase>Service.");
    }

    [Test] // many exceptions mean the limits no longer fit the code
    public void Size_exceptions_stay_few()
    {
        Exceptions.Count.ShouldBeLessThanOrEqualTo(MaxExceptions, "More exceptions call for a Decision instead of adding one (docs/ARCHITECTURE.md, Evolution thresholds).");
    }

    /// <summary>Lines that are neither blank nor only a comment.</summary>
    private static int LinesOfCode(string file) =>
        File.ReadLines(file)
            .Select(line => line.Trim())
            .Count(line => line.Length > 0 && !line.StartsWith("//", StringComparison.Ordinal)
                && !line.StartsWith("/*", StringComparison.Ordinal) && !line.StartsWith('*'));
}
