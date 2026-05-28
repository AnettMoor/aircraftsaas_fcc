using Fleet.Application.DTOs;
using Fleet.Application.Interfaces;
using Shared.Contracts.Common;
using Users.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApp.ViewModels.Maintenance;

namespace WebApp.Controllers;

[Authorize(Roles = "CompanyOwner")]
public class MaintenanceController : TenantAwareController
{
    private readonly IMaintenanceService _maintenanceService;
    private readonly IAircraftService _aircraftService;
    private readonly ILogger<MaintenanceController> _logger;

    public MaintenanceController(
        IMaintenanceService maintenanceService,
        IAircraftService aircraftService,
        ITenantContext tenantContext,
        ICompanyService companyService,
        ILogger<MaintenanceController> logger)
        : base(tenantContext, companyService)
    {
        _maintenanceService = maintenanceService;
        _aircraftService = aircraftService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] Guid? aircraftId = null)
    {
        var tenantId = await GetTenantIdOrRedirect();
        if (!tenantId.HasValue) return RedirectAfterTenantCheck();

        var records = await _maintenanceService.GetAllForCompanyAsync(tenantId.Value, aircraftId);
        var aircraft = await _aircraftService.GetAllAsync(tenantId.Value);

        var model = new MaintenanceIndexViewModel
        {
            Records = records,
            Aircraft = aircraft,
            FilterAircraftId = aircraftId
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        var tenantId = await GetTenantIdOrRedirect();
        if (!tenantId.HasValue) return RedirectAfterTenantCheck();

        var record = await _maintenanceService.GetByIdAsync(id, tenantId.Value);

        if (record == null)
            return NotFound();

        var model = new MaintenanceDetailsViewModel
        {
            Record = record
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var tenantId = await GetTenantIdOrRedirect();
        if (!tenantId.HasValue) return RedirectAfterTenantCheck();

        var aircraft = await _aircraftService.GetAllAsync(tenantId.Value);

        var model = new MaintenanceFormViewModel
        {
            MaintenanceDate = DateTime.Today,
            MaintenanceType = "Annual Inspection",
            IsCompleted = true,
            Aircraft = aircraft
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(MaintenanceFormViewModel model)
    {
        var userId = GetCurrentUserIdString();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var tenantId = await GetTenantIdOrRedirect();
        if (!tenantId.HasValue) return RedirectAfterTenantCheck();

        if (!ModelState.IsValid)
        {
            model.Aircraft = await _aircraftService.GetAllAsync(tenantId.Value);
            return View(model);
        }

        var dto = new CreateMaintenanceRecordDto
        {
            AircraftId = model.AircraftId,
            MaintenanceDate = model.MaintenanceDate,
            MaintenanceType = model.MaintenanceType,
            Description = model.Description,
            PerformedBy = model.PerformedBy,
            AirframeHoursAtMaintenance = model.AirframeHoursAtMaintenance,
            NextDueDate = model.NextDueDate,
            NextDueHours = model.NextDueHours,
            Cost = model.Cost,
            IsCompleted = model.IsCompleted
        };

        try
        {
            await _maintenanceService.CreateAsync(dto, tenantId.Value, userId);
            TempData["Success"] = "Maintenance record created successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            model.Aircraft = await _aircraftService.GetAllAsync(tenantId.Value);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var tenantId = await GetTenantIdOrRedirect();
        if (!tenantId.HasValue) return RedirectAfterTenantCheck();

        var record = await _maintenanceService.GetByIdAsync(id, tenantId.Value);

        if (record == null)
            return NotFound();

        var aircraft = await _aircraftService.GetAllAsync(tenantId.Value);

        var model = new MaintenanceFormViewModel
        {
            Id = id,
            AircraftId = record.AircraftId,
            MaintenanceDate = record.MaintenanceDate,
            MaintenanceType = record.MaintenanceType,
            Description = record.Description,
            PerformedBy = record.PerformedBy,
            AirframeHoursAtMaintenance = record.AirframeHoursAtMaintenance,
            NextDueDate = record.NextDueDate,
            NextDueHours = record.NextDueHours,
            Cost = record.Cost,
            IsCompleted = record.IsCompleted,
            Aircraft = aircraft
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, MaintenanceFormViewModel model)
    {
        var userId = GetCurrentUserIdString();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var tenantId = await GetTenantIdOrRedirect();
        if (!tenantId.HasValue) return RedirectAfterTenantCheck();

        if (!ModelState.IsValid)
        {
            model.Id = id;
            model.Aircraft = await _aircraftService.GetAllAsync(tenantId.Value);
            return View(model);
        }

        var dto = new UpdateMaintenanceRecordDto
        {
            Id = id,
            AircraftId = model.AircraftId,
            MaintenanceDate = model.MaintenanceDate,
            MaintenanceType = model.MaintenanceType,
            Description = model.Description,
            PerformedBy = model.PerformedBy,
            AirframeHoursAtMaintenance = model.AirframeHoursAtMaintenance,
            NextDueDate = model.NextDueDate,
            NextDueHours = model.NextDueHours,
            Cost = model.Cost,
            IsCompleted = model.IsCompleted
        };

        try
        {
            await _maintenanceService.UpdateAsync(id, dto, tenantId.Value, userId);
            TempData["Success"] = "Maintenance record updated successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            model.Id = id;
            model.Aircraft = await _aircraftService.GetAllAsync(tenantId.Value);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetCurrentUserIdString();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var tenantId = await GetTenantIdOrRedirect();
        if (!tenantId.HasValue) return RedirectAfterTenantCheck();

        try
        {
            await _maintenanceService.DeleteAsync(id, tenantId.Value, userId);
            TempData["Success"] = "Maintenance record deleted successfully.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(Guid id)
    {
        var tenantId = await GetTenantIdOrRedirect();
        if (!tenantId.HasValue) return RedirectAfterTenantCheck();

        try
        {
            await _maintenanceService.RestoreAsync(id, tenantId.Value);
            TempData["Success"] = "Maintenance record restored successfully.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}
