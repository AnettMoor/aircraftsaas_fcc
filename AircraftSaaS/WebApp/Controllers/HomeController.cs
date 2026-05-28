using System.Diagnostics;
using Shared.Contracts.Common;
using Users.Application.Interfaces;
using Users.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using WebApp.Models;
using WebApp.ViewModels.Shared;

namespace WebApp.Controllers;

[Authorize(Roles = "Normal,CompanyOwner,SystemAdmin")]
public class HomeController : TenantAwareController
{
    private readonly UserManager<AppUser> _userManager;

    public HomeController(
        ITenantContext tenantContext,
        ICompanyService companyService,
        UserManager<AppUser> userManager)
        : base(tenantContext, companyService)
    {
        _userManager = userManager;
    }

    [AllowAnonymous]
    public async Task<IActionResult> Index()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                var userCompanies = await TenantContext.GetUserCompanySummariesAsync(userId.Value);
                var companiesList = userCompanies.ToList();

                if (companiesList.Any(uc => uc.Role == "SystemAdmin"))
                {
                    return RedirectToAction("Dashboard", "SystemAdmin", new { area = "Admin" });
                }

                // Normal users have no company associations — just show the home page
                if (!companiesList.Any())
                {
                    return View();
                }

                // Check if the current selected company is deactivated
                var currentTenantId = TenantContext.GetCurrentTenantId();
                if (currentTenantId.HasValue)
                {
                    var isActive = await CompanyService.IsCompanyActiveAsync(currentTenantId.Value);
                    if (!isActive)
                    {
                        return RedirectToAction("CompanyDeactivated", "Home");
                    }
                }
                else if (companiesList.Any())
                {
                    // No company selected yet - check if all companies are deactivated
                    var firstActive = companiesList.FirstOrDefault(uc => uc.IsActive);
                    if (firstActive == null)
                    {
                        return RedirectToAction("CompanyDeactivated", "Home");
                    }
                }
            }
        }
        return View();
    }

    [AllowAnonymous]
    public IActionResult CompanyDeactivated()
    {
        return View();
    }

    [AllowAnonymous]
    public IActionResult Privacy()
    {
        return View();
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    /// <summary>
    /// Get navigation data for the layout (companies dropdown, roles)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetNavigationData()
    {
        var userId = TenantContext.GetCurrentUserId();
        if (!userId.HasValue)
            return Json(new NavigationViewModel());

        var userCompanies = await TenantContext.GetUserCompanySummariesAsync(userId.Value);
        var companiesList = userCompanies.ToList();
        var currentTenantId = TenantContext.GetCurrentTenantId();

        // Auto-select first company if no current company is set
        if (!currentTenantId.HasValue && companiesList.Any())
        {
            var firstCompany = companiesList.First();
            TenantContext.SetCurrentTenant(firstCompany.CompanyId);
            currentTenantId = firstCompany.CompanyId;
        }

        // Normal users have no AppUserCompany records, so detect via Identity role
        var isNormalUser = User.IsInRole("Normal") && !User.IsInRole("CompanyOwner") && !User.IsInRole("SystemAdmin");

        var model = new NavigationViewModel
        {
            UserCompanies = companiesList.Select(uc => new UserCompanyViewModel
            {
                CompanyId = uc.CompanyId,
                CompanyName = uc.CompanyName,
                Role = uc.Role,
                IsCurrentCompany = uc.CompanyId == currentTenantId
            }).ToList(),
            CurrentCompany = null,
            HasCompanyOwnerRole = companiesList.Any(uc => uc.Role == "CompanyOwner"),
            HasNormalRole = isNormalUser,
            IsSystemAdmin = companiesList.Any(uc => uc.Role == "SystemAdmin")
        };

        if (currentTenantId.HasValue)
        {
            model.CurrentCompany = model.UserCompanies.FirstOrDefault(c => c.CompanyId == currentTenantId.Value);
        }

        return Json(model);
    }

    /// <summary>
    /// Set the current company context
    /// </summary>
    [HttpGet]
    public IActionResult SetCurrentCompany(Guid companyId)
    {
        TenantContext.SetCurrentTenant(companyId);
        return RedirectToAction("Index", "Home");
    }

    /// <summary>
    /// Auto-select first company and redirect to home
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> SelectFirstCompany()
    {
        var userId = TenantContext.GetCurrentUserId();
        if (userId.HasValue)
        {
            var companyIds = await TenantContext.GetUserCompanyIdsAsync(userId.Value);
            var firstId = companyIds.FirstOrDefault();
            if (firstId != Guid.Empty)
            {
                TenantContext.SetCurrentTenant(firstId);
            }
        }
        return RedirectToAction("Index", "Home");
    }

    /// <summary>
    /// Set the UI language via a cookie and redirect back to the return URL
    /// </summary>
    [AllowAnonymous]
    [HttpPost]
    public IActionResult SetLanguage(string culture, string returnUrl)
    {
        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true
            }
        );
        return LocalRedirect(returnUrl);
    }
}
