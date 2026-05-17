namespace App.Application.DTOs;

public class ContactTypeDto
{
    public Guid Id { get; set; }
    public string ContactTypeName { get; set; } = default!;
}

public class CreateContactTypeDto
{
    public string ContactTypeName { get; set; } = default!;
}

public class UpdateContactTypeDto
{
    public Guid Id { get; set; }
    public string ContactTypeName { get; set; } = default!;
}
