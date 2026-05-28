using Booking.Application.DTOs;
using Booking.Domain.Enums;
using Fleet.Application.DTOs;

namespace WebApp.ViewModels.User;

/// <summary>
/// Aircraft catalog view model for normal users
/// </summary>
public class AircraftCatalogViewModel
{
    public IEnumerable<AircraftDto> Aircraft { get; set; } = new List<AircraftDto>();
    public AircraftSearchDto SearchModel { get; set; } = new();
    public IEnumerable<AirportDto> Airports { get; set; } = new List<AirportDto>();
    public List<string> Categories { get; set; } = new()
    {
        "Single Engine",
        "Multi Engine",
        "TurboProp",
        "Helicopter",
        "Jet"
    };
}

/// <summary>
/// Aircraft details view model
/// </summary>
public class AircraftDetailsViewModel
{
    public AircraftDto Aircraft { get; set; } = default!;
    public IEnumerable<ReviewDto> Reviews { get; set; } = new List<ReviewDto>();
    public bool CanBook { get; set; }
}

/// <summary>
/// Book aircraft view model
/// </summary>
public class BookAircraftViewModel
{
    public AircraftDto Aircraft { get; set; } = default!;
    public CreateBookingDto BookingModel { get; set; } = new();
    public bool IsAvailable { get; set; }
    public DateTime? AvailableFrom { get; set; }
}

/// <summary>
/// My bookings view model
/// </summary>
public class MyBookingsViewModel
{
    public IEnumerable<BookingDto> ActiveBookings { get; set; } = new List<BookingDto>();
    public IEnumerable<BookingDto> PendingBookings { get; set; } = new List<BookingDto>();
    public IEnumerable<BookingDto> PastBookings { get; set; } = new List<BookingDto>();
    public IEnumerable<BookingDto> CancelledBookings { get; set; } = new List<BookingDto>();
}

/// <summary>
/// Booking details view model
/// </summary>
public class BookingDetailsViewModel
{
    public BookingDto Booking { get; set; } = default!;
    public bool CanCancel { get; set; }
    public bool CanEdit { get; set; }
    public bool CanPay { get; set; }
    public bool CanReview { get; set; }
    public ReviewDto? ExistingReview { get; set; }
}

/// <summary>
/// Edit booking view model
/// </summary>
public class EditBookingViewModel
{
    public BookingDto Booking { get; set; } = default!;
    public UpdateBookingDto EditModel { get; set; } = new();
    public decimal HourlyRate { get; set; }
}

/// <summary>
/// User profile view model
/// </summary>
public class ProfileViewModel
{
    public string UserId { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? Phone { get; set; }
    public string? LicenseNumber { get; set; }
    public DateTime? LicenseExpiry { get; set; }
    public int TotalBookings { get; set; }
    public int CompletedBookings { get; set; }
}

/// <summary>
/// Review view model
/// </summary>
public class ReviewViewModel
{
    public Guid AircraftId { get; set; }
    public Guid BookingId { get; set; }
    public Guid? ReviewId { get; set; }
    public string AircraftName { get; set; } = default!;
    public int Rating { get; set; } = 5;
    public string? Comment { get; set; }

    /// <summary>
    /// If a review already exists for this booking, it will be populated here.
    /// </summary>
    public ReviewDto? ExistingReview { get; set; }
}

/// <summary>
/// Search results view model
/// </summary>
public class SearchResultsViewModel
{
    public string Query { get; set; } = default!;
    public int TotalResults { get; set; }
    public IEnumerable<AircraftDto> Results { get; set; } = new List<AircraftDto>();
    public Dictionary<string, List<string>> Filters { get; set; } = new();
}