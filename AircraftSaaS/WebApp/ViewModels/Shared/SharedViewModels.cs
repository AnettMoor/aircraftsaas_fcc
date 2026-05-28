namespace WebApp.ViewModels.Shared;

public class UserCompanyViewModel
{
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsCurrentCompany { get; set; }
}

public class NavigationViewModel
{
    public List<UserCompanyViewModel> UserCompanies { get; set; } = new();
    public UserCompanyViewModel? CurrentCompany { get; set; }
    public bool HasCompanyOwnerRole { get; set; }
    public bool HasNormalRole { get; set; }
    public bool IsSystemAdmin { get; set; }
}
