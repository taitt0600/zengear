using System.Reflection;
using FluentValidation;
using FluentValidation.Validators;
using Microsoft.Extensions.DependencyInjection;
using ZenGear.Services.Catalog.Brands;

namespace ZenGear.Api.IntegrationTests.Infrastructure;

/// <summary>
/// The codes the validators of the services set (DEC-117 (2)), read from their descriptors, resolved from the test
/// host's services as the API resolves them (the password validator needs the faked Firebase). The second copy of the
/// walk of ZenGear.Services.Tests (<c>ValidatorErrorCodeTests</c>): the test projects do not reference each other.
/// </summary>
public static class ValidatorCodes
{
    /// <summary>The request types of the public validators of the services, which the assembly scan registers.</summary>
    public static IReadOnlyList<Type> RequestTypes { get; } =
    [
        .. typeof(BrandCreateRequestValidator).Assembly.GetTypes()
            .Where(t => t is { IsPublic: true, IsClass: true, IsAbstract: false })
            .Select(ValidatedType)
            .OfType<Type>()
            .OrderBy(t => t.FullName, StringComparer.Ordinal),
    ];

    /// <summary>The codes of the registered <c>IValidator&lt;<paramref name="requestType"/>&gt;</c>.</summary>
    public static IReadOnlySet<string> Of(IServiceProvider services, Type requestType)
    {
        using var scope = services.CreateScope();
        var validator = (IValidator)scope.ServiceProvider.GetRequiredService(typeof(IValidator<>).MakeGenericType(requestType));
        return Of(validator);
    }

    /// <summary>
    /// The codes of every check, including those of the child validators (<c>SetValidator</c>, <c>ChildRules</c>). A
    /// <c>Custom</c> rule has no code of its own: its failures carry codes set by hand, which the actions list in their
    /// <c>[ErrorCodes]</c>.
    /// </summary>
    public static IReadOnlySet<string> Of(IValidator validator)
    {
        var codes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var component in validator.CreateDescriptor().Rules.SelectMany(rule => rule.Components))
        {
            if (component.Validator is IChildValidatorAdaptor)
            {
                codes.UnionWith(Of(ChildValidator(component.Validator)));
            }
            else if (component.ErrorCode is { } code)
            {
                codes.Add(code);
            }
        }

        return codes;
    }

    private static Type? ValidatedType(Type type)
    {
        for (var t = type.BaseType; t != null; t = t.BaseType)
        {
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(AbstractValidator<>))
            {
                return t.GetGenericArguments()[0];
            }
        }

        return null;
    }

    /// <summary>FluentValidation exposes the validator of a child adaptor only through a method that needs a context.</summary>
    private static IValidator ChildValidator(IPropertyValidator adaptor)
    {
        for (var type = adaptor.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .FirstOrDefault(f => typeof(IValidator).IsAssignableFrom(f.FieldType));
            if (field?.GetValue(adaptor) is IValidator validator)
            {
                return validator;
            }
        }

        throw new InvalidOperationException($"{adaptor.GetType().Name} holds no fixed validator.");
    }
}
