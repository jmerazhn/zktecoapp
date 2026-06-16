using Microsoft.EntityFrameworkCore;
using ZKTecoManager.Models.Entities;
using ZKTecoManager.Models.Enums;

namespace ZKTecoManager.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<DeviceUser> DeviceUsers => Set<DeviceUser>();
    public DbSet<WorkShift> WorkShifts => Set<WorkShift>();
    public DbSet<EmployeeShift> EmployeeShifts => Set<EmployeeShift>();
    public DbSet<AttendanceLog> AttendanceLogs => Set<AttendanceLog>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<Incident> Incidents => Set<Incident>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Company ──────────────────────────────────────────────────────────
        modelBuilder.Entity<Company>(e =>
        {
            e.ToTable("Company");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.TaxId).HasMaxLength(20);
            e.Property(x => x.Address).HasMaxLength(200);
            e.Property(x => x.Phone).HasMaxLength(20);
            e.Property(x => x.Email).HasMaxLength(100);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.HasIndex(x => x.Name).IsUnique();
        });

        // ── Department ───────────────────────────────────────────────────────
        modelBuilder.Entity<Department>(e =>
        {
            e.ToTable("Department");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.HasIndex(x => new { x.CompanyId, x.Name }).IsUnique();
            e.HasOne(x => x.Company)
             .WithMany(c => c.Departments)
             .HasForeignKey(x => x.CompanyId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Employee ─────────────────────────────────────────────────────────
        modelBuilder.Entity<Employee>(e =>
        {
            e.ToTable("Employee");
            e.HasKey(x => x.Id);
            e.Property(x => x.EmployeeCode).HasMaxLength(20).IsRequired();
            e.Property(x => x.FirstName).HasMaxLength(80).IsRequired();
            e.Property(x => x.LastName).HasMaxLength(80).IsRequired();
            e.Property(x => x.Position).HasMaxLength(100);
            e.Property(x => x.CardNumber).HasMaxLength(20);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.HasIndex(x => new { x.CompanyId, x.EmployeeCode }).IsUnique();
            e.Ignore(x => x.FullName);
            e.HasOne(x => x.Company)
             .WithMany(c => c.Employees)
             .HasForeignKey(x => x.CompanyId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Department)
             .WithMany(d => d.Employees)
             .HasForeignKey(x => x.DepartmentId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ── Device ───────────────────────────────────────────────────────────
        modelBuilder.Entity<Device>(e =>
        {
            e.ToTable("Device");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(80).IsRequired();
            e.Property(x => x.IpAddress).HasColumnType("varchar(15)").IsRequired();
            e.Property(x => x.Port).HasDefaultValue(4370);
            e.Property(x => x.CommPassword).HasMaxLength(20);
            e.Property(x => x.SerialNumber).HasMaxLength(50);
            e.Property(x => x.Model).HasMaxLength(50);
            e.Property(x => x.Location).HasMaxLength(100);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.HasIndex(x => new { x.IpAddress, x.Port }).IsUnique()
             .HasFilter("[IsActive] = 1");
            e.HasOne(x => x.Company)
             .WithMany(c => c.Devices)
             .HasForeignKey(x => x.CompanyId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── DeviceUser ───────────────────────────────────────────────────────
        modelBuilder.Entity<DeviceUser>(e =>
        {
            e.ToTable("DeviceUser");
            e.HasKey(x => x.Id);
            e.Property(x => x.PinOnDevice).HasMaxLength(20).IsRequired();
            e.Property(x => x.CardNumber).HasMaxLength(20);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.HasIndex(x => new { x.DeviceId, x.EmployeeId }).IsUnique();
            e.HasOne(x => x.Device)
             .WithMany(d => d.DeviceUsers)
             .HasForeignKey(x => x.DeviceId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Employee)
             .WithMany(emp => emp.DeviceUsers)
             .HasForeignKey(x => x.EmployeeId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── WorkShift ────────────────────────────────────────────────────────
        modelBuilder.Entity<WorkShift>(e =>
        {
            e.ToTable("WorkShift");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(80).IsRequired();
            e.Property(x => x.StartTime).HasColumnType("time(0)");
            e.Property(x => x.EndTime).HasColumnType("time(0)");
            e.Property(x => x.ToleranceMinutes).HasDefaultValue(0);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.HasOne(x => x.Company)
             .WithMany(c => c.WorkShifts)
             .HasForeignKey(x => x.CompanyId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── EmployeeShift ────────────────────────────────────────────────────
        modelBuilder.Entity<EmployeeShift>(e =>
        {
            e.ToTable("EmployeeShift");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Employee)
             .WithMany(emp => emp.EmployeeShifts)
             .HasForeignKey(x => x.EmployeeId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Shift)
             .WithMany(s => s.EmployeeShifts)
             .HasForeignKey(x => x.ShiftId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── AttendanceLog ────────────────────────────────────────────────────
        modelBuilder.Entity<AttendanceLog>(e =>
        {
            e.ToTable("AttendanceLog");
            e.HasKey(x => x.Id);
            e.Property(x => x.PinOnDevice).HasMaxLength(20).IsRequired();
            e.Property(x => x.PunchType).HasColumnType("tinyint")
             .HasConversion<byte>();
            e.Property(x => x.VerifyMethod).HasColumnType("tinyint")
             .HasConversion<byte>();
            e.Property(x => x.WorkCode).HasMaxLength(10);
            e.Property(x => x.RawData).HasMaxLength(500);
            e.Property(x => x.IsProcessed).HasDefaultValue(false);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.HasOne(x => x.Device)
             .WithMany(d => d.AttendanceLogs)
             .HasForeignKey(x => x.DeviceId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Employee)
             .WithMany(emp => emp.AttendanceLogs)
             .HasForeignKey(x => x.EmployeeId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ── AttendanceRecord ─────────────────────────────────────────────────
        modelBuilder.Entity<AttendanceRecord>(e =>
        {
            e.ToTable("AttendanceRecord");
            e.HasKey(x => x.Id);
            e.Property(x => x.HoursWorked).HasColumnType("decimal(5,2)");
            e.Property(x => x.OvertimeHours).HasColumnType("decimal(5,2)").HasDefaultValue(0m);
            e.Property(x => x.LateMinutes).HasDefaultValue(0);
            e.Property(x => x.Status).HasColumnType("tinyint").HasConversion<byte>()
             .HasDefaultValue(AttendanceStatus.Pending);
            e.Property(x => x.Notes).HasMaxLength(200);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.HasIndex(x => new { x.EmployeeId, x.WorkDate }).IsUnique();
            e.HasOne(x => x.Employee)
             .WithMany(emp => emp.AttendanceRecords)
             .HasForeignKey(x => x.EmployeeId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Shift)
             .WithMany(s => s.AttendanceRecords)
             .HasForeignKey(x => x.ShiftId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ── Incident ─────────────────────────────────────────────────────────
        modelBuilder.Entity<Incident>(e =>
        {
            e.ToTable("Incident");
            e.HasKey(x => x.Id);
            e.Property(x => x.IncidentType).HasColumnType("tinyint").HasConversion<byte>();
            e.Property(x => x.Description).HasMaxLength(300);
            e.Property(x => x.ApprovedBy).HasMaxLength(100);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.HasOne(x => x.Employee)
             .WithMany(emp => emp.Incidents)
             .HasForeignKey(x => x.EmployeeId)
             .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
