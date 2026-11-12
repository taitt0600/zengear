using Shouldly;
using ZenGear.Services.Identity.Permissions;

namespace ZenGear.Services.Tests.Identity.Permissions;

/// <summary>The roles and permissions of the code (DEC-095, DEC-128).</summary>
public class PermissionCatalogTests
{
    private readonly PermissionCatalog _catalog = new();

    [Test]
    public void R_SEC_03_catalog_lists_ten_roles_with_it_support()
    {
        _catalog.Roles.Count.ShouldBe(10);
        _catalog.Roles.Count(r => r.IsSystemRole).ShouldBe(2);
        var itSupport = _catalog.Roles.Single(r => r.RoleName == "IT Support");
        itSupport.IsSystemRole.ShouldBeFalse();
        itSupport.PermissionKeys.ShouldBe([PermissionKeys.StaffRead, PermissionKeys.StaffCreate, PermissionKeys.StaffDisable],
            ignoreOrder: true);
    }

    [Test]
    public void R_SEC_03_role_permissions_are_granted_to_the_admin_only()
    {
        string[] roleKeys = [PermissionKeys.RoleRead, PermissionKeys.RoleCreate, PermissionKeys.RoleUpdate, PermissionKeys.RoleDelete];

        roleKeys.ShouldBeSubsetOf(_catalog.Permissions.Select(p => p.Key));
        roleKeys.ShouldBeSubsetOf(_catalog.Roles.Single(r => r.RoleName == PermissionCatalog.AdminRoleName).PermissionKeys);
        _catalog.Roles.Where(r => r.RoleName != PermissionCatalog.AdminRoleName)
            .ShouldAllBe(r => !r.PermissionKeys.Any(k => roleKeys.Contains(k)));
        PermissionCatalog.IsAdminOnly(PermissionKeys.RoleUpdate).ShouldBeTrue();
        PermissionCatalog.IsAdminOnly(PermissionKeys.StaffCreate).ShouldBeFalse();
    }
}
