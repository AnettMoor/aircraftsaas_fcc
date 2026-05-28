using Users.Domain.Entities;
using Shared.Kernel.Domain;
using FluentAssertions;

namespace Users.Tests.Entities;

public class CompanyTests
{
    private Company CreateCompany() => new()
    {
        CompanyName = new LangStr("Test Aviation Co"),
        Slug = "test-aviation-co",
        IsActive = true
    };

    [Fact]
    public void SoftDelete_SetsIsActiveToFalse()
    {
        // Arrange
        var company = CreateCompany();

        // Act
        company.SoftDelete("admin@test.com");

        // Assert
        company.IsActive.Should().BeFalse();
        company.IsDeleted.Should().BeTrue();
        company.DeletedBy.Should().Be("admin@test.com");
        company.DeletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Restore_SetsIsActiveToTrue()
    {
        // Arrange
        var company = CreateCompany();
        company.SoftDelete("admin@test.com");

        // Act
        company.Restore();

        // Assert
        company.IsActive.Should().BeTrue();
        company.IsDeleted.Should().BeFalse();
        company.DeletedAt.Should().BeNull();
        company.DeletedBy.Should().BeNull();
    }

    [Fact]
    public void DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var company = new Company
        {
            CompanyName = new LangStr("NewCo"),
            Slug = "newco"
        };

        // Assert
        company.MaxUsers.Should().Be(2);
        company.MaxAircraft.Should().Be(3);
        company.MaxBookingsPerMonth.Should().Be(20);
        company.IsActive.Should().BeTrue();
    }

    [Fact]
    public void SoftDelete_SetsTimestampAndActor()
    {
        // Arrange
        var company = CreateCompany();
        var before = DateTime.UtcNow;

        // Act
        company.SoftDelete("operator@test.com");

        // Assert
        company.DeletedAt.Should().BeOnOrAfter(before);
        company.DeletedBy.Should().Be("operator@test.com");
    }
}
