using ZKTecoManager.Models.Entities;

namespace ZKTecoManager.Services;

public record EnrollResult(bool Success, string? Error);

public interface IEmployeeService
{
    Task<EnrollResult> EnrollOnDeviceAsync(Employee employee, Device device, CancellationToken ct = default);
    Task<EnrollResult> RemoveFromDeviceAsync(Employee employee, Device device, CancellationToken ct = default);
    Task<EnrollResult> SyncCardToDeviceAsync(Employee employee, Device device, CancellationToken ct = default);

    // EXPERIMENTO (Fase A): prueba el protocolo ADMS/Push (ver plan del proyecto).
    Task<(bool Success, string Detail)> TestAdmsEnrollOnDeviceAsync(Employee employee, Device device, CancellationToken ct = default);
}
