namespace Shared.Common;

/// <summary>
/// Triển khai mặc định của IKioskContext cho trạm quầy Kiosk.
/// Cho phép truyền BranchId và KioskId từ Configuration, Header hoặc Scoped State.
/// </summary>
public class DefaultKioskContext : IKioskContext
{
    /// <summary>
    /// Mã ID chi nhánh cửa hàng mặc định của Kiosk (Ví dụ: 1).
    /// </summary>
    public ulong BranchId { get; set; } = 1;

    /// <summary>
    /// Mã ID thiết bị Kiosk quầy (Ví dụ: 1).
    /// </summary>
    public ulong? KioskId { get; set; } = 1;

    /// <summary>
    /// Khởi tạo DefaultKioskContext mặc định với BranchId = 1 và KioskId = 1.
    /// </summary>
    public DefaultKioskContext() { }

    /// <summary>
    /// Khởi tạo với BranchId và KioskId tùy chọn.
    /// </summary>
    public DefaultKioskContext(ulong branchId, ulong? kioskId = null)
    {
        BranchId = branchId;
        KioskId = kioskId;
    }
}
