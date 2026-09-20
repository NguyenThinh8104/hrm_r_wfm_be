namespace Modules.Auth.DTOs;

public class StoreManagerDto
{
    public ulong Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public ulong? HomeBranchId { get; set; }
    public string? BranchCode { get; set; }
    public string? BranchName { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public DateTime CreatedAt { get; set; }
}

public class CreateStoreManagerDto
{
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public ulong HomeBranchId { get; set; }
    public string? Password { get; set; }
}

public class EmployeeDetailDto
{
    public ulong Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public byte RoleId { get; set; }
    public string RoleCode { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string EmploymentType { get; set; } = "FULL_TIME";
    public ulong? HomeBranchId { get; set; }
    public string? BranchCode { get; set; }
    public string? BranchName { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateEmployeeDto
{
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public byte RoleId { get; set; }
    public string EmploymentType { get; set; } = "FULL_TIME";
    public ulong HomeBranchId { get; set; }
    public string? Password { get; set; }
}

public class UpdateEmployeeDto
{
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public byte RoleId { get; set; }
    public string EmploymentType { get; set; } = "FULL_TIME";
    public ulong HomeBranchId { get; set; }
    public string? Status { get; set; }
}

public class UpdateStatusDto
{
    public string Status { get; set; } = "ACTIVE";
}

public class ResetPasswordDto
{
    public string? NewPassword { get; set; }
}

public class RoleDto
{
    public byte Id { get; set; }
    public string RoleCode { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class BranchSimpleDto
{
    public ulong Id { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Status { get; set; } = "ACTIVE";
}

public class EmployeeFilterDto
{
    public ulong? BranchId { get; set; }
    public byte? RoleId { get; set; }
    public string? EmploymentType { get; set; }
    public string? Status { get; set; }
    public string? Search { get; set; }
    public bool? ExcludeStoreManager { get; set; }
}
