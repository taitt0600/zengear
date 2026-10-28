using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ZenGear.Api.Attributes;
using ZenGear.Api.ExceptionHandling;

namespace ZenGear.Api.IntegrationTests.Infrastructure;

/// <summary>An action of a controller of the API and the codes its <see cref="ErrorCodesAttribute"/> declare for a status.</summary>
public sealed record DeclaredErrorCodes(Type Controller, MethodInfo Action, int Status, IReadOnlySet<string> Codes)
{
    /// <summary>The name of the enum component the OpenAPI document gives it: <c>BrandCreateErrorCode</c>.</summary>
    public string EnumName =>
        Controller.Name[..^"Controller".Length] + Action.Name + (Status == StatusCodes.Status400BadRequest ? "ErrorCode" : "ConflictCode");

    /// <summary>Every declaration of the controllers of ZenGear.Api, one per action and status.</summary>
    public static IReadOnlyList<DeclaredErrorCodes> All { get; } =
    [
        .. from controller in typeof(ProblemTexts).Assembly.GetTypes()
           where controller is { IsClass: true, IsAbstract: false } && controller.IsSubclassOf(typeof(ControllerBase))
           from action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
           from byStatus in action.GetCustomAttributes<ErrorCodesAttribute>().GroupBy(a => a.Status)
           select new DeclaredErrorCodes(controller, action, byStatus.Key,
               byStatus.SelectMany(a => a.Codes).ToHashSet(StringComparer.Ordinal)),
    ];
}
