using ZKTecoManager.Models.Entities;

namespace ZKTecoManager.Data.Repositories.Interfaces;

public interface IDepartmentRepository : IRepository<Department>
{
    Task<IReadOnlyList<Department>> GetByCompanyAsync(int companyId, CancellationToken ct = default);
}
