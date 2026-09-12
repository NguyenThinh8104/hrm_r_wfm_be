using Microsoft.EntityFrameworkCore;
using Domain.Entities;
using Modules.Attendance.DTOs;
using Modules.Attendance.Interfaces;
using Shared.Common;
using Shared.Common.Constants;
using Shared.Data;
using Shared.Security;

namespace Modules.Attendance.Services;

public class AttendanceService : IAttendanceService
{
    private readonly AppDbContext _context;

    public AttendanceService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<ValidatePinResponseDto>> ValidatePinAsync(ValidatePinRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.EmployeeCode) || string.IsNullOrWhiteSpace(request.PinCode))
        {
            return ApiResponse<ValidatePinResponseDto>.Fail(AttendanceMessages.InvalidPin);
        }

        var employee = await _context.Employees
            .Include(e => e.Position)
            .FirstOrDefaultAsync(e => e.EmployeeCode.ToLower() == request.EmployeeCode.Trim().ToLower());

        if (employee == null)
        {
            return ApiResponse<ValidatePinResponseDto>.Fail(AttendanceMessages.EmployeeNotFound);
        }

        if (!employee.IsActive)
        {
            return ApiResponse<ValidatePinResponseDto>.Fail(AttendanceMessages.EmployeeInactive);
        }

        if (string.IsNullOrEmpty(employee.PinHash) || !PasswordHasher.Verify(request.PinCode.Trim(), employee.PinHash))
        {
            return ApiResponse<ValidatePinResponseDto>.Fail(AttendanceMessages.InvalidPin);
        }

        var today = DateOnly.FromDateTime(DateTime.Now);

        var assignment = await _context.ShiftAssignments
            .Include(sa => sa.Shift)
            .Include(sa => sa.AttendanceRecords)
            .FirstOrDefaultAsync(sa => sa.EmployeeId == employee.EmployeeId && sa.StoreId == request.StoreId && sa.WorkDate == today);

        var latestRecord = assignment?.AttendanceRecords.OrderByDescending(a => a.CreatedAt).FirstOrDefault();

        return ApiResponse<ValidatePinResponseDto>.Ok(new ValidatePinResponseDto
        {
            EmployeeId = employee.EmployeeId,
            EmployeeCode = employee.EmployeeCode,
            FullName = employee.FullName,
            PositionName = employee.Position?.PositionName ?? string.Empty,
            PositionCode = employee.Position?.PositionCode ?? string.Empty,
            PrimaryStoreId = employee.PrimaryStoreId,
            IsValid = true,
            HasShiftToday = assignment != null,
            AssignmentId = assignment?.AssignmentId,
            ShiftName = assignment?.Shift?.ShiftName,
            HasCheckedIn = latestRecord?.CheckInTime != null,
            HasCheckedOut = latestRecord?.CheckOutTime != null
        }, AttendanceMessages.PinValidationSuccess);
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

        if (employee == null) return ApiResponse<AttendanceRecordDto>.Fail(AttendanceMessages.EmployeeNotFound);

        if (string.IsNullOrEmpty(employee.PinHash) || !PasswordHasher.Verify(request.PinCode.Trim(), employee.PinHash))
        {
            return ApiResponse<AttendanceRecordDto>.Fail(AttendanceMessages.InvalidPin);
        }

        var today = DateOnly.FromDateTime(DateTime.Now);

        var assignment = await _context.ShiftAssignments
            .Include(sa => sa.Shift)
            .Include(sa => sa.Store)
            .Include(sa => sa.AttendanceRecords)
            .FirstOrDefaultAsync(sa => sa.EmployeeId == request.EmployeeId && sa.StoreId == request.StoreId && sa.WorkDate == today);

        if (assignment == null)
        {
            return ApiResponse<AttendanceRecordDto>.Fail(string.Format(AttendanceMessages.NoShiftToday, employee.FullName, today.ToString("dd/MM/yyyy")));
        }

        var existingAtt = assignment.AttendanceRecords.FirstOrDefault(a => a.CheckInTime != null);
        if (existingAtt != null)
        {
            return ApiResponse<AttendanceRecordDto>.Fail(AttendanceMessages.AlreadyCheckedIn);
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
            KioskId = request.KioskId,
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

        var successMessage = string.Format(AttendanceMessages.CheckInSuccess, record.CheckInTime?.ToLocalTime().ToString("HH:mm:ss"));

        return ApiResponse<AttendanceRecordDto>.Ok(new AttendanceRecordDto
        {
            AttendanceId = record.AttendanceId,
            AssignmentId = assignment.AssignmentId,
            EmployeeId = employee.EmployeeId,
            EmployeeName = employee.FullName,
            EmployeeCode = employee.EmployeeCode,
            StoreId = request.StoreId,
            KioskId = record.KioskId,
            StoreName = assignment.Store.StoreName,
            CheckInTime = record.CheckInTime,
            CheckInMethod = record.CheckInMethod,
            Status = record.Status
        }, successMessage);
    }

    public async Task<ApiResponse<AttendanceRecordDto>> KioskCheckOutAsync(KioskPinCheckOutDto request)
    {
        var employee = await _context.Employees.FindAsync(request.EmployeeId);
        if (employee == null) return ApiResponse<AttendanceRecordDto>.Fail(AttendanceMessages.EmployeeNotFound);

        if (string.IsNullOrEmpty(employee.PinHash) || !PasswordHasher.Verify(request.PinCode.Trim(), employee.PinHash))
        {
            return ApiResponse<AttendanceRecordDto>.Fail(AttendanceMessages.InvalidPin);
        }

        var today = DateOnly.FromDateTime(DateTime.Now);

        var assignment = await _context.ShiftAssignments
            .Include(sa => sa.Shift)
            .Include(sa => sa.Store)
            .Include(sa => sa.AttendanceRecords)
            .FirstOrDefaultAsync(sa => sa.EmployeeId == request.EmployeeId && sa.StoreId == request.StoreId && sa.WorkDate == today);

        if (assignment == null) return ApiResponse<AttendanceRecordDto>.Fail(AttendanceMessages.ShiftNotFound);

        var record = assignment.AttendanceRecords.FirstOrDefault(a => a.CheckInTime != null && a.CheckOutTime == null);
        if (record == null)
        {
            return ApiResponse<AttendanceRecordDto>.Fail(AttendanceMessages.NotCheckedIn);
        }

        record.CheckOutTime = DateTime.UtcNow;
        record.CheckOutMethod = "Kiosk";
        if (request.KioskId.HasValue) record.KioskId = request.KioskId.Value;
        record.Status = "Completed";
        assignment.Status = "Completed";

        await _context.SaveChangesAsync();

        var successMessage = string.Format(AttendanceMessages.CheckOutSuccess, record.CheckOutTime?.ToLocalTime().ToString("HH:mm:ss"));

        return ApiResponse<AttendanceRecordDto>.Ok(new AttendanceRecordDto
        {
            AttendanceId = record.AttendanceId,
            AssignmentId = assignment.AssignmentId,
            EmployeeId = employee.EmployeeId,
            EmployeeName = employee.FullName,
            EmployeeCode = employee.EmployeeCode,
            StoreId = request.StoreId,
            KioskId = record.KioskId,
            StoreName = assignment.Store.StoreName,
            CheckInTime = record.CheckInTime,
            CheckOutTime = record.CheckOutTime,
            CheckInMethod = record.CheckInMethod,
            CheckOutMethod = record.CheckOutMethod,
            Status = record.Status
        }, successMessage);
    }

    public async Task<ApiResponse<bool>> ReportFraudAsync(int leaderEmployeeId, ReportAttendanceFraudDto request)
    {
        var record = await _context.AttendanceRecords
            .Include(ar => ar.Assignment)
            .FirstOrDefaultAsync(ar => ar.AttendanceId == request.AttendanceId);

        if (record == null) return ApiResponse<bool>.Fail(AttendanceMessages.AttendanceRecordNotFound);

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

        return ApiResponse<bool>.Ok(true, AttendanceMessages.FraudReportSuccess);
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
                KioskId = ar.KioskId,
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

