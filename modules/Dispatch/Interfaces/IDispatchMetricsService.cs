using Modules.Dispatch.DTOs;
using Shared.Common;

namespace Modules.Dispatch.Interfaces;

/// <summary>
/// Dịch vụ tính toán báo cáo, ma trận điều động theo cặp chi nhánh và tổng số giờ công chi viện toàn chuỗi.
/// </summary>
public interface IDispatchMetricsService
{
    /// <summary>
    /// Giám sát ma trận điều chuyển và tổng số giờ công chi viện toàn mạng lưới trong khoảng thời gian chỉ định.
    /// </summary>
    Task<ApiResponse<DispatchNetworkMetricsDto>> GetNetworkMetricsAsync(DateOnly? fromDate, DateOnly? toDate);
}
