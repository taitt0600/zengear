namespace ZenGear.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Files of the repository the tests read (the OpenAPI document, the seed). A copy of the root search of
/// ZenGear.ArchitectureTests (RepositoryLayout): the test projects do not reference each other.
/// </summary>
public static class RepositoryFiles
{
    /// <summary>The full path of <paramref name="segments"/>, relative to the repository root.</summary>
    public static string PathOf(params string[] segments)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "api", "ZenGear.slnx")))
            {
                return Path.Combine([dir.FullName, .. segments]);
            }
        }

        throw new InvalidOperationException($"Repository root (api/ZenGear.slnx) not found above {AppContext.BaseDirectory}.");
    }
}
