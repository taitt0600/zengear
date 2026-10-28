using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using Npgsql;
using Shouldly;
using ZenGear.Api.ExceptionHandling;
using ZenGear.Api.Extensions;
using ZenGear.Api.Hosting;
using ZenGear.Api.IntegrationTests.Infrastructure;

namespace ZenGear.Api.IntegrationTests.Hosting;

/// <summary>The bootstrap-admin command (DEC-096), run against the services of the test host.</summary>
public class BootstrapAdminCommandTests : IntegrationTestBase
{
    private const string Email = "owner@zengear.test";

    private FakeAuthProvider Auth => Factory.Auth;

    [SetUp]
    public void ResetFakes()
    {
        Auth.Reset();
        Factory.StaffLogs.Clear();
    }

    [Test]
    public async Task Creates_the_first_admin_and_prints_the_password_reset_link()
    {
        var adminRole = await TestRoles.InsertAsync("Admin", isSystemRole: true);

        var (exitCode, output) = await RunAsync("bootstrap-admin", "--email", " Owner@ZenGear.test ", "--name", " Chủ cửa hàng ");

        exitCode.ShouldBe(BootstrapAdminCommand.Succeeded);
        var account = Auth.Find(Email).ShouldNotBeNull();
        account.Password.ShouldBeNull();
        output.ShouldContain($"oobCode=code-{account.Uid}");
        var row = await AdminRowAsync();
        row.ShouldBe(new AdminRow(account.Uid, "Chủ cửa hàng", "ACTIVE", false, adminRole) { UserId = row.UserId });
        output.ShouldContain(row.UserId.ToString());
        Factory.StaffLogs.Entries.ShouldAllBe(e => !e.Message.Contains("oobCode") && !e.Message.Contains(Email));
        Factory.StaffLogs.Entries.ShouldContain(e => e.Message.Contains(row.UserId.ToString()));
    }

    [Test]
    public async Task Refuses_when_an_admin_exists_and_changes_nothing()
    {
        var adminRole = await TestRoles.InsertAsync("Admin", isSystemRole: true);
        var existing = await TestUsers.InsertAsync("uid-admin");
        await TestRoles.AssignAsync(existing, adminRole);

        var (exitCode, output) = await RunAsync("bootstrap-admin", "--email", Email, "--name", "Owner");

        exitCode.ShouldBe(BootstrapAdminCommand.Refused);
        output.ShouldContain("Đã có quản trị viên; không thay đổi gì. Quản trị viên tạo các tài khoản khác.");
        (await CountAsync("SELECT count(*) FROM users")).ShouldBe(1);
        Auth.Accounts.ShouldBeEmpty();
    }

    [Test]
    public async Task Links_an_existing_firebase_account()
    {
        await TestRoles.InsertAsync("Admin", isSystemRole: true);
        var existing = Auth.Add(Email);

        var (exitCode, _) = await RunAsync("bootstrap-admin", "--email", Email, "--name", "Owner");

        exitCode.ShouldBe(BootstrapAdminCommand.Succeeded);
        Auth.Accounts.ShouldBe([existing]);
        (await AdminRowAsync()).FirebaseUid.ShouldBe(existing.Uid);
    }

    [TestCase("bootstrap-admin", "--email", Email)]
    [TestCase("bootstrap-admin", "--name", "Owner")]
    [TestCase("bootstrap-admin", "--email", "--name", "Owner")]
    public async Task A_missing_argument_is_a_usage_error(params string[] args)
    {
        await TestRoles.InsertAsync("Admin", isSystemRole: true);

        var (exitCode, output) = await RunAsync(args);

        exitCode.ShouldBe(BootstrapAdminCommand.UsageError);
        output.ShouldContain("Usage:");
        Auth.Accounts.ShouldBeEmpty();
    }

    [TestCase("owner@localhost", "Owner", "Email không hợp lệ.")]
    [TestCase(Email, "   ", "Vui lòng nhập tên.")]
    public async Task An_invalid_email_or_name_is_a_usage_error_before_any_change(string email, string name, string text)
    {
        await TestRoles.InsertAsync("Admin", isSystemRole: true);

        var (exitCode, output) = await RunAsync("bootstrap-admin", "--email", email, "--name", name);

        exitCode.ShouldBe(BootstrapAdminCommand.UsageError);
        output.ShouldContain(text);
        Auth.Accounts.ShouldBeEmpty();
        (await CountAsync("SELECT count(*) FROM users")).ShouldBe(0);
    }

    [Test]
    public async Task Refuses_when_the_admin_role_is_missing()
    {
        var (exitCode, output) = await RunAsync("bootstrap-admin", "--email", Email, "--name", "Owner");

        exitCode.ShouldBe(BootstrapAdminCommand.Refused);
        output.ShouldContain("Chưa có vai trò Admin: hãy chạy api/db/zengear_08_roles.sql và api/seed trước.");
        Auth.Accounts.ShouldBeEmpty();
    }

    [Test]
    public async Task Keeps_the_admin_and_says_how_to_get_a_link_when_the_link_fails()
    {
        await TestRoles.InsertAsync("Admin", isSystemRole: true);
        Auth.FailPasswordResetLink = true;

        var (exitCode, output) = await RunAsync("bootstrap-admin", "--email", Email, "--name", "Owner");

        exitCode.ShouldBe(BootstrapAdminCommand.Refused);
        output.ShouldContain("Firebase console");
        (await AdminRowAsync()).Status.ShouldBe("ACTIVE");
        Factory.StaffLogs.Entries.ShouldAllBe(e => !e.Message.Contains(Email));
    }

    [Test]
    public async Task The_emulator_in_production_is_refused_before_any_change()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Firebase:ProjectId"] = ZenGearApiFactory.FirebaseProjectId,
                ["Firebase:UseEmulator"] = "true",
            })
            .Build();
        // The problem texts, as Program registers them: the command writes its refusals with them.
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddLogging()
            .AddZenGearLocalization()
            .AddSingleton<ProblemTexts>();
        services.AddFirebaseJwtBearer(new ProductionEnvironment());
        await using var provider = services.BuildServiceProvider();
        await using var output = new StringWriter();

        var exitCode = await BootstrapAdminCommand.RunAsync(provider,
            ["bootstrap-admin", "--email", Email, "--name", "Owner"], output, CancellationToken.None);

        exitCode.ShouldBe(BootstrapAdminCommand.Refused);
        output.ToString().ShouldContain("UseEmulator");
    }

    private async Task<(int ExitCode, string Output)> RunAsync(params string[] args)
    {
        await using var output = new StringWriter();
        var exitCode = await BootstrapAdminCommand.RunAsync(Factory.Services, args, output, CancellationToken.None);
        return (exitCode, output.ToString());
    }

    private sealed record AdminRow(string FirebaseUid, string Name, string Status, bool MustChangePassword, Guid RoleId)
    {
        public Guid UserId { get; init; }
    }

    private static async Task<AdminRow> AdminRowAsync()
    {
        await using var connection = await TestRoles.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT u.id, u.firebase_uid, u.name, u.status, s.must_change_password, ur.role_id
            FROM users u JOIN staff_profiles s ON s.user_id = u.id JOIN user_roles ur ON ur.user_id = u.id
            WHERE u.email = @email
            """,
            connection);
        command.Parameters.AddWithValue("email", Email);
        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).ShouldBeTrue();
        var row = new AdminRow(reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetBoolean(4), reader.GetGuid(5))
        {
            UserId = reader.GetGuid(0),
        };
        (await reader.ReadAsync()).ShouldBeFalse("exactly one role");
        return row;
    }

    private static async Task<long> CountAsync(string sql)
    {
        await using var connection = await TestRoles.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private sealed class ProductionEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = "ZenGear.Api";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
