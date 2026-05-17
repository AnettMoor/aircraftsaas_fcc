namespace App.Application.DTOs;

public class CompanyDto
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

public class CreateCompanyDto
{
    public string CompanyName { get; set; } = default!;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}

public class UpdateCompanyDto
{
    public Guid Id { get; set; }
    public string CompanyName { get; set; } = default!;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}
