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

    [Test] // rule 3: controllers take requests and return DTOs of the services
    public void Actions_take_requests_and_return_dtos_of_the_services()
    {
        var offenders = Controllers()
            .SelectMany(controller => controller.GetMethods(System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly))
            .SelectMany(action =>
                action.GetParameters().SelectMany(parameter => Unwrap(parameter.ParameterType))
                    .Where(type => IsServicesType(type) && !type.Name.EndsWith("Request", StringComparison.Ordinal))
                    .Concat(Unwrap(action.ReturnType).Where(type => IsServicesType(type) && !type.Name.EndsWith("Dto", StringComparison.Ordinal)))
                    .Select(type => $"{action.DeclaringType!.Name}.{action.Name}: {type.Name}"))
            .ToList();

        offenders.ShouldBeEmpty();
    }

    [Test] // rule 6: the packages of external systems are referenced by ZenGear.Data only
    public void External_system_packages_are_referenced_by_data_only()
    {
        string[] externalPackages = ["FirebaseAdmin", "Google.Cloud.Storage.V1", "Resend", "StackExchange.Redis"];

        var offenders = Directory.GetFiles(RepositoryLayout.SourceDirectory, "*.csproj", SearchOption.AllDirectories)
            .Where(project => Path.GetFileNameWithoutExtension(project) != "ZenGear.Data")
            .SelectMany(project => externalPackages
                .Where(package => File.ReadAllText(project).Contains($"Include=\"{package}\"", StringComparison.Ordinal))
                .Select(package => $"{Path.GetFileName(project)}: {package}"))
            .ToList();

        offenders.ShouldBeEmpty();
    }

    internal static IEnumerable<Type> Controllers() =>
        RepositoryLayout.Api.GetTypes().Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract);

    // Task<ActionResult<List<X>>> → Task, ActionResult, List, X
    internal static IEnumerable<Type> Unwrap(Type type) =>
        new[] { type }.Concat(type.IsGenericType ? type.GetGenericArguments().SelectMany(Unwrap) : Enumerable.Empty<Type>());

    private static bool IsServicesType(Type type) => type.Assembly == RepositoryLayout.Services && !type.IsEnum;

    private static bool IsEntity(Type type) =>
        type.Namespace?.StartsWith("ZenGear.Data.Entities", StringComparison.Ordinal) == true && !type.IsEnum;

    private static string Describe(TestResult result) =>
        string.Join(", ", result.FailingTypes?.Select(type => type.FullName) ?? Enumerable.Empty<string>());
}
