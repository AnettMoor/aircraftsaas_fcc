namespace Shared.Messaging.Contracts;

public record CompanyCreatedMessage(
    Guid CompanyId,
    string CompanyName,
    string Slug,
    bool IsActive,
    DateTime CreatedAt);
