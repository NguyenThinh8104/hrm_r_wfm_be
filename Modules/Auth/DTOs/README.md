# 📦 DTOs — Data Transfer Objects

## Mục đích
Chứa các **DTO classes** — đối tượng truyền dữ liệu giữa client ↔ controller ↔ service.

## Quy tắc
- **TÁCH RIÊNG** Request DTO và Response DTO — KHÔNG dùng 1 DTO cho cả hai.
- Đặt tên theo pattern:
  - Request: `[Action][Entity]RequestDto.cs` → `LoginRequestDto.cs`, `RegisterRequestDto.cs`
  - Response: `[Entity]ResponseDto.cs` → `LoginResponseDto.cs`, `UserProfileResponseDto.cs`
- Dùng **Data Annotations** cho validation format: `[Required]`, `[StringLength]`, `[EmailAddress]`.
- Properties dùng `{ get; set; }`, string mặc định `= string.Empty;`.
- Nullable properties dùng `?` → `public string? Phone { get; set; }`
- KHÔNG include sensitive fields (Password, PasswordHash) trong Response DTO.
- KHÔNG include navigation properties — chỉ include ID hoặc tên hiển thị.

## Ví dụ file trong folder này
```
DTOs/
├── LoginRequestDto.cs              ← Dữ liệu đăng nhập từ client
├── LoginResponseDto.cs             ← JWT token trả về cho client
├── RegisterRequestDto.cs           ← Dữ liệu đăng ký từ client
└── RegisterResponseDto.cs          ← Kết quả đăng ký trả về
```

## Namespace
```csharp
namespace hrm_r_wfm_be.Modules.Auth.DTOs;
```

## Template cơ bản
```csharp
// LoginRequestDto.cs — Request
public class LoginRequestDto
{
    [Required(ErrorMessage = "Email không được để trống")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu không được để trống")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu từ 6-100 ký tự")]
    public string Password { get; set; } = string.Empty;
}

// LoginResponseDto.cs — Response
public class LoginResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    // ❌ KHÔNG có Password, PasswordHash trong Response
}
```
