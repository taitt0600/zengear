namespace ZenGear.Services.Common.Models;

/// <summary>The page of a list endpoint, <c>?page=1&amp;pageSize=20</c> (docs/API_CONVENTIONS.md, Paging).</summary>
/// <param name="Page">1 to <see cref="MaxPage"/>; defaults to 1.</param>
/// <param name="PageSize">1 to <see cref="MaxPageSize"/>; defaults to <see cref="DefaultPageSize"/>.</param>
public sealed record PageRequest(int Page = 1, int PageSize = PageRequest.DefaultPageSize)
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    /// <summary>The highest page, so that <see cref="Skip"/> stays within <see cref="int"/>.</summary>
    public const int MaxPage = int.MaxValue / MaxPageSize;

    /// <summary>Rows before this page.</summary>
    public int Skip => (Page - 1) * PageSize;
}
