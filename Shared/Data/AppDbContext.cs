using Microsoft.EntityFrameworkCore;
using Domain.Entities;

namespace Shared.Data;

public partial class AppDbContext : DbContext
{
    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Branch> Branches { get; set; } = null!;
    public DbSet<Role> Roles { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<KioskDevice> KioskDevices { get; set; } = null!;
    public DbSet<KioskActivationCode> KioskActivationCodes { get; set; } = null!;
    public DbSet<ShiftTemplate> ShiftTemplates { get; set; } = null!;
    public DbSet<WorkSchedule> WorkSchedules { get; set; } = null!;
    public DbSet<ShiftAssignment> ShiftAssignments { get; set; } = null!;
    public DbSet<ShiftSwapRequest> ShiftSwapRequests { get; set; } = null!;
    public DbSet<TemporaryDispatch> TemporaryDispatches { get; set; } = null!;
    public DbSet<AttendanceLog> AttendanceLogs { get; set; } = null!;
    public DbSet<OvertimeRequest> OvertimeRequests { get; set; } = null!;
    public DbSet<ShiftHandover> ShiftHandovers { get; set; } = null!;
    public DbSet<CashHandover> CashHandovers { get; set; } = null!;
    public DbSet<SecurityHandover> SecurityHandovers { get; set; } = null!;
    public DbSet<MonthlyTimesheet> MonthlyTimesheets { get; set; } = null!;
    public DbSet<SystemAuditLog> SystemAuditLogs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. branches
        modelBuilder.Entity<Branch>(entity =>
        {
            entity.ToTable("branches");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.BranchCode).IsUnique();
        });

        // 2. roles
        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("roles");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.RoleCode).IsUnique();
        });

        // 3. users
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.EmployeeCode).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.Phone).IsUnique();
            entity.HasIndex(e => new { e.HomeBranchId, e.Status });

            entity.HasOne(e => e.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.HomeBranch)
                .WithMany(b => b.Users)
                .HasForeignKey(e => e.HomeBranchId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // 4. kiosks
        modelBuilder.Entity<KioskDevice>(entity =>
        {
            entity.ToTable("kiosks");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.KioskCode).IsUnique();
            entity.HasIndex(e => e.DeviceToken).IsUnique();

            entity.HasOne(e => e.Branch)
                .WithMany(b => b.Kiosks)
                .HasForeignKey(e => e.BranchId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 5. kiosk_activation_codes
        modelBuilder.Entity<KioskActivationCode>(entity =>
        {
            entity.ToTable("kiosk_activation_codes");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Code);

            entity.HasOne(e => e.Branch)
                .WithMany()
                .HasForeignKey(e => e.BranchId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.GeneratedByUser)
                .WithMany()
                .HasForeignKey(e => e.GeneratedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 6. shift_templates
        modelBuilder.Entity<ShiftTemplate>(entity =>
        {
            entity.ToTable("shift_templates");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TemplateCode).IsUnique();
        });

        // 7. work_schedules
        modelBuilder.Entity<WorkSchedule>(entity =>
        {
            entity.ToTable("work_schedules");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.BranchId, e.ShiftTemplateId, e.WorkDate }).IsUnique();
            entity.HasIndex(e => new { e.BranchId, e.WorkDate, e.Status });

            entity.HasOne(e => e.Branch)
                .WithMany(b => b.WorkSchedules)
                .HasForeignKey(e => e.BranchId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.ShiftTemplate)
                .WithMany(st => st.WorkSchedules)
                .HasForeignKey(e => e.ShiftTemplateId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.CreatedByUser)
                .WithMany()
                .HasForeignKey(e => e.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 8. shift_assignments
        modelBuilder.Entity<ShiftAssignment>(entity =>
        {
            entity.ToTable("shift_assignments");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ScheduleId, e.UserId }).IsUnique();
            entity.HasIndex(e => new { e.UserId, e.ScheduleId });

            entity.HasOne(e => e.Schedule)
                .WithMany(ws => ws.ShiftAssignments)
                .HasForeignKey(e => e.ScheduleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany(u => u.ShiftAssignments)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.AssignedRole)
                .WithMany()
                .HasForeignKey(e => e.AssignedRoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 9. shift_swap_requests
        modelBuilder.Entity<ShiftSwapRequest>(entity =>
        {
            entity.ToTable("shift_swap_requests");
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.RequestingAssignment)
                .WithMany()
                .HasForeignKey(e => e.RequestingAssignmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.TargetAssignment)
                .WithMany()
                .HasForeignKey(e => e.TargetAssignmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ReviewedByUser)
                .WithMany()
                .HasForeignKey(e => e.ReviewedBy)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // 10. temporary_dispatches
        modelBuilder.Entity<TemporaryDispatch>(entity =>
        {
            entity.ToTable("temporary_dispatches");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.StartDate, e.EndDate, e.Status });

            entity.HasOne(e => e.SourceBranch)
                .WithMany()
                .HasForeignKey(e => e.SourceBranchId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.TargetBranch)
                .WithMany()
                .HasForeignKey(e => e.TargetBranchId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.RequestedByUser)
                .WithMany()
                .HasForeignKey(e => e.RequestedBy)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ApprovedByUser)
                .WithMany()
                .HasForeignKey(e => e.ApprovedBy)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // 11. attendance_logs
        modelBuilder.Entity<AttendanceLog>(entity =>
        {
            entity.ToTable("attendance_logs");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.AssignmentId).IsUnique();
            entity.HasIndex(e => new { e.CheckInTime, e.BranchId });

            entity.HasOne(e => e.Assignment)
                .WithOne(sa => sa.AttendanceLog)
                .HasForeignKey<AttendanceLog>(e => e.AssignmentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Branch)
                .WithMany()
                .HasForeignKey(e => e.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Kiosk)
                .WithMany()
                .HasForeignKey(e => e.KioskId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.FraudFlaggedByUser)
                .WithMany()
                .HasForeignKey(e => e.FraudFlaggedBy)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // 12. overtime_requests
        modelBuilder.Entity<OvertimeRequest>(entity =>
        {
            entity.ToTable("overtime_requests");
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.AttendanceLog)
                .WithMany(al => al.OvertimeRequests)
                .HasForeignKey(e => e.AttendanceLogId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.VerifiedByLeaderUser)
                .WithMany()
                .HasForeignKey(e => e.VerifiedByLeader)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.ApprovedByManagerUser)
                .WithMany()
                .HasForeignKey(e => e.ApprovedByManager)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // 13. shift_handovers
        modelBuilder.Entity<ShiftHandover>(entity =>
        {
            entity.ToTable("shift_handovers");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ScheduleId).IsUnique();

            entity.HasOne(e => e.Schedule)
                .WithOne(ws => ws.ShiftHandover)
                .HasForeignKey<ShiftHandover>(e => e.ScheduleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.ShiftLeader)
                .WithMany()
                .HasForeignKey(e => e.ShiftLeaderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 14. cash_handovers
        modelBuilder.Entity<CashHandover>(entity =>
        {
            entity.ToTable("cash_handovers");
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.ShiftHandover)
                .WithMany(sh => sh.CashHandovers)
                .HasForeignKey(e => e.ShiftHandoverId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Cashier)
                .WithMany()
                .HasForeignKey(e => e.CashierId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 15. security_handovers
        modelBuilder.Entity<SecurityHandover>(entity =>
        {
            entity.ToTable("security_handovers");
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.ShiftHandover)
                .WithMany(sh => sh.SecurityHandovers)
                .HasForeignKey(e => e.ShiftHandoverId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.SecurityGuard)
                .WithMany()
                .HasForeignKey(e => e.SecurityGuardId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 16. monthly_timesheets
        modelBuilder.Entity<MonthlyTimesheet>(entity =>
        {
            entity.ToTable("monthly_timesheets");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.BranchId, e.PeriodMonth, e.PeriodYear }).IsUnique();

            entity.HasOne(e => e.Branch)
                .WithMany()
                .HasForeignKey(e => e.BranchId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.LockedByUser)
                .WithMany()
                .HasForeignKey(e => e.LockedBy)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // 17. system_audit_logs
        modelBuilder.Entity<SystemAuditLog>(entity =>
        {
            entity.ToTable("system_audit_logs");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Timestamp, e.Action });

            entity.HasOne(e => e.Actor)
                .WithMany()
                .HasForeignKey(e => e.ActorId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
