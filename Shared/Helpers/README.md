# 🛠️ Helpers — Utility / Helper Functions

## Mục đích
Chứa các **static helper / utility classes** cung cấp functions dùng chung, không chứa business logic.

## Quy tắc
- Helper classes là `public static class`.
- Methods là `public static`, **KHÔNG** có side effects (pure functions ưu tiên).
- Mỗi nhóm chức năng **1 file riêng** → `DateTimeHelper.cs`, `StringHelper.cs`.
- KHÔNG chứa business logic — chỉ chứa functions tính toán, format, convert.
- KHÔNG inject dependencies — helper là static, không dùng DI.
- Nếu cần DI → chuyển thành Service đặt trong module tương ứng.

## Ví dụ file trong folder này
```
Helpers/
├── DateTimeHelper.cs          ← Format date, tính khoảng cách ngày
├── StringHelper.cs            ← Slugify, truncate, normalize
├── PasswordHelper.cs          ← Hash password, verify password (BCrypt)
└── FileHelper.cs              ← Validate file type, generate unique filename
```

## Namespace
```csharp
namespace hrm_r_wfm_be.Shared.Helpers;
```

## Template
```csharp
// DateTimeHelper.cs
public static class DateTimeHelper
{
    public static string ToVietnameseDate(DateTime date)
        => date.ToString("dd/MM/yyyy");

    public static string ToVietnameseDateTime(DateTime date)
        => date.ToString("dd/MM/yyyy HH:mm");

    public static int CalculateWorkingDays(DateTime start, DateTime end)
    {
        var totalDays = 0;
        for (var date = start; date <= end; date = date.AddDays(1))
        {
            if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
                totalDays++;
        }
        return totalDays;
    }
}

// PasswordHelper.cs
public static class PasswordHelper
{
    public static string HashPassword(string password)
        => BCrypt.Net.BCrypt.HashPassword(password);

    public static bool VerifyPassword(string password, string hash)
        => BCrypt.Net.BCrypt.Verify(password, hash);
}
```
