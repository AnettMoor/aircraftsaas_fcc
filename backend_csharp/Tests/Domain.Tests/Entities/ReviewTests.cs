using App.Domain.Entities;
using FluentAssertions;

namespace Domain.Tests.Entities;

public class ReviewTests
{
    [Fact]
    public void AppUserId_MapsToAuthorId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var review = new Review
        {
            AuthorId = userId,
            BookingId = Guid.NewGuid(),
            AircraftId = Guid.NewGuid(),
            Rating = 5,
            Comment = "Excellent flight"
        };

        // Act & Assert
        review.AppUserId.Should().Be(userId);
    }

    [Fact]
    public void SetAppUserId_SetsAuthorId()
    {
        // Arrange
        var review = new Review();
        var userId = Guid.NewGuid();

        // Act
        review.AppUserId = userId;

        // Assert
        review.AuthorId.Should().Be(userId);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Rating_AcceptsValidValues(int rating)
    {
        // Arrange & Act
        var review = new Review
        {
            AuthorId = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            AircraftId = Guid.NewGuid(),
            Rating = rating,
            Comment = "Test"
        };

        // Assert
        review.Rating.Should().Be(rating);
    }

    [Fact]
    public void SoftDelete_SetsDeletedFields()
    {
        // Arrange
        var review = new Review
        {
            AuthorId = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            AircraftId = Guid.NewGuid(),
            Rating = 4,
            Comment = "Good"
        };

        // Act
        review.SoftDelete("moderator@test.com");

        // Assert
        review.IsDeleted.Should().BeTrue();
        review.DeletedBy.Should().Be("moderator@test.com");
    }

    [Fact]
    public void Restore_ClearsDeletedFields()
    {
        // Arrange
        var review = new Review
        {
            AuthorId = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            AircraftId = Guid.NewGuid(),
            Rating = 3,
            Comment = "Average"
        };
        review.SoftDelete("admin@test.com");

        // Act
        review.Restore();

        // Assert
        review.IsDeleted.Should().BeFalse();
        review.DeletedAt.Should().BeNull();
        review.DeletedBy.Should().BeNull();
    }
}
