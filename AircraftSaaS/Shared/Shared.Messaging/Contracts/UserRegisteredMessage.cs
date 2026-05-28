namespace Shared.Messaging.Contracts;

public record UserRegisteredMessage(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    DateTime RegisteredAt);
