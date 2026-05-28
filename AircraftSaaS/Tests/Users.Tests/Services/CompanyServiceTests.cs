using FluentAssertions;
using Moq;
using Shared.Contracts.Fleet;
using Shared.Contracts.Fleet.DTOs;
using Shared.Kernel.Domain;
using Users.Application.Contracts;
using Users.Application.DTOs;
using Users.Application.Services;
using Users.Domain.Entities;

namespace Users.Tests.Services;

public class CompanyServiceTests
{
    private readonly Mock<IUsersUOW> _uowMock;
    private readonly Mock<ICompanyRepository> _companyRepoMock;
    private readonly Mock<IFleetModuleApi> _fleetApiMock;
    private readonly CompanyService _sut;

    public CompanyServiceTests()
    {
        _uowMock = new Mock<IUsersUOW>();
        _companyRepoMock = new Mock<ICompanyRepository>();
        _fleetApiMock = new Mock<IFleetModuleApi>();

        _uowMock.Setup(u => u.CompanyRepository).Returns(_companyRepoMock.Object);

        _sut = new CompanyService(_uowMock.Object, _fleetApiMock.Object);
    }

    // ---- CreateAsync ----

    [Fact]
    public async Task CreateAsync_ValidDto_CreatesCompanyAndReturnsDto()
    {
        // Arrange
        var dto = new CreateCompanyDto
        {
            CompanyName = "Acme Aviation",
            Address = "123 Sky Lane",
            Phone = "+1234567890",
            Email = "info@acme.aero"
        };

        _companyRepoMock
            .Setup(r => r.ExistsBySlugAsync(It.IsAny<string>()))
            .ReturnsAsync(false);

        _companyRepoMock
            .Setup(r => r.Add(It.IsAny<Company>()));

        _uowMock
            .Setup(u => u.SaveChangesAsync())
            .ReturnsAsync(1);

        // Enrich: GetUserCountAsync returns 0 for new company
        _companyRepoMock
            .Setup(r => r.GetUserCountAsync(It.IsAny<Guid>()))
            .ReturnsAsync(0);

        // Cross-module: aircraft query returns empty list
        _fleetApiMock
            .Setup(f => f.GetAircraftsByCompanyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AircraftBasicDto>());

        // Act
        var result = await _sut.CreateAsync(dto, "admin@test.com");

        // Assert
        result.Should().NotBeNull();
        result.CompanyName.Should().Be("Acme Aviation");
        result.Slug.Should().Be("acme-aviation");
        result.IsActive.Should().BeTrue();
        result.MaxUsers.Should().Be(2);
        result.MaxAircraft.Should().Be(3);
        result.MaxBookingsPerMonth.Should().Be(20);
        result.Address.Should().Be("123 Sky Lane");
        result.Phone.Should().Be("+1234567890");
        result.Email.Should().Be("info@acme.aero");
        result.CurrentUserCount.Should().Be(0);
        result.CurrentAircraftCount.Should().Be(0);

        _companyRepoMock.Verify(r => r.Add(It.IsAny<Company>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_DuplicateSlug_AppendsSuffixToSlug()
    {
        // Arrange
        var dto = new CreateCompanyDto { CompanyName = "Sky Corp" };

        _companyRepoMock
            .Setup(r => r.ExistsBySlugAsync("sky-corp"))
            .ReturnsAsync(true); // slug collision

        _companyRepoMock.Setup(r => r.Add(It.IsAny<Company>()));
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        _companyRepoMock.Setup(r => r.GetUserCountAsync(It.IsAny<Guid>())).ReturnsAsync(0);
        _fleetApiMock
            .Setup(f => f.GetAircraftsByCompanyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AircraftBasicDto>());

        // Act
        var result = await _sut.CreateAsync(dto, "admin@test.com");

        // Assert — slug should start with "sky-corp-" but have a numeric suffix
        result.Slug.Should().StartWith("sky-corp-");
        result.Slug.Should().NotBe("sky-corp");
    }

    // ---- GetByIdAsync ----

    [Fact]
    public async Task GetByIdAsync_ExistingCompany_ReturnsMappedDto()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var company = new Company
        {
            Id = companyId,
            CompanyName = new LangStr("Test Corp"),
            Slug = "test-corp",
            IsActive = true,
            MaxUsers = 5,
            MaxAircraft = 10,
            MaxBookingsPerMonth = 50,
            Address = new LangStr("Test Addr"),
            Phone = "555-1234",
            Email = "test@corp.com",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "system"
        };

        _companyRepoMock
            .Setup(r => r.FindAsync(companyId, It.IsAny<Guid>(), It.IsAny<Guid?>()))
            .ReturnsAsync(company);

        _companyRepoMock
            .Setup(r => r.GetUserCountAsync(companyId))
            .ReturnsAsync(3);

        _fleetApiMock
            .Setup(f => f.GetAircraftsByCompanyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AircraftBasicDto>
            {
                new(Guid.NewGuid(), "ES-ABC", "C172", companyId, "PPL"),
                new(Guid.NewGuid(), "ES-DEF", "PA28", companyId, "PPL")
            });

        // Act
        var result = await _sut.GetByIdAsync(companyId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(companyId);
        result.CompanyName.Should().Be("Test Corp");
        result.CurrentUserCount.Should().Be(3);
        result.CurrentAircraftCount.Should().Be(2);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentCompany_ReturnsNull()
    {
        // Arrange
        _companyRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid?>()))
            .ReturnsAsync((Company?)null);

        // Act
        var result = await _sut.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    // ---- UpdateAsync ----

    [Fact]
    public async Task UpdateAsync_AsOwner_UpdatesCompanyFields()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        var company = new Company
        {
            Id = companyId,
            CompanyName = new LangStr("Old Name"),
            Slug = "old-name",
            IsActive = true,
            MaxUsers = 5,
            MaxAircraft = 10,
            MaxBookingsPerMonth = 50,
            Address = new LangStr("Old Addr"),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "system"
        };

        _companyRepoMock
            .Setup(r => r.IsCompanyOwnerAsync(callerId, companyId))
            .ReturnsAsync(true);

        _companyRepoMock
            .Setup(r => r.GetByIdTrackingAsync(companyId))
            .ReturnsAsync(company);

        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        _companyRepoMock.Setup(r => r.GetUserCountAsync(companyId)).ReturnsAsync(1);
        _fleetApiMock
            .Setup(f => f.GetAircraftsByCompanyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AircraftBasicDto>());

        var dto = new UpdateCompanyDto
        {
            Id = companyId,
            CompanyName = "New Name",
            Address = "New Addr",
            Phone = "555-9999",
            Email = "new@corp.com"
        };

        // Act
        var result = await _sut.UpdateAsync(companyId, dto, "admin@test.com", callerId);

        // Assert
        result.Should().NotBeNull();
        result.CompanyName.Should().Be("New Name");
        result.Address.Should().Be("New Addr");
        result.Phone.Should().Be("555-9999");
        result.Email.Should().Be("new@corp.com");
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_NonOwnerNonAdmin_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var callerId = Guid.NewGuid();

        _companyRepoMock
            .Setup(r => r.IsCompanyOwnerAsync(callerId, companyId))
            .ReturnsAsync(false);

        var dto = new UpdateCompanyDto
        {
            Id = companyId,
            CompanyName = "Hacked Name"
        };

        // Act
        var act = () => _sut.UpdateAsync(companyId, dto, "hacker@evil.com", callerId);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*company owners or system admins*");
    }

    [Fact]
    public async Task UpdateAsync_NotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var callerId = Guid.NewGuid();

        _companyRepoMock
            .Setup(r => r.IsCompanyOwnerAsync(callerId, companyId))
            .ReturnsAsync(true);

        _companyRepoMock
            .Setup(r => r.GetByIdTrackingAsync(companyId))
            .ReturnsAsync((Company?)null);

        var dto = new UpdateCompanyDto { Id = companyId, CompanyName = "X" };

        // Act
        var act = () => _sut.UpdateAsync(companyId, dto, "admin@test.com", callerId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Company not found");
    }

    // ---- DeleteAsync ----

    [Fact]
    public async Task DeleteAsync_NonAdmin_ThrowsUnauthorizedAccessException()
    {
        // Act
        var act = () => _sut.DeleteAsync(Guid.NewGuid(), "user@test.com", Guid.NewGuid(), isAdmin: false);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*system admins*");
    }

    // ---- IsCompanyActiveAsync ----

    [Fact]
    public async Task IsCompanyActiveAsync_ActiveCompany_ReturnsTrue()
    {
        var companyId = Guid.NewGuid();
        var company = new Company { Id = companyId, IsActive = true, CompanyName = new LangStr("X"), Slug = "x", CreatedBy = "sys" };

        _companyRepoMock
            .Setup(r => r.FindAsync(companyId, It.IsAny<Guid>(), It.IsAny<Guid?>()))
            .ReturnsAsync(company);

        var result = await _sut.IsCompanyActiveAsync(companyId);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsCompanyActiveAsync_NonExistentCompany_ReturnsFalse()
    {
        _companyRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid?>()))
            .ReturnsAsync((Company?)null);

        var result = await _sut.IsCompanyActiveAsync(Guid.NewGuid());

        result.Should().BeFalse();
    }
}
