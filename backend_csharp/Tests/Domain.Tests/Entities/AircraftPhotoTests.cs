using App.Domain.Entities;
using FluentAssertions;

namespace Domain.Tests.Entities;

public class AircraftPhotoTests
{
    [Fact]
    public void Url_ReturnsImageUrl()
    {
        // Arrange
        var photo = new AircraftPhoto
        {
            AircraftId = Guid.NewGuid(),
            ImageUrl = "https://example.com/photo.jpg",
            IsPrimary = true,
            DisplayOrder = 1
        };

        // Act & Assert
        photo.Url.Should().Be("https://example.com/photo.jpg");
    }

    [Fact]
    public void Url_ReflectsImageUrlChanges()
    {
        // Arrange
        var photo = new AircraftPhoto
        {
            AircraftId = Guid.NewGuid(),
            ImageUrl = "https://example.com/original.jpg",
            IsPrimary = false,
            DisplayOrder = 2
        };

        // Act
        photo.ImageUrl = "https://example.com/updated.jpg";

        // Assert
        photo.Url.Should().Be("https://example.com/updated.jpg");
    }

    [Fact]
    public void SoftDelete_SetsDeletedFields()
    {
        // Arrange
        var photo = new AircraftPhoto
        {
            AircraftId = Guid.NewGuid(),
            ImageUrl = "https://example.com/photo.jpg",
            IsPrimary = true,
            DisplayOrder = 1
        };

        // Act
        photo.SoftDelete("admin@test.com");

        // Assert
        photo.IsDeleted.Should().BeTrue();
        photo.DeletedBy.Should().Be("admin@test.com");
    }

    [Fact]
    public void Restore_ClearsDeletedFields()
    {
        // Arrange
        var photo = new AircraftPhoto
        {
            AircraftId = Guid.NewGuid(),
            ImageUrl = "https://example.com/photo.jpg",
            IsPrimary = false,
            DisplayOrder = 3
        };
        photo.SoftDelete("admin@test.com");

        // Act
        photo.Restore();

        // Assert
        photo.IsDeleted.Should().BeFalse();
        photo.DeletedAt.Should().BeNull();
        photo.DeletedBy.Should().BeNull();
    }
}
