using Microsoft.AspNetCore.Identity;
using Shared.Kernel.Domain;

namespace Users.Domain.Identity;

public class AppRole : IdentityRole<Guid>, IBaseEntity
{
}
