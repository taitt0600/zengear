namespace ZenGear.Services.Common.Models;

/// <summary>
/// One page of a list endpoint, <c>{ "items": [...], "page": 1, "pageSize": 20, "totalCount": 134 }</c>
/// (docs/API_CONVENTIONS.md, Paging). Every list endpoint returns it; there is no page DTO per entity.
/// </summary>
public sealed record PagedDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
