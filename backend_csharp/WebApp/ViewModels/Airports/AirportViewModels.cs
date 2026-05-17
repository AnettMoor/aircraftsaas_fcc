using System.ComponentModel.DataAnnotations;
using App.Application.DTOs;

namespace WebApp.ViewModels.Airports;

/// <summary>
/// Airport index/list view model
/// </summary>
public class AirportIndexViewModel
{
    public IEnumerable<AirportDto> Airports { get; set; } = new List<AirportDto>();
    public string? SearchTerm { get; set; }
}

/// <summary>
/// Airport details view model
/// </summary>
public class AirportDetailsViewModel
{
    public AirportDto Airport { get; set; } = default!;
}

/// <summary>
/// Airport create form view model
/// </summary>
public class AirportCreateViewModel
{
    [Required]
    [Display(Name = "ICAO Code")]
    public string IcaoCode { get; set; } = default!;

    [Required]
    [Display(Name = "IATA Code")]
    public string IataCode { get; set; } = default!;

    [Required]
    [Display(Name = "Airport Name")]
    public string Name { get; set; } = default!;

    [Required]
    public string City { get; set; } = default!;

    [Required]
    public string Country { get; set; } = default!;

    [Display(Name = "Latitude")]
    public double Latitude { get; set; }

    [Display(Name = "Longitude")]
    public double Longitude { get; set; }

    [Display(Name = "Elevation (ft)")]
    public int Elevation { get; set; }
}

/// <summary>
/// Airport edit form view model
/// </summary>
public class AirportEditViewModel
{
    public Guid Id { get; set; }

    [Required]
    [Display(Name = "ICAO Code")]
    public string IcaoCode { get; set; } = default!;

    [Required]
    [Display(Name = "IATA Code")]
    public string IataCode { get; set; } = default!;

    [Required]
    [Display(Name = "Airport Name")]
    public string Name { get; set; } = default!;

    [Required]
    public string City { get; set; } = default!;

    [Required]
    public string Country { get; set; } = default!;

    [Display(Name = "Latitude")]
    public double Latitude { get; set; }

    [Display(Name = "Longitude")]
    public double Longitude { get; set; }

    [Display(Name = "Elevation (ft)")]
    public int Elevation { get; set; }
}
