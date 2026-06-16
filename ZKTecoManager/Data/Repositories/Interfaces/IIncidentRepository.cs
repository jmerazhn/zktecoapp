using ZKTecoManager.Models.Entities;

namespace ZKTecoManager.Data.Repositories.Interfaces;

public interface IIncidentRepository : IRepository<Incident>
{
    Task<IReadOnlyList<Incident>> GetByEmployeeAsync(int employeeId, CancellationToken ct = default);
    Task<IReadOnlyList<Incident>> GetByDateRangeAsync(int companyId, DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<Incident?> GetByEmployeeAndDateAsync(int employeeId, DateOnly date, CancellationToken ct = default);
}
