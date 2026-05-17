using App.Domain.Entities;
using Base.Domain;
using FluentAssertions;

namespace Domain.Tests.Entities;

public class MaintenanceRecordTests
{
    [Fact]
    public void SoftDelete_SetsDeletedFields()
    {
        // Arrange
        var record = new MaintenanceRecord
        {
            AircraftId = Guid.NewGuid(),
            MaintenanceDate = DateTime.UtcNow,
            MaintenanceType = new LangStr("Annual"),
            Description = new LangStr("Annual inspection"),
            PerformedBy = "Mechanic A",
            AirframeHoursAtMaintenance = 1200,
            Cost = 5000m
        };

        // Act
        record.SoftDelete("admin@test.com");

        // Assert
        record.IsDeleted.Should().BeTrue();
        record.DeletedBy.Should().Be("admin@test.com");
        record.DeletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Restore_ClearsDeletedFields()
    {
        // Arrange
        var record = new MaintenanceRecord
        {
            AircraftId = Guid.NewGuid(),
            MaintenanceDate = DateTime.UtcNow,
            MaintenanceType = new LangStr("Repair"),
            Description = new LangStr("Engine repair"),
            PerformedBy = "Mechanic B",
            AirframeHoursAtMaintenance = 800,
            Cost = 3000m
        };
        record.SoftDelete("admin@test.com");

        // Act
        record.Restore();

        // Assert
        record.IsDeleted.Should().BeFalse();
        record.DeletedAt.Should().BeNull();
        record.DeletedBy.Should().BeNull();
    }

    [Fact]
    public void MaintenanceRecord_StartAndEndDates_CanDefineMaintenanceBlock()
    {
        // Arrange
        var startDate = DateTime.UtcNow;
        var endDate = startDate.AddDays(3);

        // Act
        var record = new MaintenanceRecord
        {
            AircraftId = Guid.NewGuid(),
            MaintenanceDate = startDate,
            MaintenanceType = new LangStr("100hr"),
            Description = new LangStr("100-hour inspection"),
            PerformedBy = "Mechanic C",
            AirframeHoursAtMaintenance = 500,
            Cost = 2000m,
            StartDate = startDate,
            EndDate = endDate,
            IsCompleted = false
        };

        // Assert
        record.StartDate.Should().Be(startDate);
        record.EndDate.Should().Be(endDate);
        record.IsCompleted.Should().BeFalse();
    }
}
