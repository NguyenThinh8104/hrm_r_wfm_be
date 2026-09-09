using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using RWFM.Domain.Entities;

namespace RWFM.Infrastructure.Data;

public partial class RWFMDbContext : DbContext
{
    public RWFMDbContext()
    {
    }

    public RWFMDbContext(DbContextOptions<RWFMDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AttendanceException> AttendanceExceptions { get; set; }
    public virtual DbSet<AttendanceRecord> AttendanceRecords { get; set; }
    public virtual DbSet<AuditLog> AuditLogs { get; set; }
    public virtual DbSet<CashHandover> CashHandovers { get; set; }
    public virtual DbSet<Employee> Employees { get; set; }
    public virtual DbSet<Notification> Notifications { get; set; }
    public virtual DbSet<Position> Positions { get; set; }
    public virtual DbSet<SecurityHandover> SecurityHandovers { get; set; }
    public virtual DbSet<Shift> Shifts { get; set; }
    public virtual DbSet<ShiftAssignment> ShiftAssignments { get; set; }
    public virtual DbSet<ShiftHandoverSession> ShiftHandoverSessions { get; set; }
    public virtual DbSet<ShiftSwapRequest> ShiftSwapRequests { get; set; }
    public virtual DbSet<Store> Stores { get; set; }
    public virtual DbSet<TemporaryDispatch> TemporaryDispatches { get; set; }
    public virtual DbSet<User> Users { get; set; }
    public virtual DbSet<WorkSchedule> WorkSchedules { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlServer("Server=localhost;Database=R_WFM_DB;Trusted_Connection=True;TrustServerCertificate=True;");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AttendanceException>(entity =>
        {
            entity.HasKey(e => e.ExceptionId).HasName("PK__Attendan__26981D8886A6E369");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.ExceptionType).HasMaxLength(30);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Pending");

            entity.HasOne(d => d.Attendance).WithMany(p => p.AttendanceExceptions)
                .HasForeignKey(d => d.AttendanceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Exception_Attendance");

            entity.HasOne(d => d.ReportedByNavigation).WithMany(p => p.AttendanceExceptionReportedByNavigations)
                .HasForeignKey(d => d.ReportedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Exception_Reporter");

            entity.HasOne(d => d.ResolvedByNavigation).WithMany(p => p.AttendanceExceptionResolvedByNavigations)
                .HasForeignKey(d => d.ResolvedBy)
                .HasConstraintName("FK_Exception_Resolver");
        });

        modelBuilder.Entity<AttendanceRecord>(entity =>
        {
            entity.HasKey(e => e.AttendanceId).HasName("PK__Attendan__8B69261CBF2CADA3");

            entity.HasIndex(e => e.EmployeeId, "IX_Attendance_Employee");

            entity.HasIndex(e => e.StoreId, "IX_Attendance_Store");

            entity.Property(e => e.CheckInMethod).HasMaxLength(20);
            entity.Property(e => e.CheckOutMethod).HasMaxLength(20);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .HasDefaultValue("Present");

            entity.HasOne(d => d.Assignment).WithMany(p => p.AttendanceRecords)
                .HasForeignKey(d => d.AssignmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Attendance_Assignment");

            entity.HasOne(d => d.Employee).WithMany(p => p.AttendanceRecords)
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Attendance_Employee");

            entity.HasOne(d => d.Store).WithMany(p => p.AttendanceRecords)
                .HasForeignKey(d => d.StoreId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Attendance_Store");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.AuditLogId).HasName("PK__AuditLog__EB5F6CBDF6B5B4CE");

            entity.Property(e => e.Action).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.EntityName).HasMaxLength(100);
            entity.Property(e => e.IpAddress)
                .HasMaxLength(45)
                .IsUnicode(false);

            entity.HasOne(d => d.User).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_AuditLogs_User");
        });

        modelBuilder.Entity<CashHandover>(entity =>
        {
            entity.HasKey(e => e.CashHandoverId).HasName("PK__CashHand__F3BBCA164C9D4280");

            entity.Property(e => e.ActualCash).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.DifferenceAmount)
                .HasComputedColumnSql("(case when [ActualCash] IS NULL then NULL else [ActualCash]-[OpeningFloat] end)", true)
                .HasColumnType("decimal(19, 2)");
            entity.Property(e => e.DifferenceNote).HasMaxLength(500);
            entity.Property(e => e.OpeningFloat).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.CashierEmployee).WithMany(p => p.CashHandovers)
                .HasForeignKey(d => d.CashierEmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CashHandover_Employee");

            entity.HasOne(d => d.Handover).WithMany(p => p.CashHandovers)
                .HasForeignKey(d => d.HandoverId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CashHandover_Session");
        });

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.HasKey(e => e.EmployeeId).HasName("PK__Employee__7AD04F1113F33E18");

            entity.HasIndex(e => e.PositionId, "IX_Employees_Position");

            entity.HasIndex(e => e.PrimaryStoreId, "IX_Employees_PrimaryStore");

            entity.HasIndex(e => e.UserId, "UQ_Employees_UserId").IsUnique();

            entity.HasIndex(e => e.EmployeeCode, "UQ__Employee__1F642548DAE225BC").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Email)
                .HasMaxLength(150)
                .IsUnicode(false);
            entity.Property(e => e.EmployeeCode)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.FullName).HasMaxLength(150);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.PinHash).HasMaxLength(500);

            entity.HasOne(d => d.Position).WithMany(p => p.Employees)
                .HasForeignKey(d => d.PositionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Employees_Positions");

            entity.HasOne(d => d.PrimaryStore).WithMany(p => p.Employees)
                .HasForeignKey(d => d.PrimaryStoreId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Employees_Stores");

            entity.HasOne(d => d.User).WithOne(p => p.Employee)
                .HasForeignKey<Employee>(d => d.UserId)
                .HasConstraintName("FK_Employees_Users");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotificationId).HasName("PK__Notifica__20CF2E12AFE5BF0A");

            entity.HasIndex(e => new { e.EmployeeId, e.IsRead }, "IX_Notifications_Employee");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Message).HasMaxLength(1000);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Type).HasMaxLength(30);

            entity.HasOne(d => d.Employee).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Notifications_Employee");
        });

        modelBuilder.Entity<Position>(entity =>
        {
            entity.HasKey(e => e.PositionId).HasName("PK__Position__60BB9A798B32D738");

            entity.HasIndex(e => e.PositionCode, "UQ__Position__83745B020B5CF3B3").IsUnique();

            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.PositionCode)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.PositionName).HasMaxLength(100);
        });

        modelBuilder.Entity<SecurityHandover>(entity =>
        {
            entity.HasKey(e => e.SecurityHandoverId).HasName("PK__Security__9E160887FF713161");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.SecurityNote).HasMaxLength(500);

            entity.HasOne(d => d.Handover).WithMany(p => p.SecurityHandovers)
                .HasForeignKey(d => d.HandoverId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SecurityHandover_Session");

            entity.HasOne(d => d.SecurityEmployee).WithMany(p => p.SecurityHandovers)
                .HasForeignKey(d => d.SecurityEmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SecurityHandover_Employee");
        });

        modelBuilder.Entity<Shift>(entity =>
        {
            entity.HasKey(e => e.ShiftId).HasName("PK__Shifts__C0A83881E26348B8");

            entity.HasIndex(e => e.ShiftCode, "UQ__Shifts__9377D56208114F91").IsUnique();

            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.ShiftCode)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.ShiftName).HasMaxLength(100);
        });

        modelBuilder.Entity<ShiftAssignment>(entity =>
        {
            entity.HasKey(e => e.AssignmentId).HasName("PK__ShiftAss__32499E77026D4562");

            entity.HasIndex(e => new { e.EmployeeId, e.WorkDate }, "IX_ShiftAssignments_Employee_WorkDate");

            entity.HasIndex(e => new { e.StoreId, e.WorkDate }, "IX_ShiftAssignments_Store_WorkDate");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Scheduled");

            entity.HasOne(d => d.Employee).WithMany(p => p.ShiftAssignments)
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ShiftAssignments_Employee");

            entity.HasOne(d => d.Schedule).WithMany(p => p.ShiftAssignments)
                .HasForeignKey(d => d.ScheduleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ShiftAssignments_Schedule");

            entity.HasOne(d => d.Shift).WithMany(p => p.ShiftAssignments)
                .HasForeignKey(d => d.ShiftId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ShiftAssignments_Shift");

            entity.HasOne(d => d.Store).WithMany(p => p.ShiftAssignments)
                .HasForeignKey(d => d.StoreId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ShiftAssignments_Store");
        });

        modelBuilder.Entity<ShiftHandoverSession>(entity =>
        {
            entity.HasKey(e => e.HandoverId).HasName("PK__ShiftHan__DB2A1F81EB4E7EA3");

            entity.Property(e => e.ManagerNote).HasMaxLength(1000);
            entity.Property(e => e.OpenedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Open");

            entity.HasOne(d => d.Assignment).WithMany(p => p.ShiftHandoverSessions)
                .HasForeignKey(d => d.AssignmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Handover_Assignment");

            entity.HasOne(d => d.ClosedByNavigation).WithMany(p => p.ShiftHandoverSessionClosedByNavigations)
                .HasForeignKey(d => d.ClosedBy)
                .HasConstraintName("FK_Handover_ClosedBy");

            entity.HasOne(d => d.OpenedByNavigation).WithMany(p => p.ShiftHandoverSessionOpenedByNavigations)
                .HasForeignKey(d => d.OpenedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Handover_OpenedBy");

            entity.HasOne(d => d.Store).WithMany(p => p.ShiftHandoverSessions)
                .HasForeignKey(d => d.StoreId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Handover_Store");
        });

        modelBuilder.Entity<ShiftSwapRequest>(entity =>
        {
            entity.HasKey(e => e.SwapRequestId).HasName("PK__ShiftSwa__EEF5A489E9A0BB3E");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Pending");

            entity.HasOne(d => d.Assignment).WithMany(p => p.ShiftSwapRequests)
                .HasForeignKey(d => d.AssignmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ShiftSwap_Assignment");

            entity.HasOne(d => d.RequesterEmployee).WithMany(p => p.ShiftSwapRequestRequesterEmployees)
                .HasForeignKey(d => d.RequesterEmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ShiftSwap_Requester");

            entity.HasOne(d => d.ReviewedByNavigation).WithMany(p => p.ShiftSwapRequestReviewedByNavigations)
                .HasForeignKey(d => d.ReviewedBy)
                .HasConstraintName("FK_ShiftSwap_Reviewer");

            entity.HasOne(d => d.TargetEmployee).WithMany(p => p.ShiftSwapRequestTargetEmployees)
                .HasForeignKey(d => d.TargetEmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ShiftSwap_Target");
        });

        modelBuilder.Entity<Store>(entity =>
        {
            entity.HasKey(e => e.StoreId).HasName("PK__Stores__3B82F1019E9B97C7");

            entity.HasIndex(e => e.StoreCode, "UQ__Stores__02A384F8D8EB8161").IsUnique();

            entity.Property(e => e.Address).HasMaxLength(255);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.StoreCode)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.StoreName).HasMaxLength(100);
        });

        modelBuilder.Entity<TemporaryDispatch>(entity =>
        {
            entity.HasKey(e => e.DispatchId).HasName("PK__Temporar__434DBD5501B43404");

            entity.HasIndex(e => new { e.EmployeeId, e.StartDate, e.EndDate }, "IX_Dispatch_Employee_Date");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Pending");

            entity.HasOne(d => d.ApprovedByNavigation).WithMany(p => p.TemporaryDispatchApprovedByNavigations)
                .HasForeignKey(d => d.ApprovedBy)
                .HasConstraintName("FK_Dispatch_ApprovedBy");

            entity.HasOne(d => d.Employee).WithMany(p => p.TemporaryDispatchEmployees)
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Dispatch_Employee");

            entity.HasOne(d => d.FromStore).WithMany(p => p.TemporaryDispatchFromStores)
                .HasForeignKey(d => d.FromStoreId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Dispatch_FromStore");

            entity.HasOne(d => d.RequestedByNavigation).WithMany(p => p.TemporaryDispatchRequestedByNavigations)
                .HasForeignKey(d => d.RequestedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Dispatch_RequestedBy");

            entity.HasOne(d => d.ToStore).WithMany(p => p.TemporaryDispatchToStores)
                .HasForeignKey(d => d.ToStoreId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Dispatch_ToStore");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Users__1788CC4CD146E62B");

            entity.HasIndex(e => e.Username, "UQ__Users__536C85E49C6B9BC8").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.PasswordHash).HasMaxLength(500);
            entity.Property(e => e.Role).HasMaxLength(30);
            entity.Property(e => e.Username).HasMaxLength(50);
        });

        modelBuilder.Entity<WorkSchedule>(entity =>
        {
            entity.HasKey(e => e.ScheduleId).HasName("PK__WorkSche__9C8A5B492182D683");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Draft");

            entity.HasOne(d => d.PublishedByNavigation).WithMany(p => p.WorkSchedules)
                .HasForeignKey(d => d.PublishedBy)
                .HasConstraintName("FK_WorkSchedules_PublishedBy");

            entity.HasOne(d => d.Store).WithMany(p => p.WorkSchedules)
                .HasForeignKey(d => d.StoreId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_WorkSchedules_Stores");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
