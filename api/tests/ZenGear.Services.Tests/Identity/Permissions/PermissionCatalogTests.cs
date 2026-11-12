using Shouldly;
using ZenGear.Services.Identity.Permissions;

namespace ZenGear.Services.Tests.Identity.Permissions;

/// <summary>The roles, permissions and default manageable sets of the code (DEC-095, DEC-128).</summary>
public class PermissionCatalogTests
{
    private static readonly string[] OperationalRoles =
        ["Sales Consultant", "Technical Support", "Cashier", "Warehouse Keeper", "Purchasing", "Accountant"];

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

    [Test]
    public void R_SEC_12_catalog_never_lists_a_system_role_as_managed()
    {
        var system = _catalog.Roles.Where(r => r.IsSystemRole).Select(r => r.RoleName).ToList();
        var names = _catalog.Roles.Select(r => r.RoleName).ToList();

        _catalog.Roles.SelectMany(r => r.ManagedRoleNames).ShouldAllBe(name => names.Contains(name) && !system.Contains(name));
        _catalog.Roles.Where(r => r.IsSystemRole).ShouldAllBe(r => r.ManagedRoleNames.Count == 0);
    }

    [Test]
    public void R_SEC_12_catalog_store_manager_manages_the_default_roles()
    {
        var defaults = _catalog.Roles.Where(r => !r.IsSystemRole).Select(r => r.RoleName).ToList();

        defaults.Count.ShouldBe(8);
        _catalog.Roles.Single(r => r.RoleName == "Store Manager").ManagedRoleNames.ShouldBe(defaults, ignoreOrder: true);
    }

    [Test]
    public void R_SEC_12_catalog_it_support_manages_the_six_operational_roles()
    {
        _catalog.Roles.Single(r => r.RoleName == "IT Support").ManagedRoleNames.ShouldBe(OperationalRoles, ignoreOrder: true);
        _catalog.Roles.Where(r => r.RoleName is not ("Store Manager" or "IT Support"))
            .ShouldAllBe(r => r.ManagedRoleNames.Count == 0);
    }
}
