using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Shouldly;
using ZenGear.Api.Controllers.Identity;
using ZenGear.Api.Controllers.Locations;
using ZenGear.Api.Hosting;

namespace ZenGear.Api.IntegrationTests.Hosting;

public class AudienceRoutePrefixConventionTests
{
    [Test]
    public void A_controller_gets_the_admin_prefix()
    {
        var controller = Controller<StaffController>("staff", "{id:guid}");

        new AudienceRoutePrefixConvention().Apply(controller);

        controller.Selectors[0].AttributeRouteModel!.Template.ShouldBe("api/v1/admin/staff");
    }

    [Test]
    public void A_shop_controller_gets_the_shop_prefix()
    {
        var controller = Controller<BusinessLocationShopController>("business-locations");

        new AudienceRoutePrefixConvention().Apply(controller);

        controller.Selectors[0].AttributeRouteModel!.Template.ShouldBe("api/v1/shop/business-locations");
    }

    [TestCase(null)]
    [TestCase("api/v1/staff")]
    [TestCase("API/v1/staff")]
    [TestCase("/staff")]
    [TestCase("~/staff")]
    public void A_controller_route_that_escapes_the_prefix_throws(string? template)
    {
        var controller = Controller<StaffController>(template);

        Should.Throw<InvalidOperationException>(() => new AudienceRoutePrefixConvention().Apply(controller))
            .Message.ShouldContain(nameof(StaffController));
    }

    [TestCase("/health")]
    [TestCase("~/health")]
    public void An_absolute_action_route_throws(string actionTemplate)
    {
        var controller = Controller<StaffController>("staff", "{id:guid}", actionTemplate);

        Should.Throw<InvalidOperationException>(() => new AudienceRoutePrefixConvention().Apply(controller));
    }

    private static ControllerModel Controller<T>(string? template, params string[] actionTemplates)
    {
        var controller = new ControllerModel(typeof(T).GetTypeInfo(), []);
        controller.Selectors.Add(Selector(template));
        foreach (var actionTemplate in actionTemplates)
        {
            var action = new ActionModel(typeof(T).GetMethod(nameof(ToString))!, []);
            action.Selectors.Add(Selector(actionTemplate));
            controller.Actions.Add(action);
        }

        return controller;
    }

    private static SelectorModel Selector(string? template) =>
        new() { AttributeRouteModel = template is null ? null : new AttributeRouteModel(new RouteAttribute(template)) };
}
