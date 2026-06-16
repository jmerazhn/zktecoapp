using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using ZKTecoManager.Data;
using ZKTecoManager.Data.Repositories;
using ZKTecoManager.Data.Repositories.Interfaces;
using ZKTecoManager.Helpers;
using ZKTecoManager.Services;
using ZKTecoManager.ViewModels;
using ZKTecoManager.Views;
using ZKTecoManager.Views.Dialogs;

namespace ZKTecoManager;

public partial class App : Application
{
    private IServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        // Load connection config from AppData (or defaults)
        var connCfg = ConnectionConfig.Load();

        while (true)
        {
            _services = BuildServiceProvider(config, connCfg.BuildConnectionString());
            ServiceLocator.Initialize(_services);

            var (ok, errorMsg) = TryEnsureDatabase(_services);
            if (ok) break;

            // Connection failed — show settings dialog
            var dlg = new DatabaseSettingsDialog(connCfg)
            {
                Title = $"Error de conexión — {dlg_Title(errorMsg)}"
            };

            if (dlg.ShowDialog() != true)
            {
                Current.Shutdown();
                return;
            }

            connCfg = dlg.Result!;
            connCfg.Save();
        }

        var mainWindow = _services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    internal static IServiceProvider BuildServiceProvider(IConfiguration config, string connectionString)
    {
        var services = new ServiceCollection();
        ConfigureServices(services, config, connectionString);
        return services.BuildServiceProvider();
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration config, string connectionString)
    {
        // EF Core (scoped → one DbContext per navigation scope)
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString));

        // Repositories (scoped)
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddScoped<IAttendanceLogRepository, AttendanceLogRepository>();
        services.AddScoped<IAttendanceRecordRepository, AttendanceRecordRepository>();
        services.AddScoped<IIncidentRepository, IncidentRepository>();
        services.AddScoped<IWorkShiftRepository, WorkShiftRepository>();

        // Services (singleton — manage long-lived state)
        services.AddSingleton<IDeviceService, DeviceService>();
        services.AddSingleton<IEmployeeService, EmployeeService>();
        services.AddSingleton<IAttendanceService, AttendanceService>();
        services.AddSingleton<IReportService, ReportService>();

        // ViewModels (scoped — resolved per navigation scope)
        services.AddScoped<DevicesViewModel>();
        services.AddScoped<EmployeesViewModel>();
        services.AddScoped<AttendanceViewModel>();
        services.AddScoped<IncidentsViewModel>();
        services.AddScoped<ReportsViewModel>();

        // Views (scoped — same scope as their ViewModel)
        services.AddScoped<DevicesView>();
        services.AddScoped<EmployeesView>();
        services.AddScoped<AttendanceView>();
        services.AddScoped<IncidentsView>();
        services.AddScoped<ReportsView>();

        // Shell (singleton)
        services.AddSingleton<MainWindow>();

        services.AddSingleton(config);
    }

    private static (bool ok, string error) TryEnsureDatabase(IServiceProvider services)
    {
        try
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
            FixDeviceUniqueIndex(db);
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    // Replaces the old unfiltered unique index on Device(IpAddress,Port) with a
    // filtered one (WHERE IsActive = 1) so soft-deleted rows don't block re-insertion.
    private static void FixDeviceUniqueIndex(AppDbContext db)
    {
        db.Database.ExecuteSqlRaw("""
            DECLARE @idx nvarchar(128) = (
                SELECT i.name
                FROM sys.indexes i
                JOIN sys.tables t ON i.object_id = t.object_id
                WHERE t.name = 'Device'
                  AND i.is_unique = 1
                  AND i.filter_definition IS NULL
                  AND EXISTS (
                      SELECT 1 FROM sys.index_columns ic
                      JOIN sys.columns c
                        ON ic.object_id = c.object_id AND ic.column_id = c.column_id
                      WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id
                        AND c.name = 'IpAddress')
                  AND EXISTS (
                      SELECT 1 FROM sys.index_columns ic
                      JOIN sys.columns c
                        ON ic.object_id = c.object_id AND ic.column_id = c.column_id
                      WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id
                        AND c.name = 'Port')
            );
            IF @idx IS NOT NULL
                EXEC('DROP INDEX [' + @idx + '] ON [Device]');

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = 'IX_Device_IpAddress_Port'
                  AND object_id = OBJECT_ID('Device')
                  AND filter_definition IS NOT NULL)
            BEGIN
                CREATE UNIQUE INDEX [IX_Device_IpAddress_Port]
                ON [Device]([IpAddress],[Port])
                WHERE [IsActive] = 1;
            END
            """);
    }

    private static string dlg_Title(string error)
        => error.Length > 60 ? error[..60] + "…" : error;

    protected override void OnExit(ExitEventArgs e)
    {
        try { ServiceLocator.GetService<IDeviceService>().DisconnectAll(); } catch { }
        (_services as IDisposable)?.Dispose();
        base.OnExit(e);
    }
}
