namespace Shared.Kernel.Domain;

/// <summary>
/// Declares that an entity belongs to a company/tenant.
/// This allows automatic universal filtering in BaseRepository (company-level IDOR protection).
/// </summary>
public interface ICompanyEntity
{
    Guid CompanyId { get; set; }
}
