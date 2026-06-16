using Microsoft.EntityFrameworkCore;
using ZKTecoManager.Data.Repositories.Interfaces;
using ZKTecoManager.Models.Entities;

namespace ZKTecoManager.Data.Repositories;

public class CompanyRepository : Repository<Company>, ICompanyRepository
{
    public CompanyRepository(AppDbContext db) : base(db) { }

    public async Task<IReadOnlyList<Company>> GetActiveAsync(CancellationToken ct = default)
        => await _set.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync(ct);

    public async Task<Company?> GetWithDepartmentsAsync(int id, CancellationToken ct = default)
        => await _set.Include(c => c.Departments.Where(d => d.IsActive))
                     .FirstOrDefaultAsync(c => c.Id == id, ct);
}
