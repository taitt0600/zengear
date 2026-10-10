using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace ZenGear.Api.Hosting;

/// <summary>
/// Puts <see cref="ShopPrefix"/> before the route of a <c>*ShopController</c> and <see cref="AdminPrefix"/> before
/// every other controller's (DEC-106). A route that would escape the prefix throws while MVC builds its application
/// model, so the build (OpenAPI generation) and the tests fail instead of serving it.
/// </summary>
public sealed class AudienceRoutePrefixConvention : IControllerModelConvention
{
    public const string AdminPrefix = "api/v1/admin";
    public const string ShopPrefix = "api/v1/shop";

    public void Apply(ControllerModel controller)
    {
        var name = controller.ControllerType.Name;
        var routed = controller.Selectors.Where(selector => selector.AttributeRouteModel is not null).ToList();
        if (routed.Count == 0)
        {
            throw Escapes(name, "has no [Route]");
        }

        if (routed.Select(selector => selector.AttributeRouteModel!.Template ?? "").FirstOrDefault(IsPrefixedOrAbsolute) is { } template)
        {
            throw Escapes(name, $"declares the route \"{template}\"; declare only its resource");
        }

        if (controller.Actions.SelectMany(action => action.Selectors)
                .Select(selector => selector.AttributeRouteModel?.Template)
                .FirstOrDefault(AttributeRouteModel.IsOverridePattern) is { } actionTemplate)
        {
            throw Escapes(name, $"has an action on the absolute route \"{actionTemplate}\"");
        }

        var prefix = new AttributeRouteModel(new RouteAttribute(
            name.EndsWith("ShopController", StringComparison.Ordinal) ? ShopPrefix : AdminPrefix));
        foreach (var selector in routed)
        {
            selector.AttributeRouteModel = AttributeRouteModel.CombineAttributeRouteModel(prefix, selector.AttributeRouteModel);
        }
    }

    private static bool IsPrefixedOrAbsolute(string template) =>
        template.StartsWith("api/", StringComparison.OrdinalIgnoreCase) || AttributeRouteModel.IsOverridePattern(template);

    private static InvalidOperationException Escapes(string controller, string problem) =>
        new($"{controller} {problem}: the route prefix of DEC-106 (/api/v1/admin/ or /api/v1/shop/) is added by {nameof(AudienceRoutePrefixConvention)}.");
}
