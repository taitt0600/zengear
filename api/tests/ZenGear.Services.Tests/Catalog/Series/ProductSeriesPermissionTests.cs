using Shouldly;
using ZenGear.Services.Identity.Permissions;

namespace ZenGear.Services.Tests.Catalog.Series;

/// <summary>The default grants of the product series permissions (PRD-03, the owner's decision; DEC-095).</summary>
public class ProductSeriesPermissionTests
{
    private static readonly string[] SeriesKeys =
        [PermissionKeys.ProductSeriesRead, PermissionKeys.ProductSeriesCreate, PermissionKeys.ProductSeriesUpdate];

    [Test]
    public void Every_series_permission_is_in_the_catalog() =>
        SeriesKeys.ShouldBeSubsetOf(new PermissionCatalog().Permissions.Select(p => p.Key));

    [TestCase(PermissionCatalog.AdminRoleName)]
    [TestCase("Store Manager")]
    public void Role_gets_every_series_permission_by_default(string roleName)
    {
        var role = new PermissionCatalog().Roles.Single(r => r.RoleName == roleName);

        foreach (var key in SeriesKeys)
        {
            role.PermissionKeys.ShouldContain(key);
        }
    }

    [Test]
    public void No_other_role_gets_a_series_permission_by_default()
    {
        var others = new PermissionCatalog().Roles
            .Where(r => r.RoleName is not (PermissionCatalog.AdminRoleName or "Store Manager"));

        others.SelectMany(r => r.PermissionKeys).Intersect(SeriesKeys).ShouldBeEmpty();
    }
}
