using Domain.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.Stores.DTOs;
using Modules.Stores.Interfaces;
using Shared.Common;
using Shared.Data;
using Shared.Interfaces;

namespace Modules.Stores.Services;

/// <summary>
/// Dịch vụ xử lý nghiệp vụ Quản lý & Thẩm định Định biên Nhân sự chi nhánh theo mô hình Effective Quota.
/// </summary>
public class BranchHeadcountService : IBranchHeadcountService, IStoreHeadcountService
{
    private readonly AppDbContext _context;
    private readonly ILogger<BranchHeadcountService> _logger;

    public BranchHeadcountService(
        AppDbContext context,
        ILogger<BranchHeadcountService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Thẩm định định biên nhân sự của chi nhánh khi thêm mới nhân sự (Tạo đơn lẻ hoặc Import Excel).
    /// </summary>
    /// <param name="branchId">ID chi nhánh kiểm tra.</param>
    /// <param name="additionalBatchCount">Số lượng nhân sự đã hợp lệ tạm thời trong đợt bulk import hiện tại.</param>
    public async Task<HeadcountValidationResult> ValidateHeadcountAsync(ulong branchId, int additionalBatchCount = 0)
    {
        var branch = await _context.Branches.FindAsync(branchId);
        if (branch == null)
        {
            return HeadcountValidationResult.Fail("Chi nhánh cửa hàng chỉ định không tồn tại.");
        }

        if (branch.Status != "ACTIVE")
        {
            return HeadcountValidationResult.Fail($"Chi nhánh '{branch.Name}' đã ngừng hoạt động, không thể tiếp nhận nhân sự.");
        }

        // Đếm số lượng nhân sự cơ hữu (chính thức) đang hoạt động thực tế (Status == 'ACTIVE')
        // Lưu ý cơ chế định biên: Định biên chi nhánh chỉ quản lý quân số cơ hữu thuộc biên chế chi nhánh.
        // Nhân sự từ nơi khác được điều chuyển/chi viện sang hỗ trợ tạm thời KHÔNG tính vào định biên này.
        var currentActiveCount = await _context.Users
            .CountAsync(u => ((u.OriginalHomeBranchId != null ? u.OriginalHomeBranchId == branchId : u.HomeBranchId == branchId)) && u.Status == "ACTIVE");

        var standardQuota = HeadcountConstants.GetStandardQuota(branch.BranchTier);
        var totalProjectedCount = currentActiveCount + additionalBatchCount;

        if (totalProjectedCount >= standardQuota)
        {
            string upgradeAdvice;
            if (branch.BranchTier == Domain.Enums.BranchTier.Tier3)
            {
                upgradeAdvice = "Chi nhánh hiện là Tier 3 (tối đa 8 nhân sự). Quản trị viên cần nâng phân cấp chi nhánh lên Tier 2 (tối đa 15 nhân sự) để tiếp tục tuyển dụng.";
            }
            else if (branch.BranchTier == Domain.Enums.BranchTier.Tier2)
            {
                upgradeAdvice = "Chi nhánh hiện là Tier 2 (tối đa 15 nhân sự). Quản trị viên cần nâng phân cấp chi nhánh lên Tier 1 (tối đa 30 nhân sự) để tiếp tục tuyển dụng.";
            }
            else
            {
                upgradeAdvice = "Chi nhánh đã ở phân cấp tối đa của hệ thống (Tier 1: 30/30 nhân sự kịch trần). Không thể tuyển thêm nhân sự cho chi nhánh này.";
            }

            return HeadcountValidationResult.Fail(
                $"Chi nhánh '{branch.Name}' đã đạt trần định biên ({totalProjectedCount}/{standardQuota} nhân sự theo Tier {(int)branch.BranchTier}). {upgradeAdvice}",
                currentActiveCount, standardQuota);
        }

        return HeadcountValidationResult.Success(currentActiveCount, standardQuota);
    }

    /// <summary>
    /// Tra cứu chi tiết thông tin định biên chi nhánh theo phân cấp Tier (Quy mô Tier, Định biên chuẩn, Quân số, Vị trí trống).
    /// Tách bạch rõ ràng giữa nhân sự cơ hữu (biên chế) và nhân sự được điều chuyển đến hỗ trợ tạm thời.
    /// </summary>
    public async Task<ApiResponse<BranchHeadcountStatusDto>> GetBranchHeadcountStatusAsync(ulong branchId)
    {
        var branch = await _context.Branches.FindAsync(branchId);
        if (branch == null)
        {
            return ApiResponse<BranchHeadcountStatusDto>.Fail("Không tìm thấy chi nhánh cửa hàng.");
        }

        // 1. Quân số cơ hữu chính thức thuộc biên chế của chi nhánh (Status == 'ACTIVE')
        var officialHeadcount = await _context.Users
            .CountAsync(u => ((u.OriginalHomeBranchId != null ? u.OriginalHomeBranchId == branchId : u.HomeBranchId == branchId)) && u.Status == "ACTIVE");

        // 2. Số lượng nhân sự từ chi nhánh khác sang hỗ trợ tạm thời (chi viện)
        var dispatchedInCount = await _context.Users
            .CountAsync(u => u.HomeBranchId == branchId && u.OriginalHomeBranchId != null && u.OriginalHomeBranchId != branchId && u.Status == "ACTIVE");

        // 3. Số lượng nhân sự cơ hữu của chi nhánh đang đi điều động hỗ trợ nơi khác
        var dispatchedOutCount = await _context.Users
            .CountAsync(u => u.OriginalHomeBranchId == branchId && u.HomeBranchId != branchId && u.Status == "ACTIVE");

        // 4. Tổng số nhân sự thực tế đang có mặt làm việc tại cửa hàng hôm nay
        var actualWorkingCount = await _context.Users
            .CountAsync(u => u.HomeBranchId == branchId && u.Status == "ACTIVE");

        // 5. Kiểm tra Cửa hàng trưởng đang hoạt động tại chi nhánh
        var activeStoreManager = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => ((u.OriginalHomeBranchId != null ? u.OriginalHomeBranchId == branchId : u.HomeBranchId == branchId))
                                   && u.Role.RoleCode == "STORE_MANAGER"
                                   && u.Status == "ACTIVE");

        var inactiveCount = await _context.Users
            .CountAsync(u => ((u.OriginalHomeBranchId != null ? u.OriginalHomeBranchId == branchId : u.HomeBranchId == branchId)) && u.Status == "INACTIVE");

        var standardQuota = HeadcountConstants.GetStandardQuota(branch.BranchTier);

        // Định biên CHỈ áp dụng đối với nhân sự cơ hữu, tránh việc điều động làm sai lệch trần định biên
        var availableQuotaSlots = Math.Max(0, standardQuota - officialHeadcount);

        var result = new BranchHeadcountStatusDto
        {
            BranchId = branch.Id,
            BranchCode = branch.BranchCode,
            BranchName = branch.Name,
            BranchTierName = branch.BranchTier.ToString(),
            BranchTierValue = (int)branch.BranchTier,
            StandardQuota = standardQuota,
            StaffCount = 0,
            EffectiveQuota = standardQuota,
            CurrentHeadcount = officialHeadcount, // Nhân sự cơ hữu chính thức áp vào định biên
            OfficialHeadcount = officialHeadcount,
            DispatchedInCount = dispatchedInCount,
            DispatchedOutCount = dispatchedOutCount,
            ActualWorkingCount = actualWorkingCount,
            InactiveCount = inactiveCount,
            AvailableQuotaSlots = availableQuotaSlots,
            IsQuotaReached = officialHeadcount >= standardQuota,
            CanCreateDirectly = officialHeadcount < standardQuota,
            HasActiveStoreManager = activeStoreManager != null,
            ActiveStoreManagerId = activeStoreManager?.Id,
            ActiveStoreManagerName = activeStoreManager?.FullName,
            ActiveStoreManagerCode = activeStoreManager?.EmployeeCode,
        };

        return ApiResponse<BranchHeadcountStatusDto>.Ok(result);
    }
}
