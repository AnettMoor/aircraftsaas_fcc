using Users.Domain.Enums;

namespace Users.Application.DTOs;

public class AppUserCompanyDto
{
    public Guid Id { get; set; }
    public Guid AppUserId { get; set; }
    public Guid CompanyId { get; set; }
    public EAppUserRoleInCompany AppUserRoleInCompany { get; set; }
    public bool IsActive { get; set; }
    public DateTime JoinedAt { get; set; }
}

public class CreateAppUserCompanyDto
{
    public Guid AppUserId { get; set; }
    public Guid CompanyId { get; set; }
    public EAppUserRoleInCompany AppUserRoleInCompany { get; set; }
}

public class UpdateAppUserCompanyDto
{
    public Guid Id { get; set; }
    public EAppUserRoleInCompany AppUserRoleInCompany { get; set; }
    public bool IsActive { get; set; }
}
