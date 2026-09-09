# 🏛️ Entities — Database Models

## Mục đích
Chứa các **Entity classes** — đại diện cho bảng trong database, được Entity Framework Core mapping.

## Quy tắc
- Entity là **POCO class** — CHỈ chứa properties, KHÔNG có business logic.
- Đặt tên PascalCase, **không hậu tố** → `User.cs` (không phải `UserEntity.cs`).
- Dùng Data Annotations cho validation cơ bản (`[Required]`, `[StringLength]`).
- Cấu hình phức tạp (relationships, indexes, constraints) → dùng **Fluent API** tại `Data/Configurations/`.
- Navigation properties phải có `= null!;` để suppress nullable warning.
- String properties mặc định `= string.Empty;`.
- Boolean properties dùng tiền tố `Is/Has/Can` → `IsActive`, `HasVerifiedEmail`.
- Audit fields: `CreatedAt`, `UpdatedAt`, `CreatedBy`, `UpdatedBy` nếu cần.

## Ví dụ file trong folder này
```
Entities/
├── User.cs                    ← Entity chính cho người dùng / tài khoản
└── RefreshToken.cs            ← Entity lưu refresh token (nếu cần)
```

## Namespace
```csharp
namespace hrm_r_wfm_be.Modules.Auth.Entities;
```

## Template cơ bản
```csharp
// User.cs
public class User
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [StringLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    public string? Phone { get; set; }

    [Required]
    [StringLength(50)]
    public string Role { get; set; } = "Employee";  // Admin, Manager, Employee

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation Properties
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
```

## Lưu ý
- Sau khi tạo Entity, cần:
  1. Tạo EF Configuration tại `Data/Configurations/UserConfiguration.cs`
  2. Thêm `DbSet<User>` vào `Data/AppDbContext.cs`
  3. Chạy migration: `dotnet ef migrations add AddUserTable`
