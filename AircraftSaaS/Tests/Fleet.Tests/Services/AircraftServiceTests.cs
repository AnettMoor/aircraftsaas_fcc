using FluentAssertions;
using Moq;
using Fleet.Application.Contracts;
using Fleet.Application.DTOs;
using Fleet.Application.Services;
using Fleet.Domain.Entities;
using Shared.Contracts.Users;
using Shared.Contracts.Users.DTOs;
using Shared.Kernel.Domain;

namespace Fleet.Tests.Services;

public class AircraftServiceTests
{
    private readonly Mock<IFleetUOW> _uowMock;
    private readonly Mock<IAircraftRepository> _aircraftRepoMock;
    private readonly Mock<IAircraftAvailabilityRepository> _availRepoMock;
    private readonly Mock<IUsersModuleApi> _usersApiMock;
    private readonly AircraftService _sut;

    public AircraftServiceTests()
    {
        _uowMock = new Mock<IFleetUOW>();
        _aircraftRepoMock = new Mock<IAircraftRepository>();
        _availRepoMock = new Mock<IAircraftAvailabilityRepository>();
        _usersApiMock = new Mock<IUsersModuleApi>();

        _uowMock.Setup(u => u.AircraftRepository).Returns(_aircraftRepoMock.Object);
        _uowMock.Setup(u => u.AircraftAvailabilityRepository).Returns(_availRepoMock.Object);

        _sut = new AircraftService(_uowMock.Object, _usersApiMock.Object);
    }

    private static Aircraft CreateTestAircraft(Guid? id = null, Guid? companyId = null, Guid? airportId = null)
    {
        var airport = new Airport
        {
            Id = airportId ?? Guid.NewGuid(),
            IcaoCode = "EETN",
            IataCode = "TLL",
            Name = new LangStr("Tallinn Airport"),
            City = new LangStr("Tallinn"),
            Country = new LangStr("Estonia"),
            CreatedBy = "seed"
        };

        return new Aircraft
        {
            Id = id ?? Guid.NewGuid(),
            RegistrationNumber = "ES-ABC",
            Make = new LangStr("Cessna"),
            Model = new LangStr("172 Skyhawk"),
            Year = 2020,
            Category = new LangStr("SingleEngine"),
            RequiredLicenseType = "PPL",
            TotalAirspeedHours = 500,
            HourlyRate = 120.00m,
            BaseAirportId = airport.Id,
            BaseAirport = airport,
            Description = new LangStr("Great training aircraft"),
            IsAvailable = true,
            CompanyId = companyId ?? Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "admin@test.com",
            Photos = new List<AircraftPhoto>(),
            InsurancePolicies = new List<InsurancePolicy>
            {
                new()
                {
                    PolicyNumber = "POL-001",
                    InsuranceProvider = new LangStr("AeroInsure"),
                    StartDate = DateTime.UtcNow.AddYears(-1),
                    EndDate = DateTime.UtcNow.AddYears(1),
                    CoverageAmount = 1000000m,
                    CoverageType = new LangStr("Full Coverage")
                }
            },
            MaintenanceRecords = new List<MaintenanceRecord>()
        };
    }

    // ---- CreateAsync ----

    [Fact]
    public async Task CreateAsync_ValidDto_CreatesAircraftAndReturnsDto()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var airportId = Guid.NewGuid();
        var dto = new CreateAircraftDto
        {
            RegistrationNumber = "ES-XYZ",
            Make = "Piper",
            Model = "PA-28",
            Year = 2019,
            Category = "SingleEngine",
            RequiredLicenseType = "PPL",
            TotalAirspeedHours = 300,
            HourlyRate = 95.00m,
            BaseAirportId = airportId,
            Description = "Training aircraft"
        };

        _aircraftRepoMock.Setup(r => r.Add(It.IsAny<Aircraft>()));
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // After create, GetByIdWithIncludesAsync returns the created aircraft
        _aircraftRepoMock
            .Setup(r => r.GetByIdWithIncludesAsync(It.IsAny<Guid>(), companyId))
            .ReturnsAsync((Guid id, Guid? cId) => CreateTestAircraft(id, companyId, airportId));

        // Cross-module: company data
        _usersApiMock
            .Setup(u => u.GetCompanyByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyBasicDto(companyId, "Test Aviation"));

        // Act
        var result = await _sut.CreateAsync(dto, companyId, "admin@test.com");

        // Assert
        result.Should().NotBeNull();
        result.CompanyId.Should().Be(companyId);
        result.CompanyName.Should().Be("Test Aviation");
        result.IsAvailable.Should().BeTrue();
        result.Status.Should().Be("Available");

        _aircraftRepoMock.Verify(r => r.Add(It.IsAny<Aircraft>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithInsurancePolicy_SavesTwice()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var dto = new CreateAircraftDto
        {
            RegistrationNumber = "ES-INS",
            Make = "Cessna",
            Model = "C152",
            Year = 2018,
            Category = "SingleEngine",
            HourlyRate = 80m,
            BaseAirportId = Guid.NewGuid(),
            Description = "Training",
            InsurancePolicy = new CreateInsurancePolicyDto
            {
                PolicyNumber = "INS-001",
                InsuranceProvider = "AeroInsure",
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddYears(1),
                CoverageAmount = 500000m,
                CoverageType = "Liability"
            }
        };

        _aircraftRepoMock.Setup(r => r.Add(It.IsAny<Aircraft>()));
        _aircraftRepoMock.Setup(r => r.AddInsurancePolicy(It.IsAny<InsurancePolicy>()));
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        _aircraftRepoMock
            .Setup(r => r.GetByIdWithIncludesAsync(It.IsAny<Guid>(), companyId))
            .ReturnsAsync(CreateTestAircraft(companyId: companyId));

        _usersApiMock
            .Setup(u => u.GetCompanyByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyBasicDto(companyId, "Test Aviation"));

        // Act
        await _sut.CreateAsync(dto, companyId, "admin@test.com");

        // Assert — SaveChangesAsync called twice: once for aircraft, once for insurance
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Exactly(2));
        _aircraftRepoMock.Verify(r => r.AddInsurancePolicy(It.IsAny<InsurancePolicy>()), Times.Once);
    }

    // ---- GetByIdAsync ----

    [Fact]
    public async Task GetByIdAsync_ExistingAircraft_ReturnsMappedDto()
    {
        // Arrange
        var aircraftId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var aircraft = CreateTestAircraft(aircraftId, companyId);

        _aircraftRepoMock
            .Setup(r => r.GetByIdWithIncludesAsync(aircraftId, null))
            .ReturnsAsync(aircraft);

        _usersApiMock
            .Setup(u => u.GetCompanyByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyBasicDto(companyId, "Test Aviation"));

        // Act
        var result = await _sut.GetByIdAsync(aircraftId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(aircraftId);
        result.RegistrationNumber.Should().Be("ES-ABC");
        result.Make.Should().Be("Cessna");
        result.Model.Should().Be("172 Skyhawk");
        result.IsInsured.Should().BeTrue();
        result.Status.Should().Be("Available");
        result.CompanyName.Should().Be("Test Aviation");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistent_ReturnsNull()
    {
        _aircraftRepoMock
            .Setup(r => r.GetByIdWithIncludesAsync(It.IsAny<Guid>(), It.IsAny<Guid?>()))
            .ReturnsAsync((Aircraft?)null);

        var result = await _sut.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    // ---- SearchAsync ----

    [Fact]
    public async Task SearchAsync_ReturnsFilteredResults()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var aircraft1 = CreateTestAircraft(companyId: companyId);
        aircraft1.Make = new LangStr("Cessna");
        var aircraft2 = CreateTestAircraft(companyId: companyId);
        aircraft2.Make = new LangStr("Piper");

        _aircraftRepoMock
            .Setup(r => r.SearchAsync("Cessna", null, null, null, null, null, true, 1, 20))
            .ReturnsAsync(new List<Aircraft> { aircraft1 });

        // Act
        var search = new AircraftSearchDto { Make = "Cessna", Page = 1, PageSize = 20 };
        var results = await _sut.SearchAsync(search);

        // Assert
        results.Should().HaveCount(1);
        results.First().Make.Should().Be("Cessna");
    }

    [Fact]
    public async Task SearchAsync_WithDateRange_ExcludesUnavailableAircraft()
    {
        // Arrange — aircraft without insurance should be excluded
        var aircraft = CreateTestAircraft();
        aircraft.InsurancePolicies = new List<InsurancePolicy>(); // No insurance

        var start = DateTime.UtcNow.AddDays(1);
        var end = DateTime.UtcNow.AddDays(2);

        _aircraftRepoMock
            .Setup(r => r.SearchAsync(null, null, null, null, null, null, true, 1, 20))
            .ReturnsAsync(new List<Aircraft> { aircraft });

        var search = new AircraftSearchDto
        {
            StartDate = start,
            EndDate = end,
            Page = 1,
            PageSize = 20
        };

        // Act
        var results = await _sut.SearchAsync(search);

        // Assert — no active insurance => excluded
        results.Should().BeEmpty();
    }

    // ---- DeleteAsync / RestoreAsync ----

    [Fact]
    public async Task DeleteAsync_ExistingAircraft_SoftDeletes()
    {
        // Arrange
        var aircraft = CreateTestAircraft();
        var companyId = aircraft.CompanyId;

        _aircraftRepoMock
            .Setup(r => r.GetByIdIgnoreFiltersTrackingAsync(aircraft.Id, companyId))
            .ReturnsAsync(aircraft);

        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        await _sut.DeleteAsync(aircraft.Id, companyId, "admin@test.com");

        // Assert
        aircraft.IsDeleted.Should().BeTrue();
        aircraft.DeletedBy.Should().Be("admin@test.com");
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_NonExistentAircraft_ThrowsInvalidOperation()
    {
        _aircraftRepoMock
            .Setup(r => r.GetByIdIgnoreFiltersTrackingAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync((Aircraft?)null);

        var act = () => _sut.DeleteAsync(Guid.NewGuid(), Guid.NewGuid(), "admin@test.com");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ---- Status computation ----

    [Fact]
    public async Task GetByIdAsync_AircraftWithoutInsurance_StatusIsInsuranceInactive()
    {
        var aircraft = CreateTestAircraft();
        aircraft.InsurancePolicies = new List<InsurancePolicy>(); // no active insurance

        _aircraftRepoMock
            .Setup(r => r.GetByIdWithIncludesAsync(aircraft.Id, null))
            .ReturnsAsync(aircraft);

        _usersApiMock
            .Setup(u => u.GetCompanyByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyBasicDto(aircraft.CompanyId, "Test"));

        var result = await _sut.GetByIdAsync(aircraft.Id);

        result!.Status.Should().Be("InsuranceInactive");
        result.IsInsured.Should().BeFalse();
    }
}
