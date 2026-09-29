using System.Text.RegularExpressions;
using Shouldly;
using ZenGear.ArchitectureTests.Infrastructure;

namespace ZenGear.ArchitectureTests;

/// <summary>
/// docs/ARCHITECTURE.md, section "Folder structure": one top-level type per file, named after the type.
/// Read from the source because compiled assemblies do not keep file names. Top-level types start at column 0
/// under the file-scoped namespace every file uses (IDE0161 is a warning, hence an error).
/// </summary>
public sealed partial class SourceFileTests
{
    [Test]
    public void Each_file_declares_one_top_level_type_named_after_the_file()
    {
        var offenders = Directory.EnumerateFiles(RepositoryLayout.SourceDirectory, "*.cs", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(RepositoryLayout.SourceDirectory, path))
            .Where(path => !RepositoryLayout.IsExcluded(path))
            .Select(path => (Path: path, Types: TopLevelTypes(Path.Combine(RepositoryLayout.SourceDirectory, path))))
            .Where(file => file.Types.Count > 1
                || (file.Types.Count == 1 && file.Types[0] != Path.GetFileNameWithoutExtension(file.Path)))
            .Select(file => $"{file.Path}: {string.Join(", ", file.Types)}")
            .ToList();

        offenders.ShouldBeEmpty();
    }

    private static List<string> TopLevelTypes(string path) =>
        TypeDeclaration().Matches(File.ReadAllText(path)).Select(match => match.Groups["name"].Value).ToList();

    // Column 0 only, so nested types and types inside comments or strings are not counted.
    [GeneratedRegex(
        @"^(?:(?:public|internal|file|sealed|static|abstract|partial|readonly|ref|unsafe)\s+)*(?:class|interface|enum|struct|record(?:\s+struct|\s+class)?)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)",
        RegexOptions.Multiline)]
    private static partial Regex TypeDeclaration();
}
