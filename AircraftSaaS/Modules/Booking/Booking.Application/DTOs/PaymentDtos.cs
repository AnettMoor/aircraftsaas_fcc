namespace Booking.Application.DTOs;

public class PaymentDto
{
    public string PaymentMethod { get; set; } = default!;
    public string? TransactionId { get; set; }
    public string? PaymentDetails { get; set; }
}

public class PaymentDetailDto
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = default!;
    public string Currency { get; set; } = "USD";
    public string Status { get; set; } = default!;
    public string? TransactionId { get; set; }
    public DateTime PaymentDate { get; set; }
    public DateTime? PaidAt { get; set; }
}
