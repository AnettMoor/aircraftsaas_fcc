using Shared.Messaging;
using Shared.Messaging.Contracts;

namespace Users.WebHost.Publishers;

public class UsersEventPublisher
{
    private readonly RabbitMqPublisher _publisher;
    private const string Exchange = "users.events";

    public UsersEventPublisher(RabbitMqPublisher publisher)
    {
        _publisher = publisher;
    }

    public Task PublishUserRegisteredAsync(Guid userId, string email,
        string firstName, string lastName)
    {
        return _publisher.PublishAsync(Exchange, "user.registered",
            new UserRegisteredMessage(userId, email, firstName, lastName, DateTime.UtcNow));
    }

    public Task PublishCompanyCreatedAsync(Guid companyId, string name,
        string slug, bool isActive)
    {
        return _publisher.PublishAsync(Exchange, "company.created",
            new CompanyCreatedMessage(companyId, name, slug, isActive, DateTime.UtcNow));
    }

    public Task PublishCompanyUpdatedAsync(Guid companyId, string name,
        string slug, bool isActive)
    {
        return _publisher.PublishAsync(Exchange, "company.updated",
            new CompanyUpdatedMessage(companyId, name, slug, isActive, DateTime.UtcNow));
    }

    public Task PublishUserCompanyChangedAsync(Guid userId, Guid oldCompanyId,
        Guid newCompanyId)
    {
        return _publisher.PublishAsync(Exchange, "user.company.changed",
            new UserCompanyChangedMessage(userId, oldCompanyId, newCompanyId, DateTime.UtcNow));
    }
}
