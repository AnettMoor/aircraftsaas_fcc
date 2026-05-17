using Base.Contracts.Domain;

namespace App.Domain;

public interface ISoftDelete : IBaseEntitySoftDelete
{
    bool IsDeleted { get; }
    void SoftDelete(string deletedBy);
    void Restore();
}
