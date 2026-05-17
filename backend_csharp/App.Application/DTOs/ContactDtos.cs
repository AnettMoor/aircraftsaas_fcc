namespace App.Application.DTOs;

public class ContactDto
{
    public Guid Id { get; set; }
    public string ContactValue { get; set; } = default!;
    public Guid ContactTypeId { get; set; }
    public Guid PersonId { get; set; }
}

public class CreateContactDto
{
    public string ContactValue { get; set; } = default!;
    public Guid ContactTypeId { get; set; }
    public Guid PersonId { get; set; }
}

public class UpdateContactDto
{
    public Guid Id { get; set; }
    public string ContactValue { get; set; } = default!;
    public Guid ContactTypeId { get; set; }
    public Guid PersonId { get; set; }
}
