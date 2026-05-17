using System.ComponentModel.DataAnnotations;

namespace WebApp.v1;

public class InsurancePolicyResponse
{
    public Guid Id { get; set; }
    public Guid AircraftId { get; set; }
    public string PolicyNumber { get; set; } = default!;
    public string InsuranceProvider { get; set; } = default!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal CoverageAmount { get; set; }
    public string CoverageType { get; set; } = default!;
    public bool IsActive { get; set; }
}

public class CreateInsurancePolicyRequest
{
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string PolicyNumber { get; set; } = default!;

    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string InsuranceProvider { get; set; } = default!;

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal CoverageAmount { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string CoverageType { get; set; } = default!;
}

public class UpdateInsurancePolicyRequest
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string PolicyNumber { get; set; } = default!;

    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string InsuranceProvider { get; set; } = default!;

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal CoverageAmount { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string CoverageType { get; set; } = default!;
}