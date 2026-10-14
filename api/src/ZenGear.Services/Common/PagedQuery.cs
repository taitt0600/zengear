using Microsoft.EntityFrameworkCore;
using ZenGear.Services.Common.Models;

namespace ZenGear.Services.Common;

/// <summary>
/// One page of an ordered query as a <see cref="PagedDto{T}"/>. A first page that is not full already holds every
/// row, so its total is the number of items and no COUNT is sent.
/// </summary>
public static class PagedQuery
{
    /// <param name="ordered">Filtered and ordered by the caller, in an order that keeps pages stable.</param>
    /// <param name="page">Already validated by the caller.</param>
    /// <param name="map">Runs in memory on each row of the page.</param>
    public static async Task<PagedDto<TDto>> ToPagedDtoAsync<TRow, TDto>(IQueryable<TRow> ordered, PageRequest page,
        Func<TRow, TDto> map, CancellationToken cancellationToken)
    {
        var rows = await ordered.Skip(page.Skip).Take(page.PageSize).ToListAsync(cancellationToken);
        var total = page.Page == 1 && rows.Count < page.PageSize ? rows.Count : await ordered.CountAsync(cancellationToken);
        return new PagedDto<TDto>([.. rows.Select(map)], page.Page, page.PageSize, total);
    }

    /// <summary>The page of a query already projected to the DTO.</summary>
    public static Task<PagedDto<TDto>> ToPagedDtoAsync<TDto>(IQueryable<TDto> ordered, PageRequest page,
        CancellationToken cancellationToken) => ToPagedDtoAsync(ordered, page, row => row, cancellationToken);
}
