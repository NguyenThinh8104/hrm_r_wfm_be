using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.Stores.DTOs;
using Modules.Stores.Services;
using Moq;
using Shared.Data;
using Xunit;

namespace Stores.Tests.Services;

public class TestTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;
    private readonly TimeZoneInfo _localTimeZone;

    public TestTimeProvider(DateTimeOffset utcNow, TimeZoneInfo? localTimeZone = null)
    {
        _utcNow = utcNow;
        _localTimeZone = localTimeZone ?? TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
    }

    public override DateTimeOffset GetUtcNow() => _utcNow;
    public override TimeZoneInfo LocalTimeZone => _localTimeZone;
    public void SetUtcNow(DateTimeOffset utcNow) => _utcNow = utcNow;
}

public class BranchLockServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly TestTimeProvider _timeProvider;
    private readonly Mock<ILogger<BranchLockService>> _loggerMock;
    private readonly BranchLockService _service;

    public BranchLockServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"rwfm_test_db_{Guid.NewGuid()}")
            .Options;

        _context = new AppDbContext(options);
        // Giả lập thời gian cố định: 2026-10-01 10:00:00 UTC (17:00 giờ Việt Nam UTC+7)
        _timeProvider = new TestTimeProvider(new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.FromHours(7)));
        _loggerMock = new Mock<ILogger<BranchLockService>>();

        _service = new BranchLockService(_context, _timeProvider, _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private async Task<Branch> CreateSampleBranchAsync(ulong id = 1, string code = "CN01", string name = "Chi nhánh Quận 1", string status = "ACTIVE")
    {
        var branch = new Branch
        {
            Id = id,
            BranchCode = code,
            Name = name,
            Address = "123 Lê Lợi, Q1, TP.HCM",
            Status = status,
            BranchTier = BranchTier.Tier2,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Kiosks = new List<KioskDevice>
            {
                new KioskDevice
                {
                    Id = id * 10 + 1,
                    BranchId = id,
                    KioskCode = $"{code}-POS01",
                    Name = $"Kiosk 1 - {code}",
                    DeviceToken = $"token_{code}_01",
                    Status = "ACTIVE",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            }
        };

        _context.Branches.Add(branch);
        await _context.SaveChangesAsync();
        return branch;
    }

    // =========================================================================
    // 1. Kiểm tra Blocker: ACTIVE_CHECKIN
    // =========================================================================
    [Fact]
    public async Task CheckLockConditionsAsync_WhenActiveCheckInExists_ReturnsActiveCheckInBlocker()
    {
        // Arrange
        var branch = await CreateSampleBranchAsync();
        var user = new User
        {
            Id = 10,
            EmployeeCode = "NV001",
            FullName = "Nguyễn Văn A",
            HomeBranchId = branch.Id,
            Status = "ACTIVE"
        };
        _context.Users.Add(user);

        var template = new ShiftTemplate { Id = 1, Name = "Ca Sáng", TemplateCode = "S1", StartTime = new TimeOnly(6, 0), EndTime = new TimeOnly(14, 0) };
        _context.ShiftTemplates.Add(template);

        var schedule = new WorkSchedule { Id = 100, BranchId = branch.Id, ShiftTemplateId = 1, WorkDate = new DateOnly(2026, 10, 1), CreatedBy = 1 };
        _context.WorkSchedules.Add(schedule);

        var assignment = new ShiftAssignment { Id = 1000, ScheduleId = 100, UserId = user.Id, AssignedRoleId = 1 };
        _context.ShiftAssignments.Add(assignment);

        // Nhân viên đã check-in lúc 06:05 nhưng chưa check-out
        var attendance = new AttendanceLog
        {
            Id = 500,
            BranchId = branch.Id,
            AssignmentId = assignment.Id,
            CheckInTime = new DateTime(2026, 10, 1, 6, 5, 0),
            CheckOutTime = null,
            Status = AttendanceLogStatus.PRESENT
        };
        _context.AttendanceLogs.Add(attendance);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CheckLockConditionsAsync(branch.Id);

        // Assert
        result.CanLock.Should().BeFalse();
        result.Blockers.Should().ContainSingle(b => b.Code == "ACTIVE_CHECKIN");
        var blocker = result.Blockers.First(b => b.Code == "ACTIVE_CHECKIN");
        blocker.Count.Should().Be(1);
        blocker.Items.Should().Contain(i => i.Name == "Nguyễn Văn A");
    }

    // =========================================================================
    // 2. Kiểm tra Blocker: ONGOING_SHIFT
    // =========================================================================
    [Fact]
    public async Task CheckLockConditionsAsync_WhenOngoingShiftExists_ReturnsOngoingShiftBlocker()
    {
        // Arrange
        var branch = await CreateSampleBranchAsync();
        // Thời gian hiện tại trong FakeTimeProvider là 10:00:00 ngày 2026-10-01
        var template = new ShiftTemplate
        {
            Id = 2,
            Name = "Ca Hành Chính",
            TemplateCode = "HC",
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(17, 0)
        };
        _context.ShiftTemplates.Add(template);

        var ongoingSchedule = new WorkSchedule
        {
            Id = 200,
            BranchId = branch.Id,
            ShiftTemplateId = template.Id,
            WorkDate = new DateOnly(2026, 10, 1),
            Status = "PUBLISHED",
            CreatedBy = 1
        };
        _context.WorkSchedules.Add(ongoingSchedule);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CheckLockConditionsAsync(branch.Id);

        // Assert
        result.CanLock.Should().BeFalse();
        result.Blockers.Should().Contain(b => b.Code == "ONGOING_SHIFT");
        var blocker = result.Blockers.First(b => b.Code == "ONGOING_SHIFT");
        blocker.Count.Should().Be(1);
        blocker.Items.Should().Contain(i => i.Id == "200");
    }

    // =========================================================================
    // 3. Kiểm tra Blocker: PENDING_REQUESTS
    // =========================================================================
    [Fact]
    public async Task CheckLockConditionsAsync_WhenPendingRequestsExist_ReturnsPendingRequestsBlocker()
    {
        // Arrange
        var branch = await CreateSampleBranchAsync();
        var user = new User { Id = 20, EmployeeCode = "NV002", FullName = "Trần Thị B", HomeBranchId = branch.Id, Status = "ACTIVE" };
        _context.Users.Add(user);

        // Đơn đổi ca / nghỉ phép đang chờ duyệt (PENDING)
        var swapRequest = new ShiftSwapRequest
        {
            Id = 88,
            RequesterUserId = user.Id,
            RequestType = "LEAVE",
            Reason = "Nghỉ ốm đột xuất",
            Status = "PENDING"
        };
        _context.ShiftSwapRequests.Add(swapRequest);

        // Đơn điều động đang chờ duyệt (PENDING)
        var dispatchReq = new TemporaryDispatch
        {
            Id = 99,
            UserId = user.Id,
            SourceBranchId = branch.Id,
            TargetBranchId = 999,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            Status = "PENDING"
        };
        _context.TemporaryDispatches.Add(dispatchReq);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CheckLockConditionsAsync(branch.Id);

        // Assert
        result.CanLock.Should().BeFalse();
        result.Blockers.Should().Contain(b => b.Code == "PENDING_REQUESTS");
        var blocker = result.Blockers.First(b => b.Code == "PENDING_REQUESTS");
        blocker.Count.Should().Be(2);
    }

    // =========================================================================
    // 4. Khóa chi nhánh: Sai mã xác nhận chi nhánh (400 Bad Request)
    // =========================================================================
    [Fact]
    public async Task LockBranchAsync_WhenConfirmBranchCodeMismatches_ReturnsBadRequest()
    {
        // Arrange
        var branch = await CreateSampleBranchAsync(code: "CN_HN01");
        var request = new LockBranchRequestDto
        {
            Reason = "Sửa chữa nâng cấp cơ sở vật chất toàn bộ cửa hàng",
            ConfirmBranchCode = "WRONG_CODE",
            StaffHandlingMode = "KeepAndBlock",
            FutureShiftHandling = "Cancel"
        };

        // Act
        var result = await _service.LockBranchAsync(branch.Id, request, "Admin", 1);

        // Assert
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Message.Should().Contain("Mã xác nhận");
    }

    // =========================================================================
    // 5. Khóa chi nhánh: Thiếu lý do hoặc lý do quá ngắn < 10 ký tự (400 Bad Request)
    // =========================================================================
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ngắn")]
    [InlineData("123456789")]
    public async Task LockBranchAsync_WhenReasonIsMissingOrTooShort_ReturnsBadRequest(string invalidReason)
    {
        // Arrange
        var branch = await CreateSampleBranchAsync(code: "CN_DN01");
        var request = new LockBranchRequestDto
        {
            Reason = invalidReason,
            ConfirmBranchCode = "CN_DN01",
            StaffHandlingMode = "KeepAndBlock",
            FutureShiftHandling = "Cancel"
        };

        // Act
        var result = await _service.LockBranchAsync(branch.Id, request, "Admin", 1);

        // Assert
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Message.Should().Contain("Lý do khóa");
    }

    // =========================================================================
    // 6. Khóa chi nhánh: Chuyển nhân sự sang chi nhánh đích đang bị khóa (400 Bad Request)
    // =========================================================================
    [Fact]
    public async Task LockBranchAsync_WhenTargetBranchIsLocked_ReturnsBadRequest()
    {
        // Arrange
        var sourceBranch = await CreateSampleBranchAsync(id: 10, code: "CN_SRC", name: "Chi nhánh Nguồn");
        var targetBranch = await CreateSampleBranchAsync(id: 20, code: "CN_DST", name: "Chi nhánh Đích Đang Khóa", status: "INACTIVE");

        var request = new LockBranchRequestDto
        {
            Reason = "Tạm dừng hoạt động để bảo dưỡng hệ thống điện lạnh",
            ConfirmBranchCode = "CN_SRC",
            StaffHandlingMode = "TransferTemporarily",
            TransferToBranchId = targetBranch.Id,
            FutureShiftHandling = "Cancel"
        };

        // Act
        var result = await _service.LockBranchAsync(sourceBranch.Id, request, "Admin", 1);

        // Assert
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Message.Should().Contain("không ở trạng thái Hoạt động");
    }

    // =========================================================================
    // 7. Khóa chi nhánh: Không cho khóa nếu chi nhánh đã ở trạng thái khóa (400 Bad Request)
    // =========================================================================
    [Fact]
    public async Task LockBranchAsync_WhenBranchAlreadyLocked_ReturnsBadRequest()
    {
        // Arrange
        var branch = await CreateSampleBranchAsync(status: "INACTIVE");
        var request = new LockBranchRequestDto
        {
            Reason = "Lý do khóa hợp lệ dài hơn mười ký tự",
            ConfirmBranchCode = branch.BranchCode,
            StaffHandlingMode = "KeepAndBlock",
            FutureShiftHandling = "Cancel"
        };

        // Act
        var result = await _service.LockBranchAsync(branch.Id, request, "Admin", 1);

        // Assert
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Message.Should().Contain("đã ở trạng thái tạm khóa");
    }

    // =========================================================================
    // 8. Khóa chi nhánh: Chạy lại kiểm tra trong transaction, nếu còn blocker trả 409 Conflict
    // =========================================================================
    [Fact]
    public async Task LockBranchAsync_WhenBlockersExist_ReturnsConflictProblem()
    {
        // Arrange
        var branch = await CreateSampleBranchAsync();
        var user = new User { Id = 70, EmployeeCode = "NV070", FullName = "Nguyễn Văn Đang Trực", HomeBranchId = branch.Id, Status = "ACTIVE" };
        _context.Users.Add(user);

        var schedule = new WorkSchedule { Id = 700, BranchId = branch.Id, ShiftTemplateId = 1, WorkDate = new DateOnly(2026, 10, 1), CreatedBy = 1 };
        _context.WorkSchedules.Add(schedule);

        var assignment = new ShiftAssignment { Id = 7000, ScheduleId = 700, UserId = user.Id, AssignedRoleId = 1 };
        _context.ShiftAssignments.Add(assignment);

        // Có nhân viên check-in chưa check-out
        var attendance = new AttendanceLog
        {
            Id = 777,
            BranchId = branch.Id,
            AssignmentId = assignment.Id,
            CheckInTime = DateTime.UtcNow.AddHours(-2),
            CheckOutTime = null,
            Status = AttendanceLogStatus.PRESENT
        };
        _context.AttendanceLogs.Add(attendance);
        await _context.SaveChangesAsync();

        var request = new LockBranchRequestDto
        {
            Reason = "Lý do khóa hợp lệ dài hơn mười ký tự",
            ConfirmBranchCode = branch.BranchCode,
            StaffHandlingMode = "KeepAndBlock",
            FutureShiftHandling = "Cancel"
        };

        // Act
        var result = await _service.LockBranchAsync(branch.Id, request, "Admin", 1);

        // Assert
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(409);
        result.Blockers.Should().ContainSingle(b => b.Code == "ACTIVE_CHECKIN");
        result.ProblemDetails.Should().NotBeNull();
    }

    // =========================================================================
    // 9. Khóa chi nhánh thành công: Đổi status, ghi log, chuyển nhân sự & ca
    // =========================================================================
    [Fact]
    public async Task LockBranchAsync_Success_LocksBranchAndCreatesLog()
    {
        // Arrange
        var sourceBranch = await CreateSampleBranchAsync(id: 100, code: "CN_100", name: "Chi nhánh 100");
        var targetBranch = await CreateSampleBranchAsync(id: 200, code: "CN_200", name: "Chi nhánh 200", status: "ACTIVE");

        var user = new User
        {
            Id = 300,
            EmployeeCode = "NV300",
            FullName = "Lê Văn C",
            HomeBranchId = sourceBranch.Id,
            Status = "ACTIVE"
        };
        _context.Users.Add(user);

        // Ca làm việc trong tương lai
        var futureDate = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime).AddDays(5);
        var futureSchedule = new WorkSchedule
        {
            Id = 400,
            BranchId = sourceBranch.Id,
            ShiftTemplateId = 1,
            WorkDate = futureDate,
            Status = "PUBLISHED",
            CreatedBy = 1,
            ShiftAssignments = new List<ShiftAssignment>
            {
                new ShiftAssignment { Id = 4000, UserId = user.Id, AssignedRoleId = 1, Status = "CONFIRMED" }
            }
        };
        _context.WorkSchedules.Add(futureSchedule);
        await _context.SaveChangesAsync();

        var request = new LockBranchRequestDto
        {
            Reason = "Khóa tạm thời để cải tạo toàn diện mặt bằng theo tiêu chuẩn mới",
            ConfirmBranchCode = "CN_100",
            StaffHandlingMode = "TransferTemporarily",
            TransferToBranchId = targetBranch.Id,
            FutureShiftHandling = "Cancel"
        };

        // Act
        var result = await _service.LockBranchAsync(sourceBranch.Id, request, "Quản trị viên Vũ", 1);

        // Assert
        result.Success.Should().BeTrue();
        result.StatusCode.Should().Be(200);

        // Kiểm tra Branch cập nhật
        var updatedBranch = await _context.Branches.FindAsync(sourceBranch.Id);
        updatedBranch.Should().NotBeNull();
        updatedBranch!.Status.Should().Be("INACTIVE");
        updatedBranch.LockedAt.Should().NotBeNull();
        updatedBranch.LockedBy.Should().Be("Quản trị viên Vũ");
        updatedBranch.LockReason.Should().Be(request.Reason);

        // Kiểm tra Kiosk chuyển sang BLOCKED
        var kiosk = await _context.KioskDevices.FirstAsync(k => k.BranchId == sourceBranch.Id);
        kiosk.Status.Should().Be("BLOCKED");

        // Kiểm tra Lệnh điều động tạm thời (TemporaryDispatch) được sinh tự động
        var dispatch = await _context.TemporaryDispatches.FirstOrDefaultAsync(d => d.UserId == user.Id);
        dispatch.Should().NotBeNull();
        dispatch!.SourceBranchId.Should().Be(sourceBranch.Id);
        dispatch.TargetBranchId.Should().Be(targetBranch.Id);
        dispatch.Status.Should().Be("APPROVED");

        // Kiểm tra Ca làm việc tương lai bị hủy (CANCELLED)
        var cancelledSchedule = await _context.WorkSchedules
            .Include(ws => ws.ShiftAssignments)
            .FirstAsync(ws => ws.Id == futureSchedule.Id);
        cancelledSchedule.Status.Should().Be("CANCELLED");
        cancelledSchedule.ShiftAssignments.First().Status.Should().Be("CANCELLED");

        // Kiểm tra BranchLockLog
        var log = await _context.BranchLockLogs.FirstOrDefaultAsync(l => l.BranchId == sourceBranch.Id);
        log.Should().NotBeNull();
        log!.Action.Should().Be("Lock");
        log.Reason.Should().Be(request.Reason);
        log.PerformedBy.Should().Be("Quản trị viên Vũ");
        log.StaffHandlingMode.Should().Be("TransferTemporarily");
        log.FutureShiftHandling.Should().Be("Cancel");
        log.TransferredToBranchId.Should().Be(targetBranch.Id);
    }

    // =========================================================================
    // 10. Mở khóa chi nhánh: Khi chi nhánh đang ACTIVE trả về 400 Bad Request
    // =========================================================================
    [Fact]
    public async Task UnlockBranchAsync_WhenBranchAlreadyActive_ReturnsBadRequest()
    {
        // Arrange
        var branch = await CreateSampleBranchAsync(status: "ACTIVE");
        var request = new UnlockBranchRequestDto { Reason = "Mở lại" };

        // Act
        var result = await _service.UnlockBranchAsync(branch.Id, request, "Admin", 1);

        // Assert
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Message.Should().Contain("hiện đang ở trạng thái Hoạt động");
    }

    // =========================================================================
    // 11. Mở khóa chi nhánh thành công: Đổi lại Hoạt động, ghi log, kích hoạt lại Kiosk
    // =========================================================================
    [Fact]
    public async Task UnlockBranchAsync_Success_UnlocksBranchAndCreatesLog()
    {
        // Arrange
        var branch = await CreateSampleBranchAsync(status: "INACTIVE");
        var kiosk = await _context.KioskDevices.FirstAsync(k => k.BranchId == branch.Id);
        kiosk.Status = "BLOCKED";
        await _context.SaveChangesAsync();

        var request = new UnlockBranchRequestDto
        {
            Reason = "Đã hoàn thành sửa chữa mặt bằng, mở cửa đón khách trở lại"
        };

        // Act
        var result = await _service.UnlockBranchAsync(branch.Id, request, "Quản trị viên Minh", 1);

        // Assert
        result.Success.Should().BeTrue();
        result.StatusCode.Should().Be(200);

        var updatedBranch = await _context.Branches.FindAsync(branch.Id);
        updatedBranch.Should().NotBeNull();
        updatedBranch!.Status.Should().Be("ACTIVE");
        updatedBranch.UnlockedAt.Should().NotBeNull();
        updatedBranch.UnlockedBy.Should().Be("Quản trị viên Minh");

        // Kiosk được kích hoạt lại ACTIVE
        var updatedKiosk = await _context.KioskDevices.FindAsync(kiosk.Id);
        updatedKiosk!.Status.Should().Be("ACTIVE");

        // Ghi BranchLockLog với Action = Unlock
        var log = await _context.BranchLockLogs
            .OrderByDescending(l => l.PerformedAt)
            .FirstOrDefaultAsync(l => l.BranchId == branch.Id && l.Action == "Unlock");

        log.Should().NotBeNull();
        log!.Action.Should().Be("Unlock");
        log.Reason.Should().Be(request.Reason);
        log.PerformedBy.Should().Be("Quản trị viên Minh");
    }
}
