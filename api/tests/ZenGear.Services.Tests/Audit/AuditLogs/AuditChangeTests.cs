using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using ZenGear.Data.Common;
using ZenGear.Data.Entities.Audit.Enums;
using ZenGear.Data.Entities.Identity.Enums;
using ZenGear.Services.Audit.AuditLogs;

namespace ZenGear.Services.Tests.Audit.AuditLogs;

/// <summary>The changes R-AUD-01 audits, read from a change tracker without a database.</summary>
public class AuditChangeTests
{
    private static readonly Guid RowId = Guid.Parse("01900000-0000-7000-8000-000000000001");

    private static readonly DateTimeOffset Then = new(2027, 2, 5, 3, 0, 0, TimeSpan.Zero);

    [Test]
    public void R_AUD_01_an_insert_writes_the_whole_row_but_xmin()
    {
        using var db = new TestDbContext();
        db.Add(NewRow());

        var row = SingleRow(db);

        row.TableName.ShouldBe("test_rows");
        row.Action.ShouldBe(AuditAction.Insert);
        row.OldValues.ShouldBeNull();
        ShouldBeJson(row.RecordKey, new JsonObject { ["id"] = RowId });
        var values = Json(row.NewValues);
        values.Select(pair => pair.Key).Order().ShouldBe(["id", "name", "note", "payload", "status", "updated_at"]);
        values["name"]!.GetValue<string>().ShouldBe("Asus");
        values["note"].ShouldBeNull();
        values["status"]!.GetValue<string>().ShouldBe("ACTIVE");
        values["payload"]!.ToJsonString().ShouldBe("""{"days":[1,2]}""");
        values["updated_at"]!.GetValue<DateTimeOffset>().ShouldBe(Then);
    }

    [Test]
    public void R_AUD_01_an_update_writes_only_the_changed_columns()
    {
        using var db = new TestDbContext();
        var entity = Attached(db, NewRow());
        entity.Name = "ASUS";
        entity.Status = UserStatus.Disabled;

        var row = SingleRow(db);

        row.Action.ShouldBe(AuditAction.Update);
        ShouldBeJson(row.RecordKey, new JsonObject { ["id"] = RowId });
        ShouldBeJson(row.OldValues, new JsonObject { ["name"] = "Asus", ["status"] = "ACTIVE" });
        ShouldBeJson(row.NewValues, new JsonObject { ["name"] = "ASUS", ["status"] = "DISABLED" });
    }

    [Test]
    public void R_AUD_01_an_update_that_changes_no_value_writes_no_row()
    {
        using var db = new TestDbContext();
        var entity = Attached(db, NewRow());
        db.Update(entity);

        db.Entry(entity).State.ShouldBe(EntityState.Modified);
        AuditChange.Collect(db.ChangeTracker).ShouldBeEmpty();
    }

    [Test]
    public void R_AUD_01_an_update_of_only_users_last_login_at_writes_no_row()
    {
        using var db = new TestDbContext();
        var user = Attached(db, new TestUser { Id = RowId, Name = "Nguyễn Văn A" });
        user.LastLoginAt = Then;

        AuditChange.Collect(db.ChangeTracker).ShouldBeEmpty();
    }

    [Test]
    public void R_AUD_01_an_update_of_users_last_login_at_with_another_column_writes_both()
    {
        using var db = new TestDbContext();
        var user = Attached(db, new TestUser { Id = RowId, Name = "Nguyễn Văn A" });
        user.LastLoginAt = Then;
        user.Name = "Nguyễn Văn B";

        var row = SingleRow(db);

        ShouldBeJson(row.OldValues, new JsonObject { ["last_login_at"] = null, ["name"] = "Nguyễn Văn A" });
        ShouldBeJson(row.NewValues, new JsonObject { ["last_login_at"] = Then, ["name"] = "Nguyễn Văn B" });
    }

    [Test]
    public void R_AUD_01_a_delete_writes_the_whole_row_before_it()
    {
        using var db = new TestDbContext();
        db.Remove(Attached(db, NewRow()));

        var row = SingleRow(db);

        row.Action.ShouldBe(AuditAction.Delete);
        row.NewValues.ShouldBeNull();
        ShouldBeJson(row.RecordKey, new JsonObject { ["id"] = RowId });
        Json(row.OldValues)["name"]!.GetValue<string>().ShouldBe("Asus");
        Json(row.OldValues).ContainsKey("xmin").ShouldBeFalse();
    }

    [Test]
    public void R_AUD_01_the_unaudited_tables_are_those_with_a_history_of_their_own()
    {
        AuditChange.UnauditedTables.Order().ShouldBe(
            ["audit_logs", "cart_items", "daily_sequences", "notifications", "orders", "status_history", "stock_balances"]);
    }

    [Test]
    public void R_AUD_01_a_change_to_an_unaudited_table_writes_no_row()
    {
        using var db = new TestDbContext();
        db.Add(new TestOrder { Id = RowId, Code = "DH-0001" });
        db.Remove(Attached(db, new TestOrder { Id = Guid.CreateVersion7(), Code = "DH-0002" }));
        Attached(db, new TestOrder { Id = Guid.CreateVersion7(), Code = "DH-0003" }).Code = "DH-0004";

        AuditChange.Collect(db.ChangeTracker).ShouldBeEmpty();
    }

    private static TestRow NewRow() => new()
    {
        Id = RowId,
        Name = "Asus",
        Status = UserStatus.Active,
        Payload = """{"days": [1, 2]}""",
        UpdatedAt = Then,
        Version = 42,
    };

    private static T Attached<T>(TestDbContext db, T entity) where T : class
    {
        db.Attach(entity);
        return entity;
    }

    private static AuditRow SingleRow(TestDbContext db) => AuditChange.Collect(db.ChangeTracker).ShouldHaveSingleItem().ToRow();

    private static void ShouldBeJson(string? actual, JsonObject expected) =>
        JsonNode.DeepEquals(Json(actual), expected).ShouldBeTrue($"{actual} is not {expected.ToJsonString()}");

    private static JsonObject Json(string? value) => JsonNode.Parse(value.ShouldNotBeNull())!.AsObject();

    // Internal, not private: EF reads and writes these properties, which Sonar cannot see (S1144).
    internal sealed class TestRow
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = "";

        public string? Note { get; set; }

        public UserStatus Status { get; set; }

        public string Payload { get; set; } = "";

        public DateTimeOffset UpdatedAt { get; set; }

        public uint Version { get; set; }
    }

    internal sealed class TestUser
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = "";

        public DateTimeOffset? LastLoginAt { get; set; }
    }

    internal sealed class TestOrder
    {
        public Guid Id { get; set; }

        public string Code { get; set; } = "";
    }

    private sealed class TestDbContext : DbContext
    {
        public TestDbContext()
            : base(new DbContextOptionsBuilder<TestDbContext>()
                .UseNpgsql("Host=unused")
                .UseSnakeCaseNamingConvention()
                .Options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestRow>(row =>
            {
                row.ToTable("test_rows");
                row.Property(r => r.Status).HasConversion(new AttributeEnumConverter<UserStatus>());
                row.Property(r => r.Payload).HasColumnType("jsonb");
                row.Property(r => r.Version).IsRowVersion().HasColumnName("xmin").HasColumnType("xid");
            });
            modelBuilder.Entity<TestUser>().ToTable("users");
            modelBuilder.Entity<TestOrder>().ToTable("orders");
        }
    }
}
