using Microsoft.EntityFrameworkCore;
using Shouldly;
using ZenGear.Api.IntegrationTests.Infrastructure;

namespace ZenGear.Api.IntegrationTests.Identity.Roles;

/// <summary><c>api/seed/01_roles.sql</c> writes the default manageable sets once, by the fixed role ids (R-SEC-12, DEC-129).</summary>
public class RoleSeedTests : IntegrationTestBase
{
    private static readonly string[] OperationalRoles =
        ["Sales Consultant", "Technical Support", "Cashier", "Warehouse Keeper", "Purchasing", "Accountant"];

    [Test]
    public async Task R_SEC_12_seed_writes_the_default_manageable_sets()
    {
        await TestRoles.RunSeedAsync();

        (await ManagedAsync("Store Manager")).ShouldBe(["Store Manager", "IT Support", .. OperationalRoles], ignoreOrder: true);
        (await ManagedAsync("IT Support")).ShouldBe(OperationalRoles, ignoreOrder: true);
        (await ManagingRolesAsync()).ShouldBe(["Store Manager", "IT Support"], ignoreOrder: true);
    }

    [Test]
    public async Task R_SEC_12_seed_run_again_adds_no_pair_once_any_exists()
    {
        await TestRoles.RunSeedAsync();
        // The Admin takes Cashier out of the Store Manager's set (AUT-05); the seed runs again at the next deploy.
        await TestRoles.UnmanageAsync(await RoleIdAsync("Store Manager"), await RoleIdAsync("Cashier"));

        await TestRoles.RunSeedAsync();

        (await ManagedAsync("Store Manager")).ShouldNotContain("Cashier");
        (await ManagedAsync("Store Manager")).Count.ShouldBe(7);
        (await ManagedAsync("IT Support")).ShouldBe(OperationalRoles, ignoreOrder: true);
    }

    private async Task<Guid> RoleIdAsync(string name)
    {
        var id = Guid.Empty;
        await Factory.WithDbAsync(async db => id = await db.Roles.Where(r => r.Name == name).Select(r => r.Id).SingleAsync());
        return id;
    }

    private async Task<List<string>> ManagedAsync(string roleName)
    {
        List<string> names = [];
        await Factory.WithDbAsync(async db => names = await db.RoleManageableRoles
            .Where(m => db.Roles.Any(r => r.Id == m.RoleId && r.Name == roleName))
            .Join(db.Roles, m => m.ManagedRoleId, r => r.Id, (_, r) => r.Name)
            .ToListAsync());
        return names;
    }

    private async Task<List<string>> ManagingRolesAsync()
    {
        List<string> names = [];
        await Factory.WithDbAsync(async db => names = await db.RoleManageableRoles
            .Join(db.Roles, m => m.RoleId, r => r.Id, (_, r) => r.Name)
            .Distinct()
            .ToListAsync());
        return names;
    }
}
