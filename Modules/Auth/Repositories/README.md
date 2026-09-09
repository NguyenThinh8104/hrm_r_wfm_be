# 🗄️ Repositories — Data Access Layer

## Mục đích
Chứa **Repository interfaces và implementations** — nơi truy cập database qua Entity Framework Core.

## Quy tắc
- **Interface-first**: LUÔN tạo `IAuthRepository.cs` trước, rồi mới tạo `AuthRepository.cs`.
- Repository CHỈ xử lý **CRUD + query database** — KHÔNG có business logic.
- Inject `AppDbContext` qua constructor.
- Tất cả methods phải `async Task<>`, tên hậu tố `Async`.
- Dùng `AsNoTracking()` cho các query chỉ đọc (GET).
- KHÔNG throw business exceptions — chỉ return `null` / empty collection.
- KHÔNG mapping DTO trong repository — chỉ làm việc với Entity.

## Ví dụ file trong folder này
```
Repositories/
├── IAuthRepository.cs         ← Interface định nghĩa data access contract
└── AuthRepository.cs          ← Implementation truy cập database
```

## Namespace
```csharp
namespace hrm_r_wfm_be.Modules.Auth.Repositories;
```

## Template cơ bản
```csharp
// IAuthRepository.cs
public interface IAuthRepository
{
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByIdAsync(int id);
    Task<User> AddAsync(User user);
    Task UpdateAsync(User user);
    Task<bool> EmailExistsAsync(string email);
}

// AuthRepository.cs
public class AuthRepository : IAuthRepository
{
    private readonly AppDbContext _context;

    public AuthRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<User> AddAsync(User user)
    {
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();
        return user;
    }
}
```

## Đăng ký DI
```csharp
services.AddScoped<IAuthRepository, AuthRepository>();
```
