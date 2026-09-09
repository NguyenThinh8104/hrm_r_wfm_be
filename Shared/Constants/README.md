# 📌 Constants — Hằng số dùng chung

## Mục đích
Chứa các **hằng số, cấu hình tĩnh, và Options classes** dùng chung cho toàn bộ project.

## Quy tắc
- Mỗi nhóm hằng số **1 file riêng** → `AppMessages.cs`, `AppRoles.cs`, `JwtSettings.cs`.
- Hằng số dùng `public static class` + `public const` hoặc `public static readonly`.
- Options/Settings classes dùng `IOptions<T>` pattern (xem coding-standards §14).
- Đặt tên PascalCase cho class, UPPER_SNAKE_CASE hoặc PascalCase cho constants.
- KHÔNG hardcode giá trị config (connection string, secret key) — dùng `appsettings.json`.

## Ví dụ file trong folder này
```
Constants/
├── AppMessages.cs             ← Thông báo lỗi / thành công chuẩn
├── AppRoles.cs                ← Danh sách roles: Admin, Manager, Employee
├── JwtSettings.cs             ← Options class cho JWT configuration
└── PaginationDefaults.cs      ← Default page size, max page size
```

## Namespace
```csharp
namespace hrm_r_wfm_be.Shared.Constants;
```

## Template
```csharp
// AppMessages.cs
public static class AppMessages
{
    public const string NotFound = "{0} với ID {1} không tồn tại";
    public const string AlreadyExists = "{0} đã tồn tại";
    public const string Unauthorized = "Bạn không có quyền thực hiện hành động này";
    public const string LoginFailed = "Email hoặc mật khẩu không đúng";
}

// AppRoles.cs
public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Employee = "Employee";
}
```
