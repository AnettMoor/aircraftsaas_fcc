using System.Security.Claims;
using Booking.Application.Interfaces;
using Fleet.Application.DTOs;
using Fleet.Application.Interfaces;
using Shared.Contracts.Common;
using Users.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApp.ViewModels.CompanyOwner;
using WebApp.ViewModels.User;

namespace WebApp.Controllers;

/// <summary>
/// Unified Aircraft controller – public catalog (anonymous/Normal) and
/// company-owner management actions in a single feature-based controller.
/// </summary>
[Authorize(Roles = "Normal,CompanyOwner,SystemAdmin")]
public class AircraftController : TenantAwareController
{
    private readonly IAircraftService _aircraftService;
    private readonly IAirportService _airportService;
    private readonly IReviewService _reviewService;
    private readonly ILogger<AircraftController> _logger;

    public AircraftController(
        IAircraftService aircraftService,
        IAirportService airportService,
        IReviewService reviewService,
        ITenantContext tenantContext,
        ICompanyService companyService,
        ILogger<AircraftController> logger)
        : base(tenantContext, companyService)
    {
        _aircraftService = aircraftService;
        _airportService = airportService;
        _reviewService = reviewService;
        _logger = logger;
    }

    // ── Public Catalog ───────────────────────────────────────────────-

    /// <summary>
    /// Public aircraft catalog – browse and search.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Index([FromQuery] AircraftSearchDto? search)
    {
        IEnumerable<AircraftDto> aircraft;

        if (search != null && search.StartDate.HasValue && search.EndDate.HasValue)
        {
            aircraft = await _aircraftService.GetAvailableAsync(
                search.StartDate.Value, search.EndDate.Value, search.Location);

            //apply special in memory filters
            if (!string.IsNullOrEmpty(search.Make))
                aircraft = aircraft.Where(a => a.Make.Contains(search.Make, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(search.Model))
                aircraft = aircraft.Where(a => a.Model.Contains(search.Model, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(search.Category))
                aircraft = aircraft.Where(a => a.Category == search.Category);
            if (search.MaxHourlyRate.HasValue)
                aircraft = aircraft.Where(a => a.HourlyRate <= search.MaxHourlyRate.Value);
            if (search.Year.HasValue)
                aircraft = aircraft.Where(a => a.Year == search.Year.Value);

            aircraft = aircraft
                .OrderBy(a => a.HourlyRate)
                .Skip((search.Page - 1) * search.PageSize)
                .Take(search.PageSize);
        }
        else
        {
            //general filter
            aircraft = await _aircraftService.SearchAsync(search ?? new AircraftSearchDto());
        }

        var airports = await _airportService.GetAllAirportsAsync();

        var model = new AircraftCatalogViewModel
        {
            Aircraft = aircraft,
            SearchModel = search ?? new AircraftSearchDto(),
            Airports = airports
        };

        return View(model);
    }

    /// <summary>
    /// Public aircraft details with reviews.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Details(Guid id)
    {
        var aircraft = await _aircraftService.GetByIdAsync(id, null);
        if (aircraft == null) return NotFound();

        var reviews = await _reviewService.GetReviewsByAircraftIdAsync(id);
        var userId = GetCurrentUserId();

        var model = new AircraftDetailsViewModel
        {
            Aircraft = aircraft,
            Reviews = reviews,
            CanBook = userId.HasValue
        };

        return View(model);
    }

    // ── Company Owner Management ─────────────────────────────────────

    /// <summary>
    /// Company-owner aircraft list with search &amp; soft-deleted items.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "CompanyOwner,SystemAdmin")]
    public async Task<IActionResult> Manage([FromQuery] AircraftSearchDto? search)
    {
        var tenantId = await GetTenantIdOrRedirect();
        if (!tenantId.HasValue) return RedirectAfterTenantCheck();

        IEnumerable<AircraftDto> aircraft;
        if (search != null && !string.IsNullOrEmpty(search.Make))
        {
            aircraft = await _aircraftService.SearchAsync(search);
            aircraft = aircraft.Where(a => a.CompanyId == tenantId.Value);
        }
        else
        {
            aircraft = await _aircraftService.GetAllAsync(tenantId.Value);
        }

        var deletedAircraft = await _aircraftService.GetAllDeletedAsync(tenantId.Value);

        var model = new AircraftListViewModel
        {
            Aircraft = aircraft,
            DeletedAircraft = deletedAircraft,
            SearchModel = search ?? new AircraftSearchDto()
        };

        return View(model);
    }

    /// <summary>
    /// Create aircraft – GET form.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "CompanyOwner")]
    public async Task<IActionResult> Create()
    {
        var tenantId = await GetTenantIdOrRedirect();
        if (!tenantId.HasValue) return RedirectAfterTenantCheck();

        if (!await IsCompanyOwnerAsync(tenantId))
        {
            TempData["Error"] = "Only company owners can manage aircraft.";
            return RedirectToAction(nameof(Manage));
        }

        var airports = await _airportService.GetAllAirportsAsync();
        var model = new AircraftEditViewModel
        {
            Airports = airports,
            Year = DateTime.Now.Year
        };

        return View(model);
    }

    /// <summary>
    /// Create aircraft – POST.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "CompanyOwner")]
    public async Task<IActionResult> Create(AircraftEditViewModel viewModel)
    {
        var tenantId = await GetTenantIdOrRedirect();
        if (!tenantId.HasValue) return RedirectAfterTenantCheck();

        if (!await IsCompanyOwnerAsync(tenantId))
        {
            TempData["Error"] = "Only company owners can manage aircraft.";
            return RedirectToAction(nameof(Manage));
        }

        if (!ModelState.IsValid)
        {
            viewModel.Airports = await _airportService.GetAllAirportsAsync();
            return View(viewModel);
        }

        var userId = GetCurrentUserIdString() ?? "";
        var createDto = new CreateAircraftDto
        {
            RegistrationNumber = viewModel.RegistrationNumber,
            Make = viewModel.Make,
            Model = viewModel.Model,
            Year = viewModel.Year,
            Category = viewModel.Category,
            TotalAirspeedHours = viewModel.TotalAirspeedHours,
            HourlyRate = viewModel.HourlyRate,
            BaseAirportId = viewModel.BaseAirportId ?? Guid.Empty,
            Description = viewModel.Description ?? ""
        };

        try
        {
            await _aircraftService.CreateAsync(createDto, tenantId.Value, userId);
            TempData["Success"] = "Aircraft created successfully.";
            return RedirectToAction(nameof(Manage));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("", ex.Message);
            viewModel.Airports = await _airportService.GetAllAirportsAsync();
            return View(viewModel);
        }
    }

    /// <summary>
    /// Edit aircraft – GET form.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "CompanyOwner")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var tenantId = await GetTenantIdOrRedirect();
        if (!tenantId.HasValue) return RedirectAfterTenantCheck();

        if (!await IsCompanyOwnerAsync(tenantId))
        {
            TempData["Error"] = "Only company owners can manage aircraft.";
            return RedirectToAction(nameof(Manage));
        }

        var aircraft = await _aircraftService.GetByIdAsync(id, tenantId);
        if (aircraft == null) return NotFound();

        var airports = await _airportService.GetAllAirportsAsync();
        var model = new AircraftEditViewModel
        {
            Id = aircraft.Id,
            RegistrationNumber = aircraft.RegistrationNumber,
            Make = aircraft.Make,
            Model = aircraft.Model,
            Year = aircraft.Year,
            Category = aircraft.Category,
            TotalAirspeedHours = aircraft.TotalAirspeedHours,
            HourlyRate = aircraft.HourlyRate,
            BaseAirportId = aircraft.BaseAirportId,
            Description = aircraft.Description,
            IsAvailable = aircraft.IsAvailable,
            Airports = airports
        };

        return View("Create", model);
    }

    /// <summary>
    /// Edit aircraft – POST.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "CompanyOwner")]
    public async Task<IActionResult> Edit(Guid id, AircraftEditViewModel viewModel)
    {
        var tenantId = await GetTenantIdOrRedirect();
        if (!tenantId.HasValue) return RedirectAfterTenantCheck();

        if (!await IsCompanyOwnerAsync(tenantId))
        {
            TempData["Error"] = "Only company owners can manage aircraft.";
            return RedirectToAction(nameof(Manage));
        }

        if (!ModelState.IsValid)
        {
            viewModel.Airports = await _airportService.GetAllAirportsAsync();
            return View("Create", viewModel);
        }

        var updateDto = new UpdateAircraftDto
        {
            RegistrationNumber = viewModel.RegistrationNumber,
            Make = viewModel.Make,
            Model = viewModel.Model,
            Year = viewModel.Year,
            Category = viewModel.Category,
            TotalAirspeedHours = viewModel.TotalAirspeedHours,
            HourlyRate = viewModel.HourlyRate,
            BaseAirportId = viewModel.BaseAirportId ?? Guid.Empty,
            Description = viewModel.Description ?? "",
            IsAvailable = viewModel.IsAvailable
        };

        try
        {
            await _aircraftService.UpdateAsync(id, updateDto, tenantId.Value,
                GetCurrentUserIdString() ?? "");
            TempData["Success"] = "Aircraft updated successfully.";
            return RedirectToAction(nameof(Manage));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("", ex.Message);
            viewModel.Airports = await _airportService.GetAllAirportsAsync();
            return View("Create", viewModel);
        }
    }

    /// <summary>
    /// Delete (soft) aircraft – POST.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "CompanyOwner")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var tenantId = await GetTenantIdOrRedirect();
        if (!tenantId.HasValue) return RedirectAfterTenantCheck();

        if (!await IsCompanyOwnerAsync(tenantId))
        {
            TempData["Error"] = "Only company owners can manage aircraft.";
            return RedirectToAction(nameof(Manage));
        }

        try
        {
            await _aircraftService.DeleteAsync(id, tenantId.Value,
                GetCurrentUserIdString() ?? "");
            TempData["Success"] = "Aircraft deleted successfully.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Manage));
    }

    /// <summary>
    /// Restore a soft-deleted aircraft – POST.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "CompanyOwner")]
    public async Task<IActionResult> Restore(Guid id)
    {
        var tenantId = await GetTenantIdOrRedirect();
        if (!tenantId.HasValue) return RedirectAfterTenantCheck();

        if (!await IsCompanyOwnerAsync(tenantId))
        {
            TempData["Error"] = "Only company owners can manage aircraft.";
            return RedirectToAction(nameof(Manage));
        }

        try
        {
            await _aircraftService.RestoreAsync(id, tenantId.Value,
                GetCurrentUserIdString() ?? "");
            TempData["Success"] = "Aircraft reactivated successfully.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Manage));
    }
}
