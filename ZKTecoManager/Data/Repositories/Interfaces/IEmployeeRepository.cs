using ZKTecoManager.Models.Entities;

namespace ZKTecoManager.Data.Repositories.Interfaces;

public interface IEmployeeRepository : IRepository<Employee>
{
    Task<IReadOnlyList<Employee>> GetByCompanyAsync(int companyId, bool activeOnly = true, CancellationToken ct = default);
    Task<IReadOnlyList<Employee>> GetByDepartmentAsync(int departmentId, CancellationToken ct = default);
    Task<Employee?> GetByCardNumberAsync(string cardNumber, CancellationToken ct = default);
    Task<Employee?> GetWithShiftsAsync(int id, CancellationToken ct = default);
    Task<Employee?> GetByCodeAsync(int companyId, string employeeCode, CancellationToken ct = default);
}
