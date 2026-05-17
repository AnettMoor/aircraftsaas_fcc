using App.Domain.Contracts;
using App.Domain.DTOs;
using App.Domain;
using App.Domain.Enums;
using App.Domain.Identity;
using Base.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace App.Infrastructure.Services;

public class SystemAdminService : ISystemAdminService
{
    private readonly IAppUOW _uow;
    private readonly IRefreshTokenRepository _refreshTokenRepo;
    private readonly UserManager<AppUser> _userManager;
    private readonly RoleManager<AppRole> _roleManager;
    private readonly ILogger<SystemAdminService> _logger;

    public SystemAdminService(
        IAppUOW uow,
        IRefreshTokenRepository refreshTokenRepo,
        UserManager<AppUser> userManager,
        RoleManager<AppRole> roleManager,
        ILogger<SystemAdminService> logger)
    {
        _uow = uow;
        _refreshTokenRepo = refreshTokenRepo;
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    // ── Dashboard ────────────────────────────────────────────────────────────

    public async Task<SystemAdminDashboardDto> GetDashboardAsync()
    {
        var allUsers = await _userManager.Users.ToListAsync();
        var allCompanies = (await _uow.CompanyRepository.GetAllNonDeletedAsync()).ToList();

        var totalBookings = await _uow.BookingRepository.CountAllAsync();
        var totalAircraft = await _uow.AircraftRepository.CountAllActiveAsync();
        var totalAirports = await _uow.AirportRepository.CountAllActiveAsync();

        var tenantStats = new List<TenantStatsDto>();
        foreach (var company in allCompanies)
        {
            var stats = await BuildTenantStatsAsync(company);
            tenantStats.Add(stats);
        }

        return new SystemAdminDashboardDto
        {
            TotalUsers = allUsers.Count,
            TotalTenants = allCompanies.Count,
            ActiveTenants = allCompanies.Count(c => c.IsActive),
            TotalBookings = totalBookings,
            TotalAircraft = totalAircraft,
            TotalAirports = totalAirports,
            TopTenantsByBookings = tenantStats.OrderByDescending(t => t.BookingCount).Take(5)
        };
    }

    // ── Users ────────────────────────────────────────────────────────────────

    public async Task<PagedResult<SystemAdminUserDto>> GetUsersAsync(string? search, bool? deactivated, int page, int pageSize)
    {
        var allUsers = _userManager.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            allUsers = allUsers.Where(u =>
                (u.Email != null && u.Email.ToLower().Contains(s)) ||
                (u.FirstName != null && u.FirstName.ToLower().Contains(s)) ||
                (u.LastName != null && u.LastName.ToLower().Contains(s)));
        }

        var userList = allUsers.OrderBy(u => u.Email).ToList();
        var userDtos = new List<SystemAdminUserDto>();

        foreach (var user in userList)
        {
            var isDeactivated = user.LockoutEnabled && user.LockoutEnd >= DateTimeOffset.UtcNow.AddYears(99);

            if (deactivated.HasValue && deactivated.Value != isDeactivated)
                continue;

            var bookingCount = await _uow.BookingRepository.CountByPilotAsync(user.Id);
            var roles = await _userManager.GetRolesAsync(user);

            var companyNames = (await _uow.AppUserCompanyRepository.GetCompanyNamesForUserAsync(user.Id)).ToList();

            userDtos.Add(new SystemAdminUserDto
            {
                UserId = user.Id,
                Name = $"{user.FirstName} {user.LastName}".Trim().Length > 0
                    ? $"{user.FirstName} {user.LastName}".Trim()
                    : user.UserName ?? user.Email ?? "Unknown",
                Email = user.Email ?? "",
                CreatedAt = user.CreatedAt,
                BookingCount = bookingCount,
                Roles = roles.ToList(),
                IsDeactivated = isDeactivated,
                CompanyName = companyNames.Any() ? string.Join(", ", companyNames) : null
            });
        }

        var totalItems = userDtos.Count;
        var pagedUsers = userDtos.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<SystemAdminUserDto>
        {
            Items = pagedUsers,
            TotalItems = totalItems,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<(bool Succeeded, string? Error)> DeactivateUserAsync(Guid userId, Guid currentUserId)
    {
        if (currentUserId == userId)
            return (false, "You cannot deactivate your own account.");

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
            return (false, "User not found.");

        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;

        await _refreshTokenRepo.DeleteAllForUserAsync(userId);

        var userCompanies = (await _uow.AppUserCompanyRepository.GetAllForUserTrackingAsync(userId)).ToList();
        _uow.AppUserCompanyRepository.RemoveRange(userCompanies);

        var activeBookings = (await _uow.BookingRepository.GetActiveForPilotTrackingAsync(userId)).ToList();
        foreach (var booking in activeBookings)
        {
            booking.Status = EBookingStatus.Cancelled;
            booking.CancelledAt = DateTime.UtcNow;
            booking.UpdatedAt = DateTime.UtcNow;
        }

        var result = await _userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            await _uow.SaveChangesAsync();
            _logger.LogInformation("SystemAdmin deactivated user {UserId} ({Email})", userId, user.Email);
            return (true, null);
        }

        return (false, "Failed to deactivate user: " + string.Join(", ", result.Errors.Select(e => e.Description)));
    }

    public async Task<(bool Succeeded, string? Error)> ReactivateUserAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
            return (false, "User not found.");

        user.LockoutEnd = null;
        user.LockoutEnabled = false;

        var result = await _userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            _logger.LogInformation("SystemAdmin reactivated user {UserId} ({Email})", userId, user.Email);
            return (true, null);
        }

        return (false, "Failed to reactivate user: " + string.Join(", ", result.Errors.Select(e => e.Description)));
    }

    // ── User Roles ───────────────────────────────────────────────────────────

    public async Task<UserRolesDataDto?> GetUserRolesDataAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) return null;

        var assignedRoles = (await _userManager.GetRolesAsync(user)).ToList();
        var allRoles = await _roleManager.Roles.Select(r => r.Name!).OrderBy(n => n).ToListAsync();
        var currentRole = assignedRoles.FirstOrDefault() ?? "Normal";

        var membershipsRaw = (await _uow.AppUserCompanyRepository.GetAllForUserWithCompanyAsync(userId)).ToList();
        var memberships = membershipsRaw.Select(uc => new UserCompanyMembershipDto
        {
            CompanyId = uc.CompanyId,
            CompanyName = uc.Company?.CompanyName.ToString() ?? "",
            Role = uc.AppUserRoleInCompany
        }).ToList();

        return new UserRolesDataDto
        {
            UserId = user.Id,
            UserName = $"{user.FirstName} {user.LastName}".Trim().Length > 0
                ? $"{user.FirstName} {user.LastName}".Trim()
                : user.UserName ?? user.Email ?? "Unknown",
            Email = user.Email ?? "",
            AllRoles = allRoles,
            AssignedRole = currentRole,
            CompanyMemberships = memberships
        };
    }

    public async Task UpdateUserRoleAsync(Guid userId, string selectedRole)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) return;

        var validRoles = new[] { "Normal", "CompanyOwner", "SystemAdmin" };
        selectedRole = validRoles.Contains(selectedRole) ? selectedRole : "Normal";

        var currentRoles = await _userManager.GetRolesAsync(user);

        if (currentRoles.Any())
            await _userManager.RemoveFromRolesAsync(user, currentRoles);

        await _userManager.AddToRoleAsync(user, selectedRole);

        // Sync AppUserCompany roles
        var memberships = (await _uow.AppUserCompanyRepository.GetAllForUserTrackingAsync(userId)).ToList();

        if (selectedRole == "Normal")
        {
            if (memberships.Any())
            {
                _uow.AppUserCompanyRepository.RemoveRange(memberships);
                await _uow.SaveChangesAsync();
            }
        }
        else
        {
            var newCompanyRole = selectedRole == "SystemAdmin"
                ? EAppUserRoleInCompany.SystemAdmin
                : EAppUserRoleInCompany.CompanyOwner;

            foreach (var m in memberships)
                m.AppUserRoleInCompany = newCompanyRole;

            if (memberships.Any())
                await _uow.SaveChangesAsync();
        }

        _logger.LogInformation("SystemAdmin updated role for user {UserId} to {Role}", userId, selectedRole);
    }

    // ── User Company Assignment ──────────────────────────────────────────────

    public async Task<ChangeUserCompanyDataDto?> GetChangeUserCompanyDataAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) return null;

        var roles = (await _userManager.GetRolesAsync(user)).ToList();

        var membershipsRaw = (await _uow.AppUserCompanyRepository.GetAllForUserWithCompanyAsync(userId)).ToList();
        var memberships = membershipsRaw.Select(uc => new UserCompanyMembershipDto
        {
            CompanyId = uc.CompanyId,
            CompanyName = uc.Company?.CompanyName.ToString() ?? "",
            Role = uc.AppUserRoleInCompany
        }).ToList();

        var allCompanies = (await _uow.CompanyRepository.GetAllActiveAsync()).ToList();

        var currentCompanyId = memberships.FirstOrDefault()?.CompanyId;

        return new ChangeUserCompanyDataDto
        {
            UserId = user.Id,
            UserName = $"{user.FirstName} {user.LastName}".Trim().Length > 0
                ? $"{user.FirstName} {user.LastName}".Trim()
                : user.UserName ?? user.Email ?? "Unknown",
            Email = user.Email ?? "",
            Roles = roles,
            CurrentMemberships = memberships,
            CurrentCompanyId = currentCompanyId,
            ActiveCompanies = allCompanies.Select(c => new CompanySelectItemDto
            {
                Id = c.Id,
                CompanyName = c.CompanyName.ToString()
            }).ToList()
        };
    }

    public async Task<string?> ValidateChangeUserCompanyAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) return "User not found.";

        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains("Normal") && !roles.Contains("CompanyOwner") && !roles.Contains("SystemAdmin"))
            return "Normal users cannot be assigned to a company.";

        return null;
    }

    public async Task<(bool Succeeded, string? Error, string? CompanyName)> ChangeUserCompanyAsync(
        Guid userId, Guid companyId, string updatedBy)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) return (false, "User not found.", null);

        var userRoles = await _userManager.GetRolesAsync(user);
        if (userRoles.Contains("Normal") && !userRoles.Contains("CompanyOwner") && !userRoles.Contains("SystemAdmin"))
            return (false, "Normal users cannot be assigned to a company.", null);

        var targetCompany = await _uow.CompanyRepository.FindAsync(companyId);

        if (targetCompany == null)
            return (false, "Selected company not found or is inactive.", null);

        var existingMemberships = (await _uow.AppUserCompanyRepository.GetAllForUserTrackingAsync(userId)).ToList();

        if (existingMemberships.Any())
        {
            foreach (var m in existingMemberships)
                m.CompanyId = companyId;
        }
        else
        {
            var companyRole = userRoles.Contains("CompanyOwner")
                ? EAppUserRoleInCompany.CompanyOwner
                : EAppUserRoleInCompany.SystemAdmin;

            _uow.AppUserCompanyRepository.Add(new AppUserCompany
            {
                AppUserId = userId,
                CompanyId = companyId,
                AppUserRoleInCompany = companyRole,
                IsActive = true,
                JoinedAt = DateTime.UtcNow,
                CreatedBy = updatedBy
            });
        }

        await _refreshTokenRepo.DeleteAllForUserAsync(userId);

        await _uow.SaveChangesAsync();

        _logger.LogInformation(
            "SystemAdmin changed company assignment for user {UserId} ({Email}) to company {CompanyId} ({CompanyName}).",
            userId, user.Email, companyId, targetCompany.CompanyName);

        return (true, null, targetCompany.CompanyName.ToString());
    }

    // ── Tenants ──────────────────────────────────────────────────────────────

    public async Task<TenantsListDto> GetTenantsAsync(string? search, bool? active, int page, int pageSize)
    {
        var allCompaniesRaw = (await _uow.CompanyRepository.GetAllNonDeletedAsync()).ToList();

        // Sort in memory (LangStr CompanyName cannot be sorted in SQL)
        var sortedCompanies = allCompaniesRaw.OrderBy(c => c.CompanyName.ToString()).ToList();

        IEnumerable<Company> filtered = sortedCompanies;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            filtered = filtered.Where(c =>
                c.CompanyName.ToString().Contains(s, StringComparison.OrdinalIgnoreCase) ||
                c.Slug.Contains(s, StringComparison.OrdinalIgnoreCase));
        }

        if (active.HasValue)
            filtered = filtered.Where(c => c.IsActive == active.Value);

        var allCompanies = filtered.ToList();
        var totalItems = allCompanies.Count;

        var tenantStats = new List<TenantStatsDto>();
        foreach (var company in allCompanies)
        {
            var stats = await BuildTenantStatsAsync(company);
            tenantStats.Add(stats);
        }

        var pagedTenants = tenantStats.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new TenantsListDto
        {
            Tenants = new PagedResult<TenantStatsDto>
            {
                Items = pagedTenants,
                TotalItems = totalItems,
                Page = page,
                PageSize = pageSize
            },
            ActiveTenants = tenantStats.Count(t => t.IsActive),
            TotalBookingsAcrossSystem = tenantStats.Sum(t => t.BookingCount)
        };
    }

    public async Task<(bool Succeeded, string Status, string? Error)> ToggleTenantActiveAsync(Guid companyId, string updatedBy)
    {
        var company = await _uow.CompanyRepository.GetByIdNonDeletedTrackingAsync(companyId);

        if (company == null)
            return (false, "", "Tenant not found.");

        company.IsActive = !company.IsActive;
        company.UpdatedAt = DateTime.UtcNow;
        company.UpdatedBy = updatedBy;

        await _uow.SaveChangesAsync();

        var status = company.IsActive ? "activated" : "deactivated";
        _logger.LogInformation("SystemAdmin {action} tenant {CompanyId} ({Name})", status, companyId, company.CompanyName);

        return (true, status, null);
    }

    // ── Audit Log ────────────────────────────────────────────────────────────

    public async Task<AuditLogListDto> GetAuditLogsAsync(string? search, string? entity, string? action, Guid? tenantId, int page, int pageSize)
    {
        var allLogs = (await _uow.AuditLogRepository.GetAllSystemWideWithUserAsync(entity, action, tenantId))
            .Select(a => new AuditLogDto
            {
                Id = a.Id,
                TenantId = a.TenantId,
                UserId = a.UserId,
                UserName = a.User != null ? (a.User.Email ?? a.User.UserName) : null,
                EntityName = a.EntityName,
                EntityId = a.EntityId,
                Action = a.Action,
                OldValues = a.OldValues,
                NewValues = a.NewValues,
                Timestamp = a.Timestamp,
                IpAddress = a.IpAddress,
                Details = a.Details
            })
            .ToList();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            allLogs = allLogs.Where(a =>
                (a.Details != null && a.Details.Contains(s, StringComparison.OrdinalIgnoreCase)) ||
                (a.UserName != null && a.UserName.Contains(s, StringComparison.OrdinalIgnoreCase)) ||
                a.IpAddress.Contains(s, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var totalItems = allLogs.Count;
        var logs = allLogs.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var distinctEntities = (await _uow.AuditLogRepository.GetDistinctEntityNamesAsync()).ToList();
        var distinctActions = (await _uow.AuditLogRepository.GetDistinctActionsAsync()).ToList();

        var companies = await GetActiveCompaniesForSelectAsync();

        return new AuditLogListDto
        {
            Logs = new PagedResult<AuditLogDto>
            {
                Items = logs,
                TotalItems = totalItems,
                Page = page,
                PageSize = pageSize
            },
            DistinctEntities = distinctEntities,
            DistinctActions = distinctActions,
            Companies = companies
        };
    }

    // ── All Bookings (system-wide) ───────────────────────────────────────────

    public async Task<BookingsListDto> GetAllBookingsAsync(string? search, string? status, Guid? tenantId, int page, int pageSize)
    {
        var allBookingsRaw = (await _uow.BookingRepository.GetAllSystemWideWithIncludesAsync()).ToList();

        IEnumerable<SystemAdminBookingDto> allBookings = allBookingsRaw
            .Select(b => new SystemAdminBookingDto
            {
                BookingId = b.Id,
                CompanyName = b.Company != null ? b.Company.CompanyName.ToString() : "",
                AircraftRegistration = b.Aircraft != null ? b.Aircraft.RegistrationNumber : "",
                PilotEmail = b.Pilot != null ? (b.Pilot.Email ?? b.Pilot.UserName ?? "") : "",
                StartDateTime = b.StartDateTime,
                EndDateTime = b.EndDateTime,
                Status = b.Status,
                TotalAmount = b.TotalAmount,
                CreatedAt = b.CreatedAt
            })
            .ToList();

        if (tenantId.HasValue)
        {
            // Filter by company after mapping (CompanyId is on the raw entity)
            var filteredBookings = allBookingsRaw
                .Where(b => b.CompanyId == tenantId.Value)
                .Select(b => new SystemAdminBookingDto
                {
                    BookingId = b.Id,
                    CompanyName = b.Company != null ? b.Company.CompanyName.ToString() : "",
                    AircraftRegistration = b.Aircraft != null ? b.Aircraft.RegistrationNumber : "",
                    PilotEmail = b.Pilot != null ? (b.Pilot.Email ?? b.Pilot.UserName ?? "") : "",
                    StartDateTime = b.StartDateTime,
                    EndDateTime = b.EndDateTime,
                    Status = b.Status,
                    TotalAmount = b.TotalAmount,
                    CreatedAt = b.CreatedAt
                })
                .ToList();
            allBookings = filteredBookings;
        }

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<EBookingStatus>(status, out var parsedStatus))
        {
            allBookings = allBookings.Where(b => b.Status == parsedStatus).ToList();
        }

        var allBookingsList = allBookings.ToList();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            allBookingsList = allBookingsList.Where(b =>
                b.PilotEmail.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                b.AircraftRegistration.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                b.CompanyName.Contains(s, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var totalItems = allBookingsList.Count;
        var paged = allBookingsList.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var companies = await GetActiveCompaniesForSelectAsync();

        return new BookingsListDto
        {
            Bookings = new PagedResult<SystemAdminBookingDto>
            {
                Items = paged,
                TotalItems = totalItems,
                Page = page,
                PageSize = pageSize
            },
            Companies = companies
        };
    }

    // ── All Aircraft (system-wide) ───────────────────────────────────────────

    public async Task<AircraftListDto> GetAllAircraftAsync(string? search, Guid? tenantId, bool? available, int page, int pageSize)
    {
        var allAircraftRaw = (await _uow.AircraftRepository.GetAllSystemWideWithIncludesAsync()).ToList();

        IEnumerable<Domain.Entities.Aircraft> filtered = allAircraftRaw;

        if (tenantId.HasValue)
            filtered = filtered.Where(a => a.CompanyId == tenantId.Value);

        if (available.HasValue)
            filtered = filtered.Where(a => a.IsAvailable == available.Value);

        var allAircraft = filtered
            .Select(a => new SystemAdminAircraftDto
            {
                AircraftId = a.Id,
                RegistrationNumber = a.RegistrationNumber,
                Make = a.Make,
                Model = a.Model,
                Year = a.Year,
                HourlyRate = a.HourlyRate,
                IsAvailable = a.IsAvailable,
                CompanyName = a.Company != null ? a.Company.CompanyName.ToString() : "",
                BaseAirport = a.BaseAirport != null
                    ? (a.BaseAirport.IcaoCode + " – " + a.BaseAirport.Name)
                    : "",
                TotalBookings = a.Bookings != null ? a.Bookings.Count : 0,
                CreatedAt = a.CreatedAt
            })
            .ToList();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            allAircraft = allAircraft.Where(a =>
                a.RegistrationNumber.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                a.Make.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                a.Model.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                a.CompanyName.Contains(s, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var totalItems = allAircraft.Count;
        var paged = allAircraft.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var companies = await GetActiveCompaniesForSelectAsync();

        return new AircraftListDto
        {
            Aircraft = new PagedResult<SystemAdminAircraftDto>
            {
                Items = paged,
                TotalItems = totalItems,
                Page = page,
                PageSize = pageSize
            },
            Companies = companies
        };
    }

    // ── Airports ─────────────────────────────────────────────────────────────

    public async Task<AirportsListDto> GetAirportsAsync(string? search, bool showDeleted, int page, int pageSize)
    {
        var allAirports = (await _uow.AirportRepository.GetAllIgnoreFiltersAsync()).ToList();

        var filtered = showDeleted
            ? allAirports
            : allAirports.Where(a => a.DeletedAt == null).ToList();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            filtered = filtered.Where(a =>
                a.IcaoCode.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                a.IataCode.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                a.Name.ToString().Contains(s, StringComparison.OrdinalIgnoreCase) ||
                a.City.ToString().Contains(s, StringComparison.OrdinalIgnoreCase) ||
                a.Country.ToString().Contains(s, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var totalItems = filtered.Count;
        var deletedCount = allAirports.Count(a => a.DeletedAt != null);

        var aircraftCounts = await _uow.AircraftRepository.GetAircraftCountsByAirportAsync();

        var paged = filtered
            .OrderBy(a => a.IcaoCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new SystemAdminAirportDto
            {
                AirportId = a.Id,
                IcaoCode = a.IcaoCode,
                IataCode = a.IataCode,
                Name = a.Name.ToString(),
                City = a.City.ToString(),
                Country = a.Country.ToString(),
                Latitude = a.Latitude,
                Longitude = a.Longitude,
                Elevation = a.Elevation,
                AircraftCount = aircraftCounts.TryGetValue(a.Id, out var cnt) ? cnt : 0,
                IsDeleted = a.DeletedAt.HasValue,
                DeletedBy = a.DeletedBy,
                DeletedAt = a.DeletedAt
            })
            .ToList();

        return new AirportsListDto
        {
            Airports = new PagedResult<SystemAdminAirportDto>
            {
                Items = paged,
                TotalItems = totalItems,
                Page = page,
                PageSize = pageSize
            },
            DeletedAirports = deletedCount
        };
    }

    public async Task<AirportEditDto?> GetAirportForEditAsync(Guid id)
    {
        var airport = await _uow.AirportRepository.FindAsync(id);

        if (airport == null) return null;

        return new AirportEditDto
        {
            Id = airport.Id,
            IcaoCode = airport.IcaoCode,
            IataCode = airport.IataCode,
            Name = airport.Name.ToString(),
            City = airport.City.ToString(),
            Country = airport.Country.ToString(),
            Latitude = airport.Latitude,
            Longitude = airport.Longitude,
            Elevation = airport.Elevation
        };
    }

    public async Task<bool> AirportExistsByIcaoCodeAsync(string icaoCode, Guid? excludeId = null)
    {
        return await _uow.AirportRepository.ExistsByIcaoCodeIgnoreFiltersAsync(icaoCode, excludeId);
    }

    public async Task<bool> HasActiveAircraftAtAirportAsync(Guid airportId)
    {
        return await _uow.AircraftRepository.HasActiveAtAirportAsync(airportId);
    }

    // ── Create Tenant ────────────────────────────────────────────────────────

    public async Task<bool> SlugExistsAsync(string slug)
    {
        return await _uow.CompanyRepository.ExistsBySlugIgnoreFiltersAsync(slug);
    }

    public async Task<Guid> CreateTenantAsync(CreateTenantDto dto, string createdBy)
    {
        var company = new Company
        {
            CompanyName = new LangStr(dto.CompanyName),
            Slug = dto.Slug,
            IsActive = true,
            MaxUsers = dto.MaxUsers,
            MaxAircraft = dto.MaxAircraft,
            MaxBookingsPerMonth = dto.MaxBookingsPerMonth,
            Address = dto.Address ?? string.Empty,
            Phone = dto.Phone,
            Email = dto.Email,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        };

        _uow.CompanyRepository.Add(company);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("SystemAdmin created tenant '{CompanyName}' (slug: {Slug})", dto.CompanyName, dto.Slug);

        return company.Id;
    }

    public async Task AssignTenantOwnerAsync(Guid companyId, Guid ownerUserId, string createdBy)
    {
        var ownerUser = await _userManager.FindByIdAsync(ownerUserId.ToString());
        if (ownerUser == null) return;

        _uow.AppUserCompanyRepository.Add(new AppUserCompany
        {
            AppUserId = ownerUser.Id,
            CompanyId = companyId,
            AppUserRoleInCompany = EAppUserRoleInCompany.CompanyOwner,
            IsActive = true,
            JoinedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        });
        await _uow.SaveChangesAsync();

        if (!await _userManager.IsInRoleAsync(ownerUser, "CompanyOwner"))
        {
            await _userManager.AddToRoleAsync(ownerUser, "CompanyOwner");
        }
    }

    public async Task<IEnumerable<UserSelectItemDto>> GetAllUsersForSelectAsync()
    {
        var users = await _userManager.Users
            .OrderBy(u => u.Email)
            .Select(u => new UserSelectItemDto
            {
                Id = u.Id,
                Display = (u.FirstName ?? "") + " " + (u.LastName ?? "") + " (" + (u.Email ?? "") + ")"
            })
            .ToListAsync();

        return users;
    }

    // ── Create User ──────────────────────────────────────────────────────────

    public async Task<CreateUserResultDto> CreateUserAsync(CreateSystemUserDto dto, string createdBy)
    {
        var result = new CreateUserResultDto { Email = dto.Email, Role = dto.Role };

        // Check email uniqueness
        var existingUser = await _userManager.FindByEmailAsync(dto.Email);
        if (existingUser != null)
        {
            result.Errors = new[] { "A user with this email already exists." };
            return result;
        }

        // If creating a new company, validate slug uniqueness
        string? newCompanySlug = null;
        if (dto.CreateNewCompany)
        {
            newCompanySlug = string.IsNullOrWhiteSpace(dto.NewCompanySlug)
                ? GenerateSlug(dto.NewCompanyName!)
                : GenerateSlug(dto.NewCompanySlug);

            if (await SlugExistsAsync(newCompanySlug))
            {
                result.Errors = new[] { $"A tenant with slug '{newCompanySlug}' already exists." };
                return result;
            }
        }

        // Create the user
        var user = new AppUser
        {
            Email = dto.Email,
            UserName = dto.Email,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };

        var createResult = await _userManager.CreateAsync(user, dto.Password);
        if (!createResult.Succeeded)
        {
            result.Errors = createResult.Errors.Select(e => e.Description);
            return result;
        }

        // Assign Identity role
        var validRoles = new[] { "Normal", "CompanyOwner", "SystemAdmin" };
        var roleName = validRoles.Contains(dto.Role) ? dto.Role : "Normal";

        var roleResult = await _userManager.AddToRoleAsync(user, roleName);
        if (!roleResult.Succeeded)
        {
            _logger.LogWarning("Failed to add role {Role} to user {Email}: {Errors}",
                roleName, dto.Email, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
        }

        // Handle company assignment
        Guid? assignedCompanyId = null;

        if (dto.CreateNewCompany && roleName == "CompanyOwner" && !string.IsNullOrWhiteSpace(dto.NewCompanyName))
        {
            var company = new Company
            {
                CompanyName = new LangStr(dto.NewCompanyName),
                Slug = newCompanySlug!,
                IsActive = true,
                MaxUsers = dto.NewCompanyMaxUsers,
                MaxAircraft = dto.NewCompanyMaxAircraft,
                MaxBookingsPerMonth = dto.NewCompanyMaxBookingsPerMonth,
                Address = dto.NewCompanyAddress ?? string.Empty,
                Phone = dto.NewCompanyPhone,
                Email = dto.NewCompanyEmail,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = createdBy
            };

            _uow.CompanyRepository.Add(company);
            await _uow.SaveChangesAsync();

            _uow.AppUserCompanyRepository.Add(new AppUserCompany
            {
                AppUserId = user.Id,
                CompanyId = company.Id,
                AppUserRoleInCompany = EAppUserRoleInCompany.CompanyOwner,
                IsActive = true,
                JoinedAt = DateTime.UtcNow,
                CreatedBy = createdBy
            });
            await _uow.SaveChangesAsync();

            assignedCompanyId = company.Id;
            result.NewCompanyName = dto.NewCompanyName;

            _logger.LogInformation(
                "SystemAdmin created new company '{CompanyName}' (slug: {Slug}) for user {Email}",
                dto.NewCompanyName, newCompanySlug, dto.Email);
        }
        else if (dto.CompanyId.HasValue && roleName != "Normal")
        {
            var companyExists = await _uow.CompanyRepository.ExistsByIdNonDeletedAsync(dto.CompanyId.Value);

            if (companyExists)
            {
                var companyRole = roleName switch
                {
                    "CompanyOwner" => EAppUserRoleInCompany.CompanyOwner,
                    "SystemAdmin" => EAppUserRoleInCompany.SystemAdmin,
                    _ => EAppUserRoleInCompany.CompanyOwner
                };

                _uow.AppUserCompanyRepository.Add(new AppUserCompany
                {
                    AppUserId = user.Id,
                    CompanyId = dto.CompanyId.Value,
                    AppUserRoleInCompany = companyRole,
                    IsActive = true,
                    JoinedAt = DateTime.UtcNow,
                    CreatedBy = createdBy
                });
                await _uow.SaveChangesAsync();
                assignedCompanyId = dto.CompanyId.Value;
            }
        }

        result.Succeeded = true;
        result.AssignedCompanyId = assignedCompanyId;
        result.Role = roleName;

        _logger.LogInformation(
            "SystemAdmin created user {Email} with role {Role} (CompanyId: {CompanyId})",
            dto.Email, roleName, assignedCompanyId);

        return result;
    }

    public async Task<IEnumerable<CompanySelectItemDto>> GetActiveCompaniesForSelectAsync()
    {
        var companies = (await _uow.CompanyRepository.GetAllNonDeletedAsync()).ToList();

        return companies
            .OrderBy(c => c.CompanyName.ToString())
            .Select(c => new CompanySelectItemDto
            {
                Id = c.Id,
                CompanyName = c.CompanyName.ToString()
            }).ToList();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    public string GenerateSlug(string name)
    {
        return name.ToLowerInvariant()
            .Trim()
            .Replace(" ", "-")
            .Replace("'", "")
            .Replace("\"", "");
    }

    private async Task<TenantStatsDto> BuildTenantStatsAsync(Company company)
    {
        var bookingCount = await _uow.BookingRepository.CountByCompanyAsync(company.Id);
        var userCount = await _uow.CompanyRepository.GetUserCountAsync(company.Id);
        var aircraftCount = await _uow.CompanyRepository.GetAircraftCountAsync(company.Id);

        var (ownerName, ownerEmail) = await _uow.AppUserCompanyRepository.GetCompanyOwnerInfoAsync(company.Id);
        if (string.IsNullOrWhiteSpace(ownerName))
            ownerName = ownerEmail;

        return new TenantStatsDto
        {
            CompanyId = company.Id,
            CompanyName = company.CompanyName,
            Slug = company.Slug,
            IsActive = company.IsActive,
            UserCount = userCount,
            AircraftCount = aircraftCount,
            BookingCount = bookingCount,
            CreatedAt = company.CreatedAt,
            OwnerName = ownerName
        };
    }
}

// Extension to allow ToListAsync on UserManager.Users (IQueryable)
// This is needed because UserManager.Users returns IQueryable<AppUser>
// and we need EntityFrameworkQueryableExtensions.ToListAsync
internal static class QueryableExtensions
{
    public static async Task<List<T>> ToListAsync<T>(this IQueryable<T> source)
    {
        return await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(source);
    }
}
