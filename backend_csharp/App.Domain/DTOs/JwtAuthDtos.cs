namespace App.Domain.DTOs;

/// <summary>
/// Result of an authentication operation (Register, Login, RefreshToken).
/// </summary>
public class JwtAuthResult
{
    public bool Succeeded { get; set; }
    public string? Jwt { get; set; }
    public string? RefreshToken { get; set; }
    public string? ErrorMessage { get; set; }
    public AuthErrorType ErrorType { get; set; }

    public static JwtAuthResult Success(string jwt, string refreshToken) =>
        new() { Succeeded = true, Jwt = jwt, RefreshToken = refreshToken };

    public static JwtAuthResult Fail(AuthErrorType errorType, string message) =>
        new() { Succeeded = false, ErrorType = errorType, ErrorMessage = message };
}

/// <summary>
/// Result of a logout operation.
/// </summary>
public class LogoutResult
{
    public bool Succeeded { get; set; }
    public int DeletedTokenCount { get; set; }
    public string? ErrorMessage { get; set; }
    public AuthErrorType ErrorType { get; set; }

    public static LogoutResult Success(int deletedCount) =>
        new() { Succeeded = true, DeletedTokenCount = deletedCount };

    public static LogoutResult Fail(AuthErrorType errorType, string message) =>
        new() { Succeeded = false, ErrorType = errorType, ErrorMessage = message };
}

public enum AuthErrorType
{
    None,
    UserAlreadyExists,
    UserNotFound,
    InvalidCredentials,
    InvalidToken,
    InvalidRefreshToken,
    RegistrationFailed
}
