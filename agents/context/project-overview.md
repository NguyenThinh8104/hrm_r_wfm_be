# 🌐 Project Overview — Ngữ cảnh dự án Backend cho AI

## Mục đích
File này cung cấp **ngữ cảnh tổng quan** về backend để AI hiểu đúng codebase ngay từ đầu.  
**Đính kèm nội dung này** khi bắt đầu session mới với AI.

---

## Thông tin dự án

- **Tên dự án**: SWP391 — HRM & Workforce Management (Backend)
- **Framework**: ASP.NET Core 8 Web API
- **Ngôn ngữ**: C# 12
- **Kiến trúc**: Monolith, Modular Architecture (chia theo phân hệ nghiệp vụ)
- **Pattern**: Repository Pattern + Service Layer + Thin Controllers
- **ORM**: Entity Framework Core 8
- **Quản lý code**: Git, chuẩn Gitflow

## Tech Stack

| Hạng mục | Công nghệ |
|---|---|
| Runtime | .NET 8 |
| Web Framework | ASP.NET Core Web API |
| ORM | Entity Framework Core 8 |
| Database | SQL Server |
| Authentication | JWT Bearer Token |
| Authorization | Role-based (`[Authorize(Roles)]`) |
| Validation | Data Annotations + Service-layer validation |
| Mapping | Manual mapping (private helper methods) |
| API Documentation | Swagger / OpenAPI |
| Logging | Built-in ILogger |
| Error Handling | Custom Exceptions + Global Middleware |
| Configuration | Options Pattern (`IOptions<T>`) |
| Password Hashing | BCrypt |

## Cấu trúc thư mục (Modular Architecture)

```
backend/
├── agents/                              🤖 AI Prompts & Skills cho team
│   ├── rules/coding-standards.md
│   ├── prompts/                         Prompt templates
│   └── context/project-overview.md      ← File này
│
├── Modules/                             📦 PHÂN HỆ NGHIỆP VỤ
│   ├── Auth/                            Đăng nhập, xác thực JWT
│   │   ├── Controllers/
│   │   ├── Services/
│   │   │   ├── IAuthService.cs
│   │   │   └── AuthService.cs
│   │   ├── Repositories/
│   │   │   ├── IAuthRepository.cs
│   │   │   └── AuthRepository.cs
│   │   ├── DTOs/
│   │   └── Entities/
│   ├── Employee/                        Quản lý nhân sự
│   ├── Department/                      Quản lý phòng ban
│   ├── Attendance/                      Chấm công
│   ├── Schedule/                        Ca làm việc
│   ├── Leave/                           Nghỉ phép
│   └── Dashboard/                       Tổng quan
│
├── Shared/                              🔗 DÙNG CHUNG cho tất cả modules
│   ├── Constants/                       Hằng số chung (Roles, Messages,...)
│   ├── Enums/                           Enums dùng chung (Status, Role,...)
│   ├── Exceptions/                      Custom exception classes
│   ├── Helpers/                         Utility / Helper functions
│   ├── Middleware/                       Exception, Logging middleware
│   └── Extensions/                      DI extension methods
│
├── Data/                                🗄️ DATABASE
│   ├── AppDbContext.cs                  EF Core DbContext
│   └── Configurations/                  Fluent API entity configs
│
├── Program.cs                           ⚙️ Entry point, DI, Middleware pipeline
├── appsettings.json
└── hrm_r_wfm_be.csproj
```

## Quy tắc quan trọng

### Modular Architecture
1. **Code nghiệp vụ** nằm trong `Modules/[TênModule]/`.
2. **Code dùng chung** nằm trong `Shared/`.
3. **KHÔNG tham chiếu chéo** giữa các module. Nếu cần dùng chung → chuyển vào `Shared/`.
4. Mỗi module có **Controllers, Services, Repositories, DTOs, Entities riêng**.
5. Service và Repository **phải có Interface** (Interface-first design).

### Gitflow
- `main` — Production, luôn stable
- `develop` — Nhánh phát triển chính
- `feature/be-[module-name]` — Nhánh tính năng BE theo module
- `hotfix/be-[bug-name]` — Sửa lỗi khẩn cấp
- `release/[version]` — Chuẩn bị release

### Quy ước đặt tên

- **Namespace**: `hrm_r_wfm_be.Modules.[Module].[Layer]`
  - VD: `hrm_r_wfm_be.Modules.Employee.Services`
- **Controllers**: PascalCase + hậu tố `Controller`
  - VD: `EmployeeController.cs`
- **Services**: PascalCase + hậu tố `Service`, interface `IService`
  - VD: `IEmployeeService.cs`, `EmployeeService.cs`
- **Repositories**: PascalCase + hậu tố `Repository`, interface `IRepository`
  - VD: `IEmployeeRepository.cs`, `EmployeeRepository.cs`
- **DTOs**: PascalCase + hậu tố mô tả loại DTO
  - VD: `CreateEmployeeRequestDto.cs`, `EmployeeResponseDto.cs`
- **Entities**: PascalCase, không hậu tố
  - VD: `Employee.cs`, `Department.cs`
- **Enums**: PascalCase + hậu tố `Enum` (optional)
  - VD: `UserRole.cs`, `LeaveStatus.cs`

### Workflow khi code tính năng mới trong module

```
1️⃣  Entities           →  Tạo model tại Modules/[tên]/Entities/
2️⃣  EF Configuration   →  Cấu hình tại Data/Configurations/
3️⃣  DTOs               →  Tạo Request/Response tại Modules/[tên]/DTOs/
4️⃣  Repository         →  Interface + Implementation tại Modules/[tên]/Repositories/
5️⃣  Service            →  Interface + Implementation tại Modules/[tên]/Services/
6️⃣  Controller         →  API endpoints tại Modules/[tên]/Controllers/
7️⃣  DI Registration    →  Đăng ký tại Shared/Extensions/ hoặc Program.cs
```

### Namespace & Using rules

```csharp
// ✅ Import từ Shared
using hrm_r_wfm_be.Shared.Constants;
using hrm_r_wfm_be.Shared.Exceptions;

// ✅ Import trong cùng module
using hrm_r_wfm_be.Modules.Employee.DTOs;
using hrm_r_wfm_be.Modules.Employee.Services;

// ✅ Import Data layer
using hrm_r_wfm_be.Data;

// ❌ KHÔNG import từ module khác
using hrm_r_wfm_be.Modules.Attendance.Services;
```
