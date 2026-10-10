using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.Shifts.Common;
using Modules.Shifts.DTOs;
using Modules.Shifts.Services;
using Moq;
using Shared.Data;
using Shared.Interfaces;
using Xunit;

namespace Stores.Tests.Services;

public class ShiftTemplateRulesTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Mock<IEmailService> _emailMock;
    private readonly Mock<ILogger<ShiftService>> _loggerMock;
    private readonly ShiftService _service;

    public ShiftTemplateRulesTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"rwfm_shifts_test_{Guid.NewGuid()}")
            .Options;

        _context = new AppDbContext(options);
        _emailMock = new Mock<IEmailService>();
        _loggerMock = new Mock<ILogger<ShiftService>>();
        _service = new ShiftService(_context, _emailMock.Object, _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task Rule2_CannotSwitchToCustom_WhenBranchHasNoActiveCustomShifts_ReturnsBadRequest()
    {
        // Arrange
        var branch = new Branch
        {
            Id = 1,
            BranchCode = "CH01",
            Name = "Chi nhánh Quận 1",
            Address = "123 Lê Lợi",
            ShiftMode = "GLOBAL",
            Status = "ACTIVE",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Branches.Add(branch);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.UpdateBranchShiftModeAsync(branch.Id, "CUSTOM", confirm: false);

        // Assert
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Code.Should().Be("NO_ACTIVE_CUSTOM_SHIFT");
    }

    [Fact]
    public async Task Rule2_CanSwitchToCustom_WhenBranchHasActiveCustomShift()
    {
        // Arrange
        var branch = new Branch
        {
            Id = 1,
            BranchCode = "CH01",
            Name = "Chi nhánh Quận 1",
            Address = "123 Lê Lợi",
            ShiftMode = "GLOBAL",
            Status = "ACTIVE",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Branches.Add(branch);

        var customShift = new ShiftTemplate
        {
            Id = 10,
            TemplateCode = "CH01-S1",
            Name = "Ca sáng riêng",
            Scope = "BRANCH",
            BranchId = branch.Id,
            ShiftType = "SANG",
            StartTime = new TimeOnly(7, 0),
            EndTime = new TimeOnly(15, 0),
            BreakDurationMinutes = 60,
            IsOvernight = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.ShiftTemplates.Add(customShift);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.UpdateBranchShiftModeAsync(branch.Id, "CUSTOM", confirm: false);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.ShiftMode.Should().Be("CUSTOM");

        var updatedBranch = await _context.Branches.FindAsync(branch.Id);
        updatedBranch!.ShiftMode.Should().Be("CUSTOM");

        // Verify audit log
        var auditLog = await _context.SystemAuditLogs
            .FirstOrDefaultAsync(l => l.Action == "UPDATE_BRANCH_SHIFT_MODE");
        auditLog.Should().NotBeNull();
    }

    [Fact]
    public async Task Rule3_SwitchShiftMode_WithFutureSchedules_RequiresConfirm_ReturnsConflict()
    {
        // Arrange
        var branch = new Branch
        {
            Id = 2,
            BranchCode = "CH02",
            Name = "Chi nhánh Quận 2",
            Address = "456 Mai Chí Thọ",
            ShiftMode = "GLOBAL",
            Status = "ACTIVE",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Branches.Add(branch);

        var customShift = new ShiftTemplate
        {
            Id = 20,
            TemplateCode = "CH02-S1",
            Name = "Ca riêng CH02",
            Scope = "BRANCH",
            BranchId = branch.Id,
            ShiftType = "SANG",
            StartTime = new TimeOnly(7, 0),
            EndTime = new TimeOnly(15, 0),
            BreakDurationMinutes = 60,
            IsOvernight = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.ShiftTemplates.Add(customShift);

        // Add a future schedule
        var futureSchedule = new WorkSchedule
        {
            Id = 100,
            BranchId = branch.Id,
            ShiftTemplateId = 20,
            WorkDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
            Status = "CONFIRMED",
            CreatedAt = DateTime.UtcNow
        };
        _context.WorkSchedules.Add(futureSchedule);
        await _context.SaveChangesAsync();

        // Act 1: confirm = false -> should conflict 409
        var resultNoConfirm = await _service.UpdateBranchShiftModeAsync(branch.Id, "CUSTOM", confirm: false);
        resultNoConfirm.Success.Should().BeFalse();
        resultNoConfirm.StatusCode.Should().Be(409);
        resultNoConfirm.Code.Should().Be("FUTURE_SCHEDULES_EXIST");
        resultNoConfirm.FutureAssignmentCount.Should().Be(1);

        // Act 2: confirm = true -> should succeed
        var resultConfirm = await _service.UpdateBranchShiftModeAsync(branch.Id, "CUSTOM", confirm: true);
        resultConfirm.Success.Should().BeTrue();
        resultConfirm.Data!.ShiftMode.Should().Be("CUSTOM");
    }

    [Fact]
    public async Task Rule4_SoftDeleteShiftTemplate_SetsIsActiveFalse()
    {
        // Arrange
        var template = new ShiftTemplate
        {
            Id = 30,
            TemplateCode = "GLOBAL_TEST",
            Name = "Ca kiểm tra",
            Scope = "GLOBAL",
            ShiftType = "CHIEU",
            StartTime = new TimeOnly(14, 0),
            EndTime = new TimeOnly(22, 0),
            BreakDurationMinutes = 60,
            IsOvernight = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.ShiftTemplates.Add(template);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.UpdateShiftTemplateActiveAsync(template.Id, isActive: false, confirm: true);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.IsActive.Should().BeFalse();

        var reloaded = await _context.ShiftTemplates.FindAsync(template.Id);
        reloaded.Should().NotBeNull();
        reloaded!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Rule6_UpdateOrDeactivateGlobalShift_WhenBranchesUsingGlobal_RequiresConfirm()
    {
        // Arrange
        var branch = new Branch
        {
            Id = 3,
            BranchCode = "CH03",
            Name = "Chi nhánh Quận 3",
            Address = "789 Cách Mạng Tháng 8",
            ShiftMode = "GLOBAL",
            Status = "ACTIVE",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Branches.Add(branch);

        var globalShift = new ShiftTemplate
        {
            Id = 40,
            TemplateCode = "CA_CHIEU",
            Name = "Ca chiều chung",
            Scope = "GLOBAL",
            ShiftType = "CHIEU",
            StartTime = new TimeOnly(14, 0),
            EndTime = new TimeOnly(22, 0),
            BreakDurationMinutes = 60,
            IsOvernight = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.ShiftTemplates.Add(globalShift);
        await _context.SaveChangesAsync();

        // Act 1: Deactivate without confirm -> 409 IMPACT_CONFIRM_REQUIRED
        var deactivateNoConfirm = await _service.UpdateShiftTemplateActiveAsync(globalShift.Id, isActive: false, confirm: false);
        deactivateNoConfirm.Success.Should().BeFalse();
        deactivateNoConfirm.StatusCode.Should().Be(409);
        deactivateNoConfirm.Code.Should().Be("IMPACT_CONFIRM_REQUIRED");
        deactivateNoConfirm.AffectedBranchCount.Should().Be(1);

        // Act 2: Update without confirm -> 409 IMPACT_CONFIRM_REQUIRED
        var updateRequest = new UpdateShiftTemplateRequest
        {
            Name = "Ca chiều mới",
            StartTime = "14:30",
            EndTime = "22:30",
            BreakDuration = 60
        };
        var updateNoConfirm = await _service.UpdateShiftTemplateAsync(globalShift.Id, updateRequest, confirm: false);
        updateNoConfirm.Success.Should().BeFalse();
        updateNoConfirm.StatusCode.Should().Be(409);
        updateNoConfirm.Code.Should().Be("IMPACT_CONFIRM_REQUIRED");
        updateNoConfirm.AffectedBranchCount.Should().Be(1);

        // Act 3: Update with confirm -> Success
        var updateConfirm = await _service.UpdateShiftTemplateAsync(globalShift.Id, updateRequest, confirm: true);
        updateConfirm.Success.Should().BeTrue();
        updateConfirm.Data!.Name.Should().Be("Ca chiều mới");
    }

    [Fact]
    public async Task Rule8_DuplicateShiftTime_InSameSet_IsRejected()
    {
        // Arrange
        var existingGlobal = new ShiftTemplate
        {
            Id = 50,
            TemplateCode = "CA_SANG_1",
            Name = "Ca sáng 1",
            Scope = "GLOBAL",
            ShiftType = "SANG",
            StartTime = new TimeOnly(6, 0),
            EndTime = new TimeOnly(14, 0),
            BreakDurationMinutes = 60,
            IsOvernight = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.ShiftTemplates.Add(existingGlobal);
        await _context.SaveChangesAsync();

        // Act: Attempt to create another Global shift with same 06:00 - 14:00
        var request = new CreateShiftTemplateRequest
        {
            Name = "Ca sáng trùng giờ",
            Scope = "GLOBAL",
            StartTime = "06:00",
            EndTime = "14:00",
            BreakDuration = 60
        };
        var result = await _service.CreateShiftTemplateAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Code.Should().Be("DUPLICATE_SHIFT_TIME");
    }

    [Fact]
    public async Task Rule9_IsOvernight_CalculatedCorrectly_WhenEndTimeBeforeStartTime()
    {
        // Arrange
        var overnightRequest = new CreateShiftTemplateRequest
        {
            Name = "Ca đêm xuyên ngày",
            Scope = "GLOBAL",
            StartTime = "22:00",
            EndTime = "06:00",
            BreakDuration = 30
        };

        // Act
        var result = await _service.CreateShiftTemplateAsync(overnightRequest);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.IsOvernight.Should().BeTrue();
        result.Data.ShiftType.Should().Be("DEM");
    }

    [Fact]
    public async Task Rule11_WhenBranchIsLocked_DisallowsShiftModeChangeAndCustomShiftCreation()
    {
        // Arrange
        var lockedBranch = new Branch
        {
            Id = 4,
            BranchCode = "CH04",
            Name = "Chi nhánh Quận 4 (Bị khóa)",
            Address = "101 Hoàng Diệu",
            ShiftMode = "GLOBAL",
            Status = "ACTIVE",
            LockedAt = DateTime.UtcNow.AddHours(-1),
            LockReason = "Kiểm toán đột xuất",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Branches.Add(lockedBranch);
        await _context.SaveChangesAsync();

        // Act 1: Attempt to switch shift mode on locked branch -> 423
        var modeResult = await _service.UpdateBranchShiftModeAsync(lockedBranch.Id, "CUSTOM", confirm: false);
        modeResult.Success.Should().BeFalse();
        modeResult.StatusCode.Should().Be(423);
        modeResult.Code.Should().Be("BRANCH_LOCKED");

        // Act 2: Attempt to create custom shift on locked branch -> 423
        var createShiftRequest = new CreateShiftTemplateRequest
        {
            Name = "Ca riêng CH04",
            Scope = "BRANCH",
            BranchId = lockedBranch.Id,
            StartTime = "07:00",
            EndTime = "15:00",
            BreakDuration = 60
        };
        var createResult = await _service.CreateShiftTemplateAsync(createShiftRequest);
        createResult.Success.Should().BeFalse();
        createResult.StatusCode.Should().Be(423);
        createResult.Code.Should().Be("BRANCH_LOCKED");
    }

    [Fact]
    public void Rule13_PaidHoursAndNightHours_CalculatedCorrectly()
    {
        // Case 1: Day shift: 06:00 to 14:00 (8h duration), break 60 min (1h)
        // -> PaidHours = 7.0h, NightHours = 0.0h
        var startDay = new TimeOnly(6, 0);
        var endDay = new TimeOnly(14, 0);
        var isOvernightDay = ShiftCalculationHelper.IsOvernight(startDay, endDay);
        var paidDay = ShiftCalculationHelper.CalculatePaidHours(startDay, endDay, 60);
        var nightDay = ShiftCalculationHelper.CalculateNightHours(startDay, endDay);

        isOvernightDay.Should().BeFalse();
        paidDay.Should().Be(7.0);
        nightDay.Should().Be(0.0);

        // Case 2: Night shift: 22:00 to 06:00 (+1 day, 8h duration), break 30 min (0.5h)
        // -> PaidHours = 7.5h, NightHours = 8.0h (all 8h in 22:00-06:00 window)
        var startNight = new TimeOnly(22, 0);
        var endNight = new TimeOnly(6, 0);
        var isOvernightNight = ShiftCalculationHelper.IsOvernight(startNight, endNight);
        var paidNight = ShiftCalculationHelper.CalculatePaidHours(startNight, endNight, 30);
        var nightNight = ShiftCalculationHelper.CalculateNightHours(startNight, endNight);

        isOvernightNight.Should().BeTrue();
        paidNight.Should().Be(7.5);
        nightNight.Should().Be(8.0);

        // Case 3: Afternoon-to-Night shift: 14:00 to 23:00 (9h duration), break 60 min (1h)
        // -> PaidHours = 8.0h, NightHours = 1.0h (22:00 to 23:00)
        var startAft = new TimeOnly(14, 0);
        var endAft = new TimeOnly(23, 0);
        var paidAft = ShiftCalculationHelper.CalculatePaidHours(startAft, endAft, 60);
        var nightAft = ShiftCalculationHelper.CalculateNightHours(startAft, endAft);

        paidAft.Should().Be(8.0);
        nightAft.Should().Be(1.0);
    }

    [Fact]
    public async Task StoreManager_CannotViewEffectiveShifts_OfOtherBranch_ReturnsForbidden()
    {
        // Arrange
        var branch1 = new Branch
        {
            Id = 11,
            BranchCode = "CH11",
            Name = "Chi nhánh 11",
            Address = "11 Lý Tự Trọng",
            ShiftMode = "GLOBAL",
            Status = "ACTIVE",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var branch12 = new Branch
        {
            Id = 12,
            BranchCode = "CH12",
            Name = "Chi nhánh 12",
            Address = "12 Hai Bà Trưng",
            ShiftMode = "GLOBAL",
            Status = "ACTIVE",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Branches.AddRange(branch1, branch12);

        var storeManagerUser = new User
        {
            Id = 99,
            EmployeeCode = "NV099",
            FullName = "Nguyễn Văn Quản Lý",
            Email = "sm99@rwfm.vn",
            PasswordHash = "hashed",
            HomeBranchId = branch1.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(storeManagerUser);
        await _context.SaveChangesAsync();

        // Act 1: Accessing other branch (branch 12) as StoreManager -> 403 Forbidden
        var forbiddenResult = await _service.GetBranchEffectiveShiftsAsync(
            branch12.Id,
            currentUserId: storeManagerUser.Id,
            currentUserRole: "StoreManager");

        forbiddenResult.Success.Should().BeFalse();
        forbiddenResult.StatusCode.Should().Be(403);
        forbiddenResult.Code.Should().Be("FORBIDDEN");

        // Act 2: Accessing own branch (branch 11) as StoreManager -> Success
        var allowedResult = await _service.GetBranchEffectiveShiftsAsync(
            branch1.Id,
            currentUserId: storeManagerUser.Id,
            currentUserRole: "StoreManager");

        allowedResult.Success.Should().BeTrue();
        allowedResult.Data!.BranchId.Should().Be(branch1.Id);
    }
}
