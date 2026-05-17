using App.Application.DTOs;

namespace App.Application.Interfaces;

public interface IBookingService
{
    Task<BookingDto?> GetByIdAsync(Guid id, Guid? companyId = null, Guid? userId = null);
    Task<IEnumerable<BookingDto>> GetAllForPilotAsync(Guid userId);
    Task<IEnumerable<BookingDto>> GetAllForCompanyAsync(Guid companyId);
    Task<BookingDto> RequestBookingAsync(CreateBookingDto dto, Guid userId);
    Task<BookingDto> ApproveAsync(Guid bookingId, Guid companyId);
    Task<BookingDto> RejectAsync(Guid bookingId, Guid companyId, string reason);
    Task<BookingDto> ConfirmPaymentAsync(Guid bookingId, PaymentDto payment, Guid userId);
    Task<BookingDto> UpdateBookingAsync(UpdateBookingDto dto, Guid userId);
    Task<BookingDto> CancelAsync(Guid bookingId, Guid userId);
    Task<BookingDto> CompleteAsync(Guid bookingId, Guid companyId);
    Task<bool> ValidateBookingAsync(Guid aircraftId, DateTime start, DateTime end);
    Task<bool> ValidatePilotLicenseAsync(Guid userId, Guid aircraftId, DateTime bookingDate);
    Task<bool> ValidateInsuranceCoverageAsync(Guid aircraftId, DateTime start, DateTime end);
}
