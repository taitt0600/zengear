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

/// <summary>The bootstrap-admin command (DEC-096, its command line replaced by DEC-156), run against the services of the test host.</summary>
public class BootstrapAdminCommandTests : IntegrationTestBase
{
    /// <summary>The Admin of the sample cast (DEC-146 (2)): ZG260001 when it is the first account of 2026.</summary>
    private const string Name = "Trần Minh Khoa";

    private const string Email = "khoatmzg260001@zengear.test";

    private FakeAuthProvider Auth => Factory.Auth;

    [SetUp]
    public void ResetFakes()
    {
        Auth.Reset();
        Factory.StaffLogs.Clear();
    }

    [Test]
    public async Task R_USR_24_creates_the_first_admin_with_generated_identifiers_and_prints_the_link()
    {
        var adminRole = await TestRoles.InsertAsync("Admin", isSystemRole: true);

        var (exitCode, output) = await RunAsync("bootstrap-admin", "--name", " Trần Minh Khoa ", "--joined-date", "2026-01-05");

        exitCode.ShouldBe(BootstrapAdminCommand.Succeeded);
        var account = Auth.Find(Email).ShouldNotBeNull();
        account.Password.ShouldBeNull();
        output.ShouldContain($"oobCode=code-{account.Uid}");
        output.ShouldContain("employee code ZG260001");
        output.ShouldContain("Sign in with the email khoatmzg260001@zengear.test; the username KhoaTMZG260001");
        var row = await AdminRowAsync();
        row.ShouldBe(new AdminRow(account.Uid, Name, "ACTIVE", false, adminRole, "ZG260001", "KhoaTMZG260001",
            new DateOnly(2026, 1, 5)) { UserId = row.UserId });
        output.ShouldContain(row.UserId.ToString());
        Factory.StaffLogs.Entries.ShouldAllBe(e =>
            !e.Message.Contains("oobCode") && !e.Message.Contains(Email) && !e.Message.Contains("KhoaTM"));
        Factory.StaffLogs.Entries.ShouldContain(e => e.Message.Contains(row.UserId.ToString()));
    }

    [Test]
    public async Task R_USR_24_the_joined_date_defaults_to_today_in_vietnam()
    {
        await TestRoles.InsertAsync("Admin", isSystemRole: true);
        // 17:30 UTC on 31 December is already 1 January in Asia/Ho_Chi_Minh (UTC+7): a default taken in UTC fails.
        var before = Factory.Time.GetUtcNow();
        Factory.Time.AdjustTime(new DateTimeOffset(2026, 12, 31, 17, 30, 0, TimeSpan.Zero));
        try
        {
            var (exitCode, _) = await RunAsync("bootstrap-admin", "--name", Name);

            exitCode.ShouldBe(BootstrapAdminCommand.Succeeded);
            var row = await AdminRowAsync();
            row.JoinedDate.ShouldBe(new DateOnly(2027, 1, 1));
            row.EmployeeCode.ShouldBe("ZG270001");
        }
        finally
        {
            Factory.Time.AdjustTime(before);
        }
    }

    [Test]
    public async Task Refuses_when_an_admin_exists_and_changes_nothing()
    {
        var adminRole = await TestRoles.InsertAsync("Admin", isSystemRole: true);
        var existing = await TestUsers.InsertAsync("uid-admin");
        await TestRoles.AssignAsync(existing, adminRole);

        var (exitCode, output) = await RunAsync("bootstrap-admin", "--name", Name, "--joined-date", "2026-01-05");

        exitCode.ShouldBe(BootstrapAdminCommand.Refused);
        output.ShouldContain("Đã có quản trị viên; không thay đổi gì. Quản trị viên tạo các tài khoản khác.");
        (await CountAsync("SELECT count(*) FROM users")).ShouldBe(1);
        (await CountAsync("SELECT count(*) FROM employee_code_sequences")).ShouldBe(0);
        Auth.Accounts.ShouldBeEmpty();
    }

    [Test]
    public async Task Links_an_existing_firebase_account_of_the_generated_email()
    {
        await TestRoles.InsertAsync("Admin", isSystemRole: true);
        var existing = Auth.Add(Email);

        var (exitCode, _) = await RunAsync("bootstrap-admin", "--name", Name, "--joined-date", "2026-01-05");

        exitCode.ShouldBe(BootstrapAdminCommand.Succeeded);
        Auth.Accounts.ShouldBe([existing]);
        (await AdminRowAsync()).FirebaseUid.ShouldBe(existing.Uid);
    }

    [TestCase("bootstrap-admin")]
    [TestCase("bootstrap-admin", "--name")]
    [TestCase("bootstrap-admin", "--joined-date", "2026-01-05")]
    [TestCase("bootstrap-admin", "--name", Name, "--joined-date")]
    [TestCase("bootstrap-admin", "--name", Name, "--joined-date", "05/01/2026")]
    [TestCase("bootstrap-admin", "--name", Name, "--joined-date", "2026-02-30")]
    public async Task A_missing_or_invalid_argument_is_a_usage_error(params string[] args)
    {
        await TestRoles.InsertAsync("Admin", isSystemRole: true);

        var (exitCode, output) = await RunAsync(args);

        exitCode.ShouldBe(BootstrapAdminCommand.UsageError);
        output.ShouldContain("Usage:");
        Auth.Accounts.ShouldBeEmpty();
    }

    [TestCase("   ", "Vui lòng nhập tên.")]
    [TestCase("12345", "Họ tên cần có chữ cái để sinh tên đăng nhập.")]
    public async Task R_USR_24_an_invalid_name_is_a_usage_error_before_any_change(string name, string text)
    {
        await TestRoles.InsertAsync("Admin", isSystemRole: true);

        var (exitCode, output) = await RunAsync("bootstrap-admin", "--name", name, "--joined-date", "2026-01-05");

        exitCode.ShouldBe(BootstrapAdminCommand.UsageError);
        output.ShouldContain(text);
        Auth.Accounts.ShouldBeEmpty();
        (await CountAsync("SELECT count(*) FROM users")).ShouldBe(0);
        (await CountAsync("SELECT count(*) FROM employee_code_sequences")).ShouldBe(0);
    }

    [TestCase("1926-01-05")]
    [TestCase("2126-01-05")]
    public async Task R_USR_24_a_joined_year_outside_2000_to_2099_is_a_usage_error_before_any_change(string joinedDate)
    {
        await TestRoles.InsertAsync("Admin", isSystemRole: true);

        var (exitCode, output) = await RunAsync("bootstrap-admin", "--name", Name, "--joined-date", joinedDate);

        exitCode.ShouldBe(BootstrapAdminCommand.UsageError);
        output.ShouldContain("Năm vào làm phải từ 2000 đến 2099.");
        Auth.Accounts.ShouldBeEmpty();
        (await CountAsync("SELECT count(*) FROM users")).ShouldBe(0);
        (await CountAsync("SELECT count(*) FROM employee_code_sequences")).ShouldBe(0);
    }

    [Test]
    public async Task Refuses_when_the_admin_role_is_missing()
    {
        var (exitCode, output) = await RunAsync("bootstrap-admin", "--name", Name, "--joined-date", "2026-01-05");

        exitCode.ShouldBe(BootstrapAdminCommand.Refused);
        output.ShouldContain("Chưa có vai trò Admin: hãy chạy api/db/zengear_08_roles.sql và api/seed trước.");
        Auth.Accounts.ShouldBeEmpty();
    }

    [Test]
    public async Task Keeps_the_admin_and_says_how_to_get_a_link_when_the_link_fails()
    {
        await TestRoles.InsertAsync("Admin", isSystemRole: true);
        Auth.FailPasswordResetLink = true;

        var (exitCode, output) = await RunAsync("bootstrap-admin", "--name", Name, "--joined-date", "2026-01-05");

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
            ["bootstrap-admin", "--name", Name, "--joined-date", "2026-01-05"], output, CancellationToken.None);

        exitCode.ShouldBe(BootstrapAdminCommand.Refused);
        output.ToString().ShouldContain("UseEmulator");
    }

    private async Task<(int ExitCode, string Output)> RunAsync(params string[] args)
    {
        await using var output = new StringWriter();
        var exitCode = await BootstrapAdminCommand.RunAsync(Factory.Services, args, output, CancellationToken.None);
        return (exitCode, output.ToString());
    }

    private sealed record AdminRow(string FirebaseUid, string FullName, string Status, bool MustChangePassword, Guid RoleId,
        string EmployeeCode, string Username, DateOnly JoinedDate)
    {
        public Guid UserId { get; init; }
    }

    private static async Task<AdminRow> AdminRowAsync()
    {
        await using var connection = await TestRoles.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT u.id, u.firebase_uid, u.name, u.status, s.must_change_password, ur.role_id, s.employee_code, s.username,
                   s.joined_date
            FROM users u JOIN staff_profiles s ON s.user_id = u.id JOIN user_roles ur ON ur.user_id = u.id
            """,
            connection);
        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).ShouldBeTrue();
        var row = new AdminRow(reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetBoolean(4), reader.GetGuid(5),
            reader.GetString(6), reader.GetString(7), reader.GetFieldValue<DateOnly>(8))
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
