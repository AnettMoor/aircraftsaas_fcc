namespace Shared.Contracts.Common;

/// <summary>
/// Lightweight company select item used by admin list views across modules.
/// </summary>
public class CompanySelectItemDto
{
    public Guid Id { get; set; }
    public string CompanyName { get; set; } = default!;
}
