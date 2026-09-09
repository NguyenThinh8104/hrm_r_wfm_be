# R-WFM (Retail Chain Workforce Management Platform)
## Nền tảng Quản trị Nhân sự Vận hành Chuỗi Siêu thị Tiện lợi

> **Đồ án môn học SWP391 / SWP - Đại học FPT (Học kỳ Fall 2026)**  
> **Backend:** .NET 8 (LTS) / ASP.NET Core Web API (**Modular Architecture / Modular Monolith**)  
> **Database:** Microsoft SQL Server (`R_WFM_DB`)  
> **Authentication:** JWT Bearer Token + Kiosk PIN Code  
> **Logging:** Serilog (Console + File Rolling `logs/rwfm-*.txt`)

---

## 1. Cấu trúc Solution Modular Architecture (Modular Monolith)

Hệ thống được tổ chức thành các **Module nghiệp vụ độc lập (Self-contained Modules)**:

```text
E:\SWP\backend\
├── RWFM.sln                         # Solution chính Visual Studio / .NET 8
├── RWFM.Domain/                     # Core Entities chung khớp 100% với SQL Server R_WFM_DB
│   ├── Entities/ (User, Employee, Store, Position, Shift, WorkSchedule,
│   │              ShiftAssignment, AttendanceRecord, AttendanceException,
│   │              TemporaryDispatch, ShiftHandoverSession, CashHandover, SecurityHandover)
│   └── Common/ (BaseEntity.cs)
├── RWFM.Shared/                     # Thành phần dùng chung (Cross-cutting Concerns)
│   ├── Data/ (RWFMDbContext.cs, DbInitializer.cs)
│   ├── Common/ (ApiResponse.cs)
│   ├── Security/ (PasswordHasher.cs, JwtTokenService.cs)
│   ├── Middlewares/ (ExceptionHandlingMiddleware, RequestLoggingMiddleware)
│   └── SharedModuleExtensions.cs    # Đăng ký DbContext, JWT, Security
├── RWFM.Modules.Auth/               # Module 1: Xác thực JWT & Quản lý Nhân sự, Kiosk PIN
│   ├── Controllers/ (AuthController.cs)
│   ├── Interfaces/ (IAuthService.cs)
│   ├── Services/ (AuthService.cs)
│   ├── DTOs/ (AuthDTOs.cs)
│   └── AuthModuleExtensions.cs      # services.AddAuthModule()
├── RWFM.Modules.Stores/             # Module 2: Quản lý Chi nhánh Cửa hàng
│   ├── Controllers/ (StoresController.cs)
│   └── StoresModuleExtensions.cs    # services.AddStoresModule()
├── RWFM.Modules.Shifts/             # Module 3: Lập lịch Ca trực tuần, Chặn trùng lịch, Đổi ca
│   ├── Controllers/ (ShiftsController.cs)
│   ├── Interfaces/ (IShiftService.cs)
│   ├── Services/ (ShiftService.cs)
│   ├── DTOs/ (ShiftDTOs.cs)
│   └── ShiftsModuleExtensions.cs    # services.AddShiftsModule()
├── RWFM.Modules.Attendance/         # Module 4: Chấm công Kiosk tại quầy & Xử lý Gian lận
│   ├── Controllers/ (AttendanceController.cs)
│   ├── Interfaces/ (IAttendanceService.cs)
│   ├── Services/ (AttendanceService.cs)
│   ├── DTOs/ (AttendanceDTOs.cs)
│   └── AttendanceModuleExtensions.cs# services.AddAttendanceModule()
├── RWFM.Modules.Dispatch/           # Module 5: Điều phối Nhân sự Tạm thời Liên Chi nhánh
│   ├── Controllers/ (DispatchController.cs)
│   ├── Interfaces/ (IDispatchService.cs)
│   ├── Services/ (DispatchService.cs)
│   ├── DTOs/ (DispatchDTOs.cs)
│   └── DispatchModuleExtensions.cs  # services.AddDispatchModule()
├── RWFM.Modules.Handovers/         # Module 6: Bàn giao Ca (Két tiền Thu ngân & An ninh)
│   ├── Controllers/ (HandoversController.cs)
│   ├── Interfaces/ (IHandoverService.cs)
│   ├── Services/ (HandoverService.cs)
│   ├── DTOs/ (HandoverDTOs.cs)
│   └── HandoversModuleExtensions.cs # services.AddHandoversModule()
└── RWFM.API/                        # Host Application (Entry Point)
    ├── Program.cs                   # Tự động nạp toàn bộ các Module qua DI
    ├── appsettings.json             # Chuỗi kết nối R_WFM_DB
    └── Properties/launchSettings.json # Cấu hình cổng port 5050
```

---

## 2. Hướng dẫn Khởi chạy Backend API

### Yêu cầu môi trường
- .NET SDK 8 (LTS)
- Microsoft SQL Server Local với Database `R_WFM_DB`

### Lệnh chạy Backend API
Mở terminal tại thư mục `E:\SWP\backend`:
```bash
cd E:\SWP\backend
dotnet build RWFM.sln
dotnet run --project RWFM.API --urls "http://localhost:5050"
```
*(Hoặc gõ `.\run.ps1` hoặc chạy `run.bat`)*

Sau khi chạy:
- **Swagger UI:** Truy cập trực tiếp tại `http://localhost:5050/swagger`
- **File Log Serilog:** Được lưu tự động hàng ngày tại `backend/RWFM.API/bin/Debug/net8.0/logs/rwfm-YYYYMMDD.txt`

---

## 3. Danh sách Tài khoản Demo & Mã PIN Kiosk (Đã nạp sẵn)

| Tên đăng nhập (Username) | Mật khẩu Web | Mã NV (EmployeeCode) | Mã PIN Kiosk | Họ và tên | Vai trò (Role) | Chi nhánh làm việc |
|---|---|---|:---:|---|---|---|
| `manager.store01` | `Password@123` | `MGR001` | `1234` | Trần Thị Mai | **StoreManager** | Cửa hàng Nguyễn Trãi (Store 1) |
| `leader.store01` | `Password@123` | `SLD001` | `1234` | Phạm Gia Bảo | **ShiftLeader** | Cửa hàng Nguyễn Trãi (Store 1) |
| `cashier.store01` | `Password@123` | `CSH001` | `1234` | Đỗ Hoàng Ngân | **Employee** (Thu ngân) | Cửa hàng Nguyễn Trãi (Store 1) |
| `sales.store01` | `Password@123` | `SAL001` | `1234` | Võ Minh Khang | **Employee** (Bán hàng) | Cửa hàng Nguyễn Trãi (Store 1) |
| `security.store01` | `Password@123` | `SEC001` | `1234` | Đinh Hùng Dũng | **Employee** (Bảo vệ) | Cửa hàng Nguyễn Trãi (Store 1) |
| `manager.store02` | `Password@123` | `MGR002` | `1234` | Lê Hoàng Phúc | **StoreManager** | Cửa hàng Lê Văn Việt (Store 2) |

---

## 4. Danh mục API Endpoints theo Module

### 4.1. Module Auth (`RWFM.Modules.Auth`)
- `POST /api/auth/login`: Đăng nhập Web Quản trị (Username + Password $\rightarrow$ JWT Token).
- `POST /api/auth/kiosk-login`: Đăng nhập nhanh tại trạm Kiosk (EmployeeCode + PIN Code + StoreId).
- `GET /api/auth/me`: Lấy thông tin phiên đăng nhập hiện tại từ JWT Token.
- `GET /api/auth/store-employees/{storeId}`: Lấy danh sách nhân sự của một chi nhánh.

### 4.2. Module Stores & Shifts (`RWFM.Modules.Stores`, `RWFM.Modules.Shifts`)
- `GET /api/stores`: Danh sách toàn bộ chi nhánh cửa hàng.
- `GET /api/shifts`: Danh mục khung ca trực (Ca sáng, Ca chiều, Ca tối, Ca đêm).
- `GET /api/shifts/schedule`: Xem bảng xếp lịch theo khoảng ngày.
- `POST /api/shifts/assign`: Gán ca cho nhân viên (**Tự động chặn gán trùng giờ/ngày**).
- `POST /api/shifts/publish`: Cửa hàng trưởng công bố lịch tuần cho toàn bộ nhân viên.
- `POST /api/shifts/swap-request`: Nhân viên gửi đơn xin đổi ca.
- `POST /api/shifts/swap-review`: Cửa hàng trưởng duyệt / từ chối đổi ca.

### 4.3. Module Attendance (`RWFM.Modules.Attendance`)
- `GET /api/attendance/kiosk-roster`: Lấy danh sách nhân viên có lịch trực hôm nay tại quầy.
- `POST /api/attendance/kiosk-checkin`: Nhân viên nhập PIN điểm danh đầu ca (Server Timestamp, nhận diện đi muộn). Nếu là Thu ngân, tự động ghi nhận số tiền lẻ đầu ca (Opening Float).
- `POST /api/attendance/kiosk-checkout`: Nhân viên nhập PIN kết thúc ca.
- `POST /api/attendance/report-fraud`: Trưởng ca báo cáo gian lận / vắng mặt, hủy công và chuyển ngoại lệ lên Cửa hàng trưởng.
- `GET /api/attendance/history`: Xem lịch sử chấm công trong ngày của chi nhánh.

### 4.4. Module Dispatch (`RWFM.Modules.Dispatch`)
- `POST /api/dispatch/request`: Cửa hàng trưởng chi nhánh A đề nghị mượn nhân sự từ chi nhánh B.
- `POST /api/dispatch/review`: Cửa hàng trưởng chi nhánh B duyệt cho điều động.
- `GET /api/dispatch/store/{storeId}`: Danh sách các lệnh điều động liên quan đến cửa hàng.

### 4.5. Module Handovers (`RWFM.Modules.Handovers`)
- `GET /api/handovers/current`: Lấy phiên giao ca hiện tại.
- `POST /api/handovers/cashier-submit`: Thu ngân nhập số tiền kiểm đếm thực tế cuối ca $\rightarrow$ hệ thống tự động tính chênh lệch thừa/thiếu.
- `POST /api/handovers/security-submit`: Bảo vệ đối soát số vé xe qua đêm, xác nhận niêm phong khóa cửa kho & cửa cuốn.
- `POST /api/handovers/leader-sign`: Trưởng ca kiểm tra số liệu, ký duyệt chốt ca và đóng phiên làm việc.
