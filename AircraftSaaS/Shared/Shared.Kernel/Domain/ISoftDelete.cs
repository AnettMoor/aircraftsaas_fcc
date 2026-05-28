namespace Shared.Kernel.Domain;

public interface ISoftDelete : IBaseEntitySoftDelete
{
    bool IsDeleted { get; }
    void SoftDelete(string deletedBy);
    void Restore();
}
