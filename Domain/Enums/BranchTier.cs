namespace Domain.Enums;

/// <summary>
/// Phân cấp quy mô chi nhánh cửa hàng trong chuỗi (Branch Tier).
/// Dùng để phân bổ định biên nhân sự, cấu hình KPI và chính sách quản lý theo cấp cửa hàng.
/// </summary>
public enum BranchTier
{
    /// <summary>
    /// Cấp 1 - Quy mô lớn (Flagship Store, Siêu thị trung tâm với lưu lượng khách và doanh số cao nhất).
    /// </summary>
    Tier1 = 1,

    /// <summary>
    /// Cấp 2 - Quy mô tiêu chuẩn (Standard Store, cửa hàng tiện lợi phổ thông trong chuỗi - giá trị mặc định).
    /// </summary>
    Tier2 = 2,

    /// <summary>
    /// Cấp 3 - Quy mô nhỏ (Mini/Express Store, kiosk hoặc cửa hàng diện tích nhỏ).
    /// </summary>
    Tier3 = 3
}
