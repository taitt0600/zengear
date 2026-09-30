using Shouldly;
using ZenGear.ArchitectureTests.Infrastructure;

namespace ZenGear.ArchitectureTests;

/// <summary>docs/ARCHITECTURE.md, rule 4: an area writes only its own tables and calls the services of the others.</summary>
public sealed class AreaBoundaryTests
{
    [Test]
    public void Services_write_only_the_entities_of_their_area()
    {
        var writes = EntityWrites.In(RepositoryLayout.Services, IsEntity);

        // Every service that saves something writes its own entities; none found means the scan no longer reads the code.
        writes.ShouldNotBeEmpty("The IL scan found no entity write at all.");

        writes
            .Where(write => ServiceArea(write.Caller.DeclaringType!) != EntityArea(write.Entity))
            .Select(write => $"{Outermost(write.Caller.DeclaringType!).FullName} writes {write.Entity.FullName}")
            .Distinct()
            .ShouldBeEmpty("Call the service of the area that owns the entity (rule 4).");
    }

    [Test]
    public void Areas_do_not_depend_on_each_other_in_a_cycle()
    {
        var dependencies = RepositoryLayout.Services.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsNested: false })
            .SelectMany(type => type.GetConstructors()
                .SelectMany(constructor => constructor.GetParameters())
                .SelectMany(parameter => LayerTests.Unwrap(parameter.ParameterType))
                .Select(dependency => (From: ServiceArea(type), To: ServiceArea(dependency))))
            .Where(edge => edge.From is not null && edge.To is not null && edge.From != edge.To)
            .Distinct()
            .ToLookup(edge => edge.From!, edge => edge.To!);

        dependencies.Select(area => area.Key)
            .Where(area => Reaches(dependencies, area, area, []))
            .ShouldBeEmpty("A cycle between areas means a use case sits in the wrong area (rule 4).");
    }

    private static bool Reaches(ILookup<string, string> dependencies, string from, string target, HashSet<string> visited) =>
        dependencies[from].Any(next => next == target || (visited.Add(next) && Reaches(dependencies, next, target, visited)));

    private static bool IsEntity(Type type) =>
        type.Assembly == RepositoryLayout.Data && type.IsClass && EntityArea(type) is not null;

    // ZenGear.Data.Entities.<Area>
    private static string? EntityArea(Type type) =>
        type.Namespace?.Split('.') is ["ZenGear", "Data", "Entities", var area, ..] && RepositoryLayout.Areas.Contains(area) ? area : null;

    // ZenGear.Services.<Area>; Common and the root belong to no area
    private static string? ServiceArea(Type type) =>
        type.Assembly == RepositoryLayout.Services
        && Outermost(type).Namespace?.Split('.') is ["ZenGear", "Services", var area, ..]
        && RepositoryLayout.Areas.Contains(area)
            ? area
            : null;

    private static Type Outermost(Type type) => type.DeclaringType is { } declaring ? Outermost(declaring) : type;
}
