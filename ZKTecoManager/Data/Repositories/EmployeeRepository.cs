using Microsoft.EntityFrameworkCore;
using ZKTecoManager.Data.Repositories.Interfaces;
using ZKTecoManager.Models.Entities;

namespace ZKTecoManager.Data.Repositories;

public class EmployeeRepository : Repository<Employee>, IEmployeeRepository
{
    public EmployeeRepository(AppDbContext db) : base(db) { }

    public async Task<IReadOnlyList<Employee>> GetByCompanyAsync(int companyId, bool activeOnly = true, CancellationToken ct = default)
    {
        var query = _set.AsNoTracking()
                        .Include(e => e.Department)
                        .Where(e => e.CompanyId == companyId);
        if (activeOnly) query = query.Where(e => e.IsActive);
        return await query.OrderBy(e => e.LastName).ThenBy(e => e.FirstName).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Employee>> GetByDepartmentAsync(int departmentId, CancellationToken ct = default)
        => await _set.AsNoTracking()
                     .Where(e => e.DepartmentId == departmentId && e.IsActive)
                     .OrderBy(e => e.LastName)
                     .ToListAsync(ct);

    public async Task<Employee?> GetByCardNumberAsync(string cardNumber, CancellationToken ct = default)
        => await _set.AsNoTracking()
                     .FirstOrDefaultAsync(e => e.CardNumber == cardNumber && e.IsActive, ct);

    public async Task<Employee?> GetWithShiftsAsync(int id, CancellationToken ct = default)
        => await _set.Include(e => e.EmployeeShifts)
                        .ThenInclude(es => es.Shift)
                     .FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<Employee?> GetByCodeAsync(int companyId, string employeeCode, CancellationToken ct = default)
        => await _set.AsNoTracking()
                     .FirstOrDefaultAsync(e => e.CompanyId == companyId && e.EmployeeCode == employeeCode, ct);
}
