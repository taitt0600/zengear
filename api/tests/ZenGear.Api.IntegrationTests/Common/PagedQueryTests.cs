using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using ZenGear.Api.IntegrationTests.Infrastructure;
using ZenGear.Data;
using ZenGear.Data.Entities.Catalog;
using ZenGear.Services.Catalog.Brands;
using ZenGear.Services.Common;
using ZenGear.Services.Common.Models;

namespace ZenGear.Api.IntegrationTests.Common;

/// <summary>
/// <see cref="PagedQuery"/> against PostgreSQL, on brands: the commands it sends are recorded by an interceptor added to
/// the API's own <see cref="ZenGearDbContext"/>.
/// </summary>
public class PagedQueryTests : IntegrationTestBase
{
    private static readonly DateTimeOffset Now = new(2027, 2, 5, 3, 0, 0, TimeSpan.Zero);

    private readonly CommandRecorder _recorder = new();
    private Guid _actorId;

    protected override ZenGearApiFactory CreateFactory() => new RecordingFactory(_recorder);

    [SetUp]
    public async Task SeedActorAsync() => _actorId = await TestUsers.InsertAsync("uid-actor");

    [Test]
    public async Task First_page_that_is_not_full_sends_no_count()
    {
        await InsertBrandsAsync(2);

        var result = await NamesAsync(new PageRequest(1, 3));

        result.Items.ShouldBe(["Brand 01", "Brand 02"]);
        result.TotalCount.ShouldBe(2);
        _recorder.Commands.Count.ShouldBe(1);
        CountCommands().ShouldBeEmpty();
    }

    [Test]
    public async Task Full_first_page_counts_without_ordering()
    {
        await InsertBrandsAsync(3);

        var result = await NamesAsync(new PageRequest(1, 3));

        result.Items.ShouldBe(["Brand 01", "Brand 02", "Brand 03"]);
        result.TotalCount.ShouldBe(3);
        _recorder.Commands.Count.ShouldBe(2);
        CountCommands().ShouldHaveSingleItem().ShouldNotContain("ORDER BY", Case.Insensitive);
    }

    [Test]
    public async Task Later_page_counts()
    {
        await InsertBrandsAsync(5);

        var result = await NamesAsync(new PageRequest(2, 3));

        result.Items.ShouldBe(["Brand 04", "Brand 05"]);
        result.TotalCount.ShouldBe(5);
        CountCommands().ShouldHaveSingleItem();
    }

    [Test]
    public async Task Page_after_the_last_is_empty_with_the_total()
    {
        await InsertBrandsAsync(5);

        var result = await NamesAsync(new PageRequest(3, 3));

        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(5);
        result.Page.ShouldBe(3);
        result.PageSize.ShouldBe(3);
    }

    [Test]
    public async Task Map_and_projected_overloads_give_the_same_page()
    {
        await InsertBrandsAsync(5);
        var page = new PageRequest(2, 2);
        PagedDto<BrandDto> mapped = null!;
        PagedDto<BrandDto> projected = null!;

        await Factory.WithDbAsync(async db =>
        {
            mapped = await PagedQuery.ToPagedDtoAsync(Ordered(db), page,
                b => new BrandDto(b.Id, b.Name, b.Slug, b.IsActive, b.Version), CancellationToken.None);
            projected = await PagedQuery.ToPagedDtoAsync(
                Ordered(db).Select(b => new BrandDto(b.Id, b.Name, b.Slug, b.IsActive, b.Version)), page, CancellationToken.None);
        });

        mapped.Items.Select(b => b.Name).ShouldBe(["Brand 03", "Brand 04"]);
        projected.Items.ShouldBe(mapped.Items);
        (projected.Page, projected.PageSize, projected.TotalCount).ShouldBe((mapped.Page, mapped.PageSize, mapped.TotalCount));
    }

    private static IQueryable<Brand> Ordered(ZenGearDbContext db) =>
        db.Brands.AsNoTracking().OrderBy(b => b.Name).ThenBy(b => b.Id);

    private Task InsertBrandsAsync(int count) =>
        Factory.WithDbAsync(async db =>
        {
            db.Brands.AddRange(Enumerable.Range(1, count)
                .Select(i => Brand.Create($"Brand {i:00}", $"brand-{i:00}", _actorId, Now)));
            await db.SaveChangesAsync();
        });

    /// <summary>The page of brand names; <see cref="_recorder"/> then holds only the commands of this call.</summary>
    private async Task<PagedDto<string>> NamesAsync(PageRequest page)
    {
        PagedDto<string> result = null!;
        await Factory.WithDbAsync(async db =>
        {
            _recorder.Clear();
            result = await PagedQuery.ToPagedDtoAsync(Ordered(db), page, b => b.Name, CancellationToken.None);
        });
        return result;
    }

    private List<string> CountCommands() =>
        [.. _recorder.Commands.Where(c => c.Contains("count(", StringComparison.OrdinalIgnoreCase))];

    private sealed class RecordingFactory(CommandRecorder recorder) : ZenGearApiFactory
    {
        // The API's AddDbContext adds every IInterceptor of the container.
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services => services.AddSingleton<IInterceptor>(recorder));
        }
    }

    private sealed class CommandRecorder : DbCommandInterceptor
    {
        private readonly ConcurrentQueue<string> _commands = new();

        public IReadOnlyList<string> Commands => [.. _commands];

        public void Clear() => _commands.Clear();

        public override DbCommand CommandInitialized(CommandEndEventData eventData, DbCommand result)
        {
            _commands.Enqueue(result.CommandText);
            return result;
        }
    }
}
