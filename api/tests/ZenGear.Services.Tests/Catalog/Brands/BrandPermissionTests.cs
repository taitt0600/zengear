using Shouldly;
using ZenGear.Services.Identity.Permissions;

namespace ZenGear.Services.Tests.Catalog.Brands;

/// <summary>The default grants of the brand permissions (PRD-02, the owner's decision; DEC-095).</summary>
public class BrandPermissionTests
{
    private static readonly string[] BrandKeys = [PermissionKeys.BrandRead, PermissionKeys.BrandCreate, PermissionKeys.BrandUpdate];

    [TestCase(PermissionCatalog.AdminRoleName)]
    [TestCase("Store Manager")]
    public void Role_gets_every_brand_permission_by_default(string roleName)
    {
        var role = new PermissionCatalog().Roles.Single(r => r.RoleName == roleName);

        foreach (var key in BrandKeys)
        {
            role.PermissionKeys.ShouldContain(key);
        }
    }

    [Test]
    public void No_other_role_gets_a_brand_permission_by_default()
    {
        var others = new PermissionCatalog().Roles
            .Where(r => r.RoleName is not (PermissionCatalog.AdminRoleName or "Store Manager"));

        others.SelectMany(r => r.PermissionKeys).Intersect(BrandKeys).ShouldBeEmpty();
    }
}
