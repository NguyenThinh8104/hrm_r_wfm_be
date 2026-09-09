# 📦 Modules — Phân hệ nghiệp vụ (Modular Architecture)

## Mục đích
Thư mục `Modules/` là trung tâm tổ chức code theo **phân hệ nghiệp vụ**.  
Mỗi module là một **đơn vị độc lập**, chứa đầy đủ Controllers, Services, Repositories, DTOs, Entities riêng.

## Cấu trúc tổng quan

```
Modules/
├── README.md               ← Bạn đang đọc file này
├── Auth/                   ← Đăng nhập, đăng ký, JWT
│   ├── Controllers/
│   ├── Services/           ← IAuthService + AuthService
│   ├── Repositories/       ← IAuthRepository + AuthRepository
│   ├── DTOs/               ← LoginRequestDto, LoginResponseDto,...
│   └── Entities/           ← User.cs
├── Employee/               ← Quản lý nhân sự
├── Department/             ← Quản lý phòng ban
├── Attendance/             ← Chấm công
├── Schedule/               ← Quản lý ca làm việc
├── Leave/                  ← Quản lý nghỉ phép
└── Dashboard/              ← Tổng quan / thống kê
```

## Quy tắc quan trọng

### 1. Mỗi module là một đơn vị độc lập
```
Modules/Employee/
├── Controllers/
│   └── EmployeeController.cs
├── Services/
│   ├── IEmployeeService.cs          ← Interface
│   └── EmployeeService.cs           ← Implementation
├── Repositories/
│   ├── IEmployeeRepository.cs       ← Interface
│   └── EmployeeRepository.cs        ← Implementation
├── DTOs/
│   ├── CreateEmployeeRequestDto.cs
│   ├── UpdateEmployeeRequestDto.cs
│   └── EmployeeResponseDto.cs
└── Entities/
    └── Employee.cs
```

### 2. KHÔNG tham chiếu chéo giữa các module
```csharp
// ✅ ĐÚNG — Import từ Shared
using hrm_r_wfm_be.Shared.Constants;

// ✅ ĐÚNG — Import trong cùng module
using hrm_r_wfm_be.Modules.Employee.DTOs;

// ❌ SAI — Import từ module khác
using hrm_r_wfm_be.Modules.Attendance.Services;
```

> Nếu 2 module cần dùng chung → chuyển vào `Shared/`.

### 3. Khi nào tạo module mới?
- Khi có **1 phân hệ nghiệp vụ mới** cần quản lý riêng (VD: thêm quản lý lương).
- Khi **1 nhóm chức năng** có đủ controllers + services + entities riêng.

### 4. Phân chia task team theo module
- Mỗi thành viên / nhóm nhỏ phụ trách **1-2 modules**.
- Feature branch theo module: `feature/be-employee`, `feature/be-attendance`.
- Giảm conflict Git vì mỗi người code ở folder riêng.

## Workflow khi code tính năng mới trong module

```
1️⃣  Entities           →  Tạo model tại Modules/[tên]/Entities/
2️⃣  EF Configuration   →  Cấu hình tại Data/Configurations/
3️⃣  DTOs               →  Tạo Request/Response tại Modules/[tên]/DTOs/
4️⃣  Repository         →  Interface + Implementation tại Modules/[tên]/Repositories/
5️⃣  Service            →  Interface + Implementation tại Modules/[tên]/Services/
6️⃣  Controller         →  API endpoints tại Modules/[tên]/Controllers/
7️⃣  DI Registration    →  Đăng ký tại Shared/Extensions/ServiceCollectionExtensions.cs
```
