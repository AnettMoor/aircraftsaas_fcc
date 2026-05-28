namespace Shared.Messaging.Contracts;

public record UserCompanyChangedMessage(
    Guid UserId,
    Guid OldCompanyId,
    Guid NewCompanyId,
    DateTime ChangedAt);
