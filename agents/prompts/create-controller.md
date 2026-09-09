# 🎮 Prompt: Tạo Controller API mới

## Hướng dẫn sử dụng
Copy toàn bộ nội dung bên dưới, thay thế các `[PLACEHOLDER]` bằng thông tin thực tế, sau đó gửi cho AI.

---

## Prompt Template

```
Tôi cần tạo một API Controller mới cho dự án ASP.NET Core 8.

### Thông tin controller:
- **Tên controller**: [TÊN_CONTROLLER] (VD: EmployeeController, DepartmentController)
- **Thuộc module nào?**: [TÊN_MODULE] (VD: Employee, Attendance, Schedule)
- **Route prefix**: api/[TÊN_ROUTE] (VD: api/employees, api/departments)
- **Danh sách endpoints**:
  1. [HTTP_METHOD] [ROUTE] — [MÔ_TẢ] (VD: GET /api/employees — Lấy danh sách nhân viên)
  2. [HTTP_METHOD] [ROUTE] — [MÔ_TẢ]
  3. ...
- **Cần xác thực (Authorize)?**: [CÓ/KHÔNG] (nếu có, chỉ rõ roles)

### Quy tắc bắt buộc:
1. Đặt file tại: `Modules/[MODULE]/Controllers/[TênController]Controller.cs`
2. Namespace: `hrm_r_wfm_be.Modules.[MODULE].Controllers`
3. Controller phải là **Thin Controller** — CHỈ điều phối, KHÔNG có business logic.
4. Inject Service qua constructor (IService interface).
5. Sử dụng `[ApiController]` và `[Route("api/[controller]")]` attributes.
6. Mỗi action method phải có:
   - XML documentation comment (`/// <summary>`)
   - `[ProducesResponseType]` attribute cho mỗi status code có thể trả về
   - Return type `Task<IActionResult>`
7. Sử dụng `[FromBody]` cho POST/PUT body, `[FromQuery]` cho query params.
8. KHÔNG dùng try/catch trong controller — lỗi xử lý qua ExceptionHandlingMiddleware.
9. KHÔNG gọi Repository trực tiếp — phải qua Service.

### Output mong muốn:
- File `[TênController]Controller.cs` hoàn chỉnh.
- Danh sách DTOs cần tạo (nếu chưa có).
- Cách đăng ký DI trong Program.cs (nếu module mới).
```
