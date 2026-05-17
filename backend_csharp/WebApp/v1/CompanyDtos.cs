using System.ComponentModel.DataAnnotations;

namespace WebApp.v1;

public class CompanyResponse
{
    public Guid Id { get; set; }
    public string CompanyName { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public bool IsActive { get; set; }
    public int MaxUsers { get; set; }
    public int MaxAircraft { get; set; }
    public int MaxBookingsPerMonth { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public int CurrentUserCount { get; set; }
    public int CurrentAircraftCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateCompanyRequest
{
    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string CompanyName { get; set; } = default!;

    [StringLength(500)]
    public string? Address { get; set; }

    [Phone]
    [StringLength(20)]
    public string? Phone { get; set; }

    [EmailAddress]
    [StringLength(254)]
    public string? Email { get; set; }
}

public class UpdateCompanyRequest
{
    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string CompanyName { get; set; } = default!;

    [StringLength(500)]
    public string? Address { get; set; }

    [Phone]
    [StringLength(20)]
    public string? Phone { get; set; }

    [EmailAddress]
    [StringLength(254)]
    public string? Email { get; set; }
}
