using System.Reflection;

namespace ZenGear.Api.Hosting;

/// <summary>
/// True while the build runs Program.cs to write the OpenAPI document. GetDocument.Insider starts the
/// host (Production environment, no user-secrets), so start-up checks that need configuration are skipped.
/// </summary>
public static class BuildTimeDocumentGeneration
{
    public static bool IsActive { get; } = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
}
