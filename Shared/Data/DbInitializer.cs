using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Shared.Security;

namespace Shared.Data;

public static class DbInitializer
{
    public static void Initialize(AppDbContext context, bool reseed = true)
    {
        if (reseed)
        {
            context.Database.EnsureDeleted();
        }

        context.Database.EnsureCreated();

        // 0. Ensure Missing Spatial / Geofence Columns in MySQL cleanly without throwing DbCommand ERR
        try
        {
            var connection = context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                connection.Open();
            }

            // Check branches table columns
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT COLUMN_NAME 
                    FROM information_schema.COLUMNS 
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'branches';";
                
                var branchCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        branchCols.Add(reader.GetString(0));
                    }
                }

                if (!branchCols.Contains("Location"))
                {
                    using var alterCmd = connection.CreateCommand();
                    alterCmd.CommandText = "ALTER TABLE `branches` ADD COLUMN `Location` POINT NULL;";
                    alterCmd.ExecuteNonQuery();
                }

                if (!branchCols.Contains("GeofenceRadiusMeters"))
                {
                    using var alterCmd = connection.CreateCommand();
                    alterCmd.CommandText = "ALTER TABLE `branches` ADD COLUMN `GeofenceRadiusMeters` INT NOT NULL DEFAULT 50;";
                    alterCmd.ExecuteNonQuery();
                }
            }

            // Check kiosks table columns
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT COLUMN_NAME 
                    FROM information_schema.COLUMNS 
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'kiosks';";
                
                var kioskCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        kioskCols.Add(reader.GetString(0));
                    }
                }

                if (!kioskCols.Contains("IpAddress"))
                {
                    using var alterCmd = connection.CreateCommand();
                    alterCmd.CommandText = "ALTER TABLE `kiosks` ADD COLUMN `IpAddress` LONGTEXT NULL;";
                    alterCmd.ExecuteNonQuery();
                }
            }
        }
        catch
        {
            // Silent fallback
        }

        // 1. Seed Roles
        if (!context.Roles.Any())
        {
            var roles = new List<Role>
            {
                new Role { Id = 1, RoleCode = "BUSINESS_OWNER", RoleName = "Chủ Doanh Nghiệp", Description = "Chỉ xem Dashboard & Audit Log toàn hệ thống" },
                new Role { Id = 2, RoleCode = "OPERATIONS_ADMIN", RoleName = "Quản Trị Vận Hành", Description = "Quản trị Master Data toàn chuỗi" },
                new Role { Id = 3, RoleCode = "STORE_MANAGER", RoleName = "Quản Lý Cửa Hàng", Description = "Quản lý vận hành & lập lịch ca chi nhánh" },
                new Role { Id = 4, RoleCode = "SHIFT_LEADER", RoleName = "Trưởng Ca Trực", Description = "Trưởng ca trực tại chỗ & báo cáo gian lận" },
                new Role { Id = 5, RoleCode = "CASHIER", RoleName = "Thu Ngân", Description = "Bán hàng, thu tiền & bàn giao két tiền" },
                new Role { Id = 6, RoleCode = "SALES_STAFF", RoleName = "Nhân Viên Bán Hàng", Description = "Quản lý quầy kệ, xếp hàng hóa" },
                new Role { Id = 7, RoleCode = "SECURITY_GUARD", RoleName = "Nhân Viên Bảo Vệ", Description = "An ninh, kho bãi & xe qua đêm" },
            };
            context.Roles.AddRange(roles);
            context.SaveChanges();
        }

        // 2. Seed Branches
        if (!context.Branches.Any())
        {
            var branches = new List<Branch>
            {
                new Branch
                {
                    Id = 1,
                    BranchCode = "CH01",
                    Name = "Cửa hàng Tiện lợi Chi nhánh Cầu Giấy",
                    Address = "123 Cầu Giấy, Q. Cầu Giấy, Hà Nội",
                    Status = "ACTIVE",
                    Latitude = 21.0333,
                    Longitude = 105.7833,
                    GeofenceRadiusMeters = 50,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new Branch
                {
                    Id = 2,
                    BranchCode = "CH02",
                    Name = "Cửa hàng Tiện lợi Chi nhánh Lê Văn Việt",
                    Address = "456 Lê Văn Việt, TP. Thủ Đức, TP. Hồ Chí Minh",
                    Status = "ACTIVE",
                    Latitude = 10.8456,
                    Longitude = 106.7925,
                    GeofenceRadiusMeters = 50,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new Branch
                {
                    Id = 3,
                    BranchCode = "CH03",
                    Name = "Cửa hàng Tiện lợi Chi nhánh Hoàn Kiếm",
                    Address = "78 Hàng Bài, Q. Hoàn Kiếm, Hà Nội",
                    Status = "ACTIVE",
                    Location = new NetTopologySuite.Geometries.Point(105.8525, 21.0245) { SRID = 4326 },
                    GeofenceRadiusMeters = 200,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            };
            context.Branches.AddRange(branches);
            context.SaveChanges();
        }
        else
        {
            // Reset mutated branch coordinates in DB to static real store coordinates
            var existingBranches = context.Branches.ToList();
            foreach (var b in existingBranches)
            {
                b.GeofenceRadiusMeters = 200;
                if (b.Id == 1 || b.BranchCode == "CH01")
                {
                    b.Name = "Cửa hàng Tiện lợi Chi nhánh Cầu Giấy";
                    b.Location = new NetTopologySuite.Geometries.Point(105.7833, 21.0333) { SRID = 4326 };
                }
                else if (b.Id == 2 || b.BranchCode == "CH02")
                {
                    b.Name = "Cửa hàng Tiện lợi Chi nhánh Lê Văn Việt";
                    b.Location = new NetTopologySuite.Geometries.Point(106.7925, 10.8456) { SRID = 4326 };
                }
                else if (b.Id == 3 || b.BranchCode == "CH03")
                {
                    b.Name = "Cửa hàng Tiện lợi Chi nhánh Hoàn Kiếm";
                    b.Location = new NetTopologySuite.Geometries.Point(105.8525, 21.0245) { SRID = 4326 };
                }
            }

            // Ensure CH03 exists
            if (!existingBranches.Any(b => b.BranchCode == "CH03" || b.Id == 3))
            {
                context.Branches.Add(new Branch
                {
                    Id = 3,
                    BranchCode = "CH03",
                    Name = "Cửa hàng Tiện lợi Chi nhánh Hoàn Kiếm",
                    Address = "78 Hàng Bài, Q. Hoàn Kiếm, Hà Nội",
                    Status = "ACTIVE",
                    Location = new NetTopologySuite.Geometries.Point(105.8525, 21.0245) { SRID = 4326 },
                    GeofenceRadiusMeters = 200,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            context.SaveChanges();
        }

        // 3. Seed Users (Tối thiểu 19 nhân sự / chi nhánh cho 3 ca làm việc: 1 Store Manager, 3 Leader, 6 Cashier, 6 Sales, 3 Security)
        var userCount = context.Users.Count();
        if (userCount < 20)
        {
            var defaultPasswordHash = PasswordHasher.Hash("Password@123");
            var defaultPinHash = PasswordHasher.Hash("1234");
            
            var existingEmails = context.Users.Select(u => u.Email.ToLower()).ToHashSet();
            var existingPhones = context.Users.Select(u => u.Phone).Where(p => !string.IsNullOrEmpty(p)).ToHashSet();
            var existingCodes = context.Users.Select(u => u.EmployeeCode.ToUpper()).ToHashSet();

            int phoneSeq = 1000;
            string GetUniquePhone(ulong branchId, int roleId, int idx)
            {
                while (true)
                {
                    var phone = $"090{branchId % 10}{roleId}{idx:D2}{phoneSeq % 10000:D4}";
                    if (!existingPhones.Contains(phone))
                    {
                        existingPhones.Add(phone);
                        return phone;
                    }
                    phoneSeq++;
                }
            }

            var usersToSeed = new List<User>();

            // 3.1 Headquarter Admins
            if (!existingEmails.Contains("owner@rwfm.vn"))
            {
                var phone = GetUniquePhone(0, 1, 1);
                usersToSeed.Add(new User
                {
                    EmployeeCode = "OWN001",
                    FullName = "Nguyễn Văn Chủ",
                    Email = "owner@rwfm.vn",
                    Phone = phone,
                    PasswordHash = defaultPasswordHash,
                    KioskPinHash = defaultPinHash,
                    RoleId = 1, // BUSINESS_OWNER
                    EmploymentType = "FULL_TIME",
                    HomeBranchId = null,
                    Status = "ACTIVE"
                });
                existingEmails.Add("owner@rwfm.vn");
            }

            if (!existingEmails.Contains("ops.admin@rwfm.vn"))
            {
                var phone = GetUniquePhone(0, 2, 1);
                usersToSeed.Add(new User
                {
                    EmployeeCode = "OPS001",
                    FullName = "Trần Văn Vận Hành",
                    Email = "ops.admin@rwfm.vn",
                    Phone = phone,
                    PasswordHash = defaultPasswordHash,
                    KioskPinHash = defaultPinHash,
                    RoleId = 2, // OPERATIONS_ADMIN
                    EmploymentType = "FULL_TIME",
                    HomeBranchId = null,
                    Status = "ACTIVE"
                });
                existingEmails.Add("ops.admin@rwfm.vn");
            }

            // 3.2 Branch Staff Seeding Helper Data
            var branchList = context.Branches.ToList();

            // Họ tên tiếng Việt đa dạng
            var managerNames = new[] { "Trần Thị Mai", "Lê Hoàng Phúc", "Phạm Văn Đức", "Nguyễn Minh Châu" };
            var leaderNames = new[] { "Phạm Gia Bảo", "Trịnh Quốc Việt", "Đặng Thanh Tùng", "Bùi Hoàng Nam", "Vũ Minh Tiến", "Hồ Đức Anh", "Dương Quốc Bảo", "Nguyễn Thái Học", "Trần Đình Trọng" };
            var cashierNames = new[] { "Đỗ Hoàng Ngân", "Nguyễn Thị Phương", "Trần Như Quỳnh", "Lê Khánh Linh", "Hoàng Ngọc Trâm", "Phạm Thảo Nhi", "Vũ Tuyết Mai", "Đặng Minh Ánh", "Ngô Quỳnh Anh", "Bùi Kim Oanh", "Huỳnh Thu Thảo", "Trịnh Bảo Ngọc", "Hồ Bích Ngọc", "Dương Hoài Thương", "Nguyễn Mỹ Duyên", "Phạm Linh Chi", "Đỗ Hà My", "Trần Bảo An" };
            var salesNames = new[] { "Võ Minh Khang", "Nguyễn Hoàng Long", "Trần Đức Thắng", "Phạm Văn Hải", "Lê Tuấn Kiệt", "Bùi Huy Hoàng", "Hoàng Văn Nam", "Đặng Quang Vinh", "Đỗ Hữu Phước", "Trịnh Minh Đạt", "Vũ Tấn Phát", "Hồ Thành Công", "Dương Minh Trí", "Nguyễn Quốc Anh", "Trần Đình Khôi", "Phạm Hoàng Sơn", "Lê Hữu Đạt", "Ngô Văn Hùng" };
            var securityNames = new[] { "Đinh Hùng Dũng", "Nguyễn Văn Mạnh", "Trần Văn Hùng", "Lê Văn Cường", "Phạm Quốc Tuấn", "Hoàng Văn Thái", "Đặng Văn Bằng", "Bùi Văn Thành", "Vũ Văn Lộc" };

            int leaderIdx = 0, cashierIdx = 0, salesIdx = 0, securityIdx = 0, managerIdx = 0;

            foreach (var branch in branchList)
            {
                var bCodeLower = branch.BranchCode.ToLower(); // e.g. "ch01"
                var bId = branch.Id;

                // A. 1 Store Manager
                var mgrEmail = bId == 1 ? "manager.store01@rwfm.vn" : (bId == 2 ? "manager.store02@rwfm.vn" : $"manager.{bCodeLower}@rwfm.vn");
                if (!existingEmails.Contains(mgrEmail.ToLower()))
                {
                    var empCode = $"MGR{bId:D3}";
                    if (!existingCodes.Contains(empCode))
                    {
                        usersToSeed.Add(new User
                        {
                            EmployeeCode = empCode,
                            FullName = managerNames[managerIdx % managerNames.Length],
                            Email = mgrEmail,
                            Phone = GetUniquePhone(bId, 3, 1),
                            PasswordHash = defaultPasswordHash,
                            KioskPinHash = defaultPinHash,
                            RoleId = 3, // STORE_MANAGER
                            EmploymentType = "FULL_TIME",
                            HomeBranchId = bId,
                            Status = "ACTIVE"
                        });
                        existingEmails.Add(mgrEmail.ToLower());
                        existingCodes.Add(empCode);
                        managerIdx++;
                    }
                }

                // B. 3 Shift Leaders
                for (int i = 1; i <= 3; i++)
                {
                    var email = (bId == 1 && i == 1) ? "leader.store01@rwfm.vn" : $"leader.{bCodeLower}_{i}@rwfm.vn";
                    if (!existingEmails.Contains(email.ToLower()))
                    {
                        var empCode = $"SLD{bId}{i:D2}";
                        if (!existingCodes.Contains(empCode))
                        {
                            usersToSeed.Add(new User
                            {
                                EmployeeCode = empCode,
                                FullName = leaderNames[leaderIdx % leaderNames.Length],
                                Email = email,
                                Phone = GetUniquePhone(bId, 4, i),
                                PasswordHash = defaultPasswordHash,
                                KioskPinHash = defaultPinHash,
                                RoleId = 4, // SHIFT_LEADER
                                EmploymentType = "FULL_TIME",
                                HomeBranchId = bId,
                                Status = "ACTIVE"
                            });
                            existingEmails.Add(email.ToLower());
                            existingCodes.Add(empCode);
                            leaderIdx++;
                        }
                    }
                }

                // C. 6 Cashiers
                for (int i = 1; i <= 6; i++)
                {
                    var email = (bId == 1 && i == 1) ? "cashier.store01@rwfm.vn" : $"cashier.{bCodeLower}_{i}@rwfm.vn";
                    if (!existingEmails.Contains(email.ToLower()))
                    {
                        var empCode = $"CSH{bId}{i:D2}";
                        if (!existingCodes.Contains(empCode))
                        {
                            usersToSeed.Add(new User
                            {
                                EmployeeCode = empCode,
                                FullName = cashierNames[cashierIdx % cashierNames.Length],
                                Email = email,
                                Phone = GetUniquePhone(bId, 5, i),
                                PasswordHash = defaultPasswordHash,
                                KioskPinHash = defaultPinHash,
                                RoleId = 5, // CASHIER
                                EmploymentType = i % 2 == 0 ? "PART_TIME" : "FULL_TIME",
                                HomeBranchId = bId,
                                Status = "ACTIVE"
                            });
                            existingEmails.Add(email.ToLower());
                            existingCodes.Add(empCode);
                            cashierIdx++;
                        }
                    }
                }

                // D. 6 Sales Staff
                for (int i = 1; i <= 6; i++)
                {
                    var email = (bId == 1 && i == 1) ? "sales.store01@rwfm.vn" : $"sales.{bCodeLower}_{i}@rwfm.vn";
                    if (!existingEmails.Contains(email.ToLower()))
                    {
                        var empCode = $"SAL{bId}{i:D2}";
                        if (!existingCodes.Contains(empCode))
                        {
                            usersToSeed.Add(new User
                            {
                                EmployeeCode = empCode,
                                FullName = salesNames[salesIdx % salesNames.Length],
                                Email = email,
                                Phone = GetUniquePhone(bId, 6, i),
                                PasswordHash = defaultPasswordHash,
                                KioskPinHash = defaultPinHash,
                                RoleId = 6, // SALES_STAFF
                                EmploymentType = i % 3 == 0 ? "PART_TIME" : "FULL_TIME",
                                HomeBranchId = bId,
                                Status = "ACTIVE"
                            });
                            existingEmails.Add(email.ToLower());
                            existingCodes.Add(empCode);
                            salesIdx++;
                        }
                    }
                }

                // E. 3 Security Guards
                for (int i = 1; i <= 3; i++)
                {
                    var email = (bId == 1 && i == 1) ? "security.store01@rwfm.vn" : $"security.{bCodeLower}_{i}@rwfm.vn";
                    if (!existingEmails.Contains(email.ToLower()))
                    {
                        var empCode = $"SEC{bId}{i:D2}";
                        if (!existingCodes.Contains(empCode))
                        {
                            usersToSeed.Add(new User
                            {
                                EmployeeCode = empCode,
                                FullName = securityNames[securityIdx % securityNames.Length],
                                Email = email,
                                Phone = GetUniquePhone(bId, 7, i),
                                PasswordHash = defaultPasswordHash,
                                KioskPinHash = defaultPinHash,
                                RoleId = 7, // SECURITY_GUARD
                                EmploymentType = "FULL_TIME",
                                HomeBranchId = bId,
                                Status = "ACTIVE"
                            });
                            existingEmails.Add(email.ToLower());
                            existingCodes.Add(empCode);
                            securityIdx++;
                        }
                    }
                }
            }

            if (usersToSeed.Count > 0)
            {
                context.Users.AddRange(usersToSeed);
                context.SaveChanges();
            }
        }

        // 4. Seed ShiftTemplates (4 ca x 6 tiếng = 24h)
        if (!context.ShiftTemplates.Any())
        {
            var templates = new List<ShiftTemplate>
            {
                new ShiftTemplate
                {
                    Id = 1,
                    TemplateCode = "CA_01",
                    Name = "Ca 1 - Sáng (06:00 - 12:00)",
                    Description = "Ca sáng sớm từ 06:00 đến 12:00 (6 tiếng)",
                    StartTime = new TimeOnly(6, 0),
                    EndTime = new TimeOnly(12, 0),
                    IsOvernight = false,
                    BreakDurationMinutes = 30,
                    IsActive = true,
                    Status = "ACTIVE",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new ShiftTemplate
                {
                    Id = 2,
                    TemplateCode = "CA_02",
                    Name = "Ca 2 - Chiều (12:00 - 18:00)",
                    Description = "Ca trưa - chiều từ 12:00 đến 18:00 (6 tiếng)",
                    StartTime = new TimeOnly(12, 0),
                    EndTime = new TimeOnly(18, 0),
                    IsOvernight = false,
                    BreakDurationMinutes = 30,
                    IsActive = true,
                    Status = "ACTIVE",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new ShiftTemplate
                {
                    Id = 3,
                    TemplateCode = "CA_03",
                    Name = "Ca 3 - Tối (18:00 - 00:00)",
                    Description = "Ca tối từ 18:00 đến 00:00 (6 tiếng)",
                    StartTime = new TimeOnly(18, 0),
                    EndTime = new TimeOnly(0, 0),
                    IsOvernight = true,
                    BreakDurationMinutes = 30,
                    IsActive = true,
                    Status = "ACTIVE",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new ShiftTemplate
                {
                    Id = 4,
                    TemplateCode = "CA_04",
                    Name = "Ca 4 - Đêm (00:00 - 06:00)",
                    Description = "Ca đêm xuyên sáng từ 00:00 đến 06:00 (6 tiếng)",
                    StartTime = new TimeOnly(0, 0),
                    EndTime = new TimeOnly(6, 0),
                    IsOvernight = false,
                    BreakDurationMinutes = 30,
                    IsActive = true,
                    Status = "ACTIVE",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            };
            context.ShiftTemplates.AddRange(templates);
            context.SaveChanges();
        }

        // 5. Seed Kiosks
        if (!context.KioskDevices.Any())
        {
            var kiosk = new KioskDevice
            {
                Id = 1,
                BranchId = 1,
                KioskCode = "CH01-POS01",
                DeviceName = "Máy Kiosk Cầu Giấy 01",
                KioskToken = "ksk_tok_demo_pos01",
                IpWhitelist = "192.168.1.100,127.0.0.1,::1",
                UserAgentPattern = "Chrome,Edge,KioskBrowser",
                Status = "ACTIVE",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.KioskDevices.Add(kiosk);
            context.SaveChanges();
        }
    }
}
