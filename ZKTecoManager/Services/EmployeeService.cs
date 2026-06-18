using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZKTecoManager.Data;
using ZKTecoManager.Infrastructure.Adms;
using ZKTecoManager.Models.Entities;

namespace ZKTecoManager.Services;

public class EmployeeService : IEmployeeService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDeviceService _deviceService;
    private readonly AdmsCommandQueue _admsQueue;

    public EmployeeService(IServiceScopeFactory scopeFactory, IDeviceService deviceService, AdmsCommandQueue admsQueue)
    {
        _scopeFactory = scopeFactory;
        _deviceService = deviceService;
        _admsQueue = admsQueue;
    }

    public async Task<EnrollResult> EnrollOnDeviceAsync(
        Employee employee, Device device, CancellationToken ct = default)
    {
        return await Task.Run(async () =>
        {
            var connResult = await _deviceService.TestConnectionAsync(device, ct);
            if (!connResult.Connected) return new EnrollResult(false, connResult.Error ?? "No se pudo conectar al dispositivo.");

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
            var connResult = await _deviceService.TestConnectionAsync(device, ct);
            if (!connResult.Connected) return new EnrollResult(false, connResult.Error ?? "No se pudo conectar al dispositivo.");

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var du = await db.DeviceUsers
                .FirstOrDefaultAsync(x => x.DeviceId == device.Id && x.EmployeeId == employee.Id, ct);

            if (du is null) return new EnrollResult(false, "El empleado no está enrolado en este dispositivo.");

            // Delete from physical device
            var (deleted, errCode) = await _deviceService.DeleteUserFromDeviceAsync(device, du.PinOnDevice, ct);
            if (!deleted) return new EnrollResult(false, $"No se pudo eliminar el usuario del dispositivo (código SDK {errCode}).");

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

            var connResult = await _deviceService.TestConnectionAsync(device, ct);
            if (!connResult.Connected) return new EnrollResult(false, connResult.Error ?? "No se pudo conectar al dispositivo.");

            var (cardSet, errCode) = await _deviceService.SetCardOnDeviceAsync(device, employee.EmployeeCode, employee.CardNumber, ct);
            if (!cardSet) return new EnrollResult(false, $"No se pudo actualizar la tarjeta en el dispositivo (código SDK {errCode}).");

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

    // EXPERIMENTO (Fase A): prueba el protocolo ADMS/Push encolando un comando
    // DATA UPDATE USERINFO y esperando a que el reloj lo ejecute y reporte el resultado.
    public async Task<(bool Success, string Detail)> TestAdmsEnrollOnDeviceAsync(
        Employee employee, Device device, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(device.SerialNumber))
            return (false, "El dispositivo no tiene número de serie guardado — usa \"Sincronizar\" en Dispositivos primero.");

        var command = $"DATA UPDATE USERINFO PIN={employee.EmployeeCode}\tName={employee.FullName}\tPrivilege=0\tCard=";
        return await _admsQueue.EnqueueAndWaitAsync(device.SerialNumber, command, TimeSpan.FromSeconds(60), ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<EnrollResult> DoEnrollAsync(Employee employee, Device device, CancellationToken ct)
    {
        var (userSet, errCode) = await _deviceService.SetUserOnDeviceAsync(
            device, employee.EmployeeCode, employee.FullName, ct);

        if (!userSet)
            return new EnrollResult(false, $"No se pudo registrar el usuario en el dispositivo (código SDK {errCode}).");

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
