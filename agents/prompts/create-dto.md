# 📦 Prompt: Tạo DTO (Request + Response)

## Hướng dẫn sử dụng
Copy toàn bộ nội dung bên dưới, thay thế các `[PLACEHOLDER]` bằng thông tin thực tế, sau đó gửi cho AI.

---

## Prompt Template

```
Tôi cần tạo DTOs (Data Transfer Objects) cho dự án ASP.NET Core 8.

### Thông tin DTO:
- **Tên module**: [TÊN_MODULE] (VD: Employee, Auth, Department)
- **Entity liên quan**: [TÊN_ENTITY] (VD: Employee)
- **Loại DTO cần tạo**:
  - [ ] Create Request DTO (cho POST)
  - [ ] Update Request DTO (cho PUT)
  - [ ] Response DTO (cho GET response)
  - [ ] List/Filter Request DTO (cho GET với query params)
  - [ ] [KHÁC]
- **Danh sách fields**: 
  [LIỆT_KÊ_FIELDS] (VD: FullName (string, required), Email (string, required), Phone (string?, optional))
- **Fields nào cần validation?**: [LIỆT_KÊ] (VD: Email — [EmailAddress], FullName — [Required, StringLength(100)])
- **Fields nào KHÔNG được trả về response?**: [LIỆT_KÊ] (VD: Password, PasswordHash)

### Quy tắc bắt buộc:
1. Đặt file tại: `Modules/[MODULE]/DTOs/`
2. Namespace: `hrm_r_wfm_be.Modules.[MODULE].DTOs`
3. TÁCH RIÊNG Request DTO và Response DTO — KHÔNG dùng 1 DTO cho cả hai.
4. Đặt tên theo pattern:
   - Request: `Create[Entity]RequestDto.cs`, `Update[Entity]RequestDto.cs`
   - Response: `[Entity]ResponseDto.cs`
   - Filter: `[Entity]FilterDto.cs`
5. Sử dụng Data Annotations cho validation:
   - `[Required]`, `[StringLength]`, `[EmailAddress]`, `[Range]`,...
6. Properties dùng `{ get; set; }`, string mặc định `= string.Empty;`
7. Nullable properties dùng `?` → `public string? Phone { get; set; }`
8. KHÔNG include navigation properties trong DTO — chỉ include ID hoặc tên hiển thị.
9. KHÔNG include sensitive fields (Password, PasswordHash) trong Response DTO.

### Output mong muốn:
- Tất cả file DTO cần thiết.
- Mỗi DTO có đầy đủ Data Annotations.
- Ví dụ JSON request/response tương ứng.
```
