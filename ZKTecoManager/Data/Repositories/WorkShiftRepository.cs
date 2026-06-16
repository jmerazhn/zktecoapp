using Microsoft.EntityFrameworkCore;
using ZKTecoManager.Data.Repositories.Interfaces;
using ZKTecoManager.Models.Entities;

namespace ZKTecoManager.Data.Repositories;

public class WorkShiftRepository : Repository<WorkShift>, IWorkShiftRepository
{
    public WorkShiftRepository(AppDbContext db) : base(db) { }

    public async Task<IReadOnlyList<WorkShift>> GetByCompanyAsync(int companyId, CancellationToken ct = default)
        => await _set.AsNoTracking()
                     .Where(s => s.CompanyId == companyId && s.IsActive)
                     .OrderBy(s => s.Name)
                     .ToListAsync(ct);

    public async Task<WorkShift?> GetActiveShiftForEmployeeAsync(
        int employeeId, DateOnly date, CancellationToken ct = default)
        => await _db.EmployeeShifts
                    .AsNoTracking()
                    .Where(es => es.EmployeeId == employeeId
                              && es.EffectiveFrom <= date
                              && (es.EffectiveTo == null || es.EffectiveTo >= date))
                    .OrderByDescending(es => es.EffectiveFrom)
                    .Select(es => es.Shift)
                    .FirstOrDefaultAsync(ct);
}
