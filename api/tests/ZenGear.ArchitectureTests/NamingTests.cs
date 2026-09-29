using Microsoft.EntityFrameworkCore;
using Shouldly;
using ZenGear.ArchitectureTests.Infrastructure;

namespace ZenGear.ArchitectureTests;

/// <summary>docs/ARCHITECTURE.md, section "Folder structure": where a type goes follows from its name.</summary>
public sealed class NamingTests
{
    private static readonly string[] ForbiddenSuffixes = ["Helper", "Helpers", "Util", "Utils", "Utility", "Utilities", "Manager"];

    [Test]
    public void No_type_is_a_helper_or_utility()
    {
        var offenders = new[] { RepositoryLayout.Api, RepositoryLayout.Services, RepositoryLayout.Data }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.Namespace?.StartsWith("ZenGear.", StringComparison.Ordinal) == true)
            .Where(type => ForbiddenSuffixes.Any(suffix => type.Name.EndsWith(suffix, StringComparison.Ordinal)))
            .Select(type => type.FullName)
            .ToList();

        offenders.ShouldBeEmpty("Name the type after what it does.");
    }

    [Test]
    public void Controllers_live_in_an_area_folder()
    {
        var misplaced = LayerTests.Controllers()
            .Where(type => !type.Name.EndsWith("Controller", StringComparison.Ordinal)
                || type.Namespace?.Split('.') is not ["ZenGear", "Api", var area] || !RepositoryLayout.Areas.Contains(area))
            .Select(type => type.FullName)
            .ToList();

        misplaced.ShouldBeEmpty();
    }

    [Test]
    public void Entity_configurations_live_in_configurations_by_area()
    {
        var misplaced = RepositoryLayout.Data.GetTypes()
            .Where(type => type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>)))
            .Where(type => !type.Name.EndsWith("Configuration", StringComparison.Ordinal)
                || type.Namespace?.Split('.') is not ["ZenGear", "Data", "Configurations", var area] || !RepositoryLayout.Areas.Contains(area))
            .Select(type => type.FullName)
            .ToList();

        misplaced.ShouldBeEmpty();
    }

    [Test]
    public void Jobs_are_hosted_services_in_the_jobs_folder()
    {
        var misplaced = RepositoryLayout.Services.GetTypes()
            .Where(type => type.Name.EndsWith("Job", StringComparison.Ordinal) && !type.IsAbstract)
            .Where(type => type.Namespace != "ZenGear.Services.Jobs"
                || !typeof(Microsoft.Extensions.Hosting.IHostedService).IsAssignableFrom(type))
            .Select(type => type.FullName)
            .ToList();

        misplaced.ShouldBeEmpty();
    }
}
