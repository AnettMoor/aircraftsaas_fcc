using App.Domain;
using App.Domain.Entities;
using FluentAssertions;

namespace Domain.Tests.Entities;

public class AuditLogTests
{
    [Fact]
    public void DefaultTimestamp_IsSetToApproximatelyUtcNow()
    {
        // Arrange & Act
        var auditLog = new AuditLog
        {
            TenantId = Guid.NewGuid(),
            EntityName = "Aircraft",
            EntityId = Guid.NewGuid(),
            Action = "Created",
            IpAddress = "127.0.0.1"
        };

        // Assert
        auditLog.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Properties_CanBeSetAndRetrieved()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var entityId = Guid.NewGuid();

        // Act
        var auditLog = new AuditLog
        {
            TenantId = tenantId,
            UserId = userId,
            EntityName = "Booking",
            EntityId = entityId,
            Action = "Updated",
            OldValues = "{\"Status\":\"Pending\"}",
            NewValues = "{\"Status\":\"Approved\"}",
            IpAddress = "192.168.1.1",
            Details = "Status changed"
        };

        // Assert
        auditLog.TenantId.Should().Be(tenantId);
        auditLog.UserId.Should().Be(userId);
        auditLog.EntityName.Should().Be("Booking");
        auditLog.EntityId.Should().Be(entityId);
        auditLog.Action.Should().Be("Updated");
        auditLog.OldValues.Should().Contain("Pending");
        auditLog.NewValues.Should().Contain("Approved");
    }

    [Fact]
    public void AuditLog_DoesNotImplementISoftDelete()
    {
        // AuditLog inherits from BaseEntity only, not ISoftDelete
        var auditLog = new AuditLog
        {
            TenantId = Guid.NewGuid(),
            EntityName = "Test",
            EntityId = Guid.NewGuid(),
            Action = "Created",
            IpAddress = "127.0.0.1"
        };

        auditLog.Should().NotBeAssignableTo<ISoftDelete>();
    }
}
