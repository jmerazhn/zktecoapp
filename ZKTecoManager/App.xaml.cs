using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using ZKTecoManager.Data;
using ZKTecoManager.Data.Repositories;
using ZKTecoManager.Data.Repositories.Interfaces;
using ZKTecoManager.Helpers;
using ZKTecoManager.Infrastructure.Adms;
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

        StartAdmsServer(_services, config);

        var mainWindow = _services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    // EXPERIMENTO (Fase A): servidor ADMS/Push para relojes cuyo firmware no soporta
    // escritura remota de usuarios vía Pull SDK ni Standalone SDK (ver plan/notas del
    // proyecto). Falla en silencio salvo un aviso — el arranque de la app no debe
    // depender de esto.
    private static void StartAdmsServer(IServiceProvider services, IConfiguration config)
    {
        var port = config.GetValue<int?>("AdmsPort") ?? 8080;
        try
        {
            services.GetRequiredService<AdmsServer>().Start(port);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"No se pudo iniciar el servidor ADMS en el puerto {port}: {ex.Message}\n\n" +
                $"Revisa adms-debug.log o ejecuta:\nnetsh http add urlacl url=http://+:{port}/ user=Everyone",
                "ADMS no disponible", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
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
            options.UseSqlServer(connectionString, sql =>
                // GFZKTECO runs at SQL Server compatibility level 110 (confirmed via
                // sys.databases), which doesn't support OPENJSON. EF Core 8 defaults to
                // OPENJSON for list.Contains(...) translation, so tell it the real level
                // to fall back to the older IN (@p0, @p1, ...) form.
                sql.UseCompatibilityLevel(110)));

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

        // ADMS/Push (experimento Fase A — ver plan del proyecto)
        services.AddSingleton<AdmsCommandQueue>();
        services.AddSingleton<AdmsServer>();

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

    // Best-effort: replace unfiltered unique index on Device(IpAddress,Port)
    // with a filtered one (WHERE IsActive=1). Never throws — startup must not fail here.
    private static void FixDeviceUniqueIndex(AppDbContext db)
    {
        // Drop every known unfiltered unique index name on this table
        foreach (var name in new[] { "UQ_Device_IP", "UQ_Device_IpAddress_Port",
                                     "IX_Device_IpAddress_Port", "IX_Device_IpAddress_Port_Active" })
        {
            try { db.Database.ExecuteSqlRaw(
                    $"IF EXISTS(SELECT 1 FROM sys.indexes WHERE name='{name}' " +
                    $"AND object_id=OBJECT_ID('Device') AND filter_definition IS NULL) " +
                    $"DROP INDEX [{name}] ON [Device]"); }
            catch { /* index didn't exist or couldn't be dropped — continue */ }
        }

        // Create filtered index if it doesn't exist yet
        try
        {
            db.Database.ExecuteSqlRaw(
                "IF NOT EXISTS(SELECT 1 FROM sys.indexes " +
                "WHERE name='IX_Device_IpAddress_Port' AND object_id=OBJECT_ID('Device') " +
                "AND filter_definition IS NOT NULL) " +
                "CREATE UNIQUE INDEX [IX_Device_IpAddress_Port] " +
                "ON [Device]([IpAddress],[Port]) WHERE [IsActive]=1");
        }
        catch { }
    }

    private static string dlg_Title(string error)
        => error.Length > 60 ? error[..60] + "…" : error;

    protected override void OnExit(ExitEventArgs e)
    {
        try { ServiceLocator.GetService<IDeviceService>().DisconnectAll(); } catch { }
        try { ServiceLocator.GetService<AdmsServer>().Stop(); } catch { }
        (_services as IDisposable)?.Dispose();
        base.OnExit(e);
    }
}
