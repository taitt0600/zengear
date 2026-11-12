using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using ZenGear.Api.IntegrationTests.Infrastructure;
using ZenGear.Data;
using ZenGear.Services.Common;
using ZenGear.Services.Identity.Access.AccessCache;
using ZenGear.Services.Identity.Permissions;

namespace ZenGear.Api.IntegrationTests.Identity.Permissions;

/// <summary>DEC-095: permissions and grants come from code; default roles only receive keys new to the database.</summary>
public class PermissionSyncTests : IntegrationTestBase
{
    private static readonly string[] DefaultRoles =
        ["Store Manager", "Sales Consultant", "Technical Support", "Cashier", "Warehouse Keeper", "Purchasing", "Accountant"];

    private readonly InMemoryLoggerProvider _logs = new();

    [Test]
    public async Task R_SEC_03_sync_adds_catalog_permissions_and_removes_others()
    {
        await SeedRolesAsync();
        var cashier = await RoleIdAsync("Cashier");
        await TestRoles.GrantAsync(cashier, "legacy.remove");

        (await SyncAsync(Catalog(["order.view", "order.confirm"]))).ShouldBe(PermissionSyncResult.Synchronized);

        (await PermissionKeysAsync()).ShouldBe(["order.confirm", "order.view"], ignoreOrder: true);
        (await GrantsAsync("Cashier")).ShouldBeEmpty();
        var warning = _logs.Entries.Where(e => e.Level == LogLevel.Warning).ShouldHaveSingleItem();
        warning.Message.ShouldContain("legacy.remove");
        warning.Message.ShouldContain("Cashier");
    }

    [Test]
    public async Task R_SEC_03_sync_sets_system_role_grants_to_the_catalog()
    {
        await SeedRolesAsync();
        await TestRoles.GrantAsync(await RoleIdAsync("Customer"), "order.confirm"); // not in the Customer list

        await SyncAsync(Catalog(["order.view", "order.confirm"], customer: ["order.view"]));

        (await GrantsAsync("Admin")).ShouldBe(["order.confirm", "order.view"], ignoreOrder: true);
        (await GrantsAsync("Customer")).ShouldBe(["order.view"]);
    }

    [Test]
    public async Task R_SEC_03_sync_grants_new_key_to_default_roles()
    {
        await SeedRolesAsync();
        await SyncAsync(Catalog(["order.view"], cashier: ["order.view"]));

        await SyncAsync(Catalog(["order.view", "order.confirm"], cashier: ["order.view", "order.confirm"]));

        (await GrantsAsync("Cashier")).ShouldBe(["order.confirm", "order.view"], ignoreOrder: true);
        (await GrantsAsync("Accountant")).ShouldBeEmpty();
    }

    [Test]
    public async Task R_SEC_03_sync_keeps_admin_edits_on_default_roles()
    {
        await SeedRolesAsync();
        await SyncAsync(Catalog(["order.view", "order.confirm"], cashier: ["order.view"]));
        var cashier = await RoleIdAsync("Cashier");
        // The Admin revokes a default key and adds another one (AUT-05).
        await Factory.WithDbAsync(async db =>
        {
            var role = await db.Roles.Include(r => r.RolePermissions).SingleAsync(r => r.Id == cashier);
            var confirm = await db.Permissions.SingleAsync(p => p.Key == "order.confirm");
            role.SetPermissions([confirm.Id]);
            await db.SaveChangesAsync();
        });

        await SyncAsync(Catalog(["order.view", "order.confirm"], cashier: ["order.view"]));

        (await GrantsAsync("Cashier")).ShouldBe(["order.confirm"]);
    }

    [Test]
    public async Task R_SEC_03_sync_does_not_grant_existing_key_newly_listed_for_default_role()
    {
        await SeedRolesAsync();
        await SyncAsync(Catalog(["order.view"]));

        await SyncAsync(Catalog(["order.view"], cashier: ["order.view"]));

        (await GrantsAsync("Cashier")).ShouldBeEmpty();
    }

    [Test]
    public async Task R_SEC_03_sync_keeps_custom_role_grants()
    {
        await SeedRolesAsync();
        await SyncAsync(Catalog(["order.view", "order.confirm"]));
        var custom = await TestRoles.InsertAsync("Night Shift");
        await TestRoles.GrantAsync(custom, "order.view");

        await SyncAsync(Catalog(["order.view", "order.confirm"]));

        (await GrantsAsync("Night Shift")).ShouldBe(["order.view"]);
    }

    [Test]
    public async Task R_SEC_03_sync_does_nothing_until_all_catalog_roles_exist()
    {
        await TestRoles.InsertAsync("Admin", isSystemRole: true);
        await TestRoles.InsertAsync("Customer", scopingType: "SELF", isSystemRole: true);
        var catalog = Catalog(["order.view"], cashier: ["order.view"]);

        (await SyncAsync(catalog)).ShouldBe(PermissionSyncResult.RolesMissing);

        (await PermissionKeysAsync()).ShouldBeEmpty();
        (await GrantsAsync("Admin")).ShouldBeEmpty();
        var error = _logs.Entries.Where(e => e.Level == LogLevel.Error).ShouldHaveSingleItem();
        error.Message.ShouldContain("api/seed/01_roles.sql");

        // After the seed, the next sync still sees the keys as new and grants the defaults.
        foreach (var name in DefaultRoles)
        {
            await TestRoles.InsertAsync(name);
        }

        (await SyncAsync(catalog)).ShouldBe(PermissionSyncResult.Synchronized);
        (await GrantsAsync("Cashier")).ShouldBe(["order.view"]);
        (await GrantsAsync("Admin")).ShouldBe(["order.view"]);
    }

    [Test]
    public async Task R_SEC_03_sync_warns_when_is_system_role_differs_from_the_catalog()
    {
        await TestRoles.InsertAsync("Admin", isSystemRole: true);
        await TestRoles.InsertAsync("Customer", scopingType: "SELF", isSystemRole: true);
        foreach (var name in DefaultRoles)
        {
            await TestRoles.InsertAsync(name, isSystemRole: name == "Cashier"); // seeded before DEC-095
        }

        await SyncAsync(Catalog([]));

        _logs.Entries.Where(e => e.Level == LogLevel.Warning).ShouldHaveSingleItem().Message.ShouldContain("Cashier");
    }

    [Test]
    public async Task R_SEC_07_sync_bumps_changed_roles()
    {
        await SeedRolesAsync();
        var custom = await TestRoles.InsertAsync("Night Shift");
        await TestRoles.GrantAsync(custom, "legacy.remove");
        var admin = await RoleIdAsync("Admin");
        var accountant = await RoleIdAsync("Accountant");

        await SyncAsync(Catalog(["order.view"]));

        (await ApiScope.VersionAsync(RedisAccessCache.RoleVersionKey(admin))).ShouldBe(1);  // granted order.view
        (await ApiScope.VersionAsync(RedisAccessCache.RoleVersionKey(custom))).ShouldBe(1); // lost legacy.remove by cascade
        (await ApiScope.VersionAsync(RedisAccessCache.RoleVersionKey(accountant))).ShouldBe(0);
    }

    [Test]
    public async Task R_SEC_12_sync_grants_default_managed_roles_when_none_exist()
    {
        await SeedRolesAsync();

        await SyncAsync(Catalog([], managerManages: ["Cashier", "Store Manager"]));

        (await ManagedAsync("Store Manager")).ShouldBe(["Cashier", "Store Manager"], ignoreOrder: true);
        (await ManagedAsync("Cashier")).ShouldBeEmpty();
        (await ManagedAsync("Admin")).ShouldBeEmpty();
    }

    [Test]
    public async Task R_SEC_12_sync_keeps_admin_edits_of_managed_roles()
    {
        await SeedRolesAsync();
        var catalog = Catalog([], managerManages: ["Cashier", "Accountant"]);
        await SyncAsync(catalog);
        var manager = await RoleIdAsync("Store Manager");
        var accountant = await RoleIdAsync("Accountant");
        // The Admin takes Cashier out of the Store Manager's set (AUT-05).
        await Factory.WithDbAsync(async db =>
        {
            var role = await db.Roles.Include(r => r.RolePermissions).Include(r => r.ManageableRoles)
                .SingleAsync(r => r.Id == manager);
            role.Update(role.Name, role.MaxDiscountPercent, role.RolePermissions.Select(rp => rp.PermissionId), [accountant],
                Guid.CreateVersion7(), DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        });

        await SyncAsync(catalog);

        (await ManagedAsync("Store Manager")).ShouldBe(["Accountant"]);
    }

    [Test]
    public async Task R_SEC_12_sync_adds_no_pair_once_any_exists()
    {
        await SeedRolesAsync();
        await SyncAsync(Catalog([], managerManages: ["Cashier"]));

        // A pair listed later applies to a running database only through a data migration (DEC-095, DEC-128).
        await SyncAsync(Catalog([], managerManages: ["Cashier", "Accountant"]));

        (await ManagedAsync("Store Manager")).ShouldBe(["Cashier"]);
    }

    private static TestCatalog Catalog(string[] keys, string[]? customer = null, string[]? cashier = null,
        string[]? managerManages = null) =>
        new(
            [.. keys.Select(k => new PermissionDefinition(k, $"Label of {k}"))],
            [
                new RoleGrants("Admin", true, keys),
                new RoleGrants("Customer", true, customer ?? []),
                .. DefaultRoles.Select(name => new RoleGrants(name, false, name == "Cashier" ? cashier ?? [] : [],
                    name == "Store Manager" ? managerManages ?? [] : [])),
            ]);

    private async Task<PermissionSyncResult> SyncAsync(IPermissionCatalog catalog)
    {
        _logs.Clear();
        await using var scope = Factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(_logs));
        var sync = new PermissionSyncService(
            services.GetRequiredService<ZenGearDbContext>(),
            catalog,
            services.GetRequiredService<IAccessCache>(),
            services.GetRequiredService<IClock>(),
            loggerFactory.CreateLogger<PermissionSyncService>());
        return await sync.SyncAsync(CancellationToken.None);
    }

    private static async Task SeedRolesAsync()
    {
        await TestRoles.InsertAsync("Admin", isSystemRole: true);
        await TestRoles.InsertAsync("Customer", scopingType: "SELF", isSystemRole: true);
        foreach (var name in DefaultRoles)
        {
            await TestRoles.InsertAsync(name);
        }
    }

    private async Task<Guid> RoleIdAsync(string name)
    {
        var id = Guid.Empty;
        await Factory.WithDbAsync(async db => id = await db.Roles.Where(r => r.Name == name).Select(r => r.Id).SingleAsync());
        return id;
    }

    private async Task<List<string>> PermissionKeysAsync()
    {
        List<string> keys = [];
        await Factory.WithDbAsync(async db => keys = await db.Permissions.Select(p => p.Key).ToListAsync());
        return keys;
    }

    private async Task<List<string>> GrantsAsync(string roleName)
    {
        List<string> keys = [];
        await Factory.WithDbAsync(async db => keys = await db.RolePermissions
            .Join(db.Roles, rp => rp.RoleId, r => r.Id, (rp, r) => new { rp.PermissionId, r.Name })
            .Where(x => x.Name == roleName)
            .Join(db.Permissions, x => x.PermissionId, p => p.Id, (_, p) => p.Key)
            .ToListAsync());
        return keys;
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

    private sealed record TestCatalog(IReadOnlyList<PermissionDefinition> Permissions, IReadOnlyList<RoleGrants> Roles)
        : IPermissionCatalog;
}
