namespace Shared.Contracts.Common;

/// <summary>
/// Generic paged result wrapper used across all modules.
/// </summary>
public class PagedResult<T>
{
    public IEnumerable<T> Items { get; set; } = new List<T>();
    public int TotalItems { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
