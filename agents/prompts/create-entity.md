# 🏛️ Prompt: Tạo Entity/Model + EF Configuration

## Hướng dẫn sử dụng
Copy toàn bộ nội dung bên dưới, thay thế các `[PLACEHOLDER]` bằng thông tin thực tế, sau đó gửi cho AI.

---

## Prompt Template

```
Tôi cần tạo một Entity (Model) mới và cấu hình Entity Framework Core cho dự án ASP.NET Core 8.

### Thông tin entity:
- **Tên entity**: [TÊN_ENTITY] (VD: Employee, Department, LeaveRequest)
- **Thuộc module**: [TÊN_MODULE] (VD: Employee, Department, Leave)
- **Tên bảng trong DB**: [TÊN_BẢNG] (VD: Employees, Departments, LeaveRequests)
- **Danh sách properties**:
  1. [TÊN] — [KIỂU_DỮ_LIỆU] — [REQUIRED/OPTIONAL] — [MÔ_TẢ]
  2. (VD: FullName — string — Required — Họ tên nhân viên, max 100 ký tự)
  3. (VD: DepartmentId — int — Required — FK tới Department)
  4. ...
- **Quan hệ (Relationships)**:
  1. [LOẠI_QUAN_HỆ] với [ENTITY_KHÁC] (VD: Many-to-One với Department)
  2. (VD: One-to-Many với LeaveRequest)
  3. ...
- **Có soft delete không?**: [CÓ/KHÔNG] (nếu có, thêm IsDeleted + DeletedAt)
- **Có audit fields không?**: [CÓ/KHÔNG] (CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)

### Quy tắc bắt buộc:
1. Entity đặt tại: `Modules/[MODULE]/Entities/[TênEntity].cs`
2. EF Configuration đặt tại: `Data/Configurations/[TênEntity]Configuration.cs`
3. Namespace Entity: `hrm_r_wfm_be.Modules.[MODULE].Entities`
4. Namespace Config: `hrm_r_wfm_be.Data.Configurations`
5. Entity là POCO class — CHỈ chứa properties, KHÔNG có logic.
6. Dùng Data Annotations cho validation cơ bản (`[Required]`, `[StringLength]`).
7. Dùng Fluent API (IEntityTypeConfiguration<T>) cho cấu hình phức tạp:
   - Table name, primary key
   - Relationships (HasOne, HasMany)
   - Indexes, unique constraints
   - Default values
8. Navigation properties phải có `= null!;` để suppress nullable warning.
9. String properties mặc định `= string.Empty;`
10. Thêm DbSet vào `AppDbContext.cs`.

### Output mong muốn:
- File Entity `.cs` hoàn chỉnh.
- File EF Configuration `.cs` hoàn chỉnh.
- Câu lệnh thêm DbSet vào `AppDbContext.cs`.
- Migration command: `dotnet ef migrations add Add[TênEntity]Table`
```
