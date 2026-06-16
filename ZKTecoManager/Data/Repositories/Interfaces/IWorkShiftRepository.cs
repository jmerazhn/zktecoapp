using ZKTecoManager.Models.Entities;

namespace ZKTecoManager.Data.Repositories.Interfaces;

public interface IWorkShiftRepository : IRepository<WorkShift>
{
    Task<IReadOnlyList<WorkShift>> GetByCompanyAsync(int companyId, CancellationToken ct = default);
    Task<WorkShift?> GetActiveShiftForEmployeeAsync(int employeeId, DateOnly date, CancellationToken ct = default);
}
