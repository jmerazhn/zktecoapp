using Microsoft.EntityFrameworkCore;
using ZKTecoManager.Data.Repositories.Interfaces;
using ZKTecoManager.Models.Entities;

namespace ZKTecoManager.Data.Repositories;

public class IncidentRepository : Repository<Incident>, IIncidentRepository
{
    public IncidentRepository(AppDbContext db) : base(db) { }

    public async Task<IReadOnlyList<Incident>> GetByEmployeeAsync(int employeeId, CancellationToken ct = default)
        => await _set.AsNoTracking()
                     .Where(i => i.EmployeeId == employeeId)
                     .OrderByDescending(i => i.IncidentDate)
                     .ToListAsync(ct);

    public async Task<IReadOnlyList<Incident>> GetByDateRangeAsync(
        int companyId, DateOnly from, DateOnly to, CancellationToken ct = default)
        => await _set.AsNoTracking()
                     .Include(i => i.Employee)
                     .Where(i => i.Employee.CompanyId == companyId
                              && i.IncidentDate >= from
                              && i.IncidentDate <= to)
                     .OrderBy(i => i.IncidentDate).ThenBy(i => i.Employee.LastName)
                     .ToListAsync(ct);

    public async Task<Incident?> GetByEmployeeAndDateAsync(
        int employeeId, DateOnly date, CancellationToken ct = default)
        => await _set.AsNoTracking()
                     .FirstOrDefaultAsync(i => i.EmployeeId == employeeId && i.IncidentDate == date, ct);
}
