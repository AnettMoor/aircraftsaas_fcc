namespace Shared.Contracts.Users.DTOs;

public record UserBasicDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName);
