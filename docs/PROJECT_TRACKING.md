# BẢNG THEO DÕI TIẾN ĐỘ DỰ ÁN (PROJECT TRACKING)
## Đề tài: Nền tảng Quản trị Nhân sự Vận hành Chuỗi Siêu thị Tiện lợi (Retail Chain Workforce Management Platform - R-WFM)
### Môn học: SWP391 / SWP - Đại học FPT (Học kỳ Fall 2026)

---

## 1. Tổng quan Trạng thái Milestone Tuần 1 (Theo Checklist yêu cầu)

| STT | Hạng mục công việc (Task) | Trạng thái | Ghi chú & Bằng chứng thực hiện |
|---|---|:---:|---|
| 1 | **Thành lập nhóm** | ✅ Đã xong | Đã bầu Leader, phân công vai trò thành viên nhóm. |
| 2 | **Chọn đề tài xong** | ✅ Đã xong | Đề tài R-WFM (Quản trị nhân sự vận hành chuỗi siêu thị tiện lợi 2-5 chi nhánh). |
| 3 | **Truy cập được thông tin cần thiết** | ✅ Đã xong | Đã khảo sát quy trình thực tế: Kiosk tại quầy, két thu ngân, an ninh bảo vệ, điều động liên chi nhánh. |
| 4 | **Xây dựng Codebase tính năng cơ bản** | ✅ Đã xong | Kiến trúc Clean Architecture .NET Web API, kết nối database `R_WFM_DB`. |
| 5 | **Đảm bảo Authentication & Authorization** | ✅ Đã xong | JWT Bearer Token, phân quyền đa vai trò (`StoreManager`, `ShiftLeader`, `Employee`), xác thực mã PIN Kiosk. |
| 6 | **Đảm bảo Logging có cấu trúc** | ✅ Đã xong | Serilog ghi log ra Console và File rolling `logs/rwfm-*.txt`, Request Logging Middleware đo thời gian xử lý ms. |
| 7 | **File Project Tracking & Kế hoạch 10 tuần** | ✅ Đã xong | Hoàn thiện file `PROJECT_TRACKING.md` và `README.md` phục vụ chấm điểm và tiếp nhận yêu cầu. |

---

## 2. Kế hoạch Phân bổ Sprint & Tiến độ 10 Tuần (Sprint Backlog)

### Sprint 1 (Tuần 1 - Tuần 2): Khởi tạo Codebase & Xác thực Nhân sự
- [x] **SWP-01**: Khởi tạo Solution .NET theo chuẩn Clean Architecture (`RWFM.Domain`, `RWFM.Application`, `RWFM.Infrastructure`, `RWFM.API`).
- [x] **SWP-02**: Kết nối Entity Framework Core tới cơ sở dữ liệu `R_WFM_DB` trên SQL Server.
- [x] **SWP-03**: Tự động nạp dữ liệu mẫu (Data Seeding) cho 3 Cửa hàng (`STORE01`, `STORE02`, `STORE03`), 5 chức vụ (`Positions`), 4 khung ca (`Shifts`), cùng tài khoản nhân sự và lịch trực hôm nay.
- [x] **SWP-04**: Xây dựng module Authentication (Đăng nhập Email/Username + Mật khẩu mã hóa BCrypt, trả về JWT Token).
- [x] **SWP-05**: Xây dựng module Kiosk Authentication (Đăng nhập nhanh bằng mã PIN tại quầy thu ngân).
- [x] **SWP-06**: Cấu hình Serilog và Global Exception Handling Middleware chuẩn hóa phản hồi `ApiResponse<T>`.

### Sprint 2 (Tuần 3 - Tuần 4): Lập lịch Ca trực & Chấm công Kiosk tại quầy
- [ ] **SWP-07**: API Lập lịch tuần (`POST /api/shifts/assign`) kèm thuật toán tự động chặn gán trùng giờ / trùng ngày.
- [ ] **SWP-08**: API Công bố lịch tuần (`POST /api/shifts/publish`) gửi thông báo tới nhân viên chi nhánh.
- [ ] **SWP-09**: API Xem lịch cá nhân trên điện thoại cho nhân viên (`GET /api/shifts/my-shifts`).
- [ ] **SWP-10**: Module Chấm công Kiosk (`POST /api/attendance/kiosk-checkin`, `kiosk-checkout`) với Server Timestamp chuẩn xác, tự động nhận diện đi muộn.
- [ ] **SWP-11**: Module Xử lý ngoại lệ vắng mặt & Gian lận ca trực cho Trưởng ca (`POST /api/attendance/report-fraud`).

### Sprint 3 (Tuần 5 - Tuần 6): Điều phối Liên Chi nhánh & Đổi ca Nội bộ
- [ ] **SWP-12**: Quy trình Xin đổi ca giữa nhân viên cùng chi nhánh (`POST /api/shifts/swap-request`) và Cửa hàng trưởng duyệt (`POST /api/shifts/swap-review`).
- [ ] **SWP-13**: Quy trình Điều động tạm thời liên chi nhánh (Temporary Dispatch): Quản lý chi nhánh A đề nghị mượn nhân sự, Quản lý chi nhánh B phê duyệt.
- [ ] **SWP-14**: Tự động chuyển quyền quản lý nhân sự sang chi nhánh đích trong thời hạn điều động, tự động thu hồi khi hết hạn.

### Sprint 4 (Tuần 7 - Tuần 8): Số hóa Biên bản Giao ca Đặc thù & Tổng hợp Công
- [ ] **SWP-15**: Nghiệp vụ bàn giao két tiền thu ngân (`POST /api/handovers/cashier-submit`): nhập tiền lẻ đầu ca (Opening Float), tiền kiểm đếm cuối ca, tính chênh lệch tự động.
- [ ] **SWP-16**: Nghiệp vụ kiểm tra an ninh bảo vệ (`POST /api/handovers/security-submit`): đối soát vé xe qua đêm, xác nhận niêm phong khóa cửa kho / cửa cuốn.
- [ ] **SWP-17**: Trưởng ca ký duyệt chốt phiên giao ca (`POST /api/handovers/leader-sign`), đóng phiên làm việc.
- [ ] **SWP-18**: Bảng tổng hợp công tháng (Timesheet Aggregation) phục vụ xuất dữ liệu chốt công.

### Sprint 5 (Tuần 9 - Tuần 10): Tối ưu hóa, Kiểm thử & Chuẩn bị Báo cáo Bảo vệ
- [ ] **SWP-19**: Viết Unit Test cho các Business Rules trọng yếu (chặn gán trùng lịch, điều động hết hạn, đối soát két tiền).
- [ ] **SWP-20**: Hoàn thiện tài liệu kiến trúc, tài liệu đặc tả yêu cầu (SRS) và slide thuyết trình bảo vệ đồ án.

---

## 3. Ma trận Phân công Trách nhiệm Nghiệp vụ (RACI Matrix)

| Nghiệp vụ hệ thống | Cửa hàng trưởng (Store Manager) | Trưởng ca (Shift Leader) | Thu ngân (Cashier) | Bán hàng (Sales Staff) | Bảo vệ (Security) | Hệ thống R-WFM (Tự động) |
|---|:---:|:---:|:---:|:---:|:---:|:---:|
| Lập & Công bố lịch tuần | **R / A** | I | I | I | I | Tự động chặn trùng lịch |
| Đăng ký ca làm trống (Part-time) | A | I | R | R | R | Cập nhật số chỗ trống |
| Gửi yêu cầu xin đổi ca | A | I | R | R | R | Kiểm tra cùng chi nhánh |
| Lệnh điều động liên chi nhánh | **R / A** | I | I | I | I | Tự động chuyển quyền StoreId |
| Check-in / Check-out Kiosk | I | I | **R** | **R** | **R** | Server Timestamp, Check mã PIN |
| Báo vắng mặt / Gian lận ca | A | **R** | - | - | - | Gắn cờ Fraud, hủy công |
| Bàn giao két tiền mặt | I | A | **R** | - | - | Tính chênh lệch két tự động |
| Bàn giao an ninh & niêm phong | I | A | - | - | **R** | Lưu checklist niêm phong |
| Ký duyệt chốt phiên giao ca | I | **R / A** | I | - | I | Đóng phiên làm việc |
| Khóa bảng tổng hợp công tháng | **R / A** | I | I | I | I | Xuất Timesheet báo cáo |

*(R: Responsible, A: Accountable, C: Consulted, I: Informed)*

---

## 4. Nhật ký Tiếp nhận Yêu cầu & Cập nhật Thay đổi (Change Log)

| Ngày | Người yêu cầu | Nội dung tiếp nhận | Giải pháp & Trạng thái xử lý |
|---|---|---|---|
| 08/09/2026 | Giảng viên hướng dẫn | Yêu cầu hoàn thiện Codebase Tuần 1: Authen, Logging, Solution structure, Project Tracking file | ✅ Đã hoàn thành 100% trong Backend .NET API. |
| 08/09/2026 | Thành viên nhóm | Đã có sẵn Database SQL Server `R_WFM_DB` từ trước | ✅ Đã scaffold và điều chỉnh toàn bộ Domain Entities, DbContext và Service logic khớp 100% với schema và CHECK constraints của `R_WFM_DB`. |
| 08/09/2026 | Nhóm phát triển | Chỉ tập trung hoàn thiện phần Backend API trước | ✅ Đã tinh gọn, kiểm thử toàn bộ API Backend trên port 5050 thành công. |
