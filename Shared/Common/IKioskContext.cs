namespace Shared.Common;

/// <summary>
/// Interface cung cấp ngữ cảnh thông tin máy Kiosk hiện tại (BranchId và KioskId).
/// Được inject vào các Service thuộc Module nghiệp vụ để xác định vị trí trạm quầy đang làm việc.
/// </summary>
public interface IKioskContext
{
    /// <summary>
    /// Mã ID chi nhánh cửa hàng của trạm Kiosk hiện tại.
    /// </summary>
    ulong BranchId { get; }

    /// <summary>
    /// Mã ID thiết bị Kiosk quầy (nếu có).
    /// </summary>
    ulong? KioskId { get; }
}
