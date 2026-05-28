namespace Shared.Messaging.Contracts;

public record CompanyUpdatedMessage(
    Guid CompanyId,
    string CompanyName,
    string Slug,
    bool IsActive,
    DateTime UpdatedAt);
