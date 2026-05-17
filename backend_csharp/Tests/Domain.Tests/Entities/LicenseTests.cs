using App.Domain.Entities;
using Base.Domain;
using FluentAssertions;

namespace Domain.Tests.Entities;

public class LicenseTests
{
    [Fact]
    public void IsValid_ExpiryDateInFuture_ReturnsTrue()
    {
        // Arrange
        var license = new License
        {
            AppUserId = Guid.NewGuid(),
            LicenseNumber = "PPL-12345",
            LicenseType = new LangStr("PPL"),
            IssueDate = DateTime.UtcNow.AddYears(-1),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            IssuingAuthority = new LangStr("EASA")
        };

        // Act & Assert
        license.IsValid.Should().BeTrue();
    }

    [Fact]
    public void IsValid_ExpiryDateInPast_ReturnsFalse()
    {
        // Arrange
        var license = new License
        {
            AppUserId = Guid.NewGuid(),
            LicenseNumber = "PPL-12345",
            LicenseType = new LangStr("PPL"),
            IssueDate = DateTime.UtcNow.AddYears(-3),
            ExpiryDate = DateTime.UtcNow.AddDays(-1),
            IssuingAuthority = new LangStr("EASA")
        };

        // Act & Assert
        license.IsValid.Should().BeFalse();
    }

    [Fact]
    public void SoftDelete_ValidActor_SetsDeletedFields()
    {
        var license = new License
        {
            LicenseNumber = "PPL-99999",
            LicenseType = new LangStr("CPL"),
            IssueDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddYears(2),
            IssuingAuthority = new LangStr("FAA")
        };

        license.SoftDelete("admin@test.com");

        license.IsDeleted.Should().BeTrue();
        license.DeletedBy.Should().Be("admin@test.com");
    }

    [Fact]
    public void Restore_PreviouslyDeleted_ClearsDeletedFields()
    {
        var license = new License
        {
            LicenseNumber = "CPL-11111",
            LicenseType = new LangStr("ATPL"),
            IssueDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddYears(5),
            IssuingAuthority = new LangStr("ECAA")
        };

        license.SoftDelete("admin@test.com");
        license.Restore();

        license.IsDeleted.Should().BeFalse();
        license.DeletedAt.Should().BeNull();
        license.DeletedBy.Should().BeNull();
    }
}
