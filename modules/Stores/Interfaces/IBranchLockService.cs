using Modules.Stores.DTOs;

namespace Modules.Stores.Interfaces;

/// <summary>
/// Giao diện dịch vụ xử lý quy trình Khóa và Mở khóa chi nhánh cửa hàng.
/// </summary>
public interface IBranchLockService
{
    /// <summary>
    /// Kiểm tra toàn bộ điều kiện chặn trước khi khóa chi nhánh (Không làm thay đổi dữ liệu).
    /// </summary>
    /// <param name="branchId">ID chi nhánh cần kiểm tra.</param>
    /// <param name="cancellationToken">Token hủy tác vụ.</param>
    /// <returns>Đối tượng BranchLockCheckResponseDto chứa danh sách blockers và canLock.</returns>
    Task<BranchLockCheckResponseDto> CheckLockConditionsAsync(ulong branchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Thực hiện khóa chi nhánh (kiểm tra blocker trong transaction, xử lý nhân sự và ca làm việc tương lai).
    /// </summary>
    /// <param name="branchId">ID chi nhánh cần khóa.</param>
    /// <param name="request">Thông tin yêu cầu khóa kèm confirmBranchCode và handling mode.</param>
    /// <param name="performedBy">Mã hoặc tên nhân viên/quản trị viên thực hiện.</param>
    /// <param name="performedByUserId">ID người dùng thực hiện.</param>
    /// <param name="cancellationToken">Token hủy tác vụ.</param>
    Task<BranchLockOperationResult> LockBranchAsync(
        ulong branchId,
        LockBranchRequestDto request,
        string? performedBy,
        ulong? performedByUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Thực hiện mở khóa chi nhánh và khôi phục hoạt động cho chi nhánh và trạm Kiosk.
    /// </summary>
    /// <param name="branchId">ID chi nhánh cần mở khóa.</param>
    /// <param name="request">Thông tin lý do mở khóa.</param>
    /// <param name="performedBy">Mã hoặc tên nhân viên/quản trị viên thực hiện.</param>
    /// <param name="performedByUserId">ID người dùng thực hiện.</param>
    /// <param name="cancellationToken">Token hủy tác vụ.</param>
    Task<BranchLockOperationResult> UnlockBranchAsync(
        ulong branchId,
        UnlockBranchRequestDto request,
        string? performedBy,
        ulong? performedByUserId,
        CancellationToken cancellationToken = default);
}
