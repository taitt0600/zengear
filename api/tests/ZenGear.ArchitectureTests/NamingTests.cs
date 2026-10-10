using System.Reflection;
using System.Runtime.CompilerServices;
using AutoMapper;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Shouldly;
using ZenGear.ArchitectureTests.Infrastructure;
using ZenGear.Services.Common.Models;
using ZenGear.Services.Locations.BusinessLocations;

namespace ZenGear.ArchitectureTests;

/// <summary>docs/ARCHITECTURE.md, section "Folder structure": the names that still decide a folder.</summary>
public sealed class NamingTests
{
    private static readonly string[] ForbiddenSuffixes = ["Helper", "Helpers", "Util", "Utils", "Manager"];

    /// <summary>Suffix of an API type, and the folder (namespace) it lives in; controllers go by area (below).</summary>
    private static readonly (string Suffix, string Namespace)[] ApiSuffixes =
    [
        ("ExceptionHandler", "ZenGear.Api.ExceptionHandling"),
        ("Problem", "ZenGear.Api.ExceptionHandling"),
        ("Middleware", "ZenGear.Api.Middlewares"),
        ("Setup", "ZenGear.Api.Extensions"),
        ("Extensions", "ZenGear.Api.Extensions"),
        ("Attribute", "ZenGear.Api.Attributes"),
        ("AuthorizationHandler", "ZenGear.Api.Authorization"),
        ("ResultHandler", "ZenGear.Api.Authorization"),
        ("Requirement", "ZenGear.Api.Authorization"),
        ("Options", "ZenGear.Api.Options"),
        ("Claims", "ZenGear.Api.Constants"),
    ];

    [Test] // a name that says nothing about the job hides a type that does several
    public void No_type_is_a_helper_or_a_manager()
    {
        OwnTypes(RepositoryLayout.Api, RepositoryLayout.Services, RepositoryLayout.Data)
            .Where(type => ForbiddenSuffixes.Any(suffix => NameOf(type).EndsWith(suffix, StringComparison.Ordinal)))
            .Select(type => type.FullName)
            .ShouldBeEmpty("Name the type after what it does.");
    }

    [Test] // a utility is pure and shared by two or more areas, so it lives in Common only
    public void Utilities_are_static_classes_in_services_common()
    {
        OwnTypes(RepositoryLayout.Api, RepositoryLayout.Services, RepositoryLayout.Data)
            .Where(type => NameOf(type).EndsWith("Utility", StringComparison.Ordinal) || type.Namespace == "ZenGear.Services.Common.Utilities")
            .Where(type => type.Namespace != "ZenGear.Services.Common.Utilities" || !(type.IsAbstract && type.IsSealed)
                || !NameOf(type).EndsWith("Utility", StringComparison.Ordinal))
            .Select(type => type.FullName)
            .ShouldBeEmpty();
    }

    [Test] // the technical folders of the API are found by the suffix of their types
    public void Api_types_live_in_the_folder_of_their_suffix()
    {
        var misplaced = OwnTypes(RepositoryLayout.Api)
            .Select(type => (Type: type, Expected: ApiSuffixes
                .Where(rule => NameOf(type).EndsWith(rule.Suffix, StringComparison.Ordinal))
                .Select(rule => rule.Namespace)
                .FirstOrDefault()))
            .Where(x => x.Expected is not null && x.Type.Namespace != x.Expected)
            .Select(x => $"{x.Type.FullName} (expected in {x.Expected})")
            .ToList();

        misplaced.ShouldBeEmpty();
    }

    [Test] // DEC-106 (5): a controller calls the services of one area and sits in Controllers/<Area>/ of that area
    public void Controllers_sit_in_the_folder_of_the_area_of_their_service()
    {
        var misplaced = LayerTests.Controllers()
            .Select(controller => (Controller: controller, Areas: controller.GetConstructors()
                .SelectMany(constructor => constructor.GetParameters())
                .Select(parameter => AreaBoundaryTests.ServiceArea(parameter.ParameterType))
                .OfType<string>()
                .Distinct()
                .ToList()))
            .Where(x => !NameOf(x.Controller).EndsWith("Controller", StringComparison.Ordinal)
                || x.Areas.Count != 1
                || x.Controller.Namespace != $"ZenGear.Api.Controllers.{x.Areas[0]}")
            .Select(x => $"{x.Controller.FullName} (calls services of: {string.Join(", ", x.Areas)})")
            .ToList();

        misplaced.ShouldBeEmpty("A controller calls the services of one area and sits in ZenGear.Api/Controllers/<that area>.");
    }

    [Test] // only the request and DTOs shared by every list endpoint live outside a module
    public void Requests_and_dtos_are_in_a_module_or_in_common_models()
    {
        OwnTypes(RepositoryLayout.Services)
            .Where(type => NameOf(type).EndsWith("Request", StringComparison.Ordinal) || NameOf(type).EndsWith("Dto", StringComparison.Ordinal))
            .Where(type => !IsInModule(type) && type.Namespace != "ZenGear.Services.Common.Models")
            .Select(type => type.FullName)
            .ShouldBeEmpty();
    }

    [Test] // list endpoints return PagedDto<T>, never a page DTO per entity
    public void Generic_dtos_are_only_in_services_common_models()
    {
        OwnTypes(RepositoryLayout.Services)
            .Where(IsMisplacedGenericDto)
            .Select(type => type.FullName)
            .ShouldBeEmpty("A generic DTO is shared by every list endpoint (PagedDto<T>): it lives in ZenGear.Services/Common/Models.");
    }

    [Test] // the check above must catch a generic DTO outside Common/Models, or it passes on nothing
    public void A_generic_dto_outside_common_models_fails()
    {
        IsMisplacedGenericDto(typeof(SampleDto<>)).ShouldBeTrue();
        IsMisplacedGenericDto(typeof(PagedDto<>)).ShouldBeFalse();
        IsMisplacedGenericDto(typeof(BusinessLocationPublicDto)).ShouldBeFalse();
    }

    [Test] // *Validator names the validator of one request, which sits beside it in its module
    public void Validators_are_request_validators_in_a_module_or_in_common_validators()
    {
        OwnTypes(RepositoryLayout.Services)
            .Where(type => NameOf(type).EndsWith("Validator", StringComparison.Ordinal) || typeof(IValidator).IsAssignableFrom(type))
            .Where(type => !NameOf(type).EndsWith("Validator", StringComparison.Ordinal)
                || !typeof(IValidator).IsAssignableFrom(type)
                || (!IsInModule(type) && type.Namespace != "ZenGear.Services.Common.Validators"))
            .Select(type => type.FullName)
            .ShouldBeEmpty("*Validator is the FluentValidation validator of a request; name other checks after what they check.");
    }

    [Test] // an enum of the Services layer belongs to the module that uses it
    public void Service_enums_are_in_a_module()
    {
        OwnTypes(RepositoryLayout.Services)
            .Where(type => type.IsEnum && !IsInModule(type))
            .Select(type => type.FullName)
            .ShouldBeEmpty();
    }

    [Test] // the host starts every job, and a job serves the area that owns what it changes
    public void Jobs_are_hosted_services_in_their_area()
    {
        OwnTypes(RepositoryLayout.Services)
            .Where(type => NameOf(type).EndsWith("Job", StringComparison.Ordinal) && !type.IsAbstract)
            .Where(type => !typeof(IHostedService).IsAssignableFrom(type) || !IsInModule(type))
            .Select(type => type.FullName)
            .ShouldBeEmpty();
    }

    [Test] // rule 7: one map per DTO, next to the DTO, and never to an entity
    public void Mapping_is_declared_in_the_dto_it_maps_to()
    {
        var offenders = new[] { RepositoryLayout.Api, RepositoryLayout.Services, RepositoryLayout.Data }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => typeof(Profile).IsAssignableFrom(type) && type != typeof(Profile))
            .Where(type => type.Assembly != RepositoryLayout.Services
                || type.Name != "Mapping" || !type.IsNestedPrivate
                || type.DeclaringType?.Name.EndsWith("Dto", StringComparison.Ordinal) != true)
            .Select(type => type.FullName)
            .ToList();

        offenders.ShouldBeEmpty("A map is a nested private class Mapping inside the DTO it maps to (rule 7).");
    }

    [Test] // the enums of entity columns sit with their entities
    public void Data_enums_are_in_the_enums_folder_of_their_area()
    {
        OwnTypes(RepositoryLayout.Data)
            .Where(type => type.IsEnum)
            .Where(type => type.Namespace?.Split('.') is not ["ZenGear", "Data", "Entities", var area, "Enums"] || !RepositoryLayout.Areas.Contains(area))
            .Select(type => type.FullName)
            .ShouldBeEmpty();
    }

    [Test] // value converters are shared by every area's configurations
    public void Converters_are_in_data_common()
    {
        OwnTypes(RepositoryLayout.Data)
            .Where(type => NameOf(type).EndsWith("Converter", StringComparison.Ordinal))
            .Where(type => type.Namespace != "ZenGear.Data.Common")
            .Select(type => type.FullName)
            .ShouldBeEmpty();
    }

    [Test] // a configuration sits in the area of the entity it configures
    public void Entity_configurations_live_in_configurations_by_area()
    {
        RepositoryLayout.Data.GetTypes()
            .Where(type => type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>)))
            .Where(type => !NameOf(type).EndsWith("Configuration", StringComparison.Ordinal)
                || type.Namespace?.Split('.') is not ["ZenGear", "Data", "Configurations", var area] || !RepositoryLayout.Areas.Contains(area))
            .Select(type => type.FullName)
            .ShouldBeEmpty();
    }

    [Test] // one place per project registers its services
    public void Dependency_injection_classes_are_at_the_root_of_services_and_data()
    {
        OwnTypes(RepositoryLayout.Api, RepositoryLayout.Services, RepositoryLayout.Data)
            .Where(type => type.Name == "DependencyInjection")
            .Select(type => type.FullName)
            .ShouldBe(new[] { "ZenGear.Data.DependencyInjection", "ZenGear.Services.DependencyInjection" }, ignoreOrder: true);
    }

    [Test] // the OpenAPI document names its schemas after the type
    public void Type_names_are_unique_across_the_projects()
    {
        OwnTypes(RepositoryLayout.Api, RepositoryLayout.Services, RepositoryLayout.Data)
            .Where(type => type.Name != "DependencyInjection")
            .GroupBy(NameOf)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: {string.Join(", ", group.Select(type => type.Namespace))}")
            .ShouldBeEmpty("The OpenAPI document names its schemas after the type; put the entity in the name.");
    }

    [Test] // a nested public type would be named after its parent in the OpenAPI document
    public void No_public_type_is_nested()
    {
        new[] { RepositoryLayout.Api, RepositoryLayout.Services, RepositoryLayout.Data }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.IsNestedPublic && !type.Name.Contains('<', StringComparison.Ordinal)
                && type.GetCustomAttribute<CompilerGeneratedAttribute>() is null
                && type.Namespace?.StartsWith("ZenGear.", StringComparison.Ordinal) == true)
            .Select(type => type.FullName)
            .ShouldBeEmpty("A line of a request or DTO is a top-level type (StockAdjustmentLineRequest).");
    }

    /// <summary>Top-level types written in this repository (no compiler-generated or nested types).</summary>
    private static IEnumerable<Type> OwnTypes(params Assembly[] assemblies) =>
        assemblies.SelectMany(assembly => assembly.GetTypes())
            .Where(type => !type.IsNested && type.Namespace?.StartsWith("ZenGear.", StringComparison.Ordinal) == true)
            .Where(type => type.GetCustomAttribute<CompilerGeneratedAttribute>() is null);

    /// <summary>The name without the generic arity (<c>AttributeEnumConverter`1</c> → <c>AttributeEnumConverter</c>).</summary>
    private static string NameOf(Type type) => type.Name.Split('`')[0];

    // ZenGear.Services.<Area>.<Module>, or a mechanism folder inside the module
    private static bool IsInModule(Type type) =>
        type.Namespace?.Split('.') is ["ZenGear", "Services", var area, _, ..] && RepositoryLayout.Areas.Contains(area);

    private static bool IsMisplacedGenericDto(Type type) =>
        type.IsGenericTypeDefinition && NameOf(type).EndsWith("Dto", StringComparison.Ordinal)
        && type.Namespace != "ZenGear.Services.Common.Models";

    /// <summary>A generic DTO outside ZenGear.Services/Common/Models, for <see cref="A_generic_dto_outside_common_models_fails"/>.</summary>
    private sealed record SampleDto<T>(IReadOnlyList<T> Items);
}
