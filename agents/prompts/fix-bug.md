# 🐛 Prompt: Fix Bug Backend

## Hướng dẫn sử dụng
Copy toàn bộ nội dung bên dưới, thay thế các `[PLACEHOLDER]` bằng thông tin thực tế, sau đó gửi cho AI.

---

## Prompt Template

```
Tôi cần fix một bug trong dự án ASP.NET Core 8.

### Mô tả bug:
- **File bị lỗi**: [ĐƯỜNG_DẪN_FILE] (VD: Modules/Employee/Services/EmployeeService.cs)
- **API endpoint liên quan**: [METHOD] [ROUTE] (VD: GET /api/employees/5)
- **Hành vi mong muốn**: [MÔ_TẢ] (VD: API trả về employee với ID = 5)
- **Hành vi thực tế (bug)**: [MÔ_TẢ_LỖI] (VD: API trả về 500 Internal Server Error)
- **HTTP Status Code nhận được**: [STATUS_CODE]
- **Error message / Stack trace (nếu có)**:
  ```
  [DÁN_ERROR_LOG]
  ```
- **Bước tái hiện bug**:
  1. [BƯỚC_1]
  2. [BƯỚC_2]
  3. ...

### Code hiện tại:
```csharp
[DÁN_CODE_BỊ_LỖI_VÀO_ĐÂY]
```

### Quy tắc khi fix:
1. Giải thích **root cause** trước khi sửa.
2. Chỉ sửa phần code liên quan, KHÔNG refactor toàn bộ file.
3. Giữ nguyên coding standards (`agents/rules/coding-standards.md`).
4. Kiểm tra theo đúng layer:
   - Lỗi 400 → kiểm tra DTO validation, request format
   - Lỗi 401/403 → kiểm tra authentication / authorization
   - Lỗi 404 → kiểm tra repository query, route config
   - Lỗi 500 → kiểm tra service logic, null reference, DB connection
5. Nếu lỗi liên quan đến DB → kiểm tra Entity, migration, connection string.
6. Nếu lỗi liên quan đến DI → kiểm tra đăng ký service trong Program.cs.
7. Thêm comment giải thích chỗ fix nếu logic phức tạp.

### Output mong muốn:
- Giải thích nguyên nhân bug (root cause analysis).
- Code đã sửa (chỉ phần thay đổi, dạng diff nếu có thể).
- Gợi ý cách phòng tránh bug tương tự trong tương lai.
```
