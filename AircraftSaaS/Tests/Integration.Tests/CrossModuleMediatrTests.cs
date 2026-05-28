using Fleet.Application.Contracts;
using Fleet.Application.Handlers;
using Fleet.Application.InternalCommands;
using Fleet.Application.InternalQueries;
using Fleet.Domain.Entities;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.Contracts.Fleet.DTOs;
using Shared.Contracts.Users.DTOs;
using Shared.Kernel.Domain;
using Users.Application.Contracts;
using Users.Application.InternalQueries;

namespace Integration.Tests;

/// <summary>
/// Cross-module integration tests.
/// These tests verify that MediatR handlers in each module correctly respond to
/// internal queries/commands that back the module API contracts.
///
/// Instead of wiring up a real MediatR pipeline, we test each handler in isolation
/// with mocked UOW dependencies, proving that the handler contracts are correct
/// and the mapping logic works as expected.
///
/// This is the glue that binds the modules — if a handler breaks its contract,
/// other modules will receive unexpected data at runtime via the Module API.
/// </summary>
public class CrossModuleMediatrTests
{
    #region Fleet → Users: GetAircraftByIdInternalQuery

    [Fact]
    public async Task GetAircraftByIdHandler_ExistingAircraft_ReturnsMappedDto()
    {
        // Arrange
        var aircraftId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var uowMock = new Mock<IFleetUOW>();
        var repoMock = new Mock<IAircraftRepository>();

        var aircraft = new Aircraft
        {
            RegistrationNumber = "ES-TCA",
            Make = new LangStr("Cessna"),
            Model = new LangStr("172S"),
            Year = 2019,
            Category = new LangStr("SingleEngine"),
            RequiredLicenseType = "PPL",
            HourlyRate = 120m,
            CompanyId = companyId,
            BaseAirportId = Guid.NewGuid(),
            Description = new LangStr("Training aircraft")
        };

        typeof(BaseEntity).GetProperty("Id")!.SetValue(aircraft, aircraftId);

        repoMock.Setup(r => r.FindAsync(aircraftId, It.IsAny<Guid>(), It.IsAny<Guid?>()))
            .ReturnsAsync(aircraft);

        uowMock.Setup(u => u.AircraftRepository).Returns(repoMock.Object);

        var handler = new GetAircraftByIdHandler(uowMock.Object);

        // Act
        var result = await handler.Handle(new GetAircraftByIdInternalQuery(aircraftId), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(aircraftId);
        result.Registration.Should().Be("ES-TCA");
        result.Model.Should().Be("172S");
        result.CompanyId.Should().Be(companyId);
        result.RequiredLicenseType.Should().Be("PPL");
    }

    [Fact]
    public async Task GetAircraftByIdHandler_NonExistentAircraft_ReturnsNull()
    {
        // Arrange
        var uowMock = new Mock<IFleetUOW>();
        var repoMock = new Mock<IAircraftRepository>();

        repoMock.Setup(r => r.FindAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid?>()))
            .ReturnsAsync((Aircraft?)null);

        uowMock.Setup(u => u.AircraftRepository).Returns(repoMock.Object);

        var handler = new GetAircraftByIdHandler(uowMock.Object);

        // Act
        var result = await handler.Handle(new GetAircraftByIdInternalQuery(Guid.NewGuid()), CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Fleet → Booking: CheckAircraftAvailabilityInternalQuery

    [Fact]
    public async Task CheckAircraftAvailabilityHandler_NoBlockingRecords_ReturnsTrue()
    {
        // Arrange
        var aircraftId = Guid.NewGuid();
        var start = DateTime.UtcNow.AddDays(1);
        var end = DateTime.UtcNow.AddDays(2);

        var uowMock = new Mock<IFleetUOW>();
        var availRepoMock = new Mock<IAircraftAvailabilityRepository>();

        availRepoMock.Setup(r => r.HasBlockingAvailabilityAsync(aircraftId, start, end))
            .ReturnsAsync(false); // No blocking records

        uowMock.Setup(u => u.AircraftAvailabilityRepository).Returns(availRepoMock.Object);

        var handler = new CheckAircraftAvailabilityHandler(uowMock.Object);

        // Act
        var result = await handler.Handle(
            new CheckAircraftAvailabilityInternalQuery(aircraftId, start, end), CancellationToken.None);

        // Assert — available when there are no blocking records
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CheckAircraftAvailabilityHandler_HasBlockingRecords_ReturnsFalse()
    {
        // Arrange
        var aircraftId = Guid.NewGuid();
        var start = DateTime.UtcNow.AddDays(1);
        var end = DateTime.UtcNow.AddDays(2);

        var uowMock = new Mock<IFleetUOW>();
        var availRepoMock = new Mock<IAircraftAvailabilityRepository>();

        availRepoMock.Setup(r => r.HasBlockingAvailabilityAsync(aircraftId, start, end))
            .ReturnsAsync(true); // Has blocking records (maintenance, existing booking)

        uowMock.Setup(u => u.AircraftAvailabilityRepository).Returns(availRepoMock.Object);

        var handler = new CheckAircraftAvailabilityHandler(uowMock.Object);

        // Act
        var result = await handler.Handle(
            new CheckAircraftAvailabilityInternalQuery(aircraftId, start, end), CancellationToken.None);

        // Assert — not available when there are blocking records
        result.Should().BeFalse();
    }

    #endregion

    #region Users → Booking: CheckUserLicenseInternalQuery

    [Fact]
    public async Task CheckUserLicenseHandler_UserHasValidLicense_ReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var uowMock = new Mock<IUsersUOW>();
        var licRepoMock = new Mock<ILicenseRepository>();

        licRepoMock.Setup(r => r.HasValidLicenseForTypeAsync(userId, "PPL", It.IsAny<DateTime>()))
            .ReturnsAsync(true);

        uowMock.Setup(u => u.LicenseRepository).Returns(licRepoMock.Object);

        var handler = new Users.Application.Handlers.CheckUserLicenseHandler(uowMock.Object);

        // Act
        var result = await handler.Handle(
            new CheckUserLicenseInternalQuery(userId, "PPL", DateTime.UtcNow), CancellationToken.None);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CheckUserLicenseHandler_UserHasNoValidLicense_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var uowMock = new Mock<IUsersUOW>();
        var licRepoMock = new Mock<ILicenseRepository>();

        licRepoMock.Setup(r => r.HasValidLicenseForTypeAsync(userId, "CPL", It.IsAny<DateTime>()))
            .ReturnsAsync(false);

        uowMock.Setup(u => u.LicenseRepository).Returns(licRepoMock.Object);

        var handler = new Users.Application.Handlers.CheckUserLicenseHandler(uowMock.Object);

        // Act
        var result = await handler.Handle(
            new CheckUserLicenseInternalQuery(userId, "CPL", DateTime.UtcNow), CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Fleet → Booking: BlockAircraftAvailabilityInternalCommand

    [Fact]
    public async Task BlockAircraftAvailabilityHandler_CreatesAvailabilityRecord()
    {
        // Arrange
        var aircraftId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var start = DateTime.UtcNow.AddDays(3);
        var end = DateTime.UtcNow.AddDays(4);

        var uowMock = new Mock<IFleetUOW>();
        var availRepoMock = new Mock<IAircraftAvailabilityRepository>();
        var loggerMock = new Mock<ILogger<BlockAircraftAvailabilityHandler>>();
        AircraftAvailability? capturedEntity = null;

        availRepoMock.Setup(r => r.Add(It.IsAny<AircraftAvailability>()))
            .Callback<AircraftAvailability>(e => capturedEntity = e);

        uowMock.Setup(u => u.AircraftAvailabilityRepository).Returns(availRepoMock.Object);
        uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var handler = new BlockAircraftAvailabilityHandler(uowMock.Object, loggerMock.Object);

        var command = new BlockAircraftAvailabilityInternalCommand(
            aircraftId, bookingId, start, end, "Booked", "Booked by pilot");

        // Act
        var resultId = await handler.Handle(command, CancellationToken.None);

        // Assert
        capturedEntity.Should().NotBeNull();
        capturedEntity!.AircraftId.Should().Be(aircraftId);
        capturedEntity.BookingId.Should().Be(bookingId);
        capturedEntity.StartDateTime.Should().Be(start);
        capturedEntity.EndDateTime.Should().Be(end);

        uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    #endregion

    #region Contract compatibility — ensures query/response types are compatible

    [Fact]
    public void AircraftBasicDto_HasExpectedProperties()
    {
        // This test validates the cross-module DTO contract hasn't drifted.
        // If a property is renamed or removed, this test will catch it at compile time.
        var dto = new AircraftBasicDto(
            Guid.NewGuid(), "ES-TCA", "Cessna 172", Guid.NewGuid(), "PPL");

        dto.Id.Should().NotBeEmpty();
        dto.Registration.Should().Be("ES-TCA");
        dto.Model.Should().Be("Cessna 172");
        dto.CompanyId.Should().NotBeEmpty();
        dto.RequiredLicenseType.Should().Be("PPL");
    }

    [Fact]
    public void UserBasicDto_HasExpectedProperties()
    {
        var dto = new UserBasicDto(
            Guid.NewGuid(), "pilot@test.com", "Jane", "Smith");

        dto.Id.Should().NotBeEmpty();
        dto.Email.Should().Be("pilot@test.com");
        dto.FirstName.Should().Be("Jane");
        dto.LastName.Should().Be("Smith");
    }

    [Fact]
    public void CheckAircraftAvailabilityInternalQuery_CarriesCorrectData()
    {
        var aircraftId = Guid.NewGuid();
        var start = DateTime.UtcNow;
        var end = DateTime.UtcNow.AddHours(3);

        var query = new CheckAircraftAvailabilityInternalQuery(aircraftId, start, end);

        query.AircraftId.Should().Be(aircraftId);
        query.StartDateTime.Should().Be(start);
        query.EndDateTime.Should().Be(end);
    }

    [Fact]
    public void CheckUserLicenseInternalQuery_CarriesCorrectData()
    {
        var userId = Guid.NewGuid();
        var date = DateTime.UtcNow;

        var query = new CheckUserLicenseInternalQuery(userId, "ATPL", date);

        query.UserId.Should().Be(userId);
        query.RequiredLicenseType.Should().Be("ATPL");
        query.AsOfDate.Should().Be(date);
    }

    #endregion
}
