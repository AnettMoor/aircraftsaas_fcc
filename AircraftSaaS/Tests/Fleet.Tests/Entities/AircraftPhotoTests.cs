using Fleet.Domain.Entities;
using FluentAssertions;

namespace Fleet.Tests.Entities;

public class AircraftPhotoTests
{
    [Fact]
    public void Url_ReturnsImageUrl()
    {
        var photo = new AircraftPhoto
        {
            AircraftId = Guid.NewGuid(),
            ImageUrl = "https://example.com/photo.jpg",
            IsPrimary = true,
            DisplayOrder = 1
        };

        photo.Url.Should().Be("https://example.com/photo.jpg");
    }

    [Fact]
    public void Url_ReflectsImageUrlChanges()
    {
        var photo = new AircraftPhoto
        {
            AircraftId = Guid.NewGuid(),
            ImageUrl = "https://example.com/original.jpg",
            IsPrimary = false,
            DisplayOrder = 2
        };

        photo.ImageUrl = "https://example.com/updated.jpg";

        photo.Url.Should().Be("https://example.com/updated.jpg");
    }

    [Fact]
    public void SoftDelete_SetsDeletedFields()
    {
        var photo = new AircraftPhoto
        {
            AircraftId = Guid.NewGuid(),
            ImageUrl = "https://example.com/photo.jpg",
            IsPrimary = true,
            DisplayOrder = 1
        };

        photo.SoftDelete("admin@test.com");

        photo.IsDeleted.Should().BeTrue();
        photo.DeletedBy.Should().Be("admin@test.com");
    }

    [Fact]
    public void Restore_ClearsDeletedFields()
    {
        var photo = new AircraftPhoto
        {
            AircraftId = Guid.NewGuid(),
            ImageUrl = "https://example.com/photo.jpg",
            IsPrimary = false,
            DisplayOrder = 3
        };
        photo.SoftDelete("admin@test.com");

        photo.Restore();

        photo.IsDeleted.Should().BeFalse();
        photo.DeletedAt.Should().BeNull();
        photo.DeletedBy.Should().BeNull();
    }
}
