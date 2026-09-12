using Microsoft.EntityFrameworkCore;
using Domain.Entities;
using Modules.Handovers.DTOs;
using Modules.Handovers.Interfaces;
using Shared.Common;
using Shared.Data;

namespace Modules.Handovers.Services;

public class HandoverService : IHandoverService
{
    private readonly AppDbContext _context;

    public HandoverService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<ShiftHandoverSessionDto>> GetOrCreateSessionAsync(int storeId, int assignmentId, DateOnly date, int openedByEmployeeId)
    {
        var session = await _context.ShiftHandoverSessions
            .Include(s => s.Store)
            .Include(s => s.Assignment)
                .ThenInclude(a => a.Shift)
            .Include(s => s.OpenedByNavigation)
            .Include(s => s.ClosedByNavigation)
            .Include(s => s.CashHandovers)
                .ThenInclude(c => c.CashierEmployee)
            .Include(s => s.SecurityHandovers)
                .ThenInclude(sec => sec.SecurityEmployee)
            .FirstOrDefaultAsync(s => s.StoreId == storeId && s.AssignmentId == assignmentId && s.ShiftDate == date);

        if (session == null)
        {
            var store = await _context.Stores.FindAsync(storeId);
            var assignment = await _context.ShiftAssignments
                .Include(a => a.Shift)
                .FirstOrDefaultAsync(a => a.AssignmentId == assignmentId);
            var opener = await _context.Employees.FindAsync(openedByEmployeeId);

            session = new ShiftHandoverSession
            {
                StoreId = storeId,
                AssignmentId = assignmentId,
                ShiftDate = date,
                Status = "Open",
                OpenedBy = openedByEmployeeId,
                OpenedAt = DateTime.UtcNow
            };

            _context.ShiftHandoverSessions.Add(session);
            await _context.SaveChangesAsync();

            session.Store = store!;
            session.Assignment = assignment!;
            session.OpenedByNavigation = opener!;
        }

        return ApiResponse<ShiftHandoverSessionDto>.Ok(MapSession(session));
    }

    public async Task<ApiResponse<ShiftHandoverSessionDto>> SubmitCashierHandoverAsync(int cashierEmployeeId, CashierHandoverSubmitDto request)
    {
        var session = await GetSessionWithIncludes(request.HandoverId);
        if (session == null) return ApiResponse<ShiftHandoverSessionDto>.Fail("Phiên giao ca không tồn tại.");

        var cash = session.CashHandovers.FirstOrDefault(c => c.CashierEmployeeId == cashierEmployeeId)
                   ?? session.CashHandovers.FirstOrDefault();

        if (cash == null)
        {
            cash = new CashHandover
            {
                HandoverId = session.HandoverId,
                CashierEmployeeId = cashierEmployeeId,
                OpeningFloat = 1000000,
                ActualCash = request.ActualCash,
                DifferenceAmount = request.ActualCash - 1000000,
                DifferenceNote = request.DifferenceNote,
                CreatedAt = DateTime.UtcNow
            };
            _context.CashHandovers.Add(cash);
        }
        else
        {
            cash.ActualCash = request.ActualCash;
            cash.DifferenceAmount = request.ActualCash - cash.OpeningFloat;
            cash.DifferenceNote = request.DifferenceNote;
        }

        await _context.SaveChangesAsync();
        return ApiResponse<ShiftHandoverSessionDto>.Ok(MapSession(session), "Cập nhật bàn giao két tiền thành công.");
    }

    public async Task<ApiResponse<ShiftHandoverSessionDto>> SubmitSecurityHandoverAsync(int securityEmployeeId, SecurityHandoverSubmitDto request)
    {
        var session = await GetSessionWithIncludes(request.HandoverId);
        if (session == null) return ApiResponse<ShiftHandoverSessionDto>.Fail("Phiên giao ca không tồn tại.");

        var sec = session.SecurityHandovers.FirstOrDefault(s => s.SecurityEmployeeId == securityEmployeeId)
                  ?? session.SecurityHandovers.FirstOrDefault();

        if (sec == null)
        {
            sec = new SecurityHandover
            {
                HandoverId = session.HandoverId,
                SecurityEmployeeId = securityEmployeeId,
                ParkingTickets = request.ParkingTickets,
                ParkingCards = request.ParkingCards,
                WarehouseLocked = request.WarehouseLocked,
                RollerDoorLocked = request.RollerDoorLocked,
                SecurityNote = request.SecurityNote,
                CreatedAt = DateTime.UtcNow
            };
            _context.SecurityHandovers.Add(sec);
        }
        else
        {
            sec.ParkingTickets = request.ParkingTickets;
            sec.ParkingCards = request.ParkingCards;
            sec.WarehouseLocked = request.WarehouseLocked;
            sec.RollerDoorLocked = request.RollerDoorLocked;
            sec.SecurityNote = request.SecurityNote;
        }

        await _context.SaveChangesAsync();
        return ApiResponse<ShiftHandoverSessionDto>.Ok(MapSession(session), "Cập nhật biên bản an ninh thành công.");
    }

    public async Task<ApiResponse<ShiftHandoverSessionDto>> LeaderSignHandoverAsync(int leaderEmployeeId, LeaderSignHandoverDto request)
    {
        var session = await GetSessionWithIncludes(request.HandoverId);
        if (session == null) return ApiResponse<ShiftHandoverSessionDto>.Fail("Phiên giao ca không tồn tại.");

        session.Status = request.IsApproved ? "Closed" : "PendingApproval";
        session.ClosedBy = leaderEmployeeId;
        session.ClosedAt = DateTime.UtcNow;
        session.ManagerNote = request.ManagerNote;

        await _context.SaveChangesAsync();

        var msg = request.IsApproved
            ? "Trưởng ca đã ký duyệt và đóng phiên làm việc thành công."
            : "Trưởng ca ghi nhận vi phạm và gửi cảnh báo chênh lệch lên Cửa hàng trưởng.";

        return ApiResponse<ShiftHandoverSessionDto>.Ok(MapSession(session), msg);
    }

    public async Task<ApiResponse<List<ShiftHandoverSessionDto>>> GetSessionsByStoreAsync(int storeId, DateOnly date)
    {
        var sessions = await _context.ShiftHandoverSessions
            .Include(s => s.Store)
            .Include(s => s.Assignment)
                .ThenInclude(a => a.Shift)
            .Include(s => s.OpenedByNavigation)
            .Include(s => s.ClosedByNavigation)
            .Include(s => s.CashHandovers)
                .ThenInclude(c => c.CashierEmployee)
            .Include(s => s.SecurityHandovers)
                .ThenInclude(sec => sec.SecurityEmployee)
            .Where(s => s.StoreId == storeId && s.ShiftDate == date)
            .OrderBy(s => s.Assignment.Shift.StartTime)
            .ToListAsync();

        return ApiResponse<List<ShiftHandoverSessionDto>>.Ok(sessions.Select(MapSession).ToList());
    }

    private async Task<ShiftHandoverSession?> GetSessionWithIncludes(int handoverId)
    {
        return await _context.ShiftHandoverSessions
            .Include(s => s.Store)
            .Include(s => s.Assignment)
                .ThenInclude(a => a.Shift)
            .Include(s => s.OpenedByNavigation)
            .Include(s => s.ClosedByNavigation)
            .Include(s => s.CashHandovers)
                .ThenInclude(c => c.CashierEmployee)
            .Include(s => s.SecurityHandovers)
                .ThenInclude(sec => sec.SecurityEmployee)
            .FirstOrDefaultAsync(s => s.HandoverId == handoverId);
    }

    private static ShiftHandoverSessionDto MapSession(ShiftHandoverSession s)
    {
        var cash = s.CashHandovers.OrderByDescending(c => c.CreatedAt).FirstOrDefault();
        var sec = s.SecurityHandovers.OrderByDescending(sec => sec.CreatedAt).FirstOrDefault();

        return new ShiftHandoverSessionDto
        {
            HandoverId = s.HandoverId,
            AssignmentId = s.AssignmentId,
            StoreId = s.StoreId,
            StoreName = s.Store?.StoreName ?? "",
            ShiftDate = s.ShiftDate,
            ShiftName = s.Assignment?.Shift?.ShiftName ?? "",
            Status = s.Status,
            OpenedByName = s.OpenedByNavigation?.FullName ?? "",
            ClosedByName = s.ClosedByNavigation?.FullName,
            OpenedAt = s.OpenedAt,
            ClosedAt = s.ClosedAt,
            ManagerNote = s.ManagerNote,
            CashierHandover = cash != null ? new CashHandoverItemDto
            {
                CashHandoverId = cash.CashHandoverId,
                CashierEmployeeId = cash.CashierEmployeeId,
                CashierName = cash.CashierEmployee?.FullName ?? "",
                OpeningFloat = cash.OpeningFloat,
                ActualCash = cash.ActualCash,
                DifferenceAmount = cash.DifferenceAmount,
                DifferenceNote = cash.DifferenceNote
            } : null,
            SecurityHandover = sec != null ? new SecurityHandoverItemDto
            {
                SecurityHandoverId = sec.SecurityHandoverId,
                SecurityEmployeeId = sec.SecurityEmployeeId,
                SecurityName = sec.SecurityEmployee?.FullName ?? "",
                ParkingTickets = sec.ParkingTickets,
                ParkingCards = sec.ParkingCards,
                WarehouseLocked = sec.WarehouseLocked,
                RollerDoorLocked = sec.RollerDoorLocked,
                SecurityNote = sec.SecurityNote
            } : null
        };
    }
}

