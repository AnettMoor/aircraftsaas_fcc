namespace Fleet.Application.DTOs;

public class InsurancePolicyDto
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

public class CreateInsurancePolicyDto
{
    public string PolicyNumber { get; set; } = default!;
    public string InsuranceProvider { get; set; } = default!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal CoverageAmount { get; set; }
    public string CoverageType { get; set; } = default!;
}

public class UpdateInsurancePolicyDto
{
    public Guid Id { get; set; }
    public string PolicyNumber { get; set; } = default!;
    public string InsuranceProvider { get; set; } = default!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal CoverageAmount { get; set; }
    public string CoverageType { get; set; } = default!;
}
