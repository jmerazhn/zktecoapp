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

        var services = new ServiceCollection();
        ConfigureServices(services, config);
        _services = services.BuildServiceProvider();
        ServiceLocator.Initialize(_services);

        var mainWindow = _services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        // EF Core (scoped → one DbContext per navigation scope)
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(config.GetConnectionString("DefaultConnection")));

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

        // ViewModels (scoped — resolved per navigation scope)
        services.AddScoped<DevicesViewModel>();
        services.AddScoped<EmployeesViewModel>();
        services.AddScoped<AttendanceViewModel>();

        // Views (scoped — same scope as their ViewModel)
        services.AddScoped<DevicesView>();
        services.AddScoped<EmployeesView>();
        services.AddScoped<AttendanceView>();

        // Shell (singleton)
        services.AddSingleton<MainWindow>();

        services.AddSingleton(config);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ServiceLocator.GetService<IDeviceService>().DisconnectAll();
        (_services as IDisposable)?.Dispose();
        base.OnExit(e);
    }
}
