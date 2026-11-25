# Kiểm tra API duyệt đơn chi nhánh

Chạy từ thư mục backend:

```powershell
dotnet test tests/Shifts.Tests/Shifts.Tests.csproj -m:1
```

Test khởi động controller thật qua HTTP trên cổng ngẫu nhiên và chạy service/EF Core
với SQLite trong bộ nhớ. Không dùng tài khoản, email hoặc database đang phát triển.
Model giữ các quan hệ production; test bỏ cột địa lý không liên quan và đổi kiểu
unsigned sang signed để SQLite hỗ trợ. Có thêm test dùng model/Pomelo production để
kiểm tra dịch SQL MySQL của update hết hạn, thống kê và truy vấn trang/từ khóa; test
này chặn kết nối và giả lập reader rỗng. Đây không phải kiểm thử trên MySQL thật.

Các tình huống: lọc/tìm kiếm/phân trang trong SQL, thứ tự ổn định khi trùng thời gian,
thống kê toàn chi nhánh, dữ liệu đơn cũ gắn qua assignment, tự hết hạn trước khi đếm,
không tác động chi nhánh khác, trang cuối sau khi đơn đổi trạng thái, kết quả rỗng,
validation query, quyền chi nhánh/quyền vai trò và route alias.
