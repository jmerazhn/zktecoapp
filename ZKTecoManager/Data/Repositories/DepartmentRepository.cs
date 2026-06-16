using Microsoft.EntityFrameworkCore;
using ZKTecoManager.Data.Repositories.Interfaces;
using ZKTecoManager.Models.Entities;

namespace ZKTecoManager.Data.Repositories;

public class DepartmentRepository : Repository<Department>, IDepartmentRepository
{
    public DepartmentRepository(AppDbContext db) : base(db) { }

    public async Task<IReadOnlyList<Department>> GetByCompanyAsync(int companyId, CancellationToken ct = default)
        => await _set.AsNoTracking()
                     .Where(d => d.CompanyId == companyId && d.IsActive)
                     .OrderBy(d => d.Name)
                     .ToListAsync(ct);
}
