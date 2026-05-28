using Booking.Api.DTOs;
using Booking.Application.DTOs;

namespace Booking.Api.Mappers;

public static class BookingApiMapper
{
    // ── Booking ───────────────────────────────────────────────────────────────

    public static BookingResponse ToResponse(this BookingDto dto) => new()
    {
        Id = dto.Id,
        AircraftId = dto.AircraftId,
        AircraftName = dto.AircraftName,
        PilotId = dto.PilotId,
        PilotName = dto.PilotName,
        StartDateTime = dto.StartDateTime,
        EndDateTime = dto.EndDateTime,
        Status = dto.Status,
        Purpose = dto.Purpose,
        TotalAmount = dto.TotalAmount,
        RejectionReason = dto.RejectionReason,
        ApprovedAt = dto.ApprovedAt,
        PaidAt = dto.PaidAt,
        CompletedAt = dto.CompletedAt,
        CancelledAt = dto.CancelledAt,
        CompanyId = dto.CompanyId,
        CreatedAt = dto.CreatedAt,
    };

    public static IEnumerable<BookingResponse> ToResponse(this IEnumerable<BookingDto> dtos)
        => dtos.Select(d => d.ToResponse());

    public static CreateBookingDto ToBllDto(this CreateBookingRequest req) => new()
    {
        AircraftId = req.AircraftId,
        StartDateTime = req.StartDateTime,
        EndDateTime = req.EndDateTime,
        Purpose = req.Purpose,
    };

    public static UpdateBookingDto ToBllDto(this UpdateBookingRequest req) => new()
    {
        Id = req.Id,
        StartDateTime = req.StartDateTime,
        EndDateTime = req.EndDateTime,
        Purpose = req.Purpose,
    };

    public static PaymentDto ToBllDto(this PaymentRequest req) => new()
    {
        PaymentMethod = req.PaymentMethod,
        TransactionId = req.TransactionId,
        PaymentDetails = req.PaymentDetails,
    };

    // ── Review ────────────────────────────────────────────────────────────────

    public static ReviewResponse ToResponse(this ReviewDto dto) => new()
    {
        Id = dto.Id,
        AircraftId = dto.AircraftId,
        AircraftName = dto.AircraftName,
        BookingId = dto.BookingId,
        AuthorId = dto.AuthorId,
        AuthorName = dto.AuthorName,
        Rating = dto.Rating,
        Comment = dto.Comment,
        ReviewType = dto.ReviewType,
        ReviewedAt = dto.ReviewedAt,
        IsVerifiedBooking = dto.IsVerifiedBooking,
    };

    public static IEnumerable<ReviewResponse> ToResponse(this IEnumerable<ReviewDto> dtos)
        => dtos.Select(d => d.ToResponse());

    public static CreateReviewDto ToBllDto(this CreateReviewRequest req) => new()
    {
        AircraftId = req.AircraftId,
        BookingId = req.BookingId,
        Rating = req.Rating,
        Comment = req.Comment,
        ReviewType = req.ReviewType,
    };

    public static UpdateReviewDto ToBllDto(this UpdateReviewRequest req) => new()
    {
        Id = req.Id,
        Rating = req.Rating,
        Comment = req.Comment,
        ReviewType = req.ReviewType,
    };
}
