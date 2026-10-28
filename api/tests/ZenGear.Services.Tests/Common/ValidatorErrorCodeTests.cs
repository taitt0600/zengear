using System.Reflection;
using System.Text.RegularExpressions;
using FluentValidation;
using FluentValidation.Internal;
using FluentValidation.Validators;
using Moq;
using Shouldly;
using ZenGear.Services.Catalog.Brands;

namespace ZenGear.Services.Tests.Common;

/// <summary>
/// Every check of every validator of the services has its own code (DEC-117 (2), (5)): the API answers a 400 with the
/// code and writes the text from it, so a check without an explicit code would answer FluentValidation's own name.
/// A <c>Custom</c> rule builds its failures by hand with their codes; those codes are checked by the OpenAPI and
/// problem texts tests of the API.
/// </summary>
public partial class ValidatorErrorCodeTests
{
    /// <summary>A Postgres identifier fits 63 characters; the codes keep the same limit (DEC-117 (5)).</summary>
    private const int MaxCodeLength = 63;

    private static IEnumerable<TestCaseData> Validators() =>
        typeof(BrandCreateRequestValidator).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false } && IsAbstractValidator(t))
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .Select(t => new TestCaseData(t).SetArgDisplayNames(t.Name));

    [Test]
    public void The_services_have_validators() => Validators().Count().ShouldBeGreaterThanOrEqualTo(10);

    [TestCaseSource(nameof(Validators))]
    public void Every_check_has_an_explicit_code_in_upper_snake_case(Type validatorType)
    {
        var checks = Checks(Create(validatorType), "").ToList();

        checks.ShouldNotBeEmpty();
        checks.ShouldAllBe(c => c.Code != null && CodePattern().IsMatch(c.Code) && c.Code.Length <= MaxCodeLength,
            $"{validatorType.Name}: {string.Join(", ", checks.Select(c => $"{c.Path}={c.Code ?? "(none)"} [{c.Validator}]"))}");
    }

    [TestCaseSource(nameof(Validators))]
    public void Codes_are_distinct_per_property_within_a_validator(Type validatorType)
    {
        // Each validator on its own: a child validator set on a property (the shared catalog name) is checked as itself,
        // and the parent's null check may repeat its "required" code for the same reason.
        var duplicates = Checks(Create(validatorType), "", recurse: false)
            .GroupBy(c => (c.Path, c.Code))
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key.Path}={g.Key.Code}")
            .ToList();

        duplicates.ShouldBeEmpty(validatorType.Name);
    }

    [Test]
    public void A_must_that_appends_an_argument_fills_the_placeholders_of_its_failure()
    {
        var failure = new BrandCreateRequestValidator().Validate(new BrandCreateRequest(new string('a', 101))).Errors
            .ShouldHaveSingleItem();

        failure.ErrorCode.ShouldBe("NAME_TOO_LONG");
        failure.FormattedMessagePlaceholderValues.ShouldContainKeyAndValue("MaxLength", 100);
    }

    private sealed record Check(string Path, string? Code, string Validator);

    /// <summary>
    /// The checks of a validator, with the path of their property; a child validator (<c>SetValidator</c>,
    /// <c>ChildRules</c>) adds its own checks under the property it is set on, and is not a check itself. A
    /// <c>Custom</c> rule is left out (see the summary of the class).
    /// </summary>
    private static IEnumerable<Check> Checks(IValidator validator, string prefix, bool recurse = true)
    {
        foreach (var rule in validator.CreateDescriptor().Rules)
        {
            var path = Join(prefix, rule.PropertyName ?? "") + (IsCollectionRule(rule) ? "[]" : "");
            foreach (var component in rule.Components)
            {
                if (component.Validator is IChildValidatorAdaptor)
                {
                    if (recurse)
                    {
                        foreach (var check in Checks(ChildValidator(component.Validator), path))
                        {
                            yield return check;
                        }
                    }

                    continue;
                }

                if (IsCustom(rule, component))
                {
                    continue;
                }

                yield return new Check(path, component.ErrorCode, component.Validator.Name);
            }
        }
    }

    private static string Join(string prefix, string name)
    {
        if (prefix.Length == 0 || name.Length == 0)
        {
            return prefix + name;
        }

        return $"{prefix}.{name}";
    }

    private static bool IsCollectionRule(IValidationRule rule) =>
        rule.GetType().GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollectionRule<,>));

    /// <summary>
    /// FluentValidation builds <c>RuleFor(r =&gt; r).Custom(…)</c> as a predicate on the whole request that always passes
    /// and has no code; its failures carry the codes set by hand.
    /// </summary>
    private static bool IsCustom(IValidationRule rule, IRuleComponent component) =>
        string.IsNullOrEmpty(rule.PropertyName) && component.ErrorCode is null && component.Validator.Name == "PredicateValidator";

    /// <summary>
    /// The validator a <c>ChildValidatorAdaptor</c> was built with (<c>SetValidator(new …)</c>, <c>ChildRules</c>);
    /// FluentValidation exposes it only through <c>GetValidator(context, value)</c>, so it is read from its field.
    /// </summary>
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

    private static bool IsAbstractValidator(Type type)
    {
        for (var t = type.BaseType; t != null; t = t.BaseType)
        {
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(AbstractValidator<>))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>A validator with a mock for each dependency of its constructor (the password validator needs the auth provider).</summary>
    private static IValidator Create(Type validatorType)
    {
        var constructor = validatorType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .OrderBy(c => c.GetParameters().Length).First();
        var arguments = constructor.GetParameters()
            .Select(p => ((Mock)Activator.CreateInstance(typeof(Mock<>).MakeGenericType(p.ParameterType))!).Object)
            .ToArray();
        return (IValidator)constructor.Invoke(arguments);
    }

    [GeneratedRegex("^[A-Z][A-Z0-9]*(_[A-Z0-9]+)*$", RegexOptions.None, matchTimeoutMilliseconds: 100)]
    private static partial Regex CodePattern();
}
