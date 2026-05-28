using Fleet.Application.DTOs;
using Fleet.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApp.ViewModels.Airports;

namespace WebApp.Controllers;

[Authorize(Roles = "Normal,CompanyOwner,SystemAdmin")]
public class AirportsController : Controller
{
    private readonly IAirportService _airportService;
    private readonly ILogger<AirportsController> _logger;

    public AirportsController(IAirportService airportService, ILogger<AirportsController> logger)
    {
        _airportService = airportService;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Index()
    {
        var airports = await _airportService.GetAllAirportsAsync();
        var model = new AirportIndexViewModel
        {
            Airports = airports
        };
        return View(model);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Details(Guid id)
    {
        var airport = await _airportService.GetAirportByIdAsync(id);
        if (airport == null)
            return NotFound();

        var model = new AirportDetailsViewModel
        {
            Airport = airport
        };
        return View(model);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Search([FromQuery] string? term)
    {
        var airports = await _airportService.SearchAirportsAsync(term);
        var model = new AirportIndexViewModel
        {
            Airports = airports,
            SearchTerm = term
        };
        return View("Index", model);
    }

    [HttpGet]
    [Authorize(Roles = "SystemAdmin")]
    public IActionResult Create()
    {
        return View(new AirportCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SystemAdmin")]
    public async Task<IActionResult> Create(AirportCreateViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var dto = new CreateAirportDto
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

        try
        {
            await _airportService.CreateAirportAsync(dto, User.Identity?.Name ?? "system");
            TempData["Success"] = "Airport created successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating airport");
            ModelState.AddModelError("", ex.Message);
            return View(model);
        }
    }

    [HttpGet]
    [Authorize(Roles = "SystemAdmin")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var airport = await _airportService.GetAirportByIdAsync(id);
        if (airport == null)
            return NotFound();

        var model = new AirportEditViewModel
        {
            Id = id,
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
    [Authorize(Roles = "SystemAdmin")]
    public async Task<IActionResult> Edit(Guid id, AirportEditViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.Id = id;
            return View(model);
        }

        var dto = new UpdateAirportDto
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

        try
        {
            await _airportService.UpdateAirportAsync(id, dto, User.Identity?.Name ?? "system");
            TempData["Success"] = "Airport updated successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SystemAdmin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _airportService.DeleteAirportAsync(id, User.Identity?.Name ?? "system");
            TempData["Success"] = "Airport deleted successfully.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SystemAdmin")]
    public async Task<IActionResult> Restore(Guid id)
    {
        var result = await _airportService.RestoreAirportAsync(id);
        if (!result)
        {
            TempData["Error"] = "Airport not found.";
        }
        else
        {
            TempData["Success"] = "Airport restored successfully.";
        }

        return RedirectToAction(nameof(Index));
    }
}
