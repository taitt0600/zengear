using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using ZenGear.Data;
using ZenGear.Data.Entities.Common;

namespace ZenGear.Services.Tests.Data;

public class XminConventionTests
{
    [Test]
    public void Aggregate_root_Version_maps_to_xmin_as_concurrency_token()
    {
        using var context = new TestDbContext();
        var entityType = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(TestAggregate))!;

        var version = entityType.FindProperty(nameof(IAggregateRoot.Version))!;

        version.IsConcurrencyToken.ShouldBeTrue();
        version.ValueGenerated.ShouldBe(ValueGenerated.OnAddOrUpdate);
        version.GetColumnName().ShouldBe("xmin");
        version.GetColumnType().ShouldBe("xid");
    }

    [Test]
    public void Entity_that_is_not_an_aggregate_root_has_no_concurrency_token()
    {
        using var context = new TestDbContext();
        var entityType = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(TestChild))!;

        entityType.GetProperties().ShouldNotContain(p => p.IsConcurrencyToken);
        entityType.FindProperty(nameof(TestChild.SortOrder))!.GetColumnName().ShouldBe("sort_order");
    }

    // Internal, not private: EF reads and writes these properties, which Sonar cannot see (S1144).
    internal sealed class TestAggregate : IAggregateRoot
    {
        public Guid Id { get; set; }

        public uint Version { get; set; }
    }

    internal sealed class TestChild
    {
        public Guid Id { get; set; }

        public int SortOrder { get; set; }
    }

    private sealed class TestDbContext() : ZenGearDbContext(
        new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql("Host=unused")
            .UseSnakeCaseNamingConvention()
            .Options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestAggregate>();
            modelBuilder.Entity<TestChild>();
            base.OnModelCreating(modelBuilder);
        }
    }
}
