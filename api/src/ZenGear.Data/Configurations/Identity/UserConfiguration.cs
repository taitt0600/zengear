using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZenGear.Data.Entities.Identity;
using ZenGear.Data.Entities.Identity.Enums;

namespace ZenGear.Data.Configurations.Identity;

/// <summary><c>users</c> as in api/db/zengear_02_schema_phase1_store.sql.</summary>
internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", t =>
        {
            t.HasCheckConstraint("ck_users_email", "email = lower(email)");
            t.HasCheckConstraint("ck_users_email_phone", "email is not null or phone is not null");
            t.HasCheckConstraint("ck_users_phone", @"phone is null or phone ~ '^\+[1-9][0-9]{7,14}$'");
            t.HasCheckConstraint("ck_users_phone_verified_at", "phone_verified_at is null or phone is not null");
        });

        builder.HasKey(u => u.Id).HasName("pk_users");
        builder.Property(u => u.Id).HasDefaultValueSql("uuidv7()");

        builder.Property(u => u.FirebaseUid).HasMaxLength(128);
        builder.Property(u => u.Name).HasColumnType("varchar");
        builder.Property(u => u.Email).HasMaxLength(255);
        builder.Property(u => u.Phone).HasMaxLength(20);
        builder.Property(u => u.Status).HasMaxLength(20).HasDefaultValue(UserStatus.Active);
        builder.Property(u => u.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(u => u.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(u => u.FirebaseUid).IsUnique().HasDatabaseName("ix_users_firebase_uid");
        builder.HasIndex(u => u.Email).IsUnique().HasDatabaseName("ix_users_email");
        builder.HasIndex(u => u.Phone).IsUnique().HasDatabaseName("ix_users_phone");

        builder.HasOne(u => u.StaffProfile)
            .WithOne()
            .HasForeignKey<StaffProfile>(s => s.UserId)
            .HasConstraintName("fk_staff_profiles_users_user_id")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
