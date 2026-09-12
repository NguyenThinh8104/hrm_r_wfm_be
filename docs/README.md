# Retail Chain Workforce Management Platform
## Nền tảng Quản trị Nhân sự Vận hành Chuỗi Siêu thị Tiện lợi

> **Đồ án môn học SWP391 / SWP - Đại học FPT (Học kỳ Fall 2026)**  
> **Backend:** .NET 8 (LTS) / ASP.NET Core Web API (**Modular Architecture / Modular Monolith**)  
> **Database:** MySQL / SQLite (`rwfm_db`)  
> **Authentication:** JWT Bearer Token + Kiosk PIN Code  
> **Logging:** Serilog (Console + File Rolling `logs/app-*.txt`)

---

## 1. Cấu trúc Solution Modular Architecture (Modular Monolith)

Hệ thống được tổ chức thành các **Module nghiệp vụ độc lập (Self-contained Modules)** với cấu trúc 1 cấp thư mục gọn gàng:

```text
backend/
├── backend.sln                      # Solution chính Visual Studio / .NET 8
├── Domain/                          # Core Entities & Enums (kiosks, kiosk_activation_codes, users, etc.)
│   ├── Entities/ (User, Employee, Store, Position, Shift, WorkSchedule,
│   │              ShiftAssignment, AttendanceRecord, AttendanceException,
│   │              KioskDevice, KioskActivationCode, TemporaryDispatch, 
│   │              ShiftHandoverSession, CashHandover, SecurityHandover)
│   ├── Enums/ (UserRole: BusinessOwner, OperationsAdmin, StoreManager, ShiftLeader, Cashier, SalesStaff, SecurityGuard)
│   └── Common/ (BaseEntity.cs)
├── Shared/                          # Thành phần dùng chung (Cross-cutting Concerns)
│   ├── Data/ (AppDbContext.cs, DbInitializer.cs)
│   ├── Common/ (ApiResponse.cs, Constants/KioskMessages.cs, Constants/AttendanceMessages.cs)
│   ├── Security/ (PasswordHasher.cs, JwtTokenService.cs)
│   ├── Middlewares/ (ExceptionHandlingMiddleware, RequestLoggingMiddleware)
│   └── SharedModuleExtensions.cs    # Đăng ký DbContext, JWT, Security
├── modules/                         # Tất cả 6 Module nghiệp vụ đặt gọn trong thư mục modules/
│   ├── Auth/                        # Module 1: Xác thực JWT & Quản lý Nhân sự, Kiosk PIN
│   ├── Stores/                      # Module 2: Quản lý Chi nhánh Cửa hàng & Kiosk Devices Activation
│   ├── Shifts/                      # Module 3: Lập lịch Ca trực tuần, Chặn trùng lịch, Đổi ca
│   ├── Attendance/                  # Module 4: Chấm công Kiosk tại quầy & Xử lý 2 bước PIN Check-in
│   ├── Dispatch/                    # Module 5: Điều phối Nhân sự Tạm thời Liên Chi nhánh
│   └── Handovers/                   # Module 6: Bàn giao Ca (Két tiền Thu ngân & An ninh)
└── API/                             # Host Application (Entry Point)
    ├── Program.cs                   # Tự động nạp toàn bộ các Module qua DI
    ├── appsettings.json             # Chuỗi kết nối Database & Jwt Config
    └── Properties/launchSettings.json # Cấu hình cổng port 5050
```

---

## 2. Hướng dẫn Khởi chạy Backend API

### Yêu cầu môi trường
- .NET SDK 8 (LTS)
- MySQL / SQLite Database

### Lệnh chạy Backend API
Mở terminal tại thư mục `backend`:
```bash
cd backend
dotnet build backend.sln
dotnet run --project API/API.csproj --urls "http://localhost:5050"
```
*(Hoặc gõ `.\run.ps1` hoặc chạy `run.bat`)*

Sau khi chạy:
- **Swagger UI:** Truy cập trực tiếp tại `http://localhost:5050/swagger`
- **API Endpoint Base:** `http://localhost:5050/api`

---

## 3. Danh sách Tài khoản Demo (7 Vai trò Chính thức)

| Tên đăng nhập (Username) | Mật khẩu Web | Mã NV (EmployeeCode) | Mã PIN Kiosk | Họ và tên | Vai trò (Role) | Chi nhánh làm việc |
|---|---|---|:---:|---|---|---|
| `owner` | `Password@123` | `OWN001` | `1234` | Nguyễn Văn Chủ | **BusinessOwner** | Toàn hệ thống |
| `ops.admin` | `Password@123` | `OPS001` | `1234` | Trần Văn Vận Hành | **OperationsAdmin** | HQ / Toàn hệ thống |
| `manager.store01` | `Password@123` | `MGR001` | `1234` | Trần Thị Mai | **StoreManager** | Cửa hàng Nguyễn Trãi (Store 1) |
| `leader.store01` | `Password@123` | `SLD001` | `1234` | Phạm Gia Bảo | **ShiftLeader** | Cửa hàng Nguyễn Trãi (Store 1) |
| `cashier.store01` | `Password@123` | `CSH001` | `1234` | Đỗ Hoàng Ngân | **Cashier** | Cửa hàng Nguyễn Trãi (Store 1) |
| `sales.store01` | `Password@123` | `SAL001` | `1234` | Võ Minh Khang | **SalesStaff** | Cửa hàng Nguyễn Trãi (Store 1) |
| `security.store01` | `Password@123` | `SEC001` | `1234` | Đinh Hùng Dũng | **SecurityGuard** | Cửa hàng Nguyễn Trãi (Store 1) |
