# 🔄 Prompt: Refactor Code Backend

## Hướng dẫn sử dụng
Copy toàn bộ nội dung bên dưới, thay thế các `[PLACEHOLDER]` bằng thông tin thực tế, sau đó gửi cho AI.

---

## Prompt Template

```
Tôi cần refactor code trong dự án ASP.NET Core 8.

### Thông tin refactor:
- **File cần refactor**: [ĐƯỜNG_DẪN_FILE]
- **Lý do refactor**: [LÝ_DO] (VD: code quá dài, logic lặp lại, không đúng conventions, thiếu DI)
- **Phạm vi**: [TOÀN_BỘ_FILE / CHỈ_MỘT_PHẦN]

### Code hiện tại:
```csharp
[DÁN_CODE_CẦN_REFACTOR]
```

### Mục tiêu refactor:
- [ ] Tách business logic từ Controller ra Service
- [ ] Tách data access từ Service ra Repository
- [ ] Thêm Interface cho DI
- [ ] Tách DTO riêng (Request/Response)
- [ ] Di chuyển file đúng vị trí theo Modular Architecture
- [ ] Áp dụng đúng namespace conventions
- [ ] Cải thiện error handling (custom exceptions)
- [ ] Thêm async/await cho database operations
- [ ] Tối ưu query (AsNoTracking, Select, pagination)
- [ ] Giảm code duplication
- [ ] [MỤC_TIÊU_KHÁC]

### Quy tắc khi refactor:
1. KHÔNG thay đổi behavior hiện tại (API contract phải giống y hệt).
2. Tuân thủ coding standards (`agents/rules/coding-standards.md`).
3. Đặt file đúng vị trí theo Modular Architecture.
4. Nếu tách ra file/class mới → cập nhật DI registration.
5. Giải thích lý do cho mỗi thay đổi.
6. Đảm bảo tất cả dependencies được inject qua constructor.

### Output mong muốn:
- Code đã refactor (tất cả files liên quan).
- Giải thích từng thay đổi và lý do.
- Danh sách files mới tạo / files bị thay đổi.
- Cập nhật DI registration nếu cần.
- Xác nhận API contract không thay đổi.
```
