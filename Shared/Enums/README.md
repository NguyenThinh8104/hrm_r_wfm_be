# 🏷️ Enums — Kiểu liệt kê dùng chung

## Mục đích
Chứa các **Enum types** dùng chung cho nhiều module, tránh dùng magic strings/numbers.

## Quy tắc
- Mỗi enum **1 file riêng** → `UserRole.cs`, `LeaveStatus.cs`, `AttendanceStatus.cs`.
- Đặt tên PascalCase, **KHÔNG** hậu tố `Enum` → `UserRole` (không phải `UserRoleEnum`).
- Enum values đặt tên PascalCase → `Active`, `Inactive`, `Pending`.
- Nếu cần lưu database → gán giá trị tường minh: `Active = 1, Inactive = 2`.
- Nếu enum chỉ dùng trong 1 module → đặt trong module đó, KHÔNG đặt ở Shared.

## Ví dụ file trong folder này
```
Enums/
├── UserRole.cs                ← Admin, Manager, Employee
├── LeaveStatus.cs             ← Pending, Approved, Rejected, Cancelled
├── AttendanceStatus.cs        ← Present, Absent, Late, OnLeave
└── Gender.cs                  ← Male, Female, Other
```

## Namespace
```csharp
namespace hrm_r_wfm_be.Shared.Enums;
```

## Template
```csharp
// UserRole.cs
public enum UserRole
{
    Employee = 1,
    Manager = 2,
    Admin = 3
}

// LeaveStatus.cs
public enum LeaveStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4
}
```
