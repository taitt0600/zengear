using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shouldly;
using ZenGear.Api.IntegrationTests.Infrastructure;
using ZenGear.Data.Entities.Identity;
using ZenGear.Services.Identity.Permissions;

namespace ZenGear.Api.IntegrationTests.Identity.Roles;

/// <summary>
/// The unique index <c>ix_roles_name</c> on <c>lower(name)</c> (R-SEC-14, DEC-129 (3)): a parallel request that takes the
/// name in another case after the check of the code is refused by the database with the same 409.
/// </summary>
public class RoleNameIndexTests : IntegrationTestBase
{
    private const string Path = "/api/v1/admin/roles";
    private const string AdminUid = "uid-admin";

    private readonly RivalNameInterceptor _rival = new();

    protected override ZenGearApiFactory CreateFactory() => new RivalFactory(_rival);

    [SetUp]
    public async Task SeedAsync()
    {
        var admin = await TestRoles.InsertAsync("Admin", isSystemRole: true);
        await TestRoles.GrantAsync(admin, PermissionKeys.RoleCreate);
        await TestRoles.GrantAsync(admin, PermissionKeys.RoleUpdate);
        await TestRoles.AssignAsync(await TestUsers.InsertAsync(AdminUid), admin);
    }

    [Test]
    public async Task R_SEC_14_a_name_taken_in_another_case_while_creating_is_409()
    {
        // "kho" is committed by another request between the check of the code and the save of "Kho".
        _rival.NameToInsert = "kho";

        var response = await SendAsync(HttpMethod.Post, Path, new { name = "Kho", permissionKeys = Array.Empty<string>() });

        await ShouldBeNameTakenAsync(response);
    }

    [Test]
    public async Task R_SEC_14_a_name_taken_in_another_case_while_renaming_is_409()
    {
        var role = await TestRoles.InsertAsync("Night Shift");
        _rival.NameToInsert = "kho";

        var response = await SendAsync(HttpMethod.Put, $"{Path}/{role}", new
        {
            name = "Kho", permissionKeys = Array.Empty<string>(), managedRoleIds = Array.Empty<Guid>(),
        });

        await ShouldBeNameTakenAsync(response);
        await using var connection = await TestRoles.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT name FROM roles WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", role);
        ((string)(await command.ExecuteScalarAsync())!).ShouldBe("Night Shift");
    }

    private static async Task ShouldBeNameTakenAsync(HttpResponseMessage response)
    {
        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/role-already-exists", null,
            ExpectedError.At("#/name", "ROLE_ALREADY_EXISTS"));
        await using var connection = await TestRoles.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT array_agg(name) FROM roles WHERE lower(name) = 'kho'", connection);
        ((string[])(await command.ExecuteScalarAsync())!).ShouldBe(["kho"]);
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object body)
    {
        var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative)) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.EmulatorToken(AdminUid));
        if (method == HttpMethod.Put)
        {
            request.Headers.TryAddWithoutValidation("If-Match", "*");
        }

        return Client.SendAsync(request);
    }

    private sealed class RivalFactory(RivalNameInterceptor rival) : ZenGearApiFactory
    {
        // The API's AddDbContext adds every IInterceptor of the container.
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services => services.AddSingleton<IInterceptor>(rival));
        }
    }

    /// <summary>Once armed, inserts a role of its own just before the first save that adds or changes a role.</summary>
    private sealed class RivalNameInterceptor : SaveChangesInterceptor
    {
        public string? NameToInsert { get; set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var name = NameToInsert;
            if (name is not null && eventData.Context!.ChangeTracker.Entries<Role>()
                    .Any(e => e.State is EntityState.Added or EntityState.Modified))
            {
                NameToInsert = null;
                await TestRoles.InsertAsync(name);
            }

            return result;
        }
    }
}
