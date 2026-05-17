namespace App.Application.DTOs;

public class AirportDto
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

public class CreateAirportDto
{
    public string IcaoCode { get; set; } = default!;
    public string IataCode { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string City { get; set; } = default!;
    public string Country { get; set; } = default!;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Elevation { get; set; }
}

public class UpdateAirportDto
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
