# R-WFM (Retail Chain Workforce Management Platform)
## Nền tảng Quản trị Nhân sự Vận hành Chuỗi Siêu thị Tiện lợi

> **Đồ án môn học SWP391 / SWP - Đại học FPT (Học kỳ Fall 2026)**  
> **Backend:** .NET 8 (LTS) / ASP.NET Core Web API (Clean Architecture)  
> **Database:** Microsoft SQL Server (`R_WFM_DB`)  
> **Authentication:** JWT Bearer Token + Kiosk PIN Code  
> **Logging:** Serilog (Console + File Rolling `logs/rwfm-*.txt`)

---

## 1. Cấu trúc Solution Clean Architecture

```text
rwfm-platform/
├── backend/
│   ├── RWFM.slnx                     # Solution file
│   ├── RWFM.Domain/                  # Entities khớp 100% với schema SQL Server
│   │   ├── Entities/ (User, Employee, Store, Position, Shift, WorkSchedule,
│   │   │              ShiftAssignment, AttendanceRecord, AttendanceException,
│   │   │              TemporaryDispatch, ShiftHandoverSession, CashHandover, SecurityHandover)
│   │   └── Common/ (BaseEntity.cs)
│   ├── RWFM.Application/             # DTOs, Service Interfaces, ApiResponse Wrapper
│   │   ├── DTOs/ (Auth, Shift, Attendance, Dispatch, Handover)
│   │   ├── Interfaces/ (IAuthService, IShiftService, IAttendanceService, IDispatchService, IHandoverService)
│   │   └── Common/ (ApiResponse.cs)
│   ├── RWFM.Infrastructure/          # EF Core DbContext, Services implementation, BCrypt, JWT
│   │   ├── Data/ (RWFMDbContext.cs, DbInitializer.cs)
│   │   └── Services/ (AuthService, ShiftService, AttendanceService, DispatchService, HandoverService, JwtTokenService, PasswordHasher)
│   └── RWFM.API/                     # Controllers, Middlewares, Program.cs, Serilog
│       ├── Controllers/ (AuthController, StoresController, ShiftsController, AttendanceController, DispatchController, HandoversController)
│       ├── Middlewares/ (ExceptionHandlingMiddleware, RequestLoggingMiddleware)
│       ├── appsettings.json
│       └── Program.cs
└── docs/
    ├── PROJECT_TRACKING.md           # Bảng theo dõi tiến độ 10 tuần chuẩn SWP FPT
    └── README.md                     # Hướng dẫn chạy và danh sách tài khoản demo
```

---

## 2. Hướng dẫn Khởi chạy Backend API

### Yêu cầu môi trường
- .NET SDK 10 hoặc .NET 8/9
- Microsoft SQL Server Local / Express với Database `R_WFM_DB`

### Cấu hình kết nối Database
Tệp `backend/RWFM.API/appsettings.json` đã được cấu hình sẵn:
```json
{
  "ConnectionStrings": {
    "SqlServerConnection": "Server=localhost;Database=R_WFM_DB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;"
  },
  "DatabaseProvider": "SqlServer"
}
```

### Lệnh chạy Backend API
Mở terminal tại thư mục `backend`:
```bash
cd backend
dotnet build RWFM.slnx
dotnet run --project RWFM.API/RWFM.API.csproj --urls "http://localhost:5050"
```

Sau khi chạy:
- **Swagger UI:** Truy cập trực tiếp tại `http://localhost:5050/swagger`
- **File Log Serilog:** Được lưu tự động hàng ngày tại `backend/RWFM.API/bin/Debug/net10.0/logs/rwfm-YYYYMMDD.txt`

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

## 4. Danh mục API Endpoints cốt lõi

### 4.1. Xác thực (Authentication)
- `POST /api/auth/login`: Đăng nhập Web Quản trị (Username + Password $\rightarrow$ JWT Token).
- `POST /api/auth/kiosk-login`: Đăng nhập nhanh tại trạm Kiosk (EmployeeCode + PIN Code + StoreId).
- `GET /api/auth/me`: Lấy thông tin phiên đăng nhập hiện tại từ JWT Token.
- `GET /api/auth/store-employees/{storeId}`: Lấy danh sách nhân sự của một chi nhánh.

### 4.2. Cửa hàng & Ca trực (Stores & Shifts)
- `GET /api/stores`: Danh sách toàn bộ chi nhánh cửa hàng.
- `GET /api/shifts`: Danh mục khung ca trực (Ca sáng, Ca chiều, Ca tối, Ca đêm).
- `GET /api/shifts/schedule`: Xem bảng xếp lịch theo khoảng ngày.
- `POST /api/shifts/assign`: Gán ca cho nhân viên (**Tự động chặn gán trùng giờ/ngày**).
- `POST /api/shifts/publish`: Cửa hàng trưởng công bố lịch tuần cho toàn bộ nhân viên.
- `POST /api/shifts/swap-request`: Nhân viên gửi đơn xin đổi ca.
- `POST /api/shifts/swap-review`: Cửa hàng trưởng duyệt / từ chối đổi ca.

### 4.3. Chấm công Kiosk tại quầy (Attendance)
- `GET /api/attendance/kiosk-roster`: Lấy danh sách nhân viên có lịch trực hôm nay tại quầy.
- `POST /api/attendance/kiosk-checkin`: Nhân viên nhập PIN điểm danh đầu ca (Server Timestamp, nhận diện đi muộn). Nếu là Thu ngân, tự động ghi nhận số tiền lẻ đầu ca (Opening Float).
- `POST /api/attendance/kiosk-checkout`: Nhân viên nhập PIN kết thúc ca.
- `POST /api/attendance/report-fraud`: Trưởng ca báo cáo gian lận / vắng mặt, hủy công và chuyển ngoại lệ lên Cửa hàng trưởng.
- `GET /api/attendance/history`: Xem lịch sử chấm công trong ngày của chi nhánh.

### 4.4. Điều động liên chi nhánh (Temporary Dispatch)
- `POST /api/dispatch/request`: Cửa hàng trưởng chi nhánh A đề nghị mượn nhân sự từ chi nhánh B.
- `POST /api/dispatch/review`: Cửa hàng trưởng chi nhánh B duyệt cho điều động.
- `GET /api/dispatch/store/{storeId}`: Danh sách các lệnh điều động liên quan đến cửa hàng.

### 4.5. Bàn giao ca đặc thù (Shift Handovers)
- `GET /api/handovers/current`: Lấy phiên giao ca hiện tại.
- `POST /api/handovers/cashier-submit`: Thu ngân nhập số tiền kiểm đếm thực tế cuối ca $\rightarrow$ hệ thống tự động tính chênh lệch thừa/thiếu.
- `POST /api/handovers/security-submit`: Bảo vệ đối soát số vé xe qua đêm, xác nhận niêm phong khóa cửa kho & cửa cuốn.
- `POST /api/handovers/leader-sign`: Trưởng ca kiểm tra số liệu, ký duyệt chốt ca và đóng phiên làm việc.
