using Booking.Application.Interfaces;
using Fleet.Application.Interfaces;
using Shared.Contracts.Common;
using Users.Application.DTOs;
using Users.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using WebApp.Controllers;
using WebApp.ViewModels.SystemAdmin;

namespace WebApp.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "SystemAdmin")]
public class SystemAdminController : TenantAwareController
{
    private readonly ISystemAdminUsersService _usersAdminService;
    private readonly ISystemAdminFleetService _fleetAdminService;
    private readonly ISystemAdminBookingService _bookingAdminService;
    private readonly IAirportService _airportService;
    private readonly ILogger<SystemAdminController> _logger;

    public SystemAdminController(
        ISystemAdminUsersService usersAdminService,
        ISystemAdminFleetService fleetAdminService,
        ISystemAdminBookingService bookingAdminService,
        IAirportService airportService,
        ITenantContext tenantContext,
        ICompanyService companyService,
        ILogger<SystemAdminController> logger)
        : base(tenantContext, companyService)
    {
        _usersAdminService = usersAdminService;
        _fleetAdminService = fleetAdminService;
        _bookingAdminService = bookingAdminService;
        _airportService = airportService;
        _logger = logger;
    }

    // ── Guard ─────────────────────────────────────────────────────────────────

    private async Task<bool> IsSystemAdminAsync()
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return false;
        var companies = await TenantContext.GetUserCompanySummariesAsync(userId.Value);
        return companies.Any(c => c.Role == "SystemAdmin");
    }

    // ── Dashboard ─────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Dashboard()
    {
        if (!await IsSystemAdminAsync()) return Forbid();

        var data = await _usersAdminService.GetDashboardAsync();

        var model = new SystemAdminDashboardViewModel
        {
            TotalUsers = data.TotalUsers,
            TotalTenants = data.TotalTenants,
            ActiveTenants = data.ActiveTenants,
            TotalBookings = data.TotalBookings,
            TotalAircraft = data.TotalAircraft,
            TotalAirports = data.TotalAirports,
            TopTenantsByBookings = data.TopTenantsByBookings
        };

        return View(model);
    }

    // ── Users ─────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Users(string? search, bool? deactivated, int page = 1)
    {
        if (!await IsSystemAdminAsync()) return Forbid();

        const int pageSize = 20;
        var result = await _usersAdminService.GetUsersAsync(search, deactivated, page, pageSize);

        var model = new SystemAdminUsersViewModel
        {
            Users = result.Items,
            TotalUsers = result.TotalItems,
            SearchQuery = search,
            FilterDeactivated = deactivated,
            Pagination = new PaginationInfo
            {
                CurrentPage = page,
                PageSize = pageSize,
                TotalItems = result.TotalItems
            }
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveUser(Guid id)
    {
        if (!await IsSystemAdminAsync()) return Forbid();

        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
            return RedirectToAction(nameof(Users));

        var (succeeded, error) = await _usersAdminService.DeactivateUserAsync(id, currentUserId.Value);

        if (succeeded)
            TempData["Success"] = "User has been deactivated.";
        else
            TempData["Error"] = error;

        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReactivateUser(Guid id)
    {
        if (!await IsSystemAdminAsync()) return Forbid();

        var (succeeded, error) = await _usersAdminService.ReactivateUserAsync(id);

        if (succeeded)
            TempData["Success"] = "User has been reactivated.";
        else
            TempData["Error"] = error;

        return RedirectToAction(nameof(Users));
    }

    // ── Role Assignment ───────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> EditUserRoles(Guid id)
    {
        if (!await IsSystemAdminAsync()) return Forbid();

        var data = await _usersAdminService.GetUserRolesDataAsync(id);
        if (data == null) return NotFound();

        var model = new EditUserRolesViewModel
        {
            UserId = data.UserId,
            UserName = data.UserName,
            Email = data.Email,
            AllRoles = data.AllRoles,
            AssignedRole = data.AssignedRole,
            SelectedRole = data.AssignedRole,
            CompanyMemberships = data.CompanyMemberships
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditUserRoles(EditUserRolesViewModel model)
    {
        if (!await IsSystemAdminAsync()) return Forbid();

        await _usersAdminService.UpdateUserRoleAsync(model.UserId, model.SelectedRole);

        TempData["Success"] = $"Role updated to '{model.SelectedRole}'.";
        return RedirectToAction(nameof(Users));
    }

    // ── Change User Company Assignment ────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> ChangeUserCompany(Guid id)
    {
        if (!await IsSystemAdminAsync()) return Forbid();

        var validationError = await _usersAdminService.ValidateChangeUserCompanyAsync(id);
        if (validationError != null)
        {
            TempData["Error"] = validationError;
            return RedirectToAction(nameof(Users));
        }

        var data = await _usersAdminService.GetChangeUserCompanyDataAsync(id);
        if (data == null) return NotFound();

        var model = new ChangeUserCompanyViewModel
        {
            UserId = data.UserId,
            UserName = data.UserName,
            Email = data.Email,
            Roles = data.Roles,
            CurrentMemberships = data.CurrentMemberships,
            SelectedCompanyId = data.CurrentCompanyId,
            CompanySelectList = new SelectList(
                data.ActiveCompanies, "Id", "CompanyName", data.CurrentCompanyId)
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeUserCompany(Guid id, Guid? selectedCompanyId)
    {
        if (!await IsSystemAdminAsync()) return Forbid();

        if (!selectedCompanyId.HasValue)
        {
            TempData["Error"] = "Please select a company.";
            return RedirectToAction(nameof(ChangeUserCompany), new { id });
        }

        var updatedBy = User.Identity?.Name ?? "SystemAdmin";
        var (succeeded, error, companyName) = await _usersAdminService.ChangeUserCompanyAsync(id, selectedCompanyId.Value, updatedBy);

        if (succeeded)
            TempData["Success"] = $"Company changed to '{companyName}'.";
        else
            TempData["Error"] = error;

        return succeeded
            ? RedirectToAction(nameof(Users))
            : RedirectToAction(nameof(ChangeUserCompany), new { id });
    }

    // ── Tenants ───────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Tenants(string? search, bool? active, int page = 1)
    {
        if (!await IsSystemAdminAsync()) return Forbid();

        const int pageSize = 20;
        var data = await _usersAdminService.GetTenantsAsync(search, active, page, pageSize);

        var model = new SystemAdminTenantsViewModel
        {
            Tenants = data.Tenants.Items,
            TotalTenants = data.Tenants.TotalItems,
            ActiveTenants = data.ActiveTenants,
            TotalBookingsAcrossSystem = data.TotalBookingsAcrossSystem,
            SearchQuery = search,
            FilterActive = active,
            Pagination = new PaginationInfo
            {
                CurrentPage = page,
                PageSize = pageSize,
                TotalItems = data.Tenants.TotalItems
            }
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleTenantActive(Guid id)
    {
        if (!await IsSystemAdminAsync()) return Forbid();

        var updatedBy = User.Identity?.Name ?? "SystemAdmin";
        var (succeeded, status, error) = await _usersAdminService.ToggleTenantActiveAsync(id, updatedBy);

        if (succeeded)
            TempData["Success"] = $"Tenant has been {status}.";
        else
            TempData["Error"] = error;

        return RedirectToAction(nameof(Tenants));
    }

    // ── Audit Log ─────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> AuditLog(string? search, string? entity, string? action,
        Guid? tenantId, int page = 1)
    {
        if (!await IsSystemAdminAsync()) return Forbid();

        const int pageSize = 50;
        var data = await _usersAdminService.GetAuditLogsAsync(search, entity, action, tenantId, page, pageSize);

        var model = new SystemAdminAuditLogViewModel
        {
            Logs = data.Logs.Items,
            TotalLogs = data.Logs.TotalItems,
            SearchQuery = search,
            FilterEntity = entity,
            FilterAction = action,
            FilterTenantId = tenantId,
            DistinctEntities = data.DistinctEntities,
            DistinctActions = data.DistinctActions,
            TenantSelectList = new SelectList(data.Companies, "Id", "CompanyName", tenantId),
            Pagination = new PaginationInfo
            {
                CurrentPage = page,
                PageSize = pageSize,
                TotalItems = data.Logs.TotalItems
            }
        };

        return View(model);
    }

    // ── All Bookings (system-wide) ─────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> AllBookings(string? search, string? status, Guid? tenantId, int page = 1)
    {
        if (!await IsSystemAdminAsync()) return Forbid();

        const int pageSize = 25;
        var data = await _bookingAdminService.GetAllBookingsAsync(search, status, tenantId, page, pageSize);

        var model = new SystemAdminBookingsViewModel
        {
            Bookings = data.Bookings.Items,
            TotalBookings = data.Bookings.TotalItems,
            SearchQuery = search,
            FilterStatus = status,
            FilterTenantId = tenantId,
            TenantSelectList = new SelectList(data.Companies, "Id", "CompanyName", tenantId),
            Pagination = new PaginationInfo
            {
                CurrentPage = page,
                PageSize = pageSize,
                TotalItems = data.Bookings.TotalItems
            }
        };

        return View(model);
    }

    // ── All Aircraft (system-wide) ─────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> AllAircraft(string? search, Guid? tenantId, bool? available, int page = 1)
    {
        if (!await IsSystemAdminAsync()) return Forbid();

        const int pageSize = 25;
        var data = await _fleetAdminService.GetAllAircraftAsync(search, tenantId, available, page, pageSize);

        var model = new SystemAdminAircraftViewModel
        {
            Aircraft = data.Aircraft.Items,
            TotalAircraft = data.Aircraft.TotalItems,
            SearchQuery = search,
            FilterTenantId = tenantId,
            FilterAvailable = available,
            TenantSelectList = new SelectList(data.Companies, "Id", "CompanyName", tenantId),
            Pagination = new PaginationInfo
            {
                CurrentPage = page,
                PageSize = pageSize,
                TotalItems = data.Aircraft.TotalItems
            }
        };

        return View(model);
    }

    // ── Airports (system-wide management) ────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Airports(string? search, bool showDeleted = false, int page = 1)
    {
        if (!await IsSystemAdminAsync()) return Forbid();

        const int pageSize = 25;
        var data = await _fleetAdminService.GetAirportsAsync(search, showDeleted, page, pageSize);

        var model = new SystemAdminAirportsViewModel
        {
            Airports = data.Airports.Items,
            TotalAirports = data.Airports.TotalItems,
            DeletedAirports = data.DeletedAirports,
            SearchQuery = search,
            ShowDeleted = showDeleted,
            Pagination = new PaginationInfo
            {
                CurrentPage = page,
                PageSize = pageSize,
                TotalItems = data.Airports.TotalItems
            }
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> CreateAirport()
    {
        if (!await IsSystemAdminAsync()) return Forbid();
        return View(new SystemAdminCreateAirportViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAirport(SystemAdminCreateAirportViewModel model)
    {
        if (!await IsSystemAdminAsync()) return Forbid();

        if (!ModelState.IsValid)
            return View(model);

        if (await _fleetAdminService.AirportExistsByIcaoCodeAsync(model.IcaoCode))
        {
            ModelState.AddModelError(nameof(model.IcaoCode), "An airport with this ICAO code already exists.");
            return View(model);
        }

        try
        {
            var dto = new Fleet.Application.DTOs.CreateAirportDto
            {
                IcaoCode = model.IcaoCode,
                IataCode = model.IataCode,
                Name = model.Name,
                City = model.City,
                Country = model.Country,
                Latitude = model.Latitude,
                Longitude = model.Longitude,
                Elevation = model.Elevation
            };

            await _airportService.CreateAirportAsync(dto, User.Identity?.Name ?? "system");

            _logger.LogInformation("SystemAdmin created airport {IcaoCode}", model.IcaoCode.ToUpper());
            TempData["Success"] = $"Airport '{model.IcaoCode.ToUpper()} – {model.Name}' created successfully.";
            return RedirectToAction(nameof(Airports));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating airport {IcaoCode}", model.IcaoCode);
            ModelState.AddModelError("", "An error occurred while creating the airport.");
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> EditAirport(Guid id)
    {
        if (!await IsSystemAdminAsync()) return Forbid();

        var airport = await _fleetAdminService.GetAirportForEditAsync(id);
        if (airport == null)
        {
            TempData["Error"] = "Airport not found or has been deleted.";
            return RedirectToAction(nameof(Airports));
        }

        var model = new SystemAdminEditAirportViewModel
        {
            Id = airport.Id,
            IcaoCode = airport.IcaoCode,
            IataCode = airport.IataCode,
            Name = airport.Name,
            City = airport.City,
            Country = airport.Country,
            Latitude = airport.Latitude,
            Longitude = airport.Longitude,
            Elevation = airport.Elevation
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditAirport(Guid id, SystemAdminEditAirportViewModel model)
    {
        if (!await IsSystemAdminAsync()) return Forbid();

        if (!ModelState.IsValid)
        {
            model.Id = id;
            return View(model);
        }

        if (await _fleetAdminService.AirportExistsByIcaoCodeAsync(model.IcaoCode, excludeId: id))
        {
            ModelState.AddModelError(nameof(model.IcaoCode), "Another airport with this ICAO code already exists.");
            model.Id = id;
            return View(model);
        }

        try
        {
            var dto = new Fleet.Application.DTOs.UpdateAirportDto
            {
                Id = id,
                IcaoCode = model.IcaoCode,
                IataCode = model.IataCode,
                Name = model.Name,
                City = model.City,
                Country = model.Country,
                Latitude = model.Latitude,
                Longitude = model.Longitude,
                Elevation = model.Elevation
            };

            await _airportService.UpdateAirportAsync(id, dto, User.Identity?.Name ?? "system");

            _logger.LogInformation("SystemAdmin updated airport {AirportId} ({IcaoCode})", id, model.IcaoCode.ToUpper());
            TempData["Success"] = $"Airport '{model.IcaoCode.ToUpper()}' updated successfully.";
            return RedirectToAction(nameof(Airports));
        }
        catch (InvalidOperationException)
        {
            TempData["Error"] = "Airport not found.";
            return RedirectToAction(nameof(Airports));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating airport {AirportId}", id);
            ModelState.AddModelError("", "An error occurred while updating the airport.");
            model.Id = id;
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAirport(Guid id)
    {
        if (!await IsSystemAdminAsync()) return Forbid();

        if (await _fleetAdminService.HasActiveAircraftAtAirportAsync(id))
        {
            TempData["Error"] = "Cannot delete this airport because one or more active aircraft use it as their base airport. Reassign those aircraft first.";
            return RedirectToAction(nameof(Airports));
        }

        try
        {
            await _airportService.DeleteAirportAsync(id, User.Identity?.Name ?? "system");
            _logger.LogInformation("SystemAdmin soft-deleted airport {AirportId}", id);
            TempData["Success"] = "Airport deleted successfully.";
        }
        catch (InvalidOperationException)
        {
            TempData["Error"] = "Airport not found.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting airport {AirportId}", id);
            TempData["Error"] = "An error occurred while deleting the airport.";
        }

        return RedirectToAction(nameof(Airports));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreAirport(Guid id)
    {
        if (!await IsSystemAdminAsync()) return Forbid();

        try
        {
            var restored = await _airportService.RestoreAirportAsync(id);
            if (restored)
            {
                _logger.LogInformation("SystemAdmin restored airport {AirportId}", id);
                TempData["Success"] = "Airport restored successfully.";
            }
            else
            {
                TempData["Error"] = "Airport not found or is not deleted.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring airport {AirportId}", id);
            TempData["Error"] = "An error occurred while restoring the airport.";
        }

        return RedirectToAction(nameof(Airports), new { showDeleted = true });
    }

    // ── Create Tenant ─────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> CreateTenant()
    {
        if (!await IsSystemAdminAsync()) return Forbid();

        var model = new SystemAdminCreateTenantViewModel();
        await PopulateTenantOwnerSelectListAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTenant(SystemAdminCreateTenantViewModel model)
    {
        if (!await IsSystemAdminAsync()) return Forbid();

        if (!ModelState.IsValid)
        {
            await PopulateTenantOwnerSelectListAsync(model);
            return View(model);
        }

        var slug = string.IsNullOrWhiteSpace(model.Slug)
            ? _usersAdminService.GenerateSlug(model.CompanyName)
            : _usersAdminService.GenerateSlug(model.Slug);

        if (await _usersAdminService.SlugExistsAsync(slug))
        {
            ModelState.AddModelError(nameof(model.Slug),
                $"A tenant with slug '{slug}' already exists. Please choose a different name or slug.");
            await PopulateTenantOwnerSelectListAsync(model);
            return View(model);
        }

        try
        {
            var dto = new CreateTenantDto
            {
                CompanyName = model.CompanyName,
                Slug = slug,
                Email = model.Email,
                Phone = model.Phone,
                Address = model.Address,
                MaxUsers = model.MaxUsers,
                MaxAircraft = model.MaxAircraft,
                MaxBookingsPerMonth = model.MaxBookingsPerMonth
            };

            var createdBy = User.Identity?.Name ?? "SystemAdmin";
            var companyId = await _usersAdminService.CreateTenantAsync(dto, createdBy);

            if (model.OwnerUserId.HasValue)
            {
                await _usersAdminService.AssignTenantOwnerAsync(companyId, model.OwnerUserId.Value, createdBy);
            }

            TempData["Success"] = $"Tenant '{model.CompanyName}' created successfully.";
            return RedirectToAction(nameof(Tenants));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating tenant '{CompanyName}'", model.CompanyName);
            ModelState.AddModelError("", "An error occurred while creating the tenant.");
            await PopulateTenantOwnerSelectListAsync(model);
            return View(model);
        }
    }

    private async Task PopulateTenantOwnerSelectListAsync(SystemAdminCreateTenantViewModel model)
    {
        var users = await _usersAdminService.GetAllUsersForSelectAsync();
        model.OwnerUserSelectList = new SelectList(users, "Id", "Display", model.OwnerUserId);
    }

    // ── Create User ───────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> CreateUser()
    {
        if (!await IsSystemAdminAsync()) return Forbid();

        var model = new SystemAdminCreateUserViewModel();
        await PopulateCreateUserSelectListsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(SystemAdminCreateUserViewModel model)
    {
        if (!await IsSystemAdminAsync()) return Forbid();

        // Extra validation: if CreateNewCompany is checked, NewCompanyName is required
        if (model.CreateNewCompany && string.IsNullOrWhiteSpace(model.NewCompanyName))
        {
            ModelState.AddModelError(nameof(model.NewCompanyName), "Company name is required when creating a new company.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateCreateUserSelectListsAsync(model);
            return View(model);
        }

        var dto = new CreateSystemUserDto
        {
            Email = model.Email,
            Password = model.Password,
            FirstName = model.FirstName,
            LastName = model.LastName,
            Role = model.Role,
            CompanyId = model.CompanyId,
            CreateNewCompany = model.CreateNewCompany,
            NewCompanyName = model.NewCompanyName,
            NewCompanySlug = model.NewCompanySlug,
            NewCompanyEmail = model.NewCompanyEmail,
            NewCompanyPhone = model.NewCompanyPhone,
            NewCompanyAddress = model.NewCompanyAddress,
            NewCompanyMaxUsers = model.NewCompanyMaxUsers,
            NewCompanyMaxAircraft = model.NewCompanyMaxAircraft,
            NewCompanyMaxBookingsPerMonth = model.NewCompanyMaxBookingsPerMonth
        };

        var createdBy = User.Identity?.Name ?? "SystemAdmin";
        var result = await _usersAdminService.CreateUserAsync(dto, createdBy);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError("", error);

            await PopulateCreateUserSelectListsAsync(model);
            return View(model);
        }

        TempData["Success"] = result.NewCompanyName != null && result.AssignedCompanyId.HasValue
            ? $"User '{result.Email}' created with role '{result.Role}' and new company '{result.NewCompanyName}'."
            : $"User '{result.Email}' created successfully with role '{result.Role}'.";

        return RedirectToAction(nameof(Users));
    }

    private async Task PopulateCreateUserSelectListsAsync(SystemAdminCreateUserViewModel model)
    {
        var companies = await _usersAdminService.GetActiveCompaniesForSelectAsync();
        model.CompanySelectList = new SelectList(companies, "Id", "CompanyName", model.CompanyId);
    }
}
