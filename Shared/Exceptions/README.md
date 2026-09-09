# 💥 Exceptions — Custom Exception Classes

## Mục đích
Chứa các **Custom Exception classes** dùng chung, được catch bởi `ExceptionHandlingMiddleware` để trả HTTP response phù hợp.

## Quy tắc
- Mỗi loại lỗi HTTP **1 exception class riêng**.
- Kế thừa từ `Exception`, constructor nhận `string message`.
- KHÔNG throw `Exception` chung chung — luôn dùng custom exception cụ thể.
- Mapping Exception → HTTP Status Code xử lý tại `Shared/Middleware/ExceptionHandlingMiddleware.cs`.

## Ví dụ file trong folder này
```
Exceptions/
├── NotFoundException.cs           ← 404 Not Found
├── BadRequestException.cs         ← 400 Bad Request
├── UnauthorizedException.cs       ← 401 Unauthorized
├── ForbiddenException.cs          ← 403 Forbidden
└── ConflictException.cs           ← 409 Conflict (duplicate data)
```

## Namespace
```csharp
namespace hrm_r_wfm_be.Shared.Exceptions;
```

## Template
```csharp
// NotFoundException.cs → HTTP 404
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
    public NotFoundException(string entity, object id) 
        : base($"{entity} với ID {id} không tồn tại") { }
}

// BadRequestException.cs → HTTP 400
public class BadRequestException : Exception
{
    public BadRequestException(string message) : base(message) { }
}

// UnauthorizedException.cs → HTTP 401
public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message = "Chưa đăng nhập hoặc token hết hạn") 
        : base(message) { }
}

// ForbiddenException.cs → HTTP 403
public class ForbiddenException : Exception
{
    public ForbiddenException(string message = "Bạn không có quyền thực hiện hành động này") 
        : base(message) { }
}

// ConflictException.cs → HTTP 409
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}
```

## Cách sử dụng trong Service
```csharp
// Trong EmployeeService.cs
var employee = await _repository.GetByIdAsync(id);
if (employee == null)
    throw new NotFoundException("Employee", id);

var existing = await _repository.GetByEmailAsync(dto.Email);
if (existing != null)
    throw new ConflictException($"Email '{dto.Email}' đã được sử dụng");
```
