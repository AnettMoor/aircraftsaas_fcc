using App.Domain.Enums;

namespace App.Application.DTOs;

public class BookingDto
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

public class CreateBookingDto
{
    public Guid AircraftId { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public string? Purpose { get; set; }
}

public class UpdateBookingDto
{
    public Guid Id { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public string? Purpose { get; set; }
}

public class PaymentDto
{
    public string PaymentMethod { get; set; } = default!;
    public string? TransactionId { get; set; }
    public string? PaymentDetails { get; set; }
}
