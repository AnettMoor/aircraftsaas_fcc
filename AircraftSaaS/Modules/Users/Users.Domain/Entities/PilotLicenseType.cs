using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Domain;

namespace Users.Domain.Entities;

/// <summary>
/// Lookup table for pilot licence types (PPL, CPL, ATPL, etc.).
/// Managed globally by SystemAdmin.
/// </summary>
public class PilotLicenseType : BaseEntity
{
    /// <summary>Short code, e.g. "LAPL(A)", "LAPL(H)", "PPL", "CPL", "ATPL"</summary>
    [Required]
    [StringLength(20)]
    public string Code { get; set; } = default!;

    /// <summary>Full name, e.g. "Private Pilot License"</summary>
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = default!;

    /// <summary>Optional longer description</summary>
    [StringLength(1000)]
    public string? Description { get; set; }

    /// <summary>Display ordering (lower = shown first)</summary>
    public int SortOrder { get; set; }

    /// <summary>When false the type is hidden from dropdowns but kept for historical data</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
