# TÀI LIỆU TỔNG HỢP TOÀN BỘ TRIỂN KHAI HỆ THỐNG R-WFM
## Phân hệ: Operations Admin (Quản trị Vận hành Chuỗi)
**Use Cases:**
- **UC 1.2:** Quản lý Danh mục Chi nhánh & Cấu hình Kiosk (Branch & Kiosk Network Security)
- **UC 1.3:** Chuẩn hóa Bộ Khung ca Mẫu Toàn Chuỗi (Shift Master Template Standardization)

---

## I. TỔNG QUAN KIẾN TRÚC & CÔNG NGHỆ

- **Backend:** .NET 8 (C#) theo kiến trúc **Modular Monolith** kết hợp Clean Architecture.
- **Database:** MySQL Server kết nối thông qua **Entity Framework Core (Pomelo MySQL Provider)**.
- **Frontend:** React 18 (Vite) + Vanilla CSS/HSL Design System + Lucide/Custom SVG Icons + React Router v6.
- **Cơ chế xác thực:** JWT Bearer Token (Phân quyền Role-based Authorization: `OperationsAdmin`, `BusinessOwner`, `Admin`, `StoreManager`).

```
hrm_r_wfm_be/
├── API/                        # Entry Point API Host, Middleware & Startup
├── Domain/Entities/            # Core Entities (Branch, KioskDevice, ShiftTemplate, User, WorkSchedule)
├── Shared/                     # Common ApiResponse, DbContext, Migrations, Seeders
├── modules/
│   ├── Stores/                 # UC 1.2: Branch & Kiosk Management (Controllers, Services, DTOs)
│   ├── Shifts/                 # UC 1.3: Shift Master Templates (Controllers, Services, DTOs)
│   ├── Auth/                   # JWT Login, Token generation & validation
│   ├── Attendance/             # Kiosk Clock-in/Clock-out Verification
│   ├── Dispatch/               # Store Manager Schedule & Shift Allocation
│   └── Handovers/              # Shift Handover & End-of-day reconciliation
└── docs/                       # Toàn bộ tài liệu kỹ thuật & SQL Scripts
```

---

## II. CHI TIẾT DANH SÁCH FILE CODE TẠO MỚI & CHỈNH SỬA SO VỚI GIT GỐC (GIT CHANGE LOG)

### 1. BACKEND (.NET 8 - `hrm_r_wfm_be`)

#### 🆕 A. Các File Tạo Mới Hoàn Toàn (Added Files):
| Đường dẫn File | Loại | Mục đích & Nội dung |
| :--- | :---: | :--- |
| `modules/Stores/Controllers/BranchesController.cs` | Controller | Controller RESTful chuẩn UC 1.2: `GET`, `POST`, `PUT`, `PATCH /status`, `DELETE /api/v1/branches`, `GET/POST /branches/{id}/kiosks`. |
| `modules/Stores/Controllers/KiosksController.cs` | Controller | Controller quản lý thiết bị trạm Kiosk: `GET`, `PUT`, `PATCH /status`, `DELETE /api/v1/kiosks`. |
| `modules/Stores/Interfaces/IBranchService.cs` | Interface | Interface nghiệp vụ CRUD Branch, Kiosk cascade, sinh token và xóa dữ liệu. |
| `modules/Stores/Services/BranchService.cs` | Service | Xử lý logic chống trùng mã, tự động chuyển Kiosk sang `BLOCKED` khi khóa chi nhánh, xóa Kiosk & unbind user khi xóa chi nhánh. |
| `modules/Shifts/Controllers/ShiftTemplatesController.cs` | Controller | Controller RESTful chuẩn UC 1.3: `GET`, `POST`, `PUT`, `PATCH /status`, `DELETE`, `POST /standardize /api/v1/shift-templates`. |
| `docs/OPERATIONS_ADMIN_IMPLEMENTATION.md` | Tài liệu | Tài liệu chi tiết toàn bộ kiến trúc, API, schema và log thay đổi. |
| `docs/rwfm_db.sql` | SQL Script | DDL Schema chuẩn hóa MySQL + Dữ liệu mẫu khởi tạo toàn hệ thống. |
| `docs/rwfm_db_schema.sql` | SQL Script | Bản đặc tả chi tiết toàn bộ cấu trúc bảng và khóa ngoại. |

#### 🛠️ B. Các File Chỉnh Sửa So Với Git (Modified Files):
| Đường dẫn File | Nội dung thay đổi chi tiết |
| :--- | :--- |
| `Domain/Entities/Branch.cs` | Bổ sung trường `Code` (Unique), `Name`, `Address`, `Phone`, `Status` (`ACTIVE`/`INACTIVE`), alias `BranchCode` để tương thích frontend. |
| `Domain/Entities/KioskDevice.cs` | Bổ sung `DeviceName`, `KioskCode`, `IpWhitelist`, `KioskToken` (Unique), `UserAgentPattern`, `Status` (`ACTIVE`/`BLOCKED`/`INACTIVE`), `LastPingAt`. |
| `Domain/Entities/ShiftTemplate.cs` | Bổ sung `Code` (Unique), `Name`, `Description`, `StartTime`, `EndTime`, `BreakMinutes`, `IsOvernight`, `Status` (`ACTIVE`/`INACTIVE`). |
| `Shared/Data/AppDbContext.cs` | Thiết lập Unique Index trên `Branch.Code`, `KioskDevice.KioskToken`, `ShiftTemplate.Code` và cấu hình quan hệ bảng. |
| `Shared/Data/DbInitializer.cs` | Bổ sung dữ liệu seed chuẩn cho tài khoản Operations Admin (`ops.admin@rwfm.vn`), 2 chi nhánh mẫu (`CH01`, `CH02`), trạm Kiosk và 3 khung ca mẫu. |
| `Shared/Security/JwtTokenService.cs` | Thêm claims vai trò chuẩn xác (`Role`, `RoleId`, `UserId`, `Email`) phục vụ phân quyền Controller. |
| `modules/Stores/DTOs/StoreDTOs.cs` | Xây dựng toàn bộ DTOs cho Branch & Kiosk: `BranchDto`, `CreateBranchDto`, `UpdateBranchDto`, `UpdateBranchStatusDto`, `KioskDto`, `CreateKioskDto`, `UpdateKioskDto`, `UpdateKioskStatusDto`. |
| `modules/Stores/Controllers/StoresController.cs` | Cập nhật các endpoint `/api/Stores` gọi qua `IBranchService` và bổ sung `DELETE /api/Stores/{id}` để tương thích backward. |
| `modules/Stores/Controllers/KioskController.cs` | Cập nhật route legacy `/api/Kiosk` gọi qua `IBranchService`. |
| `modules/Stores/StoresModuleExtensions.cs` | Đăng ký DI Container cho `IBranchService` $\rightarrow$ `BranchService`. |
| `modules/Shifts/DTOs/ShiftDTOs.cs` | Bổ sung `ShiftTemplateDto`, `CreateShiftTemplateDto`, `UpdateShiftTemplateDto`, `UpdateShiftTemplateStatusDto`, tính toán `WorkHours`. |
| `modules/Shifts/Interfaces/IShiftService.cs` | Thêm interface chuẩn hóa và xóa mềm khung ca mẫu. |
| `modules/Shifts/Services/ShiftService.cs` | Xử lý logic `StartTime != EndTime`, tự động nhận diện `IsOvernight`, kiểm tra `BreakMinutes < Duration`, chuẩn hóa bộ 3 ca mẫu. |
| `modules/Shifts/Controllers/ShiftsController.cs` | Cập nhật `/api/Shifts/templates` hỗ trợ filter `includeInactive`. |

---

### 2. FRONTEND (REACT - `hrm_r_wfm_fe`)

#### 🆕 A. Các File Tạo Mới Hoàn Toàn (Added Files):
| Đường dẫn File | Loại | Mục đích & Nội dung |
| :--- | :---: | :--- |
| `src/modules/branch/components/BranchDeleteModal.jsx` | Component | Modal xác nhận xóa chi nhánh nguy hiểm (màu đỏ) với cảnh báo mất dữ liệu Kiosk. |
| `src/modules/branch/components/BranchFormModal.jsx` | Component | Modal thêm/sửa chi nhánh với validate, báo lỗi đỏ, và nút **"+ Điền mẫu nhanh"**. |
| `src/modules/branch/components/BranchLockModal.jsx` | Component | Modal khóa/mở chi nhánh kèm nhập lý do lưu Audit Log. |
| `src/modules/branch/components/BranchTable.jsx` | Component | Bảng hiển thị danh mục chi nhánh, số lượng Kiosk Online và 4 nút thao tác (`Kiosk`, `Sửa`, `Khóa/Mở`, `Xóa`). |
| `src/modules/branch/components/KioskManagerModal.jsx` | Component | Modal quản lý, cấu hình IP Whitelist, User Agent và khóa/mở trạm Kiosk tại chi nhánh. |
| `src/modules/branch/components/KioskGlobalMonitor.jsx` | Component | Giám sát trạng thái toàn bộ máy trạm Kiosk toàn chuỗi theo thời gian thực. |
| `src/modules/branch/hooks/useBranch.js` | Hook | Hook xử lý state, fetch dữ liệu từ Backend, tính toán thống kê và quản lý toàn bộ modals. |
| `src/modules/branch/pages/BranchManagementPage/index.jsx` | Page | Trang Quản lý Danh mục Chi nhánh & Cấu hình Kiosk (`/branches`). |
| `src/modules/branch/services/branch.service.js` | Service | Service gọi API `/api/Stores` & `/api/v1/branches` kèm auto-login token. |
| `src/modules/branch/types/branch.types.ts` | Types | Khai báo Type definitions TypeScript cho Branch & Kiosk. |
| `src/modules/schedule/components/Shift24hTimeline.jsx` | Component | Biểu đồ trực quan 24 giờ của các khung ca làm việc trong ngày. |
| `src/modules/schedule/components/ShiftDeleteModal.jsx` | Component | Modal xác nhận vô hiệu hóa / xóa mềm khung ca mẫu. |
| `src/modules/schedule/components/ShiftEnforcementBanner.jsx` | Component | Banner thông báo chính sách bắt buộc chuẩn hóa khung ca toàn chuỗi. |
| `src/modules/schedule/components/ShiftTemplateFormModal.jsx` | Component | Modal tạo/sửa khung ca mẫu, tự tính `WorkHours` và tự nhận diện ca qua đêm. |
| `src/modules/schedule/components/ShiftTemplateTable.jsx` | Component | Bảng hiển thị danh mục khung ca mẫu và các thao tác quản trị. |
| `src/modules/schedule/hooks/useShiftTemplate.js` | Hook | Hook quản lý state khung ca mẫu và API handlers. |
| `src/modules/schedule/pages/ShiftMasterPage/index.jsx` | Page | Trang Chuẩn hóa Bộ Khung ca Mẫu (`/shifts/templates`). |
| `src/modules/schedule/services/shiftTemplate.service.js` | Service | Service kết nối Backend cho Shift Templates. |
| `src/modules/schedule/types/shift.types.ts` | Types | Type definitions TypeScript cho Shift Templates. |
| `src/modules/dashboard/pages/DashboardPage/index.jsx` | Page | Trang Tổng quan Dashboard Điều khiển Quản trị Vận hành (`/dashboard`). |

#### 🛠️ B. Các File Chỉnh Sửa So Với Git (Modified Files):
| Đường dẫn File | Nội dung thay đổi chi tiết |
| :--- | :--- |
| `src/routers/AppRouter.jsx` | Định tuyến các route: `/branches`, `/shifts/templates`, `/dashboard`, `/login`. |
| `src/shared/components/layout/DashboardSidebar.jsx` | Sửa lỗi không click chuyển trang được; thêm hàm `handleItemClick` hỗ trợ đa năng `onClick`, `path`, `onNavigate` và highlight menu đang mở. |
| `src/shared/components/ui/Icon.jsx` | Bổ sung đầy đủ biểu tượng SVG sắc nét cho `dashboard`, `pin`, `clock`, `calendar`, `building`, `lock`, `trash`, `edit`, `plus`. |
| `src/shared/components/ui/Button.jsx` | Sửa lỗi gán cứng `type="button"`, cho phép nhận `type="submit"` và hỗ trợ truyền sự kiện `onClick` an toàn không bị chặn lỗi `e.preventDefault()`. |
| `src/shared/components/ui/FormField.jsx` | Bổ sung dấu `*` đỏ cho `required`, dòng thông báo lỗi `error` chi tiết; sửa lỗi template literal CSS. |
| `src/shared/constants/api.constants.js` | Bổ sung endpoint paths cho Branch, Kiosk, Shift Templates. |
| `package.json` | Cập nhật cấu hình dependencies phục vụ chạy ứng dụng. |

---

## III. CƠ SỞ DỮ LIỆU & RÀNG BUỘC TOÀN VẸN (DATABASE SCHEMA)

### 1. Bảng `branches` (Danh mục Chi nhánh)
| Cột | Kiểu dữ liệu | Ràng buộc | Mô tả |
| :--- | :--- | :--- | :--- |
| `id` | BIGINT UNSIGNED | PK, Auto Increment | Khóa chính |
| `code` | VARCHAR(50) | NOT NULL, **UNIQUE** | Mã định danh duy nhất (VD: `CH01`, `CH02`, `HCM01`) |
| `name` | VARCHAR(255) | NOT NULL | Tên đầy đủ chi nhánh cửa hàng |
| `address` | VARCHAR(500) | NOT NULL | Địa chỉ hoạt động |
| `phone` | VARCHAR(50) | NULL | Số điện thoại liên hệ |
| `status` | VARCHAR(50) | NOT NULL, DEFAULT 'ACTIVE' | Trạng thái (`ACTIVE` / `INACTIVE`) |
| `created_at` | DATETIME(6) | NOT NULL | Thời điểm tạo |
| `updated_at` | DATETIME(6) | NOT NULL | Thời điểm cập nhật cuối |

### 2. Bảng `kiosks` (Máy trạm Kiosk quầy)
| Cột | Kiểu dữ liệu | Ràng buộc | Mô tả |
| :--- | :--- | :--- | :--- |
| `id` | BIGINT UNSIGNED | PK, Auto Increment | Khóa chính |
| `branch_id` | BIGINT UNSIGNED | FK -> `branches.id` | Chi nhánh trực thuộc |
| `device_name` | VARCHAR(255) | NOT NULL | Tên gọi thiết bị (VD: `Máy Kiosk Cầu Giấy 01`) |
| `kiosk_code` | VARCHAR(100) | NOT NULL | Mã trạm điểm danh (VD: `CH01-POS01`) |
| `ip_whitelist` | VARCHAR(500) | NULL | Dải IP / Subnet cho phép kết nối (VD: `192.168.1.0/24`) |
| `kiosk_token` | VARCHAR(255) | NOT NULL, **UNIQUE** | Token bí mật dùng định danh thiết bị (`ksk_tok_...`) |
| `user_agent_pattern` | VARCHAR(500) | NULL | Trình duyệt Kiosk Lockdown quy định |
| `status` | VARCHAR(50) | NOT NULL, DEFAULT 'ACTIVE' | Trạng thái (`ACTIVE`, `BLOCKED`, `INACTIVE`) |
| `last_ping_at` | DATETIME(6) | NULL | Thời điểm gần nhất máy trạm gửi tín hiệu Heartbeat |
| `created_at` | DATETIME(6) | NOT NULL | Thời điểm đăng ký máy trạm |
| `updated_at` | DATETIME(6) | NOT NULL | Thời điểm cấu hình cuối |

### 3. Bảng `shift_templates` (Bộ Khung ca Mẫu chuẩn hóa)
| Cột | Kiểu dữ liệu | Ràng buộc | Mô tả |
| :--- | :--- | :--- | :--- |
| `id` | BIGINT UNSIGNED | PK, Auto Increment | Khóa chính |
| `code` | VARCHAR(50) | NOT NULL, **UNIQUE** | Mã khung ca (VD: `CA_SANG`, `CA_CHIEU`, `CA_DEM`) |
| `name` | VARCHAR(255) | NOT NULL | Tên hiển thị khung ca |
| `description` | TEXT | NULL | Mô tả tính chất công việc |
| `start_time` | TIME(6) | NOT NULL | Giờ bắt đầu ca (VD: `06:00:00`) |
| `end_time` | TIME(6) | NOT NULL | Giờ kết thúc ca (VD: `14:00:00`) |
| `break_minutes` | INT | NOT NULL, DEFAULT 0 | Thời gian nghỉ giữa ca (phút) |
| `is_overnight` | TINYINT(1) | NOT NULL, DEFAULT 0 | Đánh dấu ca làm việc qua đêm (`1` nếu `StartTime > EndTime`) |
| `status` | VARCHAR(50) | NOT NULL, DEFAULT 'ACTIVE' | Trạng thái (`ACTIVE` / `INACTIVE`) |
| `created_at` | DATETIME(6) | NOT NULL | Ngày tạo |
| `updated_at` | DATETIME(6) | NOT NULL | Ngày cập nhật |

---

## IV. DANH SÁCH CHI TIẾT API ENDPOINTS (API SPECIFICATIONS)

### 1. Phân hệ Quản lý Chi nhánh & Kiosk (UC 1.2)
| Method | Endpoint | Quyền hạn (Roles) | Mô tả & Nghiệp vụ |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/v1/branches` | Mọi Admin/Manager | Lấy danh sách chi nhánh (hỗ trợ query `status`, `search`). |
| `GET` | `/api/v1/branches/{id}` | Mọi Admin/Manager | Xem chi tiết chi nhánh kèm danh sách máy Kiosk. |
| `POST` | `/api/v1/branches` | `OperationsAdmin`, `Admin` | Thêm mới chi nhánh. Bắt buộc kiểm tra trùng mã `code`. |
| `PUT` | `/api/v1/branches/{id}` | `OperationsAdmin`, `Admin` | Cập nhật tên, địa chỉ, số điện thoại chi nhánh. |
| `PATCH` | `/api/v1/branches/{id}/status` | `OperationsAdmin`, `Admin` | Khóa / Mở khóa chi nhánh. **Nghiệp vụ bắt buộc:** Khi chuyển `status = INACTIVE`, toàn bộ Kiosk thuộc chi nhánh tự động chuyển thành `BLOCKED`. |
| `DELETE` | `/api/v1/branches/{id}` | `OperationsAdmin`, `Admin` | Xóa chi nhánh: Tự động xóa sạch Kiosk trực thuộc và unbind `HomeBranchId` của nhân viên. |
| `GET` | `/api/v1/branches/{id}/kiosks` | `OperationsAdmin`, `Manager` | Lấy danh sách Kiosk thuộc một chi nhánh. |
| `POST` | `/api/v1/branches/{id}/kiosks` | `OperationsAdmin`, `Manager` | Thêm mới Kiosk. Tự động sinh `kiosk_code` (`CHxx-POSyy`) và `kiosk_token` ngẫu nhiên. |
| `GET` | `/api/v1/kiosks` | `OperationsAdmin`, `Admin` | Lấy danh sách toàn bộ Kiosk chuỗi (dùng cho Giám sát Kiosk). |
| `GET` | `/api/v1/kiosks/{id}` | `OperationsAdmin`, `Manager` | Lấy thông tin chi tiết một trạm Kiosk. |
| `PUT` | `/api/v1/kiosks/{id}` | `OperationsAdmin`, `Manager` | Cập nhật cấu hình trạm Kiosk (`DeviceName`, `IpWhitelist`, `UserAgentPattern`). |
| `PATCH` | `/api/v1/kiosks/{id}/status` | `OperationsAdmin`, `Manager` | Khóa / Mở khóa Kiosk. Không cho mở Kiosk nếu chi nhánh đang `INACTIVE`. |
| `DELETE` | `/api/v1/kiosks/{id}` | `OperationsAdmin`, `Manager` | Xóa máy trạm Kiosk khỏi chi nhánh. |

### 2. Phân hệ Chuẩn hóa Khung ca Mẫu (UC 1.3)
| Method | Endpoint | Quyền hạn (Roles) | Mô tả & Nghiệp vụ |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/v1/shift-templates` | Mọi vai trò | Lấy danh sách khung ca mẫu toàn hệ thống (mặc định chỉ lấy ca `ACTIVE`, hỗ trợ `includeInactive=true`). |
| `GET` | `/api/v1/shift-templates/{id}` | Mọi vai trò | Lấy thông tin chi tiết khung ca. |
| `POST` | `/api/v1/shift-templates` | `OperationsAdmin`, `Admin` | Tạo khung ca mẫu mới. Kiểm tra trùng mã, kiểm tra `StartTime != EndTime`, tự động nhận diện ca qua đêm, kiểm tra thời gian nghỉ, tính giờ công chuẩn `WorkHours`. |
| `PUT` | `/api/v1/shift-templates/{id}` | `OperationsAdmin`, `Admin` | Chỉnh sửa thông tin khung ca mẫu. |
| `PATCH` | `/api/v1/shift-templates/{id}/status` | `OperationsAdmin`, `Admin` | Bật / Tắt trạng thái khung ca (`ACTIVE` / `INACTIVE`). |
| `DELETE` | `/api/v1/shift-templates/{id}` | `OperationsAdmin`, `Admin` | **Xóa mềm (Soft-deactivate):** Chuyển trạng thái sang `INACTIVE` thay vì xóa vật lý trong CSDL để đảm bảo an toàn dữ liệu phân ca đã tạo trong quá khứ. |
| `POST` | `/api/v1/shift-templates/standardize` | `OperationsAdmin`, `Admin` | Tự động đồng bộ chuẩn hóa 3 khung ca mẫu mặc định: `CA_SANG` (06:00-14:00), `CA_CHIEU` (14:00-22:00), `CA_DEM` (22:00-06:00 qua đêm). |

---

## V. KẾT QUẢ KIỂM THỬ TỰ ĐỘNG (AUTOMATED TEST VERIFICATION)

| # | Kịch bản kiểm thử | Endpoint & Thao tác | Kết quả |
| :---: | :--- | :--- | :---: |
| 1 | Xác thực đăng nhập Operations Admin | `POST /api/auth/login` | **PASS (200 OK, JWT)** |
| 2 | Lấy danh sách toàn bộ chi nhánh | `GET /api/v1/branches` | **PASS (200 OK)** |
| 3 | Tạo chi nhánh mới thành công | `POST /api/v1/branches` | **PASS (201 Created)** |
| 4 | Chặn tạo chi nhánh trùng mã `code` | `POST /api/v1/branches` | **PASS (400 Bad Request)** |
| 5 | Thêm trạm Kiosk mới cho chi nhánh | `POST /api/v1/branches/{id}/kiosks` | **PASS (201 Created, Auto Token)** |
| 6 | Khóa chi nhánh $\rightarrow$ Tự động chuyển toàn bộ Kiosk sang `BLOCKED` | `PATCH /api/v1/branches/{id}/status` | **PASS (200 OK, Kiosk Auto Blocked)** |
| 7 | Cập nhật cấu hình trạm Kiosk | `PUT /api/v1/kiosks/{id}` | **PASS (200 OK)** |
| 8 | Xóa chi nhánh và các Kiosk trực thuộc | `DELETE /api/v1/branches/{id}` | **PASS (200 OK)** |
| 9 | Lấy danh sách khung ca mẫu | `GET /api/v1/shift-templates` | **PASS (200 OK)** |
| 10 | Tạo khung ca mẫu ban ngày | `POST /api/v1/shift-templates` | **PASS (201 Created, WorkHours: 7.5h)** |
| 11 | Tạo khung ca mẫu qua đêm (tự động nhận diện `isOvernight`) | `POST /api/v1/shift-templates` | **PASS (201 Created, isOvernight: true)** |
| 12 | Chặn tạo ca có giờ bắt đầu trùng giờ kết thúc | `POST /api/v1/shift-templates` | **PASS (400 Bad Request)** |
| 13 | Chặn tạo ca có thời gian nghỉ $\ge$ thời lượng ca | `POST /api/v1/shift-templates` | **PASS (400 Bad Request)** |
| 14 | Khóa / Vô hiệu hóa khung ca mẫu | `PATCH /api/v1/shift-templates/{id}/status` | **PASS (200 OK, Status: INACTIVE)** |
| 15 | Đồng bộ chuẩn hóa bộ khung ca mẫu toàn chuỗi | `POST /api/v1/shift-templates/standardize` | **PASS (200 OK, 3 Master Shifts)** |

---

## VI. HƯỚNG DẪN KHỞI CHẠY VÀ VẬN HÀNH HỆ THỐNG

### 1. Khởi chạy Backend (.NET 8):
```powershell
# Tại thư mục gốc hrm_r_wfm_be:
dotnet run --project API/API.csproj
```
- API Server sẽ lắng nghe tại: `http://localhost:5050`
- Swagger UI tài liệu API tương tác: `http://localhost:5050/swagger`

### 2. Khởi chạy Frontend (React Vite):
```powershell
# Tại thư mục hrm_r_wfm_fe:
npm run dev
```
- Ứng dụng web mở tại: `http://localhost:5174`
- Các trang chính dành cho Operations Admin:
  - **Quản lý Chi nhánh & Kiosk:** `http://localhost:5174/branches`
  - **Chuẩn hóa Khung ca Mẫu:** `http://localhost:5174/shifts/templates`
  - **Bảng điều khiển Tổng quan:** `http://localhost:5174/dashboard`
