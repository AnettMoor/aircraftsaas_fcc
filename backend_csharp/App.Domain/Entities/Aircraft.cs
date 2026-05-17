using System.ComponentModel.DataAnnotations;
using App.Domain.Identity;
using Base.Contracts.Domain;
using Base.Domain;

namespace App.Domain.Entities;

public class Aircraft : BaseEntityWithMeta, ISoftDelete, ICompanyEntity
{
    [Required]
    [StringLength(20)]
    public string RegistrationNumber { get; set; } = default!;
    
    [Required]
    public LangStr Make { get; set; } = default!;
    
    [Required]
    public LangStr Model { get; set; } = default!;
    
    public int Year { get; set; }
    
    [Required]
    public LangStr Category { get; set; } = default!; // SingleEngine, MultiEngine, Helicopter
    
    [Required]
    [StringLength(10)]
    public string RequiredLicenseType { get; set; } = "PPL"; // LAPL(A), LAPL(H), PPL, CPL, ATPL
    
    public int TotalAirspeedHours { get; set; }
    
    [Range(0, double.MaxValue)]
    public decimal HourlyRate { get; set; }
    
    [Required]
    public Guid BaseAirportId { get; set; }
    public Airport? BaseAirport { get; set; }
    
    public LangStr Description { get; set; } = default!;
    
    public bool IsAvailable { get; set; } = true;
    
    // Soft delete
    public string? DeletedBy { get; set; }
    public DateTime? DeletedAt { get; set; }
    
    // Tenant
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }
    
    // Navigation
    public ICollection<AircraftPhoto>? Photos { get; set; }
    public ICollection<AircraftAvailability>? Availabilities { get; set; }
    public ICollection<Booking>? Bookings { get; set; }
    public ICollection<MaintenanceRecord>? MaintenanceRecords { get; set; }
    public ICollection<Review>? Reviews { get; set; }
    public ICollection<InsurancePolicy>? InsurancePolicies { get; set; }
    
    public bool IsDeleted => DeletedAt.HasValue;
    
    public void SoftDelete(string deletedBy)
    {
        DeletedBy = deletedBy;
        DeletedAt = DateTime.UtcNow;
    }
    
    public void Restore()
    {
        DeletedBy = null;
        DeletedAt = null;
    }
}
