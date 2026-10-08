namespace ZenGear.Services.Inventory.Models;

/// <summary>
/// One page of the public list of business locations (docs/API_CONVENTIONS.md, Paging). Not the generic
/// <c>PagedDto&lt;T&gt;</c> of docs/ARCHITECTURE.md yet: the architecture tests do not accept a generic DTO name (owner's
/// decision for LOC-01; the move comes with that test change).
/// </summary>
/// <param name="TotalCount">Locations of every page.</param>
public sealed record BusinessLocationPublicPageDto(
    IReadOnlyList<BusinessLocationPublicDto> Items,
    int Page,
    int PageSize,
    int TotalCount);
