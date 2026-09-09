# 📏 Coding Standards — Quy tắc bắt buộc khi sử dụng AI sinh code Backend

## Mục đích
File này định nghĩa các **quy tắc bắt buộc** mà AI phải tuân thủ khi sinh code cho **Backend (.NET 8)**.  
**Mọi thành viên** phải đính kèm file này (hoặc tham chiếu nội dung) khi yêu cầu AI tạo code.

---

## 1. Cấu trúc thư mục (Modular Architecture)

Dự án sử dụng **Modular Architecture** — code được tổ chức theo **phân hệ nghiệp vụ**.  
AI phải đặt file đúng vị trí theo quy tắc sau:

### Code thuộc nghiệp vụ cụ thể → `Modules/[TênModule]/`

| Loại file | Đặt tại | Ví dụ |
|---|---|---|
| API Controller | `Modules/[Module]/Controllers/` | `Modules/Employee/Controllers/EmployeeController.cs` |
| Service Interface | `Modules/[Module]/Services/` | `Modules/Employee/Services/IEmployeeService.cs` |
| Service Implementation | `Modules/[Module]/Services/` | `Modules/Employee/Services/EmployeeService.cs` |
| Repository Interface | `Modules/[Module]/Repositories/` | `Modules/Employee/Repositories/IEmployeeRepository.cs` |
| Repository Implementation | `Modules/[Module]/Repositories/` | `Modules/Employee/Repositories/EmployeeRepository.cs` |
| DTO (Request/Response) | `Modules/[Module]/DTOs/` | `Modules/Employee/DTOs/CreateEmployeeRequestDto.cs` |
| Entity/Model | `Modules/[Module]/Entities/` | `Modules/Employee/Entities/Employee.cs` |

### Code dùng chung cho nhiều module → `Shared/`

| Loại file | Đặt tại | Ví dụ |
|---|---|---|
| Hằng số chung | `Shared/Constants/` | `Shared/Constants/AppMessages.cs` |
| Enums chung | `Shared/Enums/` | `Shared/Enums/UserRole.cs` |
| Custom Exceptions | `Shared/Exceptions/` | `Shared/Exceptions/NotFoundException.cs` |
| Helper / Utility | `Shared/Helpers/` | `Shared/Helpers/DateTimeHelper.cs` |
| Middleware | `Shared/Middleware/` | `Shared/Middleware/ExceptionHandlingMiddleware.cs` |
| DI Extensions | `Shared/Extensions/` | `Shared/Extensions/ServiceCollectionExtensions.cs` |

### Code hạ tầng (không thuộc module nào)

| Loại file | Đặt tại | Ví dụ |
|---|---|---|
| DbContext | `Data/` | `Data/AppDbContext.cs` |
| EF Configurations | `Data/Configurations/` | `Data/Configurations/EmployeeConfiguration.cs` |
| Entry Point | gốc project | `Program.cs` |
| Cấu hình | gốc project | `appsettings.json` |

> ⚠️ **KHÔNG tham chiếu chéo giữa các module.** Nếu 2 module cần dùng chung → chuyển vào `Shared/`.

---

## 2. Quy tắc đặt tên

### Files & Classes
- **Controller**: PascalCase + hậu tố `Controller`  
  ✅ `EmployeeController.cs`  
  ❌ `employeeCtrl.cs`, `Employee_Controller.cs`

- **Service**: PascalCase + hậu tố `Service`, interface có tiền tố `I`  
  ✅ `IEmployeeService.cs`, `EmployeeService.cs`  
  ❌ `EmployeeSvc.cs`, `employee.service.cs`

- **Repository**: PascalCase + hậu tố `Repository`, interface có tiền tố `I`  
  ✅ `IEmployeeRepository.cs`, `EmployeeRepository.cs`  
  ❌ `EmployeeRepo.cs`, `employee.repository.cs`

- **DTO**: PascalCase + mô tả loại  
  ✅ `CreateEmployeeRequestDto.cs`, `EmployeeResponseDto.cs`  
  ❌ `EmployeeDTO.cs`, `empReq.cs`

- **Entity**: PascalCase, không hậu tố  
  ✅ `Employee.cs`, `Department.cs`  
  ❌ `EmployeeEntity.cs`, `tbl_employee.cs`

- **Enum**: PascalCase  
  ✅ `UserRole.cs`, `LeaveStatus.cs`  

### Biến & Hàm
- **Properties / Public methods**: PascalCase → `FirstName`, `GetEmployeeById()`
- **Private fields**: `_camelCase` với underscore prefix → `_employeeRepository`
- **Local variables / Parameters**: camelCase → `employeeId`, `requestDto`
- **Constants**: PascalCase hoặc UPPER_SNAKE_CASE → `MaxRetryCount`, `DEFAULT_PAGE_SIZE`
- **Boolean**: tiền tố `Is/Has/Can/Should` → `IsActive`, `HasPermission`

### Namespace
- Theo cấu trúc folder: `hrm_r_wfm_be.[Layer].[SubLayer]`
  ```csharp
  namespace hrm_r_wfm_be.Modules.Employee.Controllers;
  namespace hrm_r_wfm_be.Modules.Employee.Services;
  namespace hrm_r_wfm_be.Shared.Exceptions;
  namespace hrm_r_wfm_be.Data;
  ```

---

## 3. Quy tắc Controller (Thin Controller)

```csharp
// ✅ Đúng cách — Controller CHỈ điều phối, không có business logic
[ApiController]
[Route("api/[controller]")]
public class EmployeeController : ControllerBase
{
    private readonly IEmployeeService _employeeService;

    public EmployeeController(IEmployeeService employeeService)
    {
        _employeeService = employeeService;
    }

    /// <summary>
    /// Lấy danh sách nhân viên
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<EmployeeResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var result = await _employeeService.GetAllAsync();
        return Ok(result);
    }

    /// <summary>
    /// Lấy nhân viên theo ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(EmployeeResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _employeeService.GetByIdAsync(id);
        return Ok(result);
    }

    /// <summary>
    /// Tạo nhân viên mới
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(EmployeeResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateEmployeeRequestDto requestDto)
    {
        var result = await _employeeService.CreateAsync(requestDto);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }
}

// ❌ Sai cách — Business logic trong Controller
[HttpPost]
public async Task<IActionResult> Create([FromBody] CreateEmployeeRequestDto dto)
{
    // ❌ Validation logic trong controller
    if (string.IsNullOrEmpty(dto.Email)) return BadRequest("Email required");
    
    // ❌ Gọi repository trực tiếp
    var entity = new Employee { Name = dto.Name };
    await _context.Employees.AddAsync(entity);
    await _context.SaveChangesAsync();
    
    return Ok(entity); // ❌ Trả entity thay vì DTO
}
```

---

## 4. Quy tắc Service (Business Logic Layer)

```csharp
// ✅ Đúng cách — Interface-first, xử lý business logic
public interface IEmployeeService
{
    Task<IEnumerable<EmployeeResponseDto>> GetAllAsync();
    Task<EmployeeResponseDto> GetByIdAsync(int id);
    Task<EmployeeResponseDto> CreateAsync(CreateEmployeeRequestDto requestDto);
    Task<EmployeeResponseDto> UpdateAsync(int id, UpdateEmployeeRequestDto requestDto);
    Task DeleteAsync(int id);
}

public class EmployeeService : IEmployeeService
{
    private readonly IEmployeeRepository _employeeRepository;

    public EmployeeService(IEmployeeRepository employeeRepository)
    {
        _employeeRepository = employeeRepository;
    }

    public async Task<EmployeeResponseDto> GetByIdAsync(int id)
    {
        var employee = await _employeeRepository.GetByIdAsync(id);
        
        if (employee == null)
            throw new NotFoundException($"Employee with ID {id} not found");

        return MapToResponseDto(employee);
    }

    public async Task<EmployeeResponseDto> CreateAsync(CreateEmployeeRequestDto requestDto)
    {
        // Business logic & validation ở đây
        var entity = MapToEntity(requestDto);
        var created = await _employeeRepository.AddAsync(entity);
        return MapToResponseDto(created);
    }

    private static EmployeeResponseDto MapToResponseDto(Employee entity) => new()
    {
        Id = entity.Id,
        FullName = entity.FullName,
        Email = entity.Email
    };

    private static Employee MapToEntity(CreateEmployeeRequestDto dto) => new()
    {
        FullName = dto.FullName,
        Email = dto.Email
    };
}
```

---

## 5. Quy tắc Repository (Data Access Layer)

```csharp
// ✅ Đúng cách — Interface-first, CHỈ xử lý CRUD / query database
public interface IEmployeeRepository
{
    Task<IEnumerable<Employee>> GetAllAsync();
    Task<Employee?> GetByIdAsync(int id);
    Task<Employee> AddAsync(Employee entity);
    Task UpdateAsync(Employee entity);
    Task DeleteAsync(Employee entity);
}

public class EmployeeRepository : IEmployeeRepository
{
    private readonly AppDbContext _context;

    public EmployeeRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Employee>> GetAllAsync()
    {
        return await _context.Employees.ToListAsync();
    }

    public async Task<Employee?> GetByIdAsync(int id)
    {
        return await _context.Employees.FindAsync(id);
    }

    public async Task<Employee> AddAsync(Employee entity)
    {
        await _context.Employees.AddAsync(entity);
        await _context.SaveChangesAsync();
        return entity;
    }
}

// ❌ Sai cách — Business logic trong Repository
public async Task<Employee> AddAsync(Employee entity)
{
    // ❌ Validation trong repository
    if (await _context.Employees.AnyAsync(e => e.Email == entity.Email))
        throw new Exception("Email already exists");
    
    // ❌ Mapping trong repository
    entity.CreatedDate = DateTime.Now;
    entity.Status = "Active";
    
    await _context.Employees.AddAsync(entity);
    await _context.SaveChangesAsync();
    return entity;
}
```

---

## 6. Quy tắc DTO

```csharp
// ✅ Tách riêng Request và Response DTO

// --- Request DTO (dữ liệu từ client gửi lên) ---
public class CreateEmployeeRequestDto
{
    [Required(ErrorMessage = "FullName is required")]
    [StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }
    public int DepartmentId { get; set; }
}

// --- Response DTO (dữ liệu trả về cho client) ---
public class EmployeeResponseDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

// ❌ Sai cách — Dùng 1 DTO cho cả Request + Response
public class EmployeeDto  // ❌ Không phân biệt Request/Response
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Password { get; set; }  // ❌ Lộ password trong response
}
```

---

## 7. Quy tắc Entity

```csharp
// ✅ Entity thuần — chỉ chứa properties, không có logic
public class Employee
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(100)]
    public string FullName { get; set; } = string.Empty;
    
    [Required]
    [StringLength(255)]
    public string Email { get; set; } = string.Empty;
    
    public string? Phone { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation Properties
    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
}
```

---

## 8. Quy tắc Dependency Injection

```csharp
// ✅ Đăng ký DI trong extension method theo module
// File: Shared/Extensions/ServiceCollectionExtensions.cs

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEmployeeModule(this IServiceCollection services)
    {
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        return services;
    }

    public static IServiceCollection AddAuthModule(this IServiceCollection services)
    {
        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }
}

// File: Program.cs
builder.Services.AddEmployeeModule();
builder.Services.AddAuthModule();
```

---

## 9. Quy tắc Error Handling

```csharp
// ✅ Sử dụng Custom Exceptions + Global Exception Middleware
// File: Shared/Exceptions/

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

public class BadRequestException : Exception
{
    public BadRequestException(string message) : base(message) { }
}

public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message) : base(message) { }
}

// File: Shared/Middleware/ExceptionHandlingMiddleware.cs
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (NotFoundException ex)
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsJsonAsync(new { error = ex.Message });
        }
        catch (BadRequestException ex)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(new { error = "Internal Server Error" });
        }
    }
}

// ❌ Sai cách — try/catch trong controller
[HttpGet("{id}")]
public async Task<IActionResult> GetById(int id)
{
    try
    {
        var result = await _service.GetByIdAsync(id);
        if (result == null) return NotFound(); // ❌ Logic phán đoán nằm ở controller
        return Ok(result);
    }
    catch (Exception ex)
    {
        return StatusCode(500, ex.Message); // ❌ Lộ exception message
    }
}
```

---

## 10. Quy tắc API Response

```csharp
// ✅ Response format thống nhất
// Thành công
{
    "data": { ... },
    "message": "Success"
}

// Lỗi
{
    "error": "Employee with ID 99 not found",
    "statusCode": 404
}

// List với phân trang
{
    "data": [ ... ],
    "pagination": {
        "currentPage": 1,
        "pageSize": 10,
        "totalCount": 50,
        "totalPages": 5
    }
}
```

---

## 11. Quy tắc Async/Await

- **TẤT CẢ** methods truy cập database phải là `async`.
- Method async đặt tên hậu tố `Async` → `GetByIdAsync()`, `CreateAsync()`.
- **LUÔN** dùng `await`, **KHÔNG** dùng `.Result` hay `.Wait()` (gây deadlock).
- **KHÔNG** dùng `async void` trừ event handlers.

---

## 12. Quy tắc Logging

- Dùng `ILogger<T>` inject qua constructor, **KHÔNG** dùng `Console.WriteLine()`.
- Log levels: `LogDebug` → dev, `LogInformation` → flow, `LogWarning` → bất thường, `LogError` → lỗi.
- **KHÔNG** log dữ liệu nhạy cảm (password, token, PII).

---

## 13. Quy tắc Authentication & Authorization

### JWT Bearer Token Setup
```csharp
// ✅ Cấu hình JWT trong Program.cs
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SecretKey"]!))
        };
    });

// Middleware pipeline — thứ tự QUAN TRỌNG
app.UseAuthentication();   // PHẢI trước Authorization
app.UseAuthorization();
```

### Sử dụng `[Authorize]` trong Controller
```csharp
// ✅ Đúng cách — Auth ở controller level, override ở action level
[ApiController]
[Route("api/[controller]")]
[Authorize]                                    // Mặc định yêu cầu đăng nhập
public class EmployeeController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()   // Yêu cầu đăng nhập (kế thừa)
    { ... }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]        // Chỉ Admin hoặc Manager
    public async Task<IActionResult> Create([FromBody] CreateEmployeeRequestDto dto)
    { ... }

    [HttpGet("public-info")]
    [AllowAnonymous]                            // Cho phép truy cập không cần đăng nhập
    public async Task<IActionResult> GetPublicInfo()
    { ... }
}

// ❌ Sai cách — Tự check role trong controller
[HttpPost]
public async Task<IActionResult> Create([FromBody] CreateEmployeeRequestDto dto)
{
    var role = User.FindFirst(ClaimTypes.Role)?.Value;
    if (role != "Admin") return Forbid();        // ❌ Dùng attribute thay vì tự check
    ...
}
```

### Lấy thông tin user hiện tại
```csharp
// ✅ Lấy thông tin từ JWT Claims trong Service
public class EmployeeService : IEmployeeService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public EmployeeService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = _httpContextAccessor.HttpContext?.User
            .FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.Parse(userIdClaim ?? "0");
    }
}
```

### Quy tắc Auth
- **Public endpoints**: Login, Register, ForgotPassword → `[AllowAnonymous]`
- **Protected endpoints**: Tất cả còn lại → `[Authorize]`
- **Role-based**: Dùng `[Authorize(Roles = "...")]` cho CRUD admin
- **KHÔNG** tự check role trong code — dùng attributes hoặc Policies
- **KHÔNG** lưu JWT secret trong code — dùng `appsettings.json` hoặc User Secrets

---

## 14. Quy tắc Configuration / Options Pattern

### Options Pattern đọc config
```csharp
// ✅ Đúng cách — Tạo class riêng cho mỗi nhóm config

// File: Shared/Constants/JwtSettings.cs
public class JwtSettings
{
    public const string SectionName = "Jwt";
    
    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; } = 60;
}

// File: appsettings.json
{
    "Jwt": {
        "SecretKey": "your-secret-key-at-least-32-characters",
        "Issuer": "hrm-api",
        "Audience": "hrm-client",
        "ExpirationMinutes": 60
    },
    "ConnectionStrings": {
        "DefaultConnection": "Server=...;Database=...;..."
    }
}

// File: Program.cs — Đăng ký Options
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection(JwtSettings.SectionName));

// File: Service — Inject qua IOptions<T>
public class AuthService : IAuthService
{
    private readonly JwtSettings _jwtSettings;

    public AuthService(IOptions<JwtSettings> jwtOptions)
    {
        _jwtSettings = jwtOptions.Value;
    }
}

// ❌ Sai cách — Hardcode hoặc đọc trực tiếp
public class AuthService
{
    private const string SecretKey = "my-secret-key";           // ❌ Hardcode
    
    public AuthService(IConfiguration config)
    {
        var key = config["Jwt:SecretKey"];                       // ❌ Magic string
    }
}
```

### Quy tắc Configuration
- **KHÔNG** hardcode connection string, JWT secret, API keys trong code.
- Dùng `IOptions<T>` hoặc `IOptionsSnapshot<T>` để inject config.
- Tạo class riêng cho mỗi nhóm config (JwtSettings, DatabaseSettings,...).
- Development: dùng `appsettings.Development.json` hoặc **User Secrets**.
- Production: dùng **Environment Variables** hoặc Azure Key Vault.
- Đặt config classes tại `Shared/Constants/` hoặc tạo folder `Shared/Options/`.

---

## 15. Quy tắc Validation

### 3 tầng validation

```
Tầng 1: Data Annotations (DTO)       → Validation format cơ bản
Tầng 2: [ApiController] auto-check   → Tự trả 400 nếu ModelState invalid
Tầng 3: Service layer                → Business rules validation
```

### Tầng 1 — Data Annotations trên DTO
```csharp
// ✅ Validation đơn giản bằng Data Annotations
public class CreateEmployeeRequestDto
{
    [Required(ErrorMessage = "Họ tên không được để trống")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Họ tên từ 2-100 ký tự")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email không được để trống")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Số điện thoại không đúng định dạng")]
    public string? Phone { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "DepartmentId phải lớn hơn 0")]
    public int DepartmentId { get; set; }
}
```

### Tầng 2 — Auto Validation bởi `[ApiController]`
```csharp
// ✅ [ApiController] tự động validate ModelState
// Nếu DTO không hợp lệ → trả 400 BadRequest tự động
// KHÔNG cần check ModelState.IsValid trong controller

// ❌ Sai cách — Check thừa
[HttpPost]
public async Task<IActionResult> Create([FromBody] CreateEmployeeRequestDto dto)
{
    if (!ModelState.IsValid) return BadRequest(ModelState);  // ❌ Thừa, [ApiController] đã handle
    ...
}
```

### Tầng 3 — Business Validation trong Service
```csharp
// ✅ Validation business rules trong Service
public async Task<EmployeeResponseDto> CreateAsync(CreateEmployeeRequestDto dto)
{
    // Business rule: Email phải unique
    var existingEmployee = await _repository.GetByEmailAsync(dto.Email);
    if (existingEmployee != null)
        throw new BadRequestException($"Email '{dto.Email}' đã được sử dụng");

    // Business rule: Department phải tồn tại
    var department = await _departmentRepository.GetByIdAsync(dto.DepartmentId);
    if (department == null)
        throw new NotFoundException($"Department với ID {dto.DepartmentId} không tồn tại");

    var entity = MapToEntity(dto);
    var created = await _repository.AddAsync(entity);
    return MapToResponseDto(created);
}
```

### Quy tắc Validation
- **Data Annotations** cho validation format (Required, StringLength, Email, Range,...).
- **Service layer** cho business rules (unique check, existence check, permission check).
- **KHÔNG** validate trong Controller — `[ApiController]` đã auto-validate.
- **KHÔNG** validate trong Repository — Repository chỉ CRUD.
- Error messages viết bằng **tiếng Việt** cho user-facing, **tiếng Anh** cho developer-facing.

---

## 16. Quy tắc Pagination

### DTOs chuẩn cho phân trang
```csharp
// File: Shared/DTOs/PaginationRequestDto.cs
public class PaginationRequestDto
{
    private const int MaxPageSize = 50;
    private int _pageSize = 10;

    public int PageNumber { get; set; } = 1;
    
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value > MaxPageSize ? MaxPageSize : value;
    }

    public string? SearchTerm { get; set; }
    public string? SortBy { get; set; }
    public bool IsDescending { get; set; } = false;
}

// File: Shared/DTOs/PaginatedResponseDto.cs
public class PaginatedResponseDto<T>
{
    public IEnumerable<T> Data { get; set; } = Enumerable.Empty<T>();
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => CurrentPage > 1;
    public bool HasNextPage => CurrentPage < TotalPages;
}
```

### Sử dụng trong Repository → Service → Controller
```csharp
// Repository — trả (items, totalCount)
public async Task<(IEnumerable<Employee> Items, int TotalCount)> GetPagedAsync(
    int pageNumber, int pageSize, string? searchTerm = null)
{
    var query = _context.Employees.AsNoTracking();

    if (!string.IsNullOrWhiteSpace(searchTerm))
        query = query.Where(e => e.FullName.Contains(searchTerm));

    var totalCount = await query.CountAsync();
    var items = await query
        .Skip((pageNumber - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync();

    return (items, totalCount);
}

// Service — wrap thành PaginatedResponseDto
public async Task<PaginatedResponseDto<EmployeeResponseDto>> GetPagedAsync(PaginationRequestDto request)
{
    var (items, totalCount) = await _repository.GetPagedAsync(
        request.PageNumber, request.PageSize, request.SearchTerm);

    return new PaginatedResponseDto<EmployeeResponseDto>
    {
        Data = items.Select(MapToResponseDto),
        CurrentPage = request.PageNumber,
        PageSize = request.PageSize,
        TotalCount = totalCount
    };
}

// Controller
[HttpGet]
public async Task<IActionResult> GetAll([FromQuery] PaginationRequestDto request)
{
    var result = await _employeeService.GetPagedAsync(request);
    return Ok(result);
}
```

### Quy tắc Pagination
- **Mọi API GET list** phải hỗ trợ phân trang.
- Default: `PageNumber = 1`, `PageSize = 10`, `MaxPageSize = 50`.
- Dùng `[FromQuery]` cho pagination params.
- Repository dùng `Skip().Take()`, trả kèm `totalCount`.
- Shared DTOs đặt tại `Shared/DTOs/`.

---

## 17. Quy tắc EF Core Best Practices

### Migration Workflow
```bash
# Tạo migration mới
dotnet ef migrations add AddEmployeeTable

# Cập nhật database
dotnet ef database update

# Rollback migration
dotnet ef database update PreviousMigrationName

# Xóa migration cuối (chưa apply)
dotnet ef migrations remove
```

### Query Optimization
```csharp
// ✅ AsNoTracking cho read-only queries — performance tốt hơn
public async Task<IEnumerable<Employee>> GetAllAsync()
{
    return await _context.Employees
        .AsNoTracking()
        .ToListAsync();
}

// ✅ Select chỉ lấy fields cần thiết
public async Task<IEnumerable<EmployeeListItemDto>> GetListAsync()
{
    return await _context.Employees
        .AsNoTracking()
        .Select(e => new EmployeeListItemDto
        {
            Id = e.Id,
            FullName = e.FullName,
            DepartmentName = e.Department.Name
        })
        .ToListAsync();
}

// ✅ Include cho eager loading — tránh N+1 problem
public async Task<Employee?> GetByIdWithDepartmentAsync(int id)
{
    return await _context.Employees
        .Include(e => e.Department)
        .FirstOrDefaultAsync(e => e.Id == id);
}

// ❌ Sai cách — N+1 problem
var employees = await _context.Employees.ToListAsync();
foreach (var emp in employees)
{
    var dept = await _context.Departments.FindAsync(emp.DepartmentId);  // ❌ Query trong loop
    emp.DepartmentName = dept?.Name;
}

// ❌ Sai cách — SaveChanges trong loop
foreach (var emp in employees)
{
    emp.IsActive = false;
    await _context.SaveChangesAsync();  // ❌ Gọi SaveChanges mỗi iteration
}

// ✅ Đúng cách — SaveChanges 1 lần
foreach (var emp in employees)
{
    emp.IsActive = false;
}
await _context.SaveChangesAsync();      // ✅ Batch save
```

### Fluent API Configuration
```csharp
// ✅ Dùng IEntityTypeConfiguration<T> tách riêng mỗi entity
// File: Data/Configurations/EmployeeConfiguration.cs
public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees");
        builder.HasKey(e => e.Id);
        
        builder.Property(e => e.FullName)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(e => e.Email)
            .IsRequired()
            .HasMaxLength(255);
        
        builder.HasIndex(e => e.Email).IsUnique();
        
        builder.HasOne(e => e.Department)
            .WithMany(d => d.Employees)
            .HasForeignKey(e => e.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

// File: Data/AppDbContext.cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
```

### Quy tắc EF Core
- **LUÔN** dùng `AsNoTracking()` cho read-only queries.
- **LUÔN** dùng `Include()` khi cần navigation properties — tránh N+1.
- **KHÔNG** gọi `SaveChangesAsync()` trong loop — batch lại.
- **KHÔNG** dùng raw SQL trừ khi EF Core không hỗ trợ được.
- Migration name phải mô tả rõ: `AddEmployeeTable`, `AddEmailUniqueIndex`.
- Mỗi Entity có 1 file Configuration riêng tại `Data/Configurations/`.

---

## 18. Quy tắc Security Best Practices

### CORS Configuration
```csharp
// ✅ Đúng cách — Chỉ allow origins cụ thể
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",             // Dev frontend
                "https://your-production-domain.com" // Prod frontend
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// ❌ Sai cách — Allow tất cả origins
policy.SetIsOriginAllowed(_ => true);   // ❌ Nguy hiểm trong production
policy.AllowAnyOrigin();                 // ❌ Không dùng kết hợp AllowCredentials
```

### Secrets Management
```
Development:
  ✅ appsettings.Development.json (KHÔNG commit secrets thật)
  ✅ User Secrets: dotnet user-secrets set "Jwt:SecretKey" "your-key"
  ❌ Hardcode trong code

Production:
  ✅ Environment Variables
  ✅ Azure Key Vault / AWS Secrets Manager
  ❌ appsettings.json chứa secrets thật
```

### Quy tắc Security
- **HTTPS**: Enforce HTTPS trong production (`app.UseHttpsRedirection()`).
- **CORS**: Chỉ allow origins cụ thể, **KHÔNG** dùng `AllowAnyOrigin()` với `AllowCredentials()`.
- **Input**: EF Core tự prevent SQL Injection qua parameterized queries. **KHÔNG** dùng raw SQL với string concatenation.
- **Response**: **KHÔNG** trả stack trace, internal error details trong production.
- **Headers**: Thêm security headers (X-Content-Type-Options, X-Frame-Options).
- **Secrets**: **KHÔNG** commit secrets vào Git. Dùng `.gitignore` cho `appsettings.*.json` nếu chứa secrets.
- **Password**: Hash bằng `BCrypt` hoặc `Microsoft.AspNetCore.Identity`, **KHÔNG** lưu plaintext.

---

## 19. Quy tắc Global Usings

```csharp
// ✅ File: GlobalUsings.cs (đặt ở gốc project)
// Tập trung các using phổ biến, giảm boilerplate trong mỗi file

// .NET & ASP.NET Core
global using System.ComponentModel.DataAnnotations;
global using Microsoft.AspNetCore.Mvc;
global using Microsoft.EntityFrameworkCore;

// Project layers
global using hrm_r_wfm_be.Data;
global using hrm_r_wfm_be.Shared.Exceptions;
global using hrm_r_wfm_be.Shared.Constants;
```

### Quy tắc Global Usings
- Đặt file `GlobalUsings.cs` tại **gốc project**.
- Chỉ thêm các using **thực sự dùng chung** (>70% files dùng).
- **KHÔNG** thêm module-specific usings vào global — dùng using bình thường.
- Giữ file ngắn gọn, tối đa **15-20 dòng**.

---

## 20. Quy tắc Swagger / OpenAPI Documentation

### Cấu hình Swagger
```csharp
// ✅ Program.cs — Enable Swagger trong Development
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "HRM & WFM API",
        Version = "v1",
        Description = "API cho hệ thống quản lý nhân sự và lịch làm việc"
    });

    // Hỗ trợ JWT trong Swagger UI
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Nhập token: Bearer {your-token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // Enable XML comments
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
});

// Middleware — chỉ enable trong Development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
```

### XML Documentation trong Controller
```csharp
// ✅ Mỗi action method PHẢI có XML comment
/// <summary>
/// Lấy danh sách nhân viên có phân trang
/// </summary>
/// <param name="request">Thông tin phân trang và tìm kiếm</param>
/// <returns>Danh sách nhân viên</returns>
/// <response code="200">Trả về danh sách nhân viên thành công</response>
/// <response code="401">Chưa đăng nhập</response>
[HttpGet]
[ProducesResponseType(typeof(PaginatedResponseDto<EmployeeResponseDto>), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public async Task<IActionResult> GetAll([FromQuery] PaginationRequestDto request)
{
    var result = await _employeeService.GetPagedAsync(request);
    return Ok(result);
}
```

### Quy tắc Swagger
- **MỌI** action method phải có `/// <summary>` XML comment.
- **MỌI** action method phải có `[ProducesResponseType]` cho **tất cả** status codes có thể trả về.
- Enable XML comments generation trong `.csproj`:
  ```xml
  <PropertyGroup>
      <GenerateDocumentationFile>true</GenerateDocumentationFile>
      <NoWarn>$(NoWarn);1591</NoWarn>
  </PropertyGroup>
  ```
- Swagger UI chỉ enable trong **Development**, **KHÔNG** expose trong Production.
