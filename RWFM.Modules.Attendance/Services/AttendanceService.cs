using Microsoft.EntityFrameworkCore;
using RWFM.Domain.Entities;
using RWFM.Modules.Attendance.DTOs;
using RWFM.Modules.Attendance.Interfaces;
using RWFM.Shared.Common;
using RWFM.Shared.Data;
using RWFM.Shared.Security;

namespace RWFM.Modules.Attendance.Services;

public class AttendanceService : IAttendanceService
{
    private readonly RWFMDbContext _context;

    public AttendanceService(RWFMDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<List<KioskEmployeeRosterDto>>> GetKioskRosterAsync(int storeId, DateOnly date)
    {
        var assignments = await _context.ShiftAssignments
            .Include(sa => sa.Employee)
                .ThenInclude(e => e.Position)
            .Include(sa => sa.Shift)
            .Include(sa => sa.AttendanceRecords)
            .Where(sa => sa.StoreId == storeId && sa.WorkDate == date)
            .OrderBy(sa => sa.Shift.StartTime)
            .ToListAsync();

        var result = assignments.Select(sa =>
        {
            var att = sa.AttendanceRecords.OrderByDescending(a => a.CreatedAt).FirstOrDefault();
            return new KioskEmployeeRosterDto
            {
                EmployeeId = sa.EmployeeId,
                EmployeeCode = sa.Employee.EmployeeCode,
                FullName = sa.Employee.FullName,
                PositionName = sa.Employee.Position.PositionName,
                AssignmentId = sa.AssignmentId,
                ShiftName = sa.Shift.ShiftName,
                StartTime = sa.Shift.StartTime,
                EndTime = sa.Shift.EndTime,
                HasCheckedIn = att?.CheckInTime != null,
                HasCheckedOut = att?.CheckOutTime != null,
                CheckInTime = att?.CheckInTime,
                CheckOutTime = att?.CheckOutTime,
                IsDispatched = sa.Employee.PrimaryStoreId != sa.StoreId
            };
        }).ToList();

        return ApiResponse<List<KioskEmployeeRosterDto>>.Ok(result);
    }

    public async Task<ApiResponse<AttendanceRecordDto>> KioskCheckInAsync(KioskPinCheckInDto request)
    {
        var employee = await _context.Employees
            .Include(e => e.Position)
            .FirstOrDefaultAsync(e => e.EmployeeId == request.EmployeeId && e.IsActive);

        if (employee == null) return ApiResponse<AttendanceRecordDto>.Fail("Không tìm thấy nhân viên.");

        if (string.IsNullOrEmpty(employee.PinHash) || !PasswordHasher.Verify(request.PinCode.Trim(), employee.PinHash))
        {
            return ApiResponse<AttendanceRecordDto>.Fail("Mã PIN không chính xác.");
        }

        var today = DateOnly.FromDateTime(DateTime.Now);

        var assignment = await _context.ShiftAssignments
            .Include(sa => sa.Shift)
            .Include(sa => sa.Store)
            .Include(sa => sa.AttendanceRecords)
            .FirstOrDefaultAsync(sa => sa.EmployeeId == request.EmployeeId && sa.StoreId == request.StoreId && sa.WorkDate == today);

        if (assignment == null)
        {
            return ApiResponse<AttendanceRecordDto>.Fail($"Nhân viên {employee.FullName} không có lịch trực hôm nay ({today:dd/MM/yyyy}) tại chi nhánh này.");
        }

        var existingAtt = assignment.AttendanceRecords.FirstOrDefault(a => a.CheckInTime != null);
        if (existingAtt != null)
        {
            return ApiResponse<AttendanceRecordDto>.Fail("Nhân viên đã check-in ca này rồi.");
        }

        var now = DateTime.UtcNow;
        var status = "Present";

        var shiftStartDt = today.ToDateTime(assignment.Shift.StartTime);
        if (DateTime.Now > shiftStartDt.AddMinutes(15))
        {
            status = "Late";
        }

        var record = new AttendanceRecord
        {
            AssignmentId = assignment.AssignmentId,
            EmployeeId = employee.EmployeeId,
            StoreId = request.StoreId,
            CheckInTime = now,
            CheckInMethod = "Kiosk",
            Status = status,
            CreatedAt = now
        };

        _context.AttendanceRecords.Add(record);
        assignment.Status = "Scheduled";

        if (employee.Position.PositionCode == "CASHIER" && request.OpeningFloatCash.HasValue)
        {
            var session = await _context.ShiftHandoverSessions
                .FirstOrDefaultAsync(s => s.StoreId == request.StoreId && s.AssignmentId == assignment.AssignmentId && s.ShiftDate == today);

            if (session == null)
            {
                session = new ShiftHandoverSession
                {
                    AssignmentId = assignment.AssignmentId,
                    StoreId = request.StoreId,
                    ShiftDate = today,
                    Status = "Open",
                    OpenedBy = employee.EmployeeId,
                    OpenedAt = now
                };
                _context.ShiftHandoverSessions.Add(session);
                await _context.SaveChangesAsync();

                var cashHandover = new CashHandover
                {
                    HandoverId = session.HandoverId,
                    CashierEmployeeId = employee.EmployeeId,
                    OpeningFloat = request.OpeningFloatCash.Value,
                    CreatedAt = now
                };
                _context.CashHandovers.Add(cashHandover);
            }
        }

        await _context.SaveChangesAsync();

        return ApiResponse<AttendanceRecordDto>.Ok(new AttendanceRecordDto
        {
            AttendanceId = record.AttendanceId,
            AssignmentId = assignment.AssignmentId,
            EmployeeId = employee.EmployeeId,
            EmployeeName = employee.FullName,
            EmployeeCode = employee.EmployeeCode,
            StoreId = request.StoreId,
            StoreName = assignment.Store.StoreName,
            CheckInTime = record.CheckInTime,
            CheckInMethod = record.CheckInMethod,
            Status = record.Status
        }, $"Check-in thành công lúc {record.CheckInTime?.ToLocalTime():HH:mm:ss}!");
    }

    public async Task<ApiResponse<AttendanceRecordDto>> KioskCheckOutAsync(KioskPinCheckOutDto request)
    {
        var employee = await _context.Employees.FindAsync(request.EmployeeId);
        if (employee == null) return ApiResponse<AttendanceRecordDto>.Fail("Không tìm thấy nhân viên.");

        if (string.IsNullOrEmpty(employee.PinHash) || !PasswordHasher.Verify(request.PinCode.Trim(), employee.PinHash))
        {
            return ApiResponse<AttendanceRecordDto>.Fail("Mã PIN không chính xác.");
        }

        var today = DateOnly.FromDateTime(DateTime.Now);

        var assignment = await _context.ShiftAssignments
            .Include(sa => sa.Shift)
            .Include(sa => sa.Store)
            .Include(sa => sa.AttendanceRecords)
            .FirstOrDefaultAsync(sa => sa.EmployeeId == request.EmployeeId && sa.StoreId == request.StoreId && sa.WorkDate == today);

        if (assignment == null) return ApiResponse<AttendanceRecordDto>.Fail("Không tìm thấy ca trực tương ứng.");

        var record = assignment.AttendanceRecords.FirstOrDefault(a => a.CheckInTime != null && a.CheckOutTime == null);
        if (record == null)
        {
            return ApiResponse<AttendanceRecordDto>.Fail("Chưa có lượt Check-in đầu ca hoặc đã Check-out trước đó.");
        }

        record.CheckOutTime = DateTime.UtcNow;
        record.CheckOutMethod = "Kiosk";
        record.Status = "Completed";
        assignment.Status = "Completed";

        await _context.SaveChangesAsync();

        return ApiResponse<AttendanceRecordDto>.Ok(new AttendanceRecordDto
        {
            AttendanceId = record.AttendanceId,
            AssignmentId = assignment.AssignmentId,
            EmployeeId = employee.EmployeeId,
            EmployeeName = employee.FullName,
            EmployeeCode = employee.EmployeeCode,
            StoreId = request.StoreId,
            StoreName = assignment.Store.StoreName,
            CheckInTime = record.CheckInTime,
            CheckOutTime = record.CheckOutTime,
            CheckInMethod = record.CheckInMethod,
            CheckOutMethod = record.CheckOutMethod,
            Status = record.Status
        }, $"Check-out thành công lúc {record.CheckOutTime?.ToLocalTime():HH:mm:ss}. Hẹn gặp lại bạn!");
    }

    public async Task<ApiResponse<bool>> ReportFraudAsync(int leaderEmployeeId, ReportAttendanceFraudDto request)
    {
        var record = await _context.AttendanceRecords
            .Include(ar => ar.Assignment)
            .FirstOrDefaultAsync(ar => ar.AttendanceId == request.AttendanceId);

        if (record == null) return ApiResponse<bool>.Fail("Không tìm thấy bản ghi chấm công.");

        record.Status = "Fraud";

        var exception = new AttendanceException
        {
            AttendanceId = record.AttendanceId,
            ReportedBy = leaderEmployeeId,
            ExceptionType = "Fraud",
            Description = request.Reason,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        _context.AttendanceExceptions.Add(exception);

        if (record.Assignment != null)
        {
            record.Assignment.Status = "Cancelled";
        }

        await _context.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true, "Đã ghi nhận báo cáo gian lận ca và chuyển ngoại lệ lên Cửa hàng trưởng.");
    }

    public async Task<ApiResponse<List<AttendanceRecordDto>>> GetAttendanceHistoryAsync(int storeId, DateOnly date)
    {
        var records = await _context.AttendanceRecords
            .Include(ar => ar.Employee)
            .Include(ar => ar.Store)
            .Include(ar => ar.AttendanceExceptions)
                .ThenInclude(ae => ae.ReportedByNavigation)
            .Where(ar => ar.StoreId == storeId && DateOnly.FromDateTime(ar.CreatedAt.ToLocalTime()) == date)
            .OrderByDescending(ar => ar.CreatedAt)
            .Select(ar => new AttendanceRecordDto
            {
                AttendanceId = ar.AttendanceId,
                AssignmentId = ar.AssignmentId,
                EmployeeId = ar.EmployeeId,
                EmployeeName = ar.Employee.FullName,
                EmployeeCode = ar.Employee.EmployeeCode,
                StoreId = ar.StoreId,
                StoreName = ar.Store.StoreName,
                CheckInTime = ar.CheckInTime,
                CheckOutTime = ar.CheckOutTime,
                CheckInMethod = ar.CheckInMethod,
                CheckOutMethod = ar.CheckOutMethod,
                Status = ar.Status,
                HasException = ar.AttendanceExceptions.Any(),
                ExceptionReason = ar.AttendanceExceptions.Select(ae => ae.Description).FirstOrDefault(),
                ReportedByName = ar.AttendanceExceptions.Select(ae => ae.ReportedByNavigation.FullName).FirstOrDefault()
            })
            .ToListAsync();

        return ApiResponse<List<AttendanceRecordDto>>.Ok(records);
    }
}
