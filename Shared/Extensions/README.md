# 🧩 Extensions — DI Registration & Extension Methods

## Mục đích
Chứa các **Extension methods** để tổ chức đăng ký Dependency Injection theo module và mở rộng chức năng của các built-in types.

## Quy tắc
- Extension classes là `public static class`, methods là `public static`.
- DI registration tổ chức **theo module**: `AddAuthModule()`, `AddEmployeeModule()`.
- Mỗi module 1 extension method riêng → dễ bật/tắt module.
- Gọi các extension methods trong `Program.cs`.
- KHÔNG đặt business logic trong extension — chỉ cấu hình và đăng ký.

## Ví dụ file trong folder này
```
Extensions/
├── ServiceCollectionExtensions.cs    ← Đăng ký DI cho các modules
└── ApplicationBuilderExtensions.cs   ← Cấu hình middleware pipeline
```

## Namespace
```csharp
namespace hrm_r_wfm_be.Shared.Extensions;
```

## Template
```csharp
// ServiceCollectionExtensions.cs
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Đăng ký DI cho module Auth
    /// </summary>
    public static IServiceCollection AddAuthModule(this IServiceCollection services)
    {
        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }

    /// <summary>
    /// Đăng ký DI cho module Employee
    /// </summary>
    public static IServiceCollection AddEmployeeModule(this IServiceCollection services)
    {
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        return services;
    }

    /// <summary>
    /// Đăng ký DI cho module Department
    /// </summary>
    public static IServiceCollection AddDepartmentModule(this IServiceCollection services)
    {
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<IDepartmentService, DepartmentService>();
        return services;
    }

    // ... thêm các modules khác tương tự
}

// ApplicationBuilderExtensions.cs
public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseCustomMiddleware(this IApplicationBuilder app)
    {
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        // Thêm middleware khác nếu cần
        return app;
    }
}
```

## Sử dụng trong Program.cs
```csharp
// Đăng ký DI
builder.Services.AddAuthModule();
builder.Services.AddEmployeeModule();
builder.Services.AddDepartmentModule();

// Cấu hình middleware
app.UseCustomMiddleware();
```
