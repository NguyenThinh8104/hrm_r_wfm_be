# 🔧 Prompt: Tạo Service + Interface

## Hướng dẫn sử dụng
Copy toàn bộ nội dung bên dưới, thay thế các `[PLACEHOLDER]` bằng thông tin thực tế, sau đó gửi cho AI.

---

## Prompt Template

```
Tôi cần tạo một Service mới (Interface + Implementation) cho dự án ASP.NET Core 8.

### Thông tin service:
- **Tên module**: [TÊN_MODULE] (VD: Employee, Auth, Department)
- **Tên service**: [TÊN_SERVICE]Service (VD: EmployeeService, AuthService)
- **Mô tả chức năng**: [MÔ_TẢ] (VD: Xử lý business logic cho quản lý nhân viên)
- **Danh sách methods**:
  1. [TÊN_METHOD] — [MÔ_TẢ] — Input: [PARAMS] — Output: [RETURN_TYPE]
  2. (VD: GetAllAsync — Lấy tất cả nhân viên — Input: none — Output: IEnumerable<EmployeeResponseDto>)
  3. ...
- **Phụ thuộc vào**: [DANH_SÁCH_DEPENDENCIES] (VD: IEmployeeRepository, ILogger)

### Quy tắc bắt buộc:
1. Tạo 2 files:
   - Interface: `Modules/[MODULE]/Services/I[Tên]Service.cs`
   - Implementation: `Modules/[MODULE]/Services/[Tên]Service.cs`
2. Namespace: `hrm_r_wfm_be.Modules.[MODULE].Services`
3. Service chứa **business logic** — validation, mapping, orchestration.
4. Inject dependencies qua constructor (IRepository, ILogger,...).
5. Tất cả methods phải là `async Task<>`, tên hậu tố `Async`.
6. KHÔNG truy cập DbContext trực tiếp — phải qua Repository.
7. KHÔNG xử lý HTTP-specific logic (StatusCode, ActionResult).
8. Throw custom exceptions khi có lỗi (NotFoundException, BadRequestException).
9. Thêm XML documentation cho interface methods.
10. Mapping Entity ↔ DTO bằng private helper methods hoặc AutoMapper.

### Output mong muốn:
- File `I[Tên]Service.cs` (interface) hoàn chỉnh.
- File `[Tên]Service.cs` (implementation) hoàn chỉnh.
- Cách đăng ký DI: `services.AddScoped<I[Tên]Service, [Tên]Service>();`
- Danh sách DTOs và Repository cần tạo (nếu chưa có).
```
