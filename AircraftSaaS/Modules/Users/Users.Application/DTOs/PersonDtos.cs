namespace Users.Application.DTOs;

public class PersonDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public Guid AppUserId { get; set; }
    public Guid? CompanyId { get; set; }
}

public class CreatePersonDto
{
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public Guid AppUserId { get; set; }
    public Guid? CompanyId { get; set; }
}

public class UpdatePersonDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
}
