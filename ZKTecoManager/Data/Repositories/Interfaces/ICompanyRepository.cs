using ZKTecoManager.Models.Entities;

namespace ZKTecoManager.Data.Repositories.Interfaces;

public interface ICompanyRepository : IRepository<Company>
{
    Task<IReadOnlyList<Company>> GetActiveAsync(CancellationToken ct = default);
    Task<Company?> GetWithDepartmentsAsync(int id, CancellationToken ct = default);
}
