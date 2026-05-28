using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Fleet.Application.Contracts;
using Fleet.Application.DTOs;
using Fleet.Application.Services;
using Fleet.Domain.Entities;
using Shared.Kernel.Domain;

namespace Fleet.Tests.Services;

public class MaintenanceServiceTests
{
    private readonly Mock<IFleetUOW> _uowMock;
    private readonly Mock<IAircraftRepository> _aircraftRepoMock;
    private readonly Mock<IMaintenanceRecordRepository> _maintenanceRepoMock;
    private readonly Mock<IAircraftAvailabilityRepository> _availRepoMock;
    private readonly MaintenanceService _sut;

    public MaintenanceServiceTests()
    {
        _uowMock = new Mock<IFleetUOW>();
        _aircraftRepoMock = new Mock<IAircraftRepository>();
        _maintenanceRepoMock = new Mock<IMaintenanceRecordRepository>();
        _availRepoMock = new Mock<IAircraftAvailabilityRepository>();

        _uowMock.Setup(u => u.AircraftRepository).Returns(_aircraftRepoMock.Object);
        _uowMock.Setup(u => u.MaintenanceRecordRepository).Returns(_maintenanceRepoMock.Object);
        _uowMock.Setup(u => u.AircraftAvailabilityRepository).Returns(_availRepoMock.Object);

        _sut = new MaintenanceService(_uowMock.Object, NullLogger<MaintenanceService>.Instance);
    }

    private static Aircraft CreateTestAircraft(Guid? id = null, Guid? companyId = null)
    {
        return new Aircraft
        {
            Id = id ?? Guid.NewGuid(),
            RegistrationNumber = "ES-MNT",
            Make = new LangStr("Cessna"),
            Model = new LangStr("172"),
            Year = 2020,
            Category = new LangStr("SingleEngine"),
            RequiredLicenseType = "PPL",
            HourlyRate = 100m,
            BaseAirportId = Guid.NewGuid(),
            Description = new LangStr("Test"),
            CompanyId = companyId ?? Guid.NewGuid(),
            CreatedBy = "admin"
        };
    }

    // ---- CreateAsync ----

    [Fact]
    public async Task CreateAsync_ValidDto_CreatesMaintenanceRecord()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var aircraft = CreateTestAircraft(companyId: companyId);
        var dto = new CreateMaintenanceRecordDto
        {
            AircraftId = aircraft.Id,
            MaintenanceDate = DateTime.UtcNow,
            MaintenanceType = "Annual Inspection",
            Description = "Full annual",
            PerformedBy = "AMO Tech",
            AirframeHoursAtMaintenance = 500,
            Cost = 2500.00m,
            IsCompleted = false
        };

        _aircraftRepoMock
            .Setup(r => r.GetByIdForCompanyTrackingAsync(aircraft.Id, companyId))
            .ReturnsAsync(aircraft);

        _maintenanceRepoMock.Setup(r => r.Add(It.IsAny<MaintenanceRecord>()));
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // After create, reload returns the record
        _maintenanceRepoMock
            .Setup(r => r.GetByIdForCompanyAsync(It.IsAny<Guid>(), companyId))
            .ReturnsAsync(new MaintenanceRecord
            {
                Id = Guid.NewGuid(),
                AircraftId = aircraft.Id,
                Aircraft = aircraft,
                MaintenanceDate = dto.MaintenanceDate,
                MaintenanceType = new LangStr(dto.MaintenanceType),
                Description = new LangStr(dto.Description!),
                PerformedBy = dto.PerformedBy!,
                AirframeHoursAtMaintenance = dto.AirframeHoursAtMaintenance,
                Cost = dto.Cost,
                IsCompleted = dto.IsCompleted,
                CreatedBy = "admin",
                CreatedAt = DateTime.UtcNow
            });

        // Act
        var result = await _sut.CreateAsync(dto, companyId, "admin@test.com");

        // Assert
        result.Should().NotBeNull();
        result.MaintenanceType.Should().Be("Annual Inspection");
        result.PerformedBy.Should().Be("AMO Tech");
        result.Cost.Should().Be(2500.00m);
        result.IsCompleted.Should().BeFalse();

        _maintenanceRepoMock.Verify(r => r.Add(It.IsAny<MaintenanceRecord>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task CreateAsync_WithDateRange_CreatesAvailabilityBlock()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var aircraft = CreateTestAircraft(companyId: companyId);
        var startDate = DateTime.UtcNow.AddDays(5);
        var endDate = DateTime.UtcNow.AddDays(7);
        var dto = new CreateMaintenanceRecordDto
        {
            AircraftId = aircraft.Id,
            MaintenanceDate = startDate,
            StartDate = startDate,
            EndDate = endDate,
            MaintenanceType = "100hr",
            Description = "100 hour check",
            PerformedBy = "Mechanic",
            AirframeHoursAtMaintenance = 1000,
            Cost = 3500.00m,
            IsCompleted = false
        };

        _aircraftRepoMock
            .Setup(r => r.GetByIdForCompanyTrackingAsync(aircraft.Id, companyId))
            .ReturnsAsync(aircraft);

        _maintenanceRepoMock.Setup(r => r.Add(It.IsAny<MaintenanceRecord>()));
        _availRepoMock.Setup(r => r.Add(It.IsAny<AircraftAvailability>()));
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        _maintenanceRepoMock
            .Setup(r => r.GetByIdForCompanyAsync(It.IsAny<Guid>(), companyId))
            .ReturnsAsync(new MaintenanceRecord
            {
                Id = Guid.NewGuid(),
                AircraftId = aircraft.Id,
                Aircraft = aircraft,
                MaintenanceDate = startDate,
                StartDate = startDate,
                EndDate = endDate,
                MaintenanceType = new LangStr("100hr"),
                Description = new LangStr("100 hour check"),
                PerformedBy = "Mechanic",
                AirframeHoursAtMaintenance = 1000,
                Cost = 3500.00m,
                IsCompleted = false,
                CreatedBy = "admin",
                CreatedAt = DateTime.UtcNow
            });

        // Act
        var result = await _sut.CreateAsync(dto, companyId, "admin@test.com");

        // Assert — availability block should be created for maintenance timeframe
        _availRepoMock.Verify(r => r.Add(It.Is<AircraftAvailability>(a =>
            a.AircraftId == aircraft.Id &&
            a.AvailabilityType == "Maintenance"
        )), Times.Once);

        // SaveChanges called at least twice: once for record, once for availability block
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.AtLeast(2));
    }

    [Fact]
    public async Task CreateAsync_AircraftNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        _aircraftRepoMock
            .Setup(r => r.GetByIdForCompanyTrackingAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync((Aircraft?)null);

        var dto = new CreateMaintenanceRecordDto
        {
            AircraftId = Guid.NewGuid(),
            MaintenanceDate = DateTime.UtcNow,
            MaintenanceType = "Annual"
        };

        // Act
        var act = () => _sut.CreateAsync(dto, Guid.NewGuid(), "admin@test.com");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Aircraft not found*");
    }

    [Fact]
    public async Task CreateAsync_InvalidDateRange_ThrowsInvalidOperationException()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var aircraft = CreateTestAircraft(companyId: companyId);

        _aircraftRepoMock
            .Setup(r => r.GetByIdForCompanyTrackingAsync(aircraft.Id, companyId))
            .ReturnsAsync(aircraft);

        var dto = new CreateMaintenanceRecordDto
        {
            AircraftId = aircraft.Id,
            MaintenanceDate = DateTime.UtcNow,
            StartDate = DateTime.UtcNow.AddDays(5),
            EndDate = DateTime.UtcNow.AddDays(2), // End before start!
            MaintenanceType = "Repair"
        };

        // Act
        var act = () => _sut.CreateAsync(dto, companyId, "admin@test.com");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Start date must be before end date*");
    }

    // ---- GetByIdAsync ----

    [Fact]
    public async Task GetByIdAsync_ExistingRecord_ReturnsMappedDto()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var aircraft = CreateTestAircraft(companyId: companyId);
        var record = new MaintenanceRecord
        {
            Id = Guid.NewGuid(),
            AircraftId = aircraft.Id,
            Aircraft = aircraft,
            MaintenanceDate = DateTime.UtcNow,
            MaintenanceType = new LangStr("Annual Inspection"),
            Description = new LangStr("Full check"),
            PerformedBy = "Technician",
            AirframeHoursAtMaintenance = 750,
            Cost = 1500m,
            IsCompleted = true,
            CreatedBy = "admin",
            CreatedAt = DateTime.UtcNow
        };

        _maintenanceRepoMock
            .Setup(r => r.GetByIdForCompanyAsync(record.Id, companyId))
            .ReturnsAsync(record);

        // Act
        var result = await _sut.GetByIdAsync(record.Id, companyId);

        // Assert
        result.Should().NotBeNull();
        result!.MaintenanceType.Should().Be("Annual Inspection");
        result.Cost.Should().Be(1500m);
        result.IsCompleted.Should().BeTrue();
        result.AircraftName.Should().Contain("ES-MNT");
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNull()
    {
        _maintenanceRepoMock
            .Setup(r => r.GetByIdForCompanyAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync((MaintenanceRecord?)null);

        var result = await _sut.GetByIdAsync(Guid.NewGuid(), Guid.NewGuid());

        result.Should().BeNull();
    }
}
