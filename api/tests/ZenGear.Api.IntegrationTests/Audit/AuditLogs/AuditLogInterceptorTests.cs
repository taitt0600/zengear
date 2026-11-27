using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shouldly;
using ZenGear.Api.IntegrationTests.Infrastructure;
using ZenGear.Data.Entities.Catalog;
using ZenGear.Services.Identity.Permissions;

namespace ZenGear.Api.IntegrationTests.Audit.AuditLogs;

/// <summary>The audit_logs rows of the EF Core interceptor (R-AUD-01, DEC-086) against PostgreSQL.</summary>
public class AuditLogInterceptorTests : IntegrationTestBase
{
    private const string ManagerUid = "uid-audit-manager";

    private const string TraceId = "0af7651916cd43dd8448eb211c80319c";

    private const long XidRange = 1L << 32;

    private static readonly DateTimeOffset Now = new(2027, 3, 1, 2, 0, 0, TimeSpan.Zero);

    private Guid _managerId;

    [SetUp]
    public async Task SeedAsync()
    {
        Factory.Time.AdjustTime(Now);
        var role = await TestRoles.InsertAsync("Store Manager");
        await TestRoles.GrantAsync(role, PermissionKeys.BrandCreate);
        _managerId = await TestUsers.InsertAsync(ManagerUid);
        await TestRoles.AssignAsync(_managerId, role);
    }

    [Test]
    public async Task R_AUD_01_a_save_outside_a_transaction_writes_its_rows_in_the_transaction_of_its_changes()
    {
        var brandId = Guid.Empty;
        await Factory.WithDbAsync(async db =>
        {
            var brand = Brand.Create("Asus", "asus", _managerId, Now);
            db.Brands.Add(brand);
            var user = await db.Users.SingleAsync(u => u.Id == _managerId);
            db.Entry(user).Property(u => u.Name).CurrentValue = "Nguyễn Văn B";

            await db.SaveChangesAsync();

            db.Database.CurrentTransaction.ShouldBeNull();
            db.Database.AutoSavepointsEnabled.ShouldBeTrue();
            brandId = brand.Id;
        });

        var rows = await RowsAsync();
        rows.Count.ShouldBe(2);
        var insert = rows.Single(r => r.TableName == "brands");
        insert.Action.ShouldBe("INSERT");
        Json(insert.RecordKey).ShouldBe(new JsonObject { ["id"] = brandId }.ToJsonString());
        insert.OldValues.ShouldBeNull();
        var inserted = JsonNode.Parse(insert.NewValues!)!.AsObject();
        inserted["id"]!.GetValue<Guid>().ShouldBe(brandId);
        inserted["slug"]!.GetValue<string>().ShouldBe("asus");
        inserted["is_active"]!.GetValue<bool>().ShouldBeTrue();
        inserted.ContainsKey("xmin").ShouldBeFalse();

        var update = rows.Single(r => r.TableName == "users");
        update.Action.ShouldBe("UPDATE");
        Json(update.RecordKey).ShouldBe(new JsonObject { ["id"] = _managerId }.ToJsonString());
        Json(update.OldValues).ShouldBe(Json("""{"name":"Nguyễn Văn A"}"""));
        Json(update.NewValues).ShouldBe(Json("""{"name":"Nguyễn Văn B"}"""));

        // The transaction of the brand itself: rows written after an autocommitted save would share another one.
        rows.Select(r => r.TransactionId).Distinct().ShouldHaveSingleItem();
        (rows[0].TransactionId % XidRange).ShouldBe(await BrandXminAsync(brandId));
        rows.ShouldAllBe(r => r.ChangedBy == null && r.CorrelationId == null);
    }

    [Test]
    public async Task R_AUD_01_a_synchronous_save_writes_its_rows_in_the_transaction_of_its_changes()
    {
        var brandId = Guid.Empty;
        await Factory.WithDbAsync(db =>
        {
            var brand = Brand.Create("Asus", "asus", _managerId, Now);
            db.Brands.Add(brand);
            db.SaveChanges();
            db.Database.CurrentTransaction.ShouldBeNull();
            brandId = brand.Id;
            return Task.CompletedTask;
        });

        var row = (await RowsAsync()).ShouldHaveSingleItem();
        row.Action.ShouldBe("INSERT");
        (row.TransactionId % XidRange).ShouldBe(await BrandXminAsync(brandId));
    }

    [Test]
    public async Task R_AUD_01_the_saves_of_one_transaction_share_its_transaction_id()
    {
        await Factory.WithDbAsync(async db =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            db.Brands.Add(Brand.Create("Asus", "asus", _managerId, Now));
            await db.SaveChangesAsync();
            db.Brands.Add(Brand.Create("MSI", "msi", _managerId, Now));
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        });
        await Factory.WithDbAsync(async db =>
        {
            db.Brands.Add(Brand.Create("Gigabyte", "gigabyte", _managerId, Now));
            await db.SaveChangesAsync();
        });

        var rows = await RowsAsync();
        rows.Count.ShouldBe(3);
        rows[0].TransactionId.ShouldBe(rows[1].TransactionId);
        rows[2].TransactionId.ShouldNotBe(rows[0].TransactionId);
    }

    [Test]
    public async Task R_AUD_01_a_rolled_back_transaction_leaves_no_row()
    {
        await Factory.WithDbAsync(async db =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            db.Brands.Add(Brand.Create("Asus", "asus", _managerId, Now));
            await db.SaveChangesAsync();
            await transaction.RollbackAsync();
        });

        (await RowsAsync()).ShouldBeEmpty();
    }

    [Test]
    public async Task R_AUD_01_a_failed_save_leaves_no_row_and_no_open_transaction()
    {
        await Factory.WithDbAsync(async db =>
        {
            db.Brands.Add(Brand.Create("Asus", "asus", _managerId, Now));
            await db.SaveChangesAsync();
        });

        await Factory.WithDbAsync(async db =>
        {
            db.Brands.Add(Brand.Create("ASUS", "asus", _managerId, Now));
            await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync());
            db.Database.CurrentTransaction.ShouldBeNull();

            db.ChangeTracker.Clear();
            db.Brands.Add(Brand.Create("MSI", "msi", _managerId, Now));
            await db.SaveChangesAsync();
        });

        var rows = await RowsAsync();
        rows.Select(r => JsonNode.Parse(r.NewValues!)!["slug"]!.GetValue<string>()).ShouldBe(["asus", "msi"]);
    }

    [Test]
    public async Task R_AUD_01_a_commit_refused_by_a_deferred_trigger_keeps_its_error_and_leaves_no_row()
    {
        await Factory.WithDbAsync(async db =>
        {
            // The manager's only role: R-SEC-01 refuses its removal at COMMIT, the interceptor's COMMIT here.
            db.UserRoles.Remove(await db.UserRoles.SingleAsync(ur => ur.UserId == _managerId));
            var error = await Should.ThrowAsync<PostgresException>(() => db.SaveChangesAsync());
            error.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
            db.Database.CurrentTransaction.ShouldBeNull();
            db.Database.AutoSavepointsEnabled.ShouldBeTrue();

            db.ChangeTracker.Clear();
            db.Brands.Add(Brand.Create("Asus", "asus", _managerId, Now));
            await db.SaveChangesAsync();
            (await db.UserRoles.CountAsync(ur => ur.UserId == _managerId)).ShouldBe(1);
        });

        (await RowsAsync()).ShouldHaveSingleItem().TableName.ShouldBe("brands");
    }

    [Test]
    public async Task R_AUD_01_ExecuteUpdate_and_ExecuteDelete_are_not_audited()
    {
        await Factory.WithDbAsync(async db =>
        {
            db.Brands.Add(Brand.Create("Asus", "asus", _managerId, Now));
            await db.SaveChangesAsync();
        });

        await Factory.WithDbAsync(async db =>
        {
            await db.Brands.Where(b => b.Slug == "asus").ExecuteUpdateAsync(s => s.SetProperty(b => b.Name, "ASUS"));
            await db.Brands.Where(b => b.Slug == "asus").ExecuteDeleteAsync();
        });

        (await RowsAsync()).ShouldHaveSingleItem().Action.ShouldBe("INSERT");
    }

    [Test]
    public async Task R_AUD_01_an_update_of_only_users_last_login_at_is_not_audited()
    {
        await Factory.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Id == _managerId);
            db.Entry(user).Property(u => u.LastLoginAt).CurrentValue = Now;
            await db.SaveChangesAsync();
        });
        (await RowsAsync()).ShouldBeEmpty();

        await Factory.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Id == _managerId);
            db.Entry(user).Property(u => u.LastLoginAt).CurrentValue = Now.AddHours(1);
            db.Entry(user).Property(u => u.Name).CurrentValue = "Nguyễn Văn B";
            await db.SaveChangesAsync();
        });

        var row = (await RowsAsync()).ShouldHaveSingleItem();
        JsonNode.Parse(row.NewValues!)!.AsObject().Select(pair => pair.Key).Order().ShouldBe(["last_login_at", "name"]);
    }

    [Test]
    public async Task R_AUD_01_changed_by_and_correlation_id_are_those_of_the_request()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/v1/admin/brands", UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.EmulatorToken(ManagerUid));
        request.Headers.Add("traceparent", $"00-{TraceId}-b7ad6b7169203331-01");
        request.Content = JsonContent.Create(new { name = "Asus" });

        (await Client.SendAsync(request)).StatusCode.ShouldBe(HttpStatusCode.Created);

        var row = (await RowsAsync()).Where(r => r.TableName == "brands").ShouldHaveSingleItem();
        row.ChangedBy.ShouldBe(_managerId);
        row.CorrelationId.ShouldBe(Guid.ParseExact(TraceId, "N"));
    }

    [Test]
    public async Task R_AUD_01_fn_audit_erase_values_erases_the_values_of_one_record_only()
    {
        var asusId = Guid.Empty;
        await Factory.WithDbAsync(async db =>
        {
            var asus = Brand.Create("Asus", "asus", _managerId, Now);
            db.Brands.AddRange(asus, Brand.Create("MSI", "msi", _managerId, Now));
            await db.SaveChangesAsync();
            asus.Update("ASUS", true, _managerId, Now.AddHours(1));
            await db.SaveChangesAsync();
            asusId = asus.Id;
        });

        await using var connection = new NpgsqlConnection(PostgresContainer.ConnectionString);
        await connection.OpenAsync();
        await using var erase = new NpgsqlCommand(
            "SELECT fn_audit_erase_values('brands', jsonb_build_object('id', @id::text))", connection);
        erase.Parameters.AddWithValue("id", asusId);
        (await erase.ExecuteScalarAsync()).ShouldBe(2);

        var rows = await RowsAsync();
        var erased = rows.Where(r => r.RecordKey.Contains(asusId.ToString(), StringComparison.Ordinal)).ToList();
        erased.Count.ShouldBe(2);
        erased.ShouldAllBe(r => AllNull(r.OldValues) && AllNull(r.NewValues));
        JsonNode.Parse(rows.Single(r => !erased.Contains(r)).NewValues!)!["slug"]!.GetValue<string>().ShouldBe("msi");
    }

    /// <summary>The xid that wrote the row: <c>transaction_id</c> is its 64-bit form (epoch × 2³² + xid).</summary>
    private static async Task<long> BrandXminAsync(Guid brandId)
    {
        await using var connection = new NpgsqlConnection(PostgresContainer.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT xmin::text::bigint FROM brands WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", brandId);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static bool AllNull(string? values) =>
        values is null || JsonNode.Parse(values)!.AsObject().All(pair => pair.Value is null);

    /// <summary>The JSON as PostgreSQL stores it, so two texts of one object compare equal.</summary>
    private static string Json(string? value) => JsonNode.Parse(value.ShouldNotBeNull())!.ToJsonString();

    private static async Task<List<AuditLogRow>> RowsAsync()
    {
        await using var connection = new NpgsqlConnection(PostgresContainer.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT table_name, record_key::text, action, old_values::text, new_values::text, transaction_id, correlation_id, changed_by
            FROM audit_logs ORDER BY changed_at, id
            """,
            connection);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<AuditLogRow>();
        while (await reader.ReadAsync())
        {
            rows.Add(new AuditLogRow(reader.GetString(0), reader.GetString(1), reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetInt64(5), reader.IsDBNull(6) ? null : reader.GetGuid(6), reader.IsDBNull(7) ? null : reader.GetGuid(7)));
        }

        return rows;
    }

    private sealed record AuditLogRow(string TableName, string RecordKey, string Action, string? OldValues, string? NewValues,
        long TransactionId, Guid? CorrelationId, Guid? ChangedBy);
}
