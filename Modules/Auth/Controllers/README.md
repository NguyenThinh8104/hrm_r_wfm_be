# 🎮 Controllers — API Endpoints

## Mục đích
Chứa các **API Controller** của module, là điểm tiếp nhận HTTP request từ client.

## Quy tắc
- Controller phải là **Thin Controller** — CHỈ điều phối, KHÔNG có business logic.
- Inject **Service (Interface)** qua constructor, KHÔNG inject Repository trực tiếp.
- Mỗi action method phải có:
  - `/// <summary>` XML documentation
  - `[ProducesResponseType]` cho mọi status code có thể trả về
  - Return type `Task<IActionResult>`
- Dùng `[Authorize]` ở controller level, `[AllowAnonymous]` cho endpoints public.
- KHÔNG dùng `try/catch` — lỗi xử lý qua `ExceptionHandlingMiddleware`.

## Ví dụ file trong folder này
```
Controllers/
└── AuthController.cs          ← Endpoints: Login, Register, RefreshToken
```

## Namespace
```csharp
namespace hrm_r_wfm_be.Modules.Auth.Controllers;
```

## Template cơ bản
```csharp
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Đăng nhập
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto requestDto)
    {
        var result = await _authService.LoginAsync(requestDto);
        return Ok(result);
    }
}
```
