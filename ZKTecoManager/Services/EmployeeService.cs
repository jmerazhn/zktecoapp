using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZKTecoManager.Data;
using ZKTecoManager.Models.Entities;

namespace ZKTecoManager.Services;

public class EmployeeService : IEmployeeService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDeviceService _deviceService;

    public EmployeeService(IServiceScopeFactory scopeFactory, IDeviceService deviceService)
    {
        _scopeFactory = scopeFactory;
        _deviceService = deviceService;
    }

    public async Task<EnrollResult> EnrollOnDeviceAsync(
        Employee employee, Device device, CancellationToken ct = default)
    {
        return await Task.Run(async () =>
        {
            var ok = await _deviceService.TestConnectionAsync(device, ct);
            if (!ok) return new EnrollResult(false, "No se pudo conectar al dispositivo.");

            // Get the ZKTecoDevice from the pool via DeviceService internal state
            // We reuse the connection pool by calling through the service
            var enrollResult = await DoEnrollAsync(employee, device, ct);
            return enrollResult;
        }, ct);
    }

    public async Task<EnrollResult> RemoveFromDeviceAsync(
        Employee employee, Device device, CancellationToken ct = default)
    {
        return await Task.Run(async () =>
        {
            var ok = await _deviceService.TestConnectionAsync(device, ct);
            if (!ok) return new EnrollResult(false, "No se pudo conectar al dispositivo.");

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var du = await db.DeviceUsers
                .FirstOrDefaultAsync(x => x.DeviceId == device.Id && x.EmployeeId == employee.Id, ct);

            if (du is null) return new EnrollResult(false, "El empleado no está enrolado en este dispositivo.");

            // Delete from physical device
            var deleted = await _deviceService.DeleteUserFromDeviceAsync(device, du.PinOnDevice, ct);
            if (!deleted) return new EnrollResult(false, "No se pudo eliminar el usuario del dispositivo.");

            db.DeviceUsers.Remove(du);
            await db.SaveChangesAsync(ct);

            return new EnrollResult(true, null);
        }, ct);
    }

    public async Task<EnrollResult> SyncCardToDeviceAsync(
        Employee employee, Device device, CancellationToken ct = default)
    {
        return await Task.Run(async () =>
        {
            if (string.IsNullOrEmpty(employee.CardNumber))
                return new EnrollResult(false, "El empleado no tiene número de tarjeta asignado.");

            var ok = await _deviceService.TestConnectionAsync(device, ct);
            if (!ok) return new EnrollResult(false, "No se pudo conectar al dispositivo.");

            var cardSet = await _deviceService.SetCardOnDeviceAsync(device, employee.EmployeeCode, employee.CardNumber, ct);
            if (!cardSet) return new EnrollResult(false, "No se pudo actualizar la tarjeta en el dispositivo.");

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var du = await db.DeviceUsers
                .FirstOrDefaultAsync(x => x.DeviceId == device.Id && x.EmployeeId == employee.Id, ct);
            if (du is not null)
            {
                du.CardNumber = employee.CardNumber;
                du.SyncedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }

            return new EnrollResult(true, null);
        }, ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<EnrollResult> DoEnrollAsync(Employee employee, Device device, CancellationToken ct)
    {
        var userSet = await _deviceService.SetUserOnDeviceAsync(
            device, employee.EmployeeCode, employee.FullName, ct);

        if (!userSet) return new EnrollResult(false, "No se pudo registrar el usuario en el dispositivo.");

        if (!string.IsNullOrEmpty(employee.CardNumber))
            await _deviceService.SetCardOnDeviceAsync(device, employee.EmployeeCode, employee.CardNumber, ct);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existing = await db.DeviceUsers
            .FirstOrDefaultAsync(x => x.DeviceId == device.Id && x.EmployeeId == employee.Id, ct);

        if (existing is null)
        {
            db.DeviceUsers.Add(new DeviceUser
            {
                DeviceId = device.Id,
                EmployeeId = employee.Id,
                PinOnDevice = employee.EmployeeCode,
                CardNumber = employee.CardNumber,
                SyncedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.PinOnDevice = employee.EmployeeCode;
            existing.CardNumber = employee.CardNumber;
            existing.SyncedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return new EnrollResult(true, null);
    }
}
