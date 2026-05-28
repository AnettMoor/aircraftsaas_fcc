using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Booking;
using Shared.Contracts.Common;
using Shared.Contracts.Fleet;
using Shared.Kernel.Domain;
using Users.Application.DTOs;
using Users.Application.Interfaces;
using Users.Domain.Entities;
using Users.Domain.Enums;
using Users.Domain.Identity;

namespace Users.Infrastructure.Services;

internal sealed class SystemAdminUsersService : ISystemAdminUsersService
{
    private readonly UsersDbContext _db;
    private readonly UserManager<AppUser> _userManager;
    private readonly RoleManager<AppRole> _roleManager;
    private readonly IFleetModuleApi _fleetApi;
    private readonly IBookingModuleApi _bookingApi;
    private readonly ILogger<SystemAdminUsersService> _logger;

    public SystemAdminUsersService(
        UsersDbContext db,
        UserManager<AppUser> userManager,
        RoleManager<AppRole> roleManager,
        IFleetModuleApi fleetApi,
        IBookingModuleApi bookingApi,
        ILogger<SystemAdminUsersService> logger)
    {
        _db = db;
        _userManager = userManager;
        _roleManager = roleManager;
        _fleetApi = fleetApi;
        _bookingApi = bookingApi;
        _logger = logger;
    }

    // ── Dashboard ────────────────────────────────────────────────────────────

    public async Task<SystemAdminDashboardDto> GetDashboardAsync()
    {
        var allUsers = await _userManager.Users.ToListAsync();
        var allCompanies = await _db.Companies
            .Where(c => c.DeletedAt == null)
            .ToListAsync();

        // Cross-module counts via module APIs
        var totalBookings = await _bookingApi.GetTotalBookingsCountAsync();
        var totalAircraft = await _fleetApi.GetTotalAircraftCountAsync();
        var totalAirports = await _fleetApi.GetTotalAirportsCountAsync();

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

        var userList = await allUsers.OrderBy(u => u.Email).ToListAsync();
        var userDtos = new List<SystemAdminUserDto>();

        foreach (var user in userList)
        {
            var isDeactivated = user.LockoutEnabled && user.LockoutEnd >= DateTimeOffset.UtcNow.AddYears(99);

            if (deactivated.HasValue && deactivated.Value != isDeactivated)
                continue;

            // Cross-module: booking count via Booking API
            var bookingCount = await _bookingApi.GetBookingCountByUserAsync(user.Id);
            var roles = await _userManager.GetRolesAsync(user);

            var companyLangNames = await _db.AppUserCompanies
                .Where(uc => uc.AppUserId == user.Id)
                .Join(_db.Companies, uc => uc.CompanyId, c => c.Id, (uc, c) => c.CompanyName)
                .ToListAsync();
            var companyNames = companyLangNames.Select(cn => cn.ToString()).ToList();

            var displayName = $"{user.FirstName} {user.LastName}".Trim();
            if (string.IsNullOrEmpty(displayName))
                displayName = user.UserName ?? user.Email ?? "Unknown";

            userDtos.Add(new SystemAdminUserDto
            {
                UserId = user.Id,
                Name = displayName,
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

        var refreshTokens = await _db.RefreshTokens.Where(t => t.AppUserId == userId).ToListAsync();
        _db.RefreshTokens.RemoveRange(refreshTokens);

        var userCompanies = await _db.AppUserCompanies.Where(uc => uc.AppUserId == userId).ToListAsync();
        _db.AppUserCompanies.RemoveRange(userCompanies);

        var result = await _userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            await _db.SaveChangesAsync();
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

        var memberships = await _db.AppUserCompanies
            .Where(uc => uc.AppUserId == userId)
            .Include(uc => uc.Company)
            .Select(uc => new UserCompanyMembershipDto
            {
                CompanyId = uc.CompanyId,
                CompanyName = uc.Company != null ? uc.Company.CompanyName.ToString() : "",
                Role = uc.AppUserRoleInCompany
            })
            .ToListAsync();

        var displayName = $"{user.FirstName} {user.LastName}".Trim();
        if (string.IsNullOrEmpty(displayName))
            displayName = user.UserName ?? user.Email ?? "Unknown";

        return new UserRolesDataDto
        {
            UserId = user.Id,
            UserName = displayName,
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
        var memberships = await _db.AppUserCompanies
            .AsTracking()
            .Where(uc => uc.AppUserId == userId)
            .ToListAsync();

        if (selectedRole == "Normal")
        {
            if (memberships.Any())
            {
                _db.AppUserCompanies.RemoveRange(memberships);
                await _db.SaveChangesAsync();
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
                await _db.SaveChangesAsync();
        }

        _logger.LogInformation("SystemAdmin updated role for user {UserId} to {Role}", userId, selectedRole);
    }

    // ── User Company Assignment ──────────────────────────────────────────────

    public async Task<ChangeUserCompanyDataDto?> GetChangeUserCompanyDataAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) return null;

        var roles = (await _userManager.GetRolesAsync(user)).ToList();

        var memberships = await _db.AppUserCompanies
            .Where(uc => uc.AppUserId == userId)
            .Include(uc => uc.Company)
            .Select(uc => new UserCompanyMembershipDto
            {
                CompanyId = uc.CompanyId,
                CompanyName = uc.Company != null ? uc.Company.CompanyName.ToString() : "",
                Role = uc.AppUserRoleInCompany
            })
            .ToListAsync();

        var allCompanies = await _db.Companies
            .Where(c => c.DeletedAt == null && c.IsActive)
            .OrderBy(c => c.CompanyName)
            .ToListAsync();

        var currentCompanyId = memberships.FirstOrDefault()?.CompanyId;

        var displayName = $"{user.FirstName} {user.LastName}".Trim();
        if (string.IsNullOrEmpty(displayName))
            displayName = user.UserName ?? user.Email ?? "Unknown";

        return new ChangeUserCompanyDataDto
        {
            UserId = user.Id,
            UserName = displayName,
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

        var targetCompany = await _db.Companies
            .FirstOrDefaultAsync(c => c.Id == companyId && c.DeletedAt == null);

        if (targetCompany == null)
            return (false, "Selected company not found or is inactive.", null);

        var existingMemberships = await _db.AppUserCompanies
            .AsTracking()
            .Where(uc => uc.AppUserId == userId)
            .ToListAsync();

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

            _db.AppUserCompanies.Add(new AppUserCompany
            {
                AppUserId = userId,
                CompanyId = companyId,
                AppUserRoleInCompany = companyRole,
                IsActive = true,
                JoinedAt = DateTime.UtcNow,
                CreatedBy = updatedBy
            });
        }

        var refreshTokens = await _db.RefreshTokens
            .Where(t => t.AppUserId == userId)
            .ToListAsync();
        _db.RefreshTokens.RemoveRange(refreshTokens);

        await _db.SaveChangesAsync();

        var companyDisplayName = targetCompany.CompanyName.ToString();
        _logger.LogInformation(
            "SystemAdmin changed company assignment for user {UserId} ({Email}) to company {CompanyId} ({CompanyName}). Invalidated {TokenCount} refresh tokens.",
            userId, user.Email, companyId, companyDisplayName, refreshTokens.Count);

        return (true, null, companyDisplayName);
    }

    // ── Tenants ──────────────────────────────────────────────────────────────

    public async Task<TenantsListDto> GetTenantsAsync(string? search, bool? active, int page, int pageSize)
    {
        var allCompaniesRaw = await _db.Companies
            .Where(c => c.DeletedAt == null)
            .OrderBy(c => c.CompanyName)
            .ToListAsync();

        var filtered = allCompaniesRaw.AsEnumerable();

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
        var company = await _db.Companies.AsTracking()
            .FirstOrDefaultAsync(c => c.Id == companyId && c.DeletedAt == null);

        if (company == null)
            return (false, "", "Tenant not found.");

        company.IsActive = !company.IsActive;
        company.UpdatedAt = DateTime.UtcNow;
        company.UpdatedBy = updatedBy;

        await _db.SaveChangesAsync();

        var status = company.IsActive ? "activated" : "deactivated";
        _logger.LogInformation("SystemAdmin {action} tenant {CompanyId} ({Name})", status, companyId, company.CompanyName);

        return (true, status, null);
    }

    // ── Audit Log ────────────────────────────────────────────────────────────

    public async Task<AuditLogListDto> GetAuditLogsAsync(string? search, string? entity, string? action, Guid? tenantId, int page, int pageSize)
    {
        var baseQuery = _db.AuditLogs
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(entity))
            baseQuery = baseQuery.Where(a => a.EntityName == entity);

        if (!string.IsNullOrWhiteSpace(action))
            baseQuery = baseQuery.Where(a => a.Action == action);

        if (tenantId.HasValue)
            baseQuery = baseQuery.Where(a => a.TenantId == tenantId.Value);

        // AuditLog has no navigation to AppUser, so we join manually
        var logsWithUsers = from log in baseQuery
                            join user in _db.Users on log.UserId equals user.Id into userJoin
                            from user in userJoin.DefaultIfEmpty()
                            orderby log.Timestamp descending
                            select new AuditLogDto
                            {
                                Id = log.Id,
                                TenantId = log.TenantId,
                                UserId = log.UserId,
                                UserName = user != null ? (user.Email ?? user.UserName) : null,
                                EntityName = log.EntityName,
                                EntityId = log.EntityId,
                                Action = log.Action,
                                OldValues = log.OldValues,
                                NewValues = log.NewValues,
                                Timestamp = log.Timestamp,
                                IpAddress = log.IpAddress,
                                Details = log.Details
                            };

        var allLogs = await logsWithUsers.ToListAsync();

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

        var distinctEntities = await _db.AuditLogs
            .Select(a => a.EntityName).Distinct().OrderBy(e => e).ToListAsync();
        var distinctActions = await _db.AuditLogs
            .Select(a => a.Action).Distinct().OrderBy(a => a).ToListAsync();

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

    // ── Create Tenant ────────────────────────────────────────────────────────

    public async Task<bool> SlugExistsAsync(string slug)
    {
        return await _db.Companies.IgnoreQueryFilters().AnyAsync(c => c.Slug == slug);
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
            Address = !string.IsNullOrEmpty(dto.Address) ? new LangStr(dto.Address) : null,
            Phone = dto.Phone,
            Email = dto.Email,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        };

        _db.Companies.Add(company);
        await _db.SaveChangesAsync();

        _logger.LogInformation("SystemAdmin created tenant '{CompanyName}' (slug: {Slug})", dto.CompanyName, dto.Slug);

        return company.Id;
    }

    public async Task AssignTenantOwnerAsync(Guid companyId, Guid ownerUserId, string createdBy)
    {
        var ownerUser = await _userManager.FindByIdAsync(ownerUserId.ToString());
        if (ownerUser == null) return;

        _db.AppUserCompanies.Add(new AppUserCompany
        {
            AppUserId = ownerUser.Id,
            CompanyId = companyId,
            AppUserRoleInCompany = EAppUserRoleInCompany.CompanyOwner,
            IsActive = true,
            JoinedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        });
        await _db.SaveChangesAsync();

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
                Address = !string.IsNullOrEmpty(dto.NewCompanyAddress) ? new LangStr(dto.NewCompanyAddress) : null,
                Phone = dto.NewCompanyPhone,
                Email = dto.NewCompanyEmail,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = createdBy
            };

            _db.Companies.Add(company);
            await _db.SaveChangesAsync();

            _db.AppUserCompanies.Add(new AppUserCompany
            {
                AppUserId = user.Id,
                CompanyId = company.Id,
                AppUserRoleInCompany = EAppUserRoleInCompany.CompanyOwner,
                IsActive = true,
                JoinedAt = DateTime.UtcNow,
                CreatedBy = createdBy
            });
            await _db.SaveChangesAsync();

            assignedCompanyId = company.Id;
            result.NewCompanyName = dto.NewCompanyName;

            _logger.LogInformation(
                "SystemAdmin created new company '{CompanyName}' (slug: {Slug}) for user {Email}",
                dto.NewCompanyName, newCompanySlug, dto.Email);
        }
        else if (dto.CompanyId.HasValue && roleName != "Normal")
        {
            var companyExists = await _db.Companies
                .AnyAsync(c => c.Id == dto.CompanyId.Value && c.DeletedAt == null);

            if (companyExists)
            {
                var companyRole = roleName switch
                {
                    "CompanyOwner" => EAppUserRoleInCompany.CompanyOwner,
                    "SystemAdmin" => EAppUserRoleInCompany.SystemAdmin,
                    _ => EAppUserRoleInCompany.CompanyOwner
                };

                _db.AppUserCompanies.Add(new AppUserCompany
                {
                    AppUserId = user.Id,
                    CompanyId = dto.CompanyId.Value,
                    AppUserRoleInCompany = companyRole,
                    IsActive = true,
                    JoinedAt = DateTime.UtcNow,
                    CreatedBy = createdBy
                });
                await _db.SaveChangesAsync();
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
        var companies = await _db.Companies
            .Where(c => c.DeletedAt == null)
            .OrderBy(c => c.CompanyName)
            .ToListAsync();

        return companies.Select(c => new CompanySelectItemDto
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
        // Cross-module counts via module APIs
        var bookingCount = await _bookingApi.GetBookingCountByCompanyAsync(company.Id);
        var userCount = await _db.AppUserCompanies.CountAsync(uc => uc.CompanyId == company.Id);
        var aircraftCount = await _fleetApi.GetAircraftCountByCompanyAsync(company.Id);

        var ownerUser = await _db.AppUserCompanies
            .Where(uc => uc.CompanyId == company.Id && uc.AppUserRoleInCompany == EAppUserRoleInCompany.CompanyOwner)
            .Join(_db.Users, uc => uc.AppUserId, u => u.Id, (uc, u) => u)
            .FirstOrDefaultAsync();

        string? ownerName = null;
        if (ownerUser != null)
        {
            ownerName = $"{ownerUser.FirstName} {ownerUser.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(ownerName))
                ownerName = ownerUser.Email;
        }

        return new TenantStatsDto
        {
            CompanyId = company.Id,
            CompanyName = company.CompanyName.ToString(),
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
