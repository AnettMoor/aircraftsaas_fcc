using System.ComponentModel.DataAnnotations;
using Booking.Domain.Enums;

namespace Booking.Api.DTOs;

public class BookingResponse
{
    public Guid Id { get; set; }
    public Guid AircraftId { get; set; }
    public string AircraftName { get; set; } = default!;
    public Guid PilotId { get; set; }
    public string PilotName { get; set; } = default!;
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public EBookingStatus Status { get; set; }
    public string? Purpose { get; set; }
    public decimal TotalAmount { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public Guid CompanyId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateBookingRequest
{
    [Required]
    public Guid AircraftId { get; set; }

    [Required]
    public DateTime StartDateTime { get; set; }

    [Required]
    public DateTime EndDateTime { get; set; }

    [StringLength(500)]
    public string? Purpose { get; set; }
}

public class UpdateBookingRequest
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public DateTime StartDateTime { get; set; }

    [Required]
    public DateTime EndDateTime { get; set; }

    [StringLength(500)]
    public string? Purpose { get; set; }
}

public class PaymentRequest
{
    [Required]
    [StringLength(50)]
    public string PaymentMethod { get; set; } = default!;

    [StringLength(200)]
    public string? TransactionId { get; set; }

    [StringLength(1000)]
    public string? PaymentDetails { get; set; }
}

public class RejectBookingRequest
{
    [StringLength(500)]
    public string? Reason { get; set; }
}
