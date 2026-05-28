namespace Users.Application.DTOs;

public class LicenseDto
{
    public Guid Id { get; set; }
    public Guid AppUserId { get; set; }
    public string LicenseNumber { get; set; } = default!;
    public string LicenseType { get; set; } = default!;
    public DateTime IssueDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public string IssuingAuthority { get; set; } = default!;
    public bool IsValid { get; set; }
}

public class CreateLicenseDto
{
    public string LicenseNumber { get; set; } = default!;
    public string LicenseType { get; set; } = default!;
    public DateTime IssueDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public string IssuingAuthority { get; set; } = default!;
}

public class UpdateLicenseDto
{
    public Guid Id { get; set; }
    public string LicenseNumber { get; set; } = default!;
    public string LicenseType { get; set; } = default!;
    public DateTime IssueDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public string IssuingAuthority { get; set; } = default!;
}
