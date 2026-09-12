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
        var assignment = await _context.ShiftAssignments
            .Include(a => a.Schedule)
                .ThenInclude(ws => ws.ShiftTemplate)
            .Include(a => a.Schedule)
                .ThenInclude(ws => ws.Branch)
            .FirstOrDefaultAsync(a => a.Id == (ulong)assignmentId);

        if (assignment == null)
        {
            return ApiResponse<ShiftHandoverSessionDto>.Fail("Phân công ca không tồn tại.");
        }

        var handover = await _context.ShiftHandovers
            .Include(sh => sh.Schedule)
                .ThenInclude(ws => ws.Branch)
            .Include(sh => sh.Schedule)
                .ThenInclude(ws => ws.ShiftTemplate)
            .Include(sh => sh.ShiftLeader)
            .Include(sh => sh.CashHandovers)
                .ThenInclude(c => c.Cashier)
            .Include(sh => sh.SecurityHandovers)
                .ThenInclude(sec => sec.SecurityGuard)
            .FirstOrDefaultAsync(sh => sh.ScheduleId == assignment.ScheduleId);

        if (handover == null)
        {
            handover = new ShiftHandover
            {
                ScheduleId = assignment.ScheduleId,
                ShiftLeaderId = (ulong)openedByEmployeeId,
                HandoverStatus = "IN_PROGRESS"
            };

            _context.ShiftHandovers.Add(handover);
            await _context.SaveChangesAsync();

            return await GetOrCreateSessionAsync(storeId, assignmentId, date, openedByEmployeeId);
        }

        return ApiResponse<ShiftHandoverSessionDto>.Ok(MapSession(handover));
    }

    public async Task<ApiResponse<ShiftHandoverSessionDto>> SubmitCashierHandoverAsync(int cashierEmployeeId, CashierHandoverSubmitDto request)
    {
        var handover = await GetSessionWithIncludes(request.HandoverId);
        if (handover == null) return ApiResponse<ShiftHandoverSessionDto>.Fail("Phiên giao ca không tồn tại.");

        var cash = handover.CashHandovers.FirstOrDefault(c => c.CashierId == (ulong)cashierEmployeeId)
                   ?? handover.CashHandovers.FirstOrDefault();

        if (cash == null)
        {
            cash = new CashHandover
            {
                ShiftHandoverId = handover.Id,
                CashierId = (ulong)cashierEmployeeId,
                OpeningCash = 1000000,
                SystemExpectedCash = 1000000,
                ClosingActualCash = request.ActualCash,
                DiscrepancyReason = request.DifferenceNote,
                CreatedAt = DateTime.UtcNow
            };
            _context.CashHandovers.Add(cash);
        }
        else
        {
            cash.ClosingActualCash = request.ActualCash;
            cash.DiscrepancyReason = request.DifferenceNote;
        }

        await _context.SaveChangesAsync();
        return ApiResponse<ShiftHandoverSessionDto>.Ok(MapSession(handover), "Cập nhật bàn giao két tiền thành công.");
    }

    public async Task<ApiResponse<ShiftHandoverSessionDto>> SubmitSecurityHandoverAsync(int securityEmployeeId, SecurityHandoverSubmitDto request)
    {
        var handover = await GetSessionWithIncludes(request.HandoverId);
        if (handover == null) return ApiResponse<ShiftHandoverSessionDto>.Fail("Phiên giao ca không tồn tại.");

        var sec = handover.SecurityHandovers.FirstOrDefault(s => s.SecurityGuardId == (ulong)securityEmployeeId)
                  ?? handover.SecurityHandovers.FirstOrDefault();

        if (sec == null)
        {
            sec = new SecurityHandover
            {
                ShiftHandoverId = handover.Id,
                SecurityGuardId = (ulong)securityEmployeeId,
                OvernightVehicleCount = (ushort)request.ParkingTickets,
                IsWarehouseLocked = request.WarehouseLocked,
                IsShutterClosed = request.RollerDoorLocked,
                SecurityNotes = request.SecurityNote,
                CreatedAt = DateTime.UtcNow
            };
            _context.SecurityHandovers.Add(sec);
        }
        else
        {
            sec.OvernightVehicleCount = (ushort)request.ParkingTickets;
            sec.IsWarehouseLocked = request.WarehouseLocked;
            sec.IsShutterClosed = request.RollerDoorLocked;
            sec.SecurityNotes = request.SecurityNote;
        }

        await _context.SaveChangesAsync();
        return ApiResponse<ShiftHandoverSessionDto>.Ok(MapSession(handover), "Cập nhật biên bản an ninh thành công.");
    }

    public async Task<ApiResponse<ShiftHandoverSessionDto>> LeaderSignHandoverAsync(int leaderEmployeeId, LeaderSignHandoverDto request)
    {
        var handover = await GetSessionWithIncludes(request.HandoverId);
        if (handover == null) return ApiResponse<ShiftHandoverSessionDto>.Fail("Phiên giao ca không tồn tại.");

        handover.HandoverStatus = request.IsApproved ? "COMPLETED" : "DISCREPANCY_FLAGGED";
        handover.ShiftLeaderId = (ulong)leaderEmployeeId;
        handover.SignedAt = DateTime.UtcNow;
        handover.GeneralNotes = request.ManagerNote;

        await _context.SaveChangesAsync();

        var msg = request.IsApproved
            ? "Trưởng ca đã ký duyệt và đóng phiên làm việc thành công."
            : "Trưởng ca ghi nhận vi phạm và gửi cảnh báo chênh lệch lên Cửa hàng trưởng.";

        return ApiResponse<ShiftHandoverSessionDto>.Ok(MapSession(handover), msg);
    }

    public async Task<ApiResponse<List<ShiftHandoverSessionDto>>> GetSessionsByStoreAsync(int storeId, DateOnly date)
    {
        var handovers = await _context.ShiftHandovers
            .Include(sh => sh.Schedule)
                .ThenInclude(ws => ws.Branch)
            .Include(sh => sh.Schedule)
                .ThenInclude(ws => ws.ShiftTemplate)
            .Include(sh => sh.ShiftLeader)
            .Include(sh => sh.CashHandovers)
                .ThenInclude(c => c.Cashier)
            .Include(sh => sh.SecurityHandovers)
                .ThenInclude(sec => sec.SecurityGuard)
            .Where(sh => sh.Schedule.BranchId == (ulong)storeId && sh.Schedule.WorkDate == date)
            .OrderBy(sh => sh.Schedule.ShiftTemplate.StartTime)
            .ToListAsync();

        return ApiResponse<List<ShiftHandoverSessionDto>>.Ok(handovers.Select(MapSession).ToList());
    }

    private async Task<ShiftHandover?> GetSessionWithIncludes(int handoverId)
    {
        return await _context.ShiftHandovers
            .Include(sh => sh.Schedule)
                .ThenInclude(ws => ws.Branch)
            .Include(sh => sh.Schedule)
                .ThenInclude(ws => ws.ShiftTemplate)
            .Include(sh => sh.ShiftLeader)
            .Include(sh => sh.CashHandovers)
                .ThenInclude(c => c.Cashier)
            .Include(sh => sh.SecurityHandovers)
                .ThenInclude(sec => sec.SecurityGuard)
            .FirstOrDefaultAsync(sh => sh.Id == (ulong)handoverId);
    }

    private static ShiftHandoverSessionDto MapSession(ShiftHandover sh)
    {
        var cash = sh.CashHandovers.OrderByDescending(c => c.CreatedAt).FirstOrDefault();
        var sec = sh.SecurityHandovers.OrderByDescending(sec => sec.CreatedAt).FirstOrDefault();

        return new ShiftHandoverSessionDto
        {
            HandoverId = (int)sh.Id,
            AssignmentId = (int)sh.ScheduleId,
            StoreId = (int)sh.Schedule.BranchId,
            StoreName = sh.Schedule.Branch?.Name ?? "",
            ShiftDate = sh.Schedule.WorkDate,
            ShiftName = sh.Schedule.ShiftTemplate?.Name ?? "",
            Status = sh.HandoverStatus,
            OpenedByName = sh.ShiftLeader?.FullName ?? "",
            ClosedByName = sh.ShiftLeader?.FullName,
            OpenedAt = sh.SignedAt ?? DateTime.UtcNow,
            ClosedAt = sh.SignedAt,
            ManagerNote = sh.GeneralNotes,
            CashierHandover = cash != null ? new CashHandoverItemDto
            {
                CashHandoverId = (int)cash.Id,
                CashierEmployeeId = (int)cash.CashierId,
                CashierName = cash.Cashier?.FullName ?? "",
                OpeningFloat = cash.OpeningCash,
                ActualCash = cash.ClosingActualCash,
                DifferenceAmount = cash.DifferenceAmount,
                DifferenceNote = cash.DiscrepancyReason
            } : null,
            SecurityHandover = sec != null ? new SecurityHandoverItemDto
            {
                SecurityHandoverId = (int)sec.Id,
                SecurityEmployeeId = (int)sec.SecurityGuardId,
                SecurityName = sec.SecurityGuard?.FullName ?? "",
                ParkingTickets = sec.OvernightVehicleCount,
                ParkingCards = sec.OvernightVehicleCount,
                WarehouseLocked = sec.IsWarehouseLocked,
                RollerDoorLocked = sec.IsShutterClosed,
                SecurityNote = sec.SecurityNotes
            } : null
        };
    }
}
