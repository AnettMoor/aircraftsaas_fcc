using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Users.Application.Contracts;
using Users.Application.DTOs;
using Users.Domain.Identity;
using Users.Infrastructure.Services;

namespace Users.Tests.Services;

public class AuthServiceTests
{
    private readonly Mock<UserManager<AppUser>> _userManagerMock;
    private readonly Mock<SignInManager<AppUser>> _signInManagerMock;
    private readonly IConfiguration _configuration;
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepoMock;
    private readonly Mock<ILogger<AuthService>> _loggerMock;
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        // UserManager requires IUserStore
        var userStoreMock = new Mock<IUserStore<AppUser>>();
        _userManagerMock = new Mock<UserManager<AppUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        // SignInManager requires UserManager, IHttpContextAccessor, IUserClaimsPrincipalFactory
        var contextAccessorMock = new Mock<IHttpContextAccessor>();
        var claimsPrincipalFactoryMock = new Mock<IUserClaimsPrincipalFactory<AppUser>>();
        _signInManagerMock = new Mock<SignInManager<AppUser>>(
            _userManagerMock.Object, contextAccessorMock.Object, claimsPrincipalFactoryMock.Object,
            null!, null!, null!, null!);

        // Use real IConfiguration — Moq cannot mock GetValue<T> extension methods
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "JWT:Key", "ThisIsATestSecretKeyForJwtTokenGeneration1234567890!!" },
                { "JWT:Issuer", "TestIssuer" },
                { "JWT:Audience", "TestAudience" }
            })
            .Build();

        _refreshTokenRepoMock = new Mock<IRefreshTokenRepository>();
        _loggerMock = new Mock<ILogger<AuthService>>();

        _sut = new AuthService(
            _userManagerMock.Object,
            _signInManagerMock.Object,
            _configuration,
            _refreshTokenRepoMock.Object,
            _loggerMock.Object);
    }

    // ---- RegisterAsync ----

    [Fact]
    public async Task RegisterAsync_ExistingEmail_ReturnsFailWithUserAlreadyExists()
    {
        // Arrange
        var existingUser = new AppUser
        {
            Id = Guid.NewGuid(),
            Email = "existing@test.com",
            UserName = "existing@test.com"
        };

        _userManagerMock
            .Setup(m => m.FindByEmailAsync("existing@test.com"))
            .ReturnsAsync(existingUser);

        // Act
        var result = await _sut.RegisterAsync("existing@test.com", "Pass123!", "John", "Doe", 3600);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.ErrorType.Should().Be(AuthErrorType.UserAlreadyExists);
        result.ErrorMessage.Should().Contain("already registered");
    }

    [Fact]
    public async Task RegisterAsync_NewUser_SuccessWithJwtAndRefreshToken()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var createdUser = new AppUser
        {
            Id = userId,
            Email = "new@test.com",
            UserName = "new@test.com",
            FirstName = "John",
            LastName = "Doe"
        };

        // FindByEmailAsync is called twice: once at start (null) and once after creation (user)
        _userManagerMock
            .SetupSequence(m => m.FindByEmailAsync("new@test.com"))
            .ReturnsAsync((AppUser?)null)
            .ReturnsAsync(createdUser);

        _userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<AppUser>(), "Pass123!"))
            .Callback<AppUser, string>((u, _) => u.Id = userId)
            .ReturnsAsync(IdentityResult.Success);

        _userManagerMock
            .Setup(m => m.AddToRoleAsync(It.IsAny<AppUser>(), "Normal"))
            .ReturnsAsync(IdentityResult.Success);

        _userManagerMock
            .Setup(m => m.GetRolesAsync(It.IsAny<AppUser>()))
            .ReturnsAsync(new List<string> { "Normal" });

        // SignInManager.CreateUserPrincipalAsync is used for JWT generation
        var claimsIdentity = new System.Security.Claims.ClaimsIdentity(new[]
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email, "new@test.com"),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userId.ToString()),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.GivenName, "John"),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Surname, "Doe")
        });
        var claimsPrincipal = new System.Security.Claims.ClaimsPrincipal(claimsIdentity);

        _signInManagerMock
            .Setup(m => m.CreateUserPrincipalAsync(It.IsAny<AppUser>()))
            .ReturnsAsync(claimsPrincipal);

        // Act
        var result = await _sut.RegisterAsync("new@test.com", "Pass123!", "John", "Doe", 3600);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Jwt.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RegisterAsync_CreateFails_ReturnsFailWithRegistrationFailed()
    {
        // Arrange
        _userManagerMock
            .Setup(m => m.FindByEmailAsync("fail@test.com"))
            .ReturnsAsync((AppUser?)null);

        _userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<AppUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Password too weak" }));

        // Act
        var result = await _sut.RegisterAsync("fail@test.com", "x", "John", "Doe", 3600);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.ErrorType.Should().Be(AuthErrorType.RegistrationFailed);
        result.ErrorMessage.Should().Contain("Password too weak");
    }

    // ---- LoginAsync ----

    [Fact]
    public async Task LoginAsync_UnknownEmail_ReturnsInvalidCredentials()
    {
        // Arrange
        _userManagerMock
            .Setup(m => m.FindByEmailAsync("unknown@test.com"))
            .ReturnsAsync((AppUser?)null);

        // Act
        var result = await _sut.LoginAsync("unknown@test.com", "Pass123!", 3600);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.ErrorType.Should().Be(AuthErrorType.InvalidCredentials);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ReturnsInvalidCredentials()
    {
        // Arrange
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Email = "user@test.com",
            UserName = "user@test.com"
        };

        _userManagerMock
            .Setup(m => m.FindByEmailAsync("user@test.com"))
            .ReturnsAsync(user);

        _signInManagerMock
            .Setup(m => m.CheckPasswordSignInAsync(user, "WrongPass", false))
            .ReturnsAsync(SignInResult.Failed);

        // Act
        var result = await _sut.LoginAsync("user@test.com", "WrongPass", 3600);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.ErrorType.Should().Be(AuthErrorType.InvalidCredentials);
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsJwtAndRefreshToken()
    {
        // Arrange
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Email = "user@test.com",
            UserName = "user@test.com",
            FirstName = "Test",
            LastName = "User"
        };

        _userManagerMock
            .Setup(m => m.FindByEmailAsync("user@test.com"))
            .ReturnsAsync(user);

        _signInManagerMock
            .Setup(m => m.CheckPasswordSignInAsync(user, "GoodPass123!", false))
            .ReturnsAsync(SignInResult.Success);

        _refreshTokenRepoMock
            .Setup(r => r.DeleteExpiredTokensAsync(user.Id))
            .ReturnsAsync(0);

        _refreshTokenRepoMock
            .Setup(r => r.CreateAsync(It.IsAny<AppRefreshToken>()))
            .ReturnsAsync((AppRefreshToken t) => t);

        _userManagerMock
            .Setup(m => m.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { "Normal" });

        // SignInManager.CreateUserPrincipalAsync is used for JWT generation
        var claimsIdentity = new System.Security.Claims.ClaimsIdentity(new[]
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email, "user@test.com"),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id.ToString()),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.GivenName, "Test"),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Surname, "User")
        });
        var claimsPrincipal = new System.Security.Claims.ClaimsPrincipal(claimsIdentity);

        _signInManagerMock
            .Setup(m => m.CreateUserPrincipalAsync(user))
            .ReturnsAsync(claimsPrincipal);

        // Act
        var result = await _sut.LoginAsync("user@test.com", "GoodPass123!", 3600);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Jwt.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        _refreshTokenRepoMock.Verify(r => r.DeleteExpiredTokensAsync(user.Id), Times.Once);
        _refreshTokenRepoMock.Verify(r => r.CreateAsync(It.IsAny<AppRefreshToken>()), Times.Once);
    }

    // ---- LogoutAsync ----

    [Fact]
    public async Task LogoutAsync_ValidToken_ReturnsSuccessWithDeletedCount()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tokenValue = "some-refresh-token";
        var tokens = new List<AppRefreshToken>
        {
            new() { Id = Guid.NewGuid(), AppUserId = userId, RefreshToken = tokenValue }
        };

        _refreshTokenRepoMock
            .Setup(r => r.GetTokensByValueAsync(userId, tokenValue))
            .ReturnsAsync(tokens);

        _refreshTokenRepoMock
            .Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _sut.LogoutAsync(userId, tokenValue);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.DeletedTokenCount.Should().Be(1);
        _refreshTokenRepoMock.Verify(r => r.Remove(It.IsAny<AppRefreshToken>()), Times.Once);
    }
}
