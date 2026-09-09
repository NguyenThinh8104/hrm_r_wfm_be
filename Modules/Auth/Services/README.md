# 🔧 Services — Business Logic Layer

## Mục đích
Chứa **Service interfaces và implementations** — nơi xử lý toàn bộ business logic của module.

## Quy tắc
- **Interface-first**: LUÔN tạo `IAuthService.cs` trước, rồi mới tạo `AuthService.cs`.
- Service chứa: validation business rules, mapping DTO ↔ Entity, orchestration.
- Inject **Repository (Interface)** và các dependencies qua constructor.
- Tất cả methods phải `async Task<>`, tên hậu tố `Async`.
- KHÔNG truy cập `DbContext` trực tiếp — phải qua Repository.
- KHÔNG xử lý HTTP-specific logic (StatusCode, ActionResult).
- Throw **custom exceptions** khi có lỗi (NotFoundException, BadRequestException).

## Ví dụ file trong folder này
```
Services/
├── IAuthService.cs            ← Interface định nghĩa contract
└── AuthService.cs             ← Implementation chứa business logic
```

## Namespace
```csharp
namespace hrm_r_wfm_be.Modules.Auth.Services;
```

## Template cơ bản
```csharp
// IAuthService.cs
public interface IAuthService
{
    /// <summary>
    /// Xử lý đăng nhập, trả về JWT token
    /// </summary>
    Task<LoginResponseDto> LoginAsync(LoginRequestDto requestDto);

    /// <summary>
    /// Đăng ký tài khoản mới
    /// </summary>
    Task<RegisterResponseDto> RegisterAsync(RegisterRequestDto requestDto);

    /// <summary>
    /// Refresh JWT token
    /// </summary>
    Task<LoginResponseDto> RefreshTokenAsync(string refreshToken);
}

// AuthService.cs
public class AuthService : IAuthService
{
    private readonly IAuthRepository _authRepository;
    private readonly IOptions<JwtSettings> _jwtOptions;

    public AuthService(IAuthRepository authRepository, IOptions<JwtSettings> jwtOptions)
    {
        _authRepository = authRepository;
        _jwtOptions = jwtOptions;
    }

    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto requestDto)
    {
        var user = await _authRepository.GetByEmailAsync(requestDto.Email);
        if (user == null || !VerifyPassword(requestDto.Password, user.PasswordHash))
            throw new UnauthorizedException("Email hoặc mật khẩu không đúng");

        var token = GenerateJwtToken(user);
        return new LoginResponseDto { Token = token, ... };
    }
}
```

## Đăng ký DI
```csharp
services.AddScoped<IAuthService, AuthService>();
```
