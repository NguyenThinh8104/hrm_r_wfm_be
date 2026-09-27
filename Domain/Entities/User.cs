namespace Domain.Entities;

public class User
{
    public ulong Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? KioskPinHash { get; set; }
    public byte RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public string EmploymentType { get; set; } = "FULL_TIME";
    public ulong? HomeBranchId { get; set; }
    public Branch? HomeBranch { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ShiftAssignment> ShiftAssignments { get; set; } = new List<ShiftAssignment>();
}
