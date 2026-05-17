using System.ComponentModel.DataAnnotations;

namespace WebApp.v1;

public class AirportResponse
{
    public Guid Id { get; set; }
    public string IcaoCode { get; set; } = default!;
    public string IataCode { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string City { get; set; } = default!;
    public string Country { get; set; } = default!;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Elevation { get; set; }
}

public class CreateAirportRequest
{
    [Required]
    [StringLength(4, MinimumLength = 4)]
    public string IcaoCode { get; set; } = default!;

    [Required]
    [StringLength(3, MinimumLength = 3)]
    public string IataCode { get; set; } = default!;

    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = default!;

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string City { get; set; } = default!;

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Country { get; set; } = default!;

    [Range(-90.0, 90.0)]
    public double Latitude { get; set; }

    [Range(-180.0, 180.0)]
    public double Longitude { get; set; }

    [Range(-2000, 30000)]
    public int Elevation { get; set; }
}

public class UpdateAirportRequest
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    [StringLength(4, MinimumLength = 4)]
    public string IcaoCode { get; set; } = default!;

    [Required]
    [StringLength(3, MinimumLength = 3)]
    public string IataCode { get; set; } = default!;

    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = default!;

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string City { get; set; } = default!;

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Country { get; set; } = default!;

    [Range(-90.0, 90.0)]
    public double Latitude { get; set; }

    [Range(-180.0, 180.0)]
    public double Longitude { get; set; }

    [Range(-2000, 30000)]
    public int Elevation { get; set; }
}
