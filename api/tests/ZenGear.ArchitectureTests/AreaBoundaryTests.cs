using NetArchTest.Rules;
using Shouldly;
using ZenGear.ArchitectureTests.Infrastructure;

namespace ZenGear.ArchitectureTests;

/// <summary>
/// docs/ARCHITECTURE.md, rule 4 and Business areas: an area writes only its own tables, calls another area only
/// through the public contract of its README, and Locations is upstream (DEC-106).
/// </summary>
public sealed class AreaBoundaryTests
{
    [Test] // rule 4: a use case writes only the tables of its own area
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

    [Test] // a cycle between areas means a use case sits in the wrong area
    public void Areas_do_not_depend_on_each_other_in_a_cycle()
    {
        var dependencies = CallsAcrossAreas()
            .Select(call => (call.From, call.To))
            .Distinct()
            .ToLookup(edge => edge.From, edge => edge.To);

        dependencies.Select(area => area.Key)
            .Where(area => Reaches(dependencies, area, area, []))
            .ShouldBeEmpty("A cycle between areas means a use case sits in the wrong area (rule 4).");
    }

    [Test] // DEC-106 (3): another area calls an area only through a service its README lists under Public contract
    public void Calls_across_areas_go_to_a_public_contract()
    {
        var contracts = RepositoryLayout.Areas.ToDictionary(area => area, PublicContract, StringComparer.Ordinal);

        CallsOutsideContract(CallsAcrossAreas(), contracts)
            .ShouldBeEmpty("List the service under Public contract in the README of its area, or move the use case (rule 4).");
    }

    [Test] // the check above must catch a call outside a contract, or it passes on nothing
    public void A_call_outside_a_public_contract_fails()
    {
        (string From, string To, Type Callee)[] calls = [("Sales", "Identity", typeof(AreaBoundaryTests))];

        CallsOutsideContract(calls, new Dictionary<string, HashSet<string>> { ["Identity"] = ["StaffService"] }).ShouldNotBeEmpty();
        CallsOutsideContract(calls, new Dictionary<string, HashSet<string>> { ["Identity"] = [nameof(AreaBoundaryTests)] }).ShouldBeEmpty();
    }

    [Test] // DEC-106 (2): every area may read Locations, so Locations depends on no area but reads of Geography
    public void Locations_depends_on_no_area_but_geography()
    {
        var forbidden = RepositoryLayout.Areas.Where(area => area != "Locations")
            .Select(area => $"ZenGear.Services.{area}")
            .Concat(RepositoryLayout.Areas.Where(area => area is not ("Locations" or "Geography")).Select(area => $"ZenGear.Data.Entities.{area}"))
            .ToArray();

        DependentTypes("ZenGear.Services.Locations", forbidden)
            .ShouldBeEmpty("Locations is upstream: it reads Geography and depends on no other area (docs/ARCHITECTURE.md, Business areas).");
    }

    [Test] // the check above must see the types of a module and a dependency in a child namespace, or it passes on nothing
    public void A_dependency_on_another_area_is_found()
    {
        // Every type of Locations sits in Locations.BusinessLocations and uses Data.Entities.Locations.
        DependentTypes("ZenGear.Services.Locations", ["ZenGear.Data.Entities"]).ShouldNotBeEmpty();
    }

    // ZenGear.Services.<Area>; Common and the root belong to no area
    internal static string? ServiceArea(Type type) =>
        type.Assembly == RepositoryLayout.Services
        && Outermost(type).Namespace?.Split('.') is ["ZenGear", "Services", var area, ..]
        && RepositoryLayout.Areas.Contains(area)
            ? area
            : null;

    /// <summary>Constructor dependencies of the services of one area on a type of another area.</summary>
    private static List<(string From, string To, Type Callee)> CallsAcrossAreas() =>
        RepositoryLayout.Services.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsNested: false })
            .SelectMany(type => type.GetConstructors()
                .SelectMany(constructor => constructor.GetParameters())
                .SelectMany(parameter => LayerTests.Unwrap(parameter.ParameterType))
                .Select(dependency => (From: ServiceArea(type), To: ServiceArea(dependency), Callee: dependency)))
            .Where(call => call.From is not null && call.To is not null && call.From != call.To)
            .Select(call => (call.From!, call.To!, call.Callee))
            .ToList();

    private static List<string> CallsOutsideContract(
        IEnumerable<(string From, string To, Type Callee)> calls, IReadOnlyDictionary<string, HashSet<string>> contracts) =>
        calls.Where(call => !(contracts.TryGetValue(call.To, out var contract) && contract.Contains(call.Callee.Name)))
            .Select(call => $"{call.From} calls {call.Callee.FullName} of {call.To}")
            .Distinct()
            .ToList();

    private static HashSet<string> PublicContract(string area) =>
        RepositoryLayout.AreaReadme(area) is { } readme ? RepositoryLayout.BulletNames(RepositoryLayout.Section(readme, "Public contract")) : [];

    private static List<string> DependentTypes(string @namespace, string[] dependencies)
    {
        var result = Types.InAssembly(RepositoryLayout.Services)
            .That().ResideInNamespace(@namespace)
            .ShouldNot().HaveDependencyOnAny(dependencies)
            .GetResult();

        return result.FailingTypes?.Select(type => type.FullName).OfType<string>().ToList() ?? [];
    }

    private static bool Reaches(ILookup<string, string> dependencies, string from, string target, HashSet<string> visited) =>
        dependencies[from].Any(next => next == target || (visited.Add(next) && Reaches(dependencies, next, target, visited)));

    private static bool IsEntity(Type type) =>
        type.Assembly == RepositoryLayout.Data && type.IsClass && EntityArea(type) is not null;

    // ZenGear.Data.Entities.<Area>
    private static string? EntityArea(Type type) =>
        type.Namespace?.Split('.') is ["ZenGear", "Data", "Entities", var area, ..] && RepositoryLayout.Areas.Contains(area) ? area : null;

    private static Type Outermost(Type type) => type.DeclaringType is { } declaring ? Outermost(declaring) : type;
}
