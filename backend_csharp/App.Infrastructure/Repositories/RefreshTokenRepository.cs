using App.Domain.Contracts;
using App.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace App.Infrastructure.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<RefreshTokenRepository> _logger;

    public RefreshTokenRepository(AppDbContext context, ILogger<RefreshTokenRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> DeleteExpiredTokensAsync(Guid userId)
    {
        // EF Core InMemory provider does not support ExecuteDeleteAsync, so skip during integration tests
        if (_context.Database.ProviderName!.Contains("InMemory"))
        {
            return 0;
        }

        var deletedRows = await _context.RefreshTokens
            .Where(t => t.AppUserId == userId && t.ExpirationDT < DateTime.UtcNow)
            .ExecuteDeleteAsync();

        _logger.LogInformation("Deleted {Count} expired refresh tokens for user {UserId}", deletedRows, userId);
        return deletedRows;
    }

    /// <inheritdoc />
    public async Task<AppRefreshToken> CreateAsync(AppRefreshToken token)
    {
        _context.RefreshTokens.Add(token);
        await _context.SaveChangesAsync();
        return token;
    }

    /// <inheritdoc />
    public async Task<IList<AppRefreshToken>> GetValidTokensForUserAsync(Guid userId, string refreshToken)
    {
        return await _context.RefreshTokens
            .Where(t => t.AppUserId == userId)
            .Where(t =>
                (t.RefreshToken == refreshToken && t.ExpirationDT > DateTime.UtcNow) ||
                (t.PreviousRefreshToken == refreshToken && t.PreviousExpirationDT > DateTime.UtcNow))
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IList<AppRefreshToken>> GetTokensByValueAsync(Guid userId, string refreshToken)
    {
        return await _context.RefreshTokens
            .Where(t => t.AppUserId == userId)
            .Where(t =>
                t.RefreshToken == refreshToken ||
                t.PreviousRefreshToken == refreshToken)
            .ToListAsync();
    }

    /// <inheritdoc />
    public void Remove(AppRefreshToken token)
    {
        _context.RefreshTokens.Remove(token);
    }

    /// <inheritdoc />
    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task DeleteAllForUserAsync(Guid userId)
    {
        var tokens = await _context.RefreshTokens
            .Where(t => t.AppUserId == userId)
            .ToListAsync();
        _context.RefreshTokens.RemoveRange(tokens);
    }
}
