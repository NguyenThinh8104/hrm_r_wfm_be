using System.ComponentModel.DataAnnotations;

namespace Modules.Stores.DTOs;

public class TierDto
{
    public int Id { get; set; }
    public string TierName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int MinStaffCount { get; set; }
    public int? MaxStaffCount { get; set; }
    public string? OtherConditions { get; set; }
    public string? Conditions { get; set; }
    public string? Benefits { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int BranchCount { get; set; }
}

public class CreateTierDto
{
    [Required(ErrorMessage = "Tên tier không được để trống")]
    [MaxLength(100, ErrorMessage = "Tên tier không được vượt quá 100 ký tự")]
    public string TierName { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Range(0, 10000, ErrorMessage = "Số nhân sự tối thiểu phải từ 0 trở lên")]
    public int MinStaffCount { get; set; } = 0;

    public int? MaxStaffCount { get; set; }

    public string? OtherConditions { get; set; }
    public string? Conditions { get; set; }
    public string? Benefits { get; set; }
}

public class UpdateTierDto
{
    [MaxLength(100, ErrorMessage = "Tên tier không được vượt quá 100 ký tự")]
    public string? TierName { get; set; }

    public string? Description { get; set; }

    public int? MinStaffCount { get; set; }

    public int? MaxStaffCount { get; set; }

    public string? OtherConditions { get; set; }
    public string? Conditions { get; set; }
    public string? Benefits { get; set; }
}

public class UpdateBranchStaffCountDto
{
    [Required(ErrorMessage = "Số lượng nhân sự là bắt buộc")]
    [Range(0, 10000, ErrorMessage = "Số lượng nhân sự phải từ 0 trở lên")]
    public int StaffCount { get; set; }
}
