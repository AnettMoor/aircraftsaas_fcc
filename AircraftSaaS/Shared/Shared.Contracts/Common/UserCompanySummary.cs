namespace Shared.Contracts.Common;

/// <summary>
/// Lightweight, boundary-safe representation of a user's company membership.
/// Lives in Shared.Contracts so that the WebApp host (and any other module)
/// can consume it without depending on Users.Domain entities or enums.
/// </summary>
public record UserCompanySummary(
    Guid CompanyId,
    string CompanyName,
    string Role,
    bool IsActive);
