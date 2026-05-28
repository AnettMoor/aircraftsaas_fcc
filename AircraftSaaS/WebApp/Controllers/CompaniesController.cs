using Shared.Contracts.Common;
using Users.Application.DTOs;
using Users.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApp.ViewModels.Companies;
using WebApp.ViewModels.CompanyOwner;

namespace WebApp.Controllers;

[Authorize(Roles = "Normal,CompanyOwner,SystemAdmin")]
public class CompaniesController : TenantAwareController
{
    private readonly ICompanyService _companyService;
    private readonly ILogger<CompaniesController> _logger;

    public CompaniesController(
        ICompanyService companyService,
        ITenantContext tenantContext,
        ILogger<CompaniesController> logger)
        : base(tenantContext, companyService)
    {
        _companyService = companyService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var companies = await _companyService.GetAllAsync();
        var model = new CompanyIndexViewModel
        {
            Companies = companies
        };
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var isAdmin = User.IsInRole("SystemAdmin");
        if (!isAdmin)
        {
            var isMember = await TenantContext.IsUserInCompanyAsync(id, userId.Value);
            if (!isMember)
            {
                TempData["Error"] = "You are not authorized to view this company.";
                return RedirectToAction(nameof(Index));
            }
        }

        var company = await _companyService.GetByIdAsync(id);

        if (company == null)
            return NotFound();

        var model = new CompanyDetailsViewModel
        {
            Company = company
        };
        return View(model);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> BySlug(string slug)
    {
        var company = await _companyService.GetBySlugAsync(slug);

        if (company == null)
            return NotFound();

        var model = new CompanyDetailsViewModel
        {
            Company = company
        };
        return View("Details", model);
    }

    [HttpGet]
    [Authorize(Roles = "SystemAdmin")]
    public IActionResult Create()
    {
        return View(new CompanyCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SystemAdmin")]
    public async Task<IActionResult> Create(CompanyCreateViewModel model)
    {
        var userId = GetCurrentUserIdString();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (!ModelState.IsValid)
            return View(model);

        var dto = new CreateCompanyDto
        {
            CompanyName = model.CompanyName,
            Address = model.Address,
            Phone = model.Phone,
            Email = model.Email
        };

        try
        {
            var company = await _companyService.CreateAsync(dto, userId);
            TempData["Success"] = "Company created successfully.";
            return RedirectToAction(nameof(Details), new { id = company.Id });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to create company");
            ModelState.AddModelError("", ex.Message);
            return View(model);
        }
    }

    [HttpGet]
    [Authorize(Roles = "CompanyOwner")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var isAdmin = User.IsInRole("SystemAdmin");
        if (!isAdmin)
        {
            var isMember = await TenantContext.IsUserInCompanyAsync(id, userId.Value);
            if (!isMember)
            {
                TempData["Error"] = "You are not authorized to edit this company.";
                return RedirectToAction(nameof(Index));
            }
        }

        var company = await _companyService.GetByIdAsync(id);
        if (company == null)
            return NotFound();

        var model = new CompanyEditViewModel
        {
            Id = id,
            Company = company,
            CompanyName = company.CompanyName,
            Address = company.Address,
            Phone = company.Phone,
            Email = company.Email
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "CompanyOwner")]
    public async Task<IActionResult> Edit(Guid id, CompanyEditViewModel model)
    {
        var callerId = GetCurrentUserId();
        var userIdStr = GetCurrentUserIdString();
        if (string.IsNullOrEmpty(userIdStr) || !callerId.HasValue)
            return Unauthorized();

        var isAdmin = User.IsInRole("SystemAdmin");

        if (!ModelState.IsValid)
        {
            model.Id = id;
            model.Company = await _companyService.GetByIdAsync(id);
            return View(model);
        }

        var dto = new UpdateCompanyDto
        {
            CompanyName = model.CompanyName,
            Address = model.Address,
            Phone = model.Phone,
            Email = model.Email
        };

        try
        {
            await _companyService.UpdateAsync(id, dto, userIdStr, callerId.Value, isAdmin);
            TempData["Success"] = "Company updated successfully.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (UnauthorizedAccessException)
        {
            TempData["Error"] = "You are not authorized to update this company.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to update company {Id}", id);
            ModelState.AddModelError("", ex.Message);
            model.Id = id;
            model.Company = await _companyService.GetByIdAsync(id);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SystemAdmin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var callerId = GetCurrentUserId();
        var userIdStr = GetCurrentUserIdString();
        if (string.IsNullOrEmpty(userIdStr) || !callerId.HasValue)
            return Unauthorized();

        var isAdmin = User.IsInRole("SystemAdmin");

        try
        {
            await _companyService.DeleteAsync(id, userIdStr, callerId.Value, isAdmin);
            TempData["Success"] = "Company deleted successfully.";
        }
        catch (UnauthorizedAccessException)
        {
            TempData["Error"] = "You are not authorized to delete this company.";
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to delete company {Id}", id);
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    // ── Company Settings (tenant-scoped) ─────────────────────────────

    /// <summary>
    /// Company Settings – GET (CompanyOwner only).
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "CompanyOwner")]
    public async Task<IActionResult> Settings()
    {
        var tenantId = await GetTenantIdOrRedirect();
        if (!tenantId.HasValue) return RedirectAfterTenantCheck();

        if (!await IsCompanyOwnerAsync(tenantId))
        {
            TempData["Error"] = "Only company owners can manage company settings.";
            return RedirectToAction("Index", "Dashboard");
        }

        var company = await _companyService.GetByIdAsync(tenantId.Value);
        if (company == null) return NotFound();

        var model = new CompanySettingsViewModel
        {
            Company = company,
            UpdateModel = new UpdateCompanyDto
            {
                CompanyName = company.CompanyName,
                Address = company.Address,
                Phone = company.Phone,
                Email = company.Email
            }
        };

        return View(model);
    }

    /// <summary>
    /// Company Settings – POST (CompanyOwner only).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "CompanyOwner")]
    public async Task<IActionResult> Settings(CompanySettingsViewModel model)
    {
        var tenantId = await GetTenantIdOrRedirect();
        if (!tenantId.HasValue) return RedirectAfterTenantCheck();

        if (!await IsCompanyOwnerAsync(tenantId))
        {
            TempData["Error"] = "Only company owners can manage company settings.";
            return RedirectToAction("Index", "Dashboard");
        }

        if (!ModelState.IsValid)
            return View(model);

        var callerId = GetCurrentUserId();
        var isAdmin = User.IsInRole("SystemAdmin");
        var updatedBy = GetCurrentUserIdString() ?? "";

        try
        {
            await _companyService.UpdateAsync(tenantId.Value, model.UpdateModel!, updatedBy, callerId ?? Guid.Empty, isAdmin);
            TempData["Success"] = "Company settings updated successfully.";
            return RedirectToAction(nameof(Settings));
        }
        catch (UnauthorizedAccessException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction("Index", "Dashboard");
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("", ex.Message);
            return View(model);
        }
    }
}
