using Microsoft.AspNetCore.Mvc;
using NetArchTest.Rules;
using Shouldly;
using ZenGear.ArchitectureTests.Infrastructure;

namespace ZenGear.ArchitectureTests;

/// <summary>Dependency rules of docs/ARCHITECTURE.md, section "Dependency rules".</summary>
public sealed class LayerTests
{
    [Test]
    public void Data_references_no_other_project()
    {
        var references = RepositoryLayout.Data.GetReferencedAssemblies().Select(name => name.Name).ToList();

        references.ShouldNotContain("ZenGear.Services");
        references.ShouldNotContain("ZenGear.Api");
    }

    [Test]
    public void Services_do_not_reference_the_api()
    {
        RepositoryLayout.Services.GetReferencedAssemblies().Select(name => name.Name).ShouldNotContain("ZenGear.Api");
    }

    [Test]
    public void Controllers_never_use_ef_core()
    {
        var result = Types.InAssembly(RepositoryLayout.Api)
            .That().Inherit(typeof(ControllerBase))
            .Should().NotHaveDependencyOnAny("Microsoft.EntityFrameworkCore", "ZenGear.Data.ZenGearDbContext")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Test] // R-SEC-09, R-SEC-10: the API returns DTOs, never entities
    public void R_SEC_09_Actions_never_return_entities()
    {
        var leaks = Controllers()
            .SelectMany(controller => controller.GetMethods(System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly))
            .Where(action => Unwrap(action.ReturnType).Any(IsEntity))
            .Select(action => $"{action.DeclaringType!.Name}.{action.Name}")
            .ToList();

        leaks.ShouldBeEmpty();
    }

    internal static IEnumerable<Type> Controllers() =>
        RepositoryLayout.Api.GetTypes().Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract);

    // Task<ActionResult<List<X>>> → Task, ActionResult, List, X
    private static IEnumerable<Type> Unwrap(Type type) =>
        new[] { type }.Concat(type.IsGenericType ? type.GetGenericArguments().SelectMany(Unwrap) : Enumerable.Empty<Type>());

    private static bool IsEntity(Type type) =>
        type.Namespace?.StartsWith("ZenGear.Data.Entities", StringComparison.Ordinal) == true && !type.IsEnum;

    private static string Describe(TestResult result) =>
        string.Join(", ", result.FailingTypes?.Select(type => type.FullName) ?? Enumerable.Empty<string>());
}
