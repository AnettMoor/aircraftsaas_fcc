using Users.Application.Contracts;
using Users.Application.Interfaces;
using Users.Application.Services;
using Shared.Contracts.Common;
using FluentAssertions;
using Moq;

namespace Users.Tests.Services;

public class TenantServiceTests
{
    private readonly Mock<IUsersUOW> _uowMock;
    private readonly Mock<ICurrentUserProvider> _currentUserProviderMock;
    private readonly Mock<IRequestContextProvider> _requestContextProviderMock;
    private readonly Mock<ICompanyRepository> _companyRepoMock;
    private readonly TenantService _sut;

    public TenantServiceTests()
    {
        _uowMock = new Mock<IUsersUOW>();
        _currentUserProviderMock = new Mock<ICurrentUserProvider>();
        _requestContextProviderMock = new Mock<IRequestContextProvider>();
        _companyRepoMock = new Mock<ICompanyRepository>();

        _uowMock.Setup(u => u.CompanyRepository).Returns(_companyRepoMock.Object);

        _sut = new TenantService(_uowMock.Object, _currentUserProviderMock.Object, _requestContextProviderMock.Object);
    }

    [Fact]
    public void GetCurrentTenantId_FromCookie_ReturnsTenantId()
    {
        // Arrange — header returns null (default mock), cookie has value
        var tenantId = Guid.NewGuid();
        _requestContextProviderMock.Setup(p => p.GetCookieValue("SelectedCompanyId")).Returns(tenantId.ToString());

        // Act
        var result = _sut.GetCurrentTenantId();

        // Assert
        result.Should().Be(tenantId);
    }

    [Fact]
    public void GetCurrentTenantId_FromHeader_ReturnsTenantId()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _requestContextProviderMock.Setup(p => p.GetCookieValue("SelectedCompanyId")).Returns((string?)null);
        _requestContextProviderMock.Setup(p => p.GetHeaderValue("X-Tenant-Id")).Returns(tenantId.ToString());

        // Act
        var result = _sut.GetCurrentTenantId();

        // Assert
        result.Should().Be(tenantId);
    }

    [Fact]
    public void GetCurrentTenantId_FromJwtClaim_ReturnsTenantId()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _requestContextProviderMock.Setup(p => p.GetCookieValue("SelectedCompanyId")).Returns((string?)null);
        _requestContextProviderMock.Setup(p => p.GetHeaderValue("X-Tenant-Id")).Returns((string?)null);
        _currentUserProviderMock.Setup(p => p.IsAuthenticated()).Returns(true);
        _currentUserProviderMock.Setup(p => p.GetClaimValue("companyId")).Returns(tenantId.ToString());

        // Act
        var result = _sut.GetCurrentTenantId();

        // Assert
        result.Should().Be(tenantId);
    }

    [Fact]
    public void GetCurrentTenantId_NoSourcesAvailable_ReturnsNull()
    {
        // Arrange
        _requestContextProviderMock.Setup(p => p.GetCookieValue("SelectedCompanyId")).Returns((string?)null);
        _requestContextProviderMock.Setup(p => p.GetHeaderValue("X-Tenant-Id")).Returns((string?)null);
        _currentUserProviderMock.Setup(p => p.IsAuthenticated()).Returns(false);

        // Act
        var result = _sut.GetCurrentTenantId();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void SetCurrentTenant_SetsCookie()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        // Act
        _sut.SetCurrentTenant(tenantId);

        // Assert
        _requestContextProviderMock.Verify(p => p.SetCookie(
            "SelectedCompanyId",
            tenantId.ToString(),
            30,
            true
        ), Times.Once);
    }

    [Fact]
    public async Task IsUserInCompanyAsync_DelegatesToRepository()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _companyRepoMock.Setup(r => r.IsUserInCompanyAsync(companyId, userId)).ReturnsAsync(true);

        // Act
        var result = await _sut.IsUserInCompanyAsync(companyId, userId);

        // Assert
        result.Should().BeTrue();
        _companyRepoMock.Verify(r => r.IsUserInCompanyAsync(companyId, userId), Times.Once);
    }

    [Fact]
    public void GetCurrentTenantId_HeaderPrioritizedOverCookie()
    {
        // Arrange — both cookie and header set with different values
        var cookieTenantId = Guid.NewGuid();
        var headerTenantId = Guid.NewGuid();
        _requestContextProviderMock.Setup(p => p.GetCookieValue("SelectedCompanyId")).Returns(cookieTenantId.ToString());
        _requestContextProviderMock.Setup(p => p.GetHeaderValue("X-Tenant-Id")).Returns(headerTenantId.ToString());

        // Act
        var result = _sut.GetCurrentTenantId();

        // Assert — header value should win (SPA clients explicitly set X-Tenant-Id;
        //          cookie may be stale after a company change via MVC)
        result.Should().Be(headerTenantId);
    }
}
