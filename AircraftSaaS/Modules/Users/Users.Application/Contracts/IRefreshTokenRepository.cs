using Users.Domain.Identity;

namespace Users.Application.Contracts;

public interface IRefreshTokenRepository
{
    /// <summary>
    /// Delete expired refresh tokens for the given user.
    /// Handles InMemory provider gracefully (no-op when ExecuteDeleteAsync is not supported).
    /// </summary>
    Task<int> DeleteExpiredTokensAsync(Guid userId);

    /// <summary>
    /// Add a new refresh token and persist it.
    /// </summary>
    Task<AppRefreshToken> CreateAsync(AppRefreshToken token);

    /// <summary>
    /// Load valid (non-expired) refresh tokens for a user that match the given token value
    /// (either as current or previous refresh token).
    /// </summary>
    Task<IList<AppRefreshToken>> GetValidTokensForUserAsync(Guid userId, string refreshToken);

    /// <summary>
    /// Load refresh tokens matching the given token value for a user (for logout).
    /// </summary>
    Task<IList<AppRefreshToken>> GetTokensByValueAsync(Guid userId, string refreshToken);

    /// <summary>
    /// Remove a refresh token.
    /// </summary>
    void Remove(AppRefreshToken token);

    /// <summary>
    /// Persist all pending changes.
    /// </summary>
    Task<int> SaveChangesAsync();
}
