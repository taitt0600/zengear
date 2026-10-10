using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using ZenGear.Data;
using ZenGear.Data.Entities.Identity;
using ZenGear.Data.Entities.Identity.Enums;

namespace ZenGear.Services.Tests.Identity.StaffAccounts;

/// <summary>The model of users and staff_profiles uses the names of api/db/zengear_02_schema_phase1_store.sql.</summary>
public class UserModelTests
{
    private static IModel Model()
    {
        using var context = new ZenGearDbContext(new DbContextOptionsBuilder<ZenGearDbContext>()
            .UseNpgsql("Host=unused")
            .UseSnakeCaseNamingConvention()
            .Options);
        return context.GetService<IDesignTimeModel>().Model;
    }

    [Test]
    public void Users_table_keys_indexes_and_checks_have_the_specified_names()
    {
        var users = Model().FindEntityType(typeof(User))!;

        users.GetTableName().ShouldBe("users");
        users.FindPrimaryKey()!.GetName().ShouldBe("pk_users");
        users.GetIndexes().Select(i => i.GetDatabaseName()).ShouldBe(
            ["ix_users_email", "ix_users_firebase_uid", "ix_users_phone"], ignoreOrder: true);
        users.GetIndexes().ShouldAllBe(i => i.IsUnique);
        users.GetCheckConstraints().Select(c => c.Name).ShouldBe(
            ["ck_users_email", "ck_users_email_phone", "ck_users_phone", "ck_users_phone_verified_at"], ignoreOrder: true);
    }

    [Test]
    public void Users_status_is_varchar_20_stored_as_the_specified_values()
    {
        var status = Model().FindEntityType(typeof(User))!.FindProperty(nameof(User.Status))!;

        status.GetColumnType().ShouldBe("character varying(20)");
        status.GetTypeMapping().Converter!.ConvertToProvider(UserStatus.Active).ShouldBe("ACTIVE");
    }

    [Test]
    public void User_is_an_aggregate_root_with_xmin()
    {
        var version = Model().FindEntityType(typeof(User))!.FindProperty(nameof(User.Version))!;

        version.GetColumnName().ShouldBe("xmin");
        version.IsConcurrencyToken.ShouldBeTrue();
    }

    [Test]
    public void Staff_profiles_shares_the_user_key_and_cascades()
    {
        var staff = Model().FindEntityType(typeof(StaffProfile))!;

        staff.GetTableName().ShouldBe("staff_profiles");
        staff.FindPrimaryKey()!.GetName().ShouldBe("pk_staff_profiles");
        var fk = staff.GetForeignKeys().Single();
        fk.GetConstraintName().ShouldBe("fk_staff_profiles_users_user_id");
        fk.DeleteBehavior.ShouldBe(DeleteBehavior.Cascade);
        staff.GetIndexes().Single().GetDatabaseName().ShouldBe("ix_staff_profiles_employee_code");
        staff.GetCheckConstraints().Single().Name.ShouldBe("ck_staff_profiles_left_date");
    }
}
