using RWFM.Application.Common;
using RWFM.Application.DTOs;

namespace RWFM.Application.Interfaces;

public interface IHandoverService
{
    Task<ApiResponse<ShiftHandoverSessionDto>> GetOrCreateSessionAsync(int storeId, int assignmentId, DateOnly date, int openedByEmployeeId);
    Task<ApiResponse<ShiftHandoverSessionDto>> SubmitCashierHandoverAsync(int cashierEmployeeId, CashierHandoverSubmitDto request);
    Task<ApiResponse<ShiftHandoverSessionDto>> SubmitSecurityHandoverAsync(int securityEmployeeId, SecurityHandoverSubmitDto request);
    Task<ApiResponse<ShiftHandoverSessionDto>> LeaderSignHandoverAsync(int leaderEmployeeId, LeaderSignHandoverDto request);
    Task<ApiResponse<List<ShiftHandoverSessionDto>>> GetSessionsByStoreAsync(int storeId, DateOnly date);
}
