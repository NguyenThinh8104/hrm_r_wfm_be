# 🗄️ Prompt: Tạo Repository + Interface

## Hướng dẫn sử dụng
Copy toàn bộ nội dung bên dưới, thay thế các `[PLACEHOLDER]` bằng thông tin thực tế, sau đó gửi cho AI.

---

## Prompt Template

```
Tôi cần tạo một Repository mới (Interface + Implementation) cho dự án ASP.NET Core 8.

### Thông tin repository:
- **Tên module**: [TÊN_MODULE] (VD: Employee, Department, Attendance)
- **Entity liên quan**: [TÊN_ENTITY] (VD: Employee, Department)
- **Tên repository**: [TÊN]Repository (VD: EmployeeRepository)
- **Danh sách methods**:
  1. [TÊN_METHOD] — [MÔ_TẢ] (VD: GetAllAsync — Lấy tất cả employees)
  2. [TÊN_METHOD] — [MÔ_TẢ] (VD: GetByIdAsync — Tìm employee theo ID)
  3. ...
- **Có cần query phức tạp không?**: [CÓ/KHÔNG] (VD: filter, search, pagination)
- **Có Include navigation properties không?**: [CÓ/KHÔNG] (VD: .Include(e => e.Department))

### Quy tắc bắt buộc:
1. Tạo 2 files:
   - Interface: `Modules/[MODULE]/Repositories/I[Tên]Repository.cs`
   - Implementation: `Modules/[MODULE]/Repositories/[Tên]Repository.cs`
2. Namespace: `hrm_r_wfm_be.Modules.[MODULE].Repositories`
3. Repository CHỈ xử lý **CRUD + query database** — KHÔNG có business logic.
4. Inject `AppDbContext` qua constructor.
5. Tất cả methods phải là `async Task<>`, tên hậu tố `Async`.
6. Sử dụng `AsNoTracking()` cho các query chỉ đọc (GET).
7. KHÔNG throw business exceptions — chỉ return null / empty collection.
8. KHÔNG mapping DTO trong repository — chỉ làm việc với Entity.
9. Pagination: nhận `pageNumber` + `pageSize`, return `(items, totalCount)`.
10. Thêm XML documentation cho interface methods.

### Output mong muốn:
- File `I[Tên]Repository.cs` (interface) hoàn chỉnh.
- File `[Tên]Repository.cs` (implementation) hoàn chỉnh.
- Cách đăng ký DI: `services.AddScoped<I[Tên]Repository, [Tên]Repository>();`
- Lưu ý: Đảm bảo Entity và DbContext đã được tạo trước.
```
