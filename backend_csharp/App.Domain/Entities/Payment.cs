using System.ComponentModel.DataAnnotations;
using App.Domain;
using Base.Domain;

namespace App.Domain.Entities;

public class Payment : BaseEntity, ISoftDelete
{
    public Guid BookingId { get; set; }
    public Booking? Booking { get; set; }
    
    [Required]
    [StringLength(50)]
    public string PaymentMethod { get; set; } = default!; // CreditCard, BankTransfer
    
    public decimal Amount { get; set; }
    
    [Required]
    [StringLength(50)]
    public string Currency { get; set; } = "USD";
    
    [Required]
    [StringLength(50)]
    public string Status { get; set; } = default!; // Pending, Completed, Failed, Refunded
    
    [StringLength(100)]
    public string? TransactionId { get; set; }
    
    [StringLength(500)]
    public string? PaymentDetails { get; set; }
    
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    
    public DateTime? PaidAt { get; set; }
    
    public DateTime? ProcessedAt { get; set; }
    
    // Soft delete
    public string? DeletedBy { get; set; }
    public DateTime? DeletedAt { get; set; }
    
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
