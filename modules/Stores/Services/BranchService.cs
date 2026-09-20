using Microsoft.EntityFrameworkCore;
using Domain.Entities;
using Domain.Enums;
using Modules.Stores.DTOs;
using Modules.Stores.Interfaces;
using Shared.Common;
using Shared.Data;

namespace Modules.Stores.Services;

/// <summary>
/// Dịch vụ xử lý nghiệp vụ quản lý danh mục chi nhánh cửa hàng & tọa độ GPS Geofence (UC 1.2 - Operations Admin).
/// </summary>
public class BranchService : IBranchService, IStoreService
{
    private readonly AppDbContext _context;

    public BranchService(AppDbContext context)
    {
        _context = context;
    }

    // ==========================================
    // 1. Quản lý Danh mục Chi nhánh (Branch CRUD)
    // ==========================================

    /// <summary>
    /// Lấy danh sách toàn bộ các chi nhánh cửa hàng trong hệ thống (hỗ trợ lọc status, tier & tìm kiếm).
    /// </summary>
    /// <param name="status">Lọc theo trạng thái hoạt động (ACTIVE / INACTIVE).</param>
    /// <param name="search">Từ khóa tìm kiếm theo mã, tên hoặc địa chỉ.</param>
    /// <param name="tier">Lọc theo phân cấp chi nhánh (1 = Tier1, 2 = Tier2, 3 = Tier3).</param>
    public async Task<ApiResponse<List<BranchDto>>> GetAllBranchesAsync(string? status = null, string? search = null, BranchTier? tier = null)
    {
        var query = _context.Branches.AsQueryable();

        // 1. Lọc theo trạng thái chi nhánh nếu có
        if (!string.IsNullOrWhiteSpace(status))
        {
            var filterStatus = status.Trim().ToUpper();
            query = query.Where(b => b.Status.ToUpper() == filterStatus);
        }

        // 2. Lọc theo phân cấp chi nhánh (BranchTier) nếu client yêu cầu
        if (tier.HasValue)
        {
            query = query.Where(b => b.BranchTier == tier.Value);
        }

        // 3. Lọc theo từ khóa tìm kiếm (Mã, Tên hoặc Địa chỉ)
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(b => 
                b.BranchCode.ToLower().Contains(s) || 
                b.Name.ToLower().Contains(s) || 
                b.Address.ToLower().Contains(s));
        }

        // 4. Lấy danh sách entities từ database và chuyển đổi sang DTO
        var branches = await query
            .OrderBy(b => b.BranchCode)
            .ToListAsync();

        return ApiResponse<List<BranchDto>>.Ok(branches.Select(MapToBranchDto).ToList(), "Lấy danh sách chi nhánh thành công.");
    }

    /// <summary>
    /// Thống kê số lượng chi nhánh theo từng phân cấp quy mô (Tier 1: Lớn, Tier 2: Tiêu chuẩn, Tier 3: Nhỏ).
    /// </summary>
    /// <returns>Đối tượng BranchTierSummaryDto gồm tier1Count, tier2Count, tier3Count và totalCount.</returns>
    public async Task<ApiResponse<BranchTierSummaryDto>> GetBranchTierSummaryAsync()
    {
        // Nhóm chi nhánh theo BranchTier và đếm số lượng từng nhóm
        var tierCounts = await _context.Branches
            .GroupBy(b => b.BranchTier)
            .Select(g => new { Tier = g.Key, Count = g.Count() })
            .ToListAsync();

        // Map số lượng từng tier (nếu không có chi nhánh nào thuộc tier thì mặc định là 0)
        var summary = new BranchTierSummaryDto
        {
            Tier1Count = tierCounts.FirstOrDefault(x => x.Tier == BranchTier.Tier1)?.Count ?? 0,
            Tier2Count = tierCounts.FirstOrDefault(x => x.Tier == BranchTier.Tier2)?.Count ?? 0,
            Tier3Count = tierCounts.FirstOrDefault(x => x.Tier == BranchTier.Tier3)?.Count ?? 0
        };

        return ApiResponse<BranchTierSummaryDto>.Ok(summary, "Lấy thống kê phân cấp chi nhánh thành công.");
    }

    /// <summary>
    /// Lấy thông tin chi tiết một chi nhánh cửa hàng theo ID.
    /// </summary>
    public async Task<ApiResponse<BranchDto>> GetBranchByIdAsync(ulong id)
    {
        var branch = await _context.Branches
            .FirstOrDefaultAsync(b => b.Id == id);

        if (branch == null)
        {
            return ApiResponse<BranchDto>.Fail("Không tìm thấy chi nhánh cửa hàng.");
        }

        return ApiResponse<BranchDto>.Ok(MapToBranchDto(branch), "Lấy thông tin chi nhánh thành công.");
    }

    /// <summary>
    /// Thêm mới chi nhánh cửa hàng (Operations Admin).
    /// </summary>
    public async Task<ApiResponse<BranchDto>> CreateBranchAsync(CreateBranchDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Code))
        {
            return ApiResponse<BranchDto>.Fail("Mã chi nhánh (code) không được để trống.");
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return ApiResponse<BranchDto>.Fail("Tên chi nhánh (name) không được để trống.");
        }

        if (string.IsNullOrWhiteSpace(dto.Address))
        {
            return ApiResponse<BranchDto>.Fail("Địa chỉ chi nhánh (address) không được để trống.");
        }

        // Kiểm tra phân cấp chi nhánh: bắt buộc phải thuộc enum BranchTier hợp lệ (1: Tier 1, 2: Tier 2, 3: Tier 3)
        if (!Enum.IsDefined(typeof(BranchTier), dto.BranchTier))
        {
            return ApiResponse<BranchDto>.Fail("Phân cấp chi nhánh (BranchTier) không hợp lệ. Chỉ chấp nhận 1 (Tier1), 2 (Tier2), 3 (Tier3).");
        }

        var normalizedCode = dto.Code.Trim().ToUpper();
        var codeExists = await _context.Branches
            .AnyAsync(b => b.BranchCode.ToUpper() == normalizedCode);

        if (codeExists)
        {
            return ApiResponse<BranchDto>.Fail($"Mã chi nhánh '{normalizedCode}' đã tồn tại trong hệ thống.");
        }

        var now = DateTime.UtcNow;
        var branch = new Branch
        {
            BranchCode = normalizedCode,
            Name = dto.Name.Trim(),
            Address = dto.Address.Trim(),
            BranchTier = dto.BranchTier, // Map phân cấp chi nhánh từ CreateBranchDto vào entity
            Status = string.IsNullOrWhiteSpace(dto.Status) ? "ACTIVE" : dto.Status.Trim().ToUpper(),
            GeofenceRadiusMeters = dto.GeofenceRadiusMeters.HasValue && dto.GeofenceRadiusMeters.Value > 0 ? dto.GeofenceRadiusMeters.Value : 50,
            CreatedAt = now,
            UpdatedAt = now
        };

        if (dto.Latitude.HasValue && dto.Longitude.HasValue)
        {
            branch.Location = new NetTopologySuite.Geometries.Point(dto.Longitude.Value, dto.Latitude.Value) { SRID = 4326 };
        }

        _context.Branches.Add(branch);
        await _context.SaveChangesAsync();

        return ApiResponse<BranchDto>.Ok(MapToBranchDto(branch), "Thêm mới chi nhánh cửa hàng thành công.");
    }

    /// <summary>
    /// Cập nhật thông tin chi nhánh cửa hàng (Tên, địa chỉ, phân cấp, tọa độ GPS, bán kính Geofence).
    /// </summary>
    public async Task<ApiResponse<BranchDto>> UpdateBranchAsync(ulong id, UpdateBranchDto dto)
    {
        var branch = await _context.Branches
            .FirstOrDefaultAsync(b => b.Id == id);

        if (branch == null)
        {
            return ApiResponse<BranchDto>.Fail("Không tìm thấy chi nhánh cửa hàng cần cập nhật.");
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return ApiResponse<BranchDto>.Fail("Tên chi nhánh không được để trống.");
        }

        if (string.IsNullOrWhiteSpace(dto.Address))
        {
            return ApiResponse<BranchDto>.Fail("Địa chỉ chi nhánh không được để trống.");
        }

        // Nếu client gửi cập nhật phân cấp chi nhánh: kiểm tra tính hợp lệ của enum trước khi cập nhật
        if (dto.BranchTier.HasValue)
        {
            if (!Enum.IsDefined(typeof(BranchTier), dto.BranchTier.Value))
            {
                return ApiResponse<BranchDto>.Fail("Phân cấp chi nhánh (BranchTier) không hợp lệ. Chỉ chấp nhận 1 (Tier1), 2 (Tier2), 3 (Tier3).");
            }
            branch.BranchTier = dto.BranchTier.Value; // Cập nhật phân cấp chi nhánh mới
        }

        branch.Name = dto.Name.Trim();
        branch.Address = dto.Address.Trim();
        if (dto.Latitude.HasValue && dto.Longitude.HasValue)
        {
            branch.Location = new NetTopologySuite.Geometries.Point(dto.Longitude.Value, dto.Latitude.Value) { SRID = 4326 };
        }
        if (dto.GeofenceRadiusMeters.HasValue && dto.GeofenceRadiusMeters.Value > 0)
        {
            branch.GeofenceRadiusMeters = dto.GeofenceRadiusMeters.Value;
        }
        branch.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return ApiResponse<BranchDto>.Ok(MapToBranchDto(branch), "Cập nhật thông tin chi nhánh thành công.");
    }

    /// <summary>
    /// Cập nhật trạng thái chi nhánh (ACTIVE / INACTIVE).
    /// </summary>
    public async Task<ApiResponse<BranchDto>> UpdateBranchStatusAsync(ulong id, UpdateBranchStatusDto dto)
    {
        var branch = await _context.Branches
            .FirstOrDefaultAsync(b => b.Id == id);

        if (branch == null)
        {
            return ApiResponse<BranchDto>.Fail("Không tìm thấy chi nhánh cửa hàng.");
        }

        var normalizedStatus = (dto.Status ?? string.Empty).Trim().ToUpper();
        if (normalizedStatus != "ACTIVE" && normalizedStatus != "INACTIVE" && normalizedStatus != "LOCKED")
        {
            return ApiResponse<BranchDto>.Fail("Trạng thái không hợp lệ. Chỉ chấp nhận 'ACTIVE' hoặc 'INACTIVE'.");
        }

        if (normalizedStatus == "LOCKED") normalizedStatus = "INACTIVE";

        branch.Status = normalizedStatus;
        branch.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var message = normalizedStatus == "INACTIVE"
            ? "Đã khóa chi nhánh thành công."
            : "Đã kích hoạt lại chi nhánh thành công.";

        return ApiResponse<BranchDto>.Ok(MapToBranchDto(branch), message);
    }

    /// <summary>
    /// Xóa chi nhánh cửa hàng (Operations Admin).
    /// </summary>
    public async Task<ApiResponse<bool>> DeleteBranchAsync(ulong id)
    {
        var branch = await _context.Branches
            .Include(b => b.Users)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (branch == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy chi nhánh cửa hàng cần xóa.");
        }

        foreach (var user in branch.Users)
        {
            user.HomeBranchId = null;
        }

        _context.Branches.Remove(branch);
        await _context.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true, "Đã xóa chi nhánh cửa hàng thành công.");
    }

    // ==========================================
    // Mapping Helpers
    // ==========================================

    private static BranchDto MapToBranchDto(Branch b)
    {
        return new BranchDto
        {
            Id = b.Id,
            Code = b.Code,
            Name = b.Name,
            Address = b.Address,
            Latitude = b.Latitude,
            Longitude = b.Longitude,
            GeofenceRadiusMeters = b.GeofenceRadiusMeters > 0 ? b.GeofenceRadiusMeters : 50,
            BranchTier = b.BranchTier, // Map phân cấp chi nhánh từ entity sang DTO (kèm BranchTierName tự tính toán)
            KioskAllowedIp = null,
            KioskAllowedBrowser = null,
            Status = b.Status,
            CreatedAt = b.CreatedAt,
            UpdatedAt = b.UpdatedAt,
            TotalKiosks = 0,
            ActiveKiosks = 0,
            Kiosks = new()
        };
    }
}
