using Fleet.Domain.Entities;
using Shared.Kernel.Domain;
using FluentAssertions;

namespace Fleet.Tests.Entities;

public class InsurancePolicyTests
{
    [Fact]
    public void IsActive_CurrentDateInRange_ReturnsTrue()
    {
        var policy = new InsurancePolicy
        {
            AircraftId = Guid.NewGuid(),
            PolicyNumber = "POL-001",
            InsuranceProvider = new LangStr("TestInsurer"),
            StartDate = DateTime.UtcNow.AddDays(-30),
            EndDate = DateTime.UtcNow.AddDays(30),
            CoverageAmount = 500_000m,
            CoverageType = new LangStr("Comprehensive")
        };

        policy.IsActive.Should().BeTrue();
    }

    [Fact]
    public void IsActive_PolicyExpired_ReturnsFalse()
    {
        var policy = new InsurancePolicy
        {
            AircraftId = Guid.NewGuid(),
            PolicyNumber = "POL-002",
            InsuranceProvider = new LangStr("TestInsurer"),
            StartDate = DateTime.UtcNow.AddDays(-60),
            EndDate = DateTime.UtcNow.AddDays(-1),
            CoverageAmount = 500_000m,
            CoverageType = new LangStr("Liability")
        };

        policy.IsActive.Should().BeFalse();
    }

    [Fact]
    public void IsActive_PolicyNotYetStarted_ReturnsFalse()
    {
        var policy = new InsurancePolicy
        {
            AircraftId = Guid.NewGuid(),
            PolicyNumber = "POL-003",
            InsuranceProvider = new LangStr("TestInsurer"),
            StartDate = DateTime.UtcNow.AddDays(10),
            EndDate = DateTime.UtcNow.AddDays(40),
            CoverageAmount = 500_000m,
            CoverageType = new LangStr("Hull")
        };

        policy.IsActive.Should().BeFalse();
    }

    [Fact]
    public void SoftDelete_ValidActor_SetsDeletedFields()
    {
        var policy = new InsurancePolicy
        {
            PolicyNumber = "POL-004",
            InsuranceProvider = new LangStr("TestInsurer"),
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddYears(1),
            CoverageAmount = 100_000m,
            CoverageType = new LangStr("Liability")
        };

        policy.SoftDelete("admin@test.com");

        policy.IsDeleted.Should().BeTrue();
        policy.DeletedBy.Should().Be("admin@test.com");
    }
}
