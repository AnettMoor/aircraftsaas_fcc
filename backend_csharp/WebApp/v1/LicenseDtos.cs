using System.ComponentModel.DataAnnotations;

namespace WebApp.v1;

public class LicenseResponse
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

public class CreateLicenseRequest
{
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string LicenseNumber { get; set; } = default!;

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string LicenseType { get; set; } = default!;

    [Required]
    public DateTime IssueDate { get; set; }

    [Required]
    public DateTime ExpiryDate { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string IssuingAuthority { get; set; } = default!;
}

public class UpdateLicenseRequest
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string LicenseNumber { get; set; } = default!;

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string LicenseType { get; set; } = default!;

    [Required]
    public DateTime IssueDate { get; set; }

    [Required]
    public DateTime ExpiryDate { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string IssuingAuthority { get; set; } = default!;
}
