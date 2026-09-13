using Microsoft.EntityFrameworkCore;
using Domain.Entities;
using Modules.Stores.DTOs;
using Modules.Stores.Interfaces;
using Shared.Common;
using Shared.Data;

namespace Modules.Stores.Services;

/// <summary>
/// Dịch vụ xử lý nghiệp vụ quản lý danh mục chi nhánh cửa hàng (UC 1.2).
/// </summary>
public class StoreService : IStoreService
{
    private readonly AppDbContext _context;

    public StoreService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Lấy danh sách toàn bộ các chi nhánh cửa hàng trong hệ thống.
    /// </summary>
    public async Task<ApiResponse<List<StoreDetailDto>>> GetAllStoresAsync(string? status = null)
    {
        var query = _context.Branches
            .Include(b => b.Kiosks)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var filterStatus = status.Trim().ToUpper();
            query = query.Where(b => b.Status.ToUpper() == filterStatus);
        }

        var stores = await query
            .OrderBy(b => b.BranchCode)
            .Select(b => MapToDetailDto(b))
            .ToListAsync();

        return ApiResponse<List<StoreDetailDto>>.Ok(stores, "Lấy danh sách chi nhánh thành công.");
    }

    /// <summary>
    /// Lấy thông tin chi tiết một chi nhánh cửa hàng theo ID.
    /// </summary>
    public async Task<ApiResponse<StoreDetailDto>> GetStoreByIdAsync(int id)
    {
        var store = await _context.Branches
            .Include(b => b.Kiosks)
            .FirstOrDefaultAsync(b => b.Id == (ulong)id);

        if (store == null)
        {
            return ApiResponse<StoreDetailDto>.Fail("Không tìm thấy chi nhánh cửa hàng.");
        }

        return ApiResponse<StoreDetailDto>.Ok(MapToDetailDto(store), "Lấy thông tin chi nhánh thành công.");
    }

    /// <summary>
    /// Thêm mới một chi nhánh cửa hàng (Operations Admin).
    /// </summary>
    public async Task<ApiResponse<StoreDetailDto>> CreateStoreAsync(CreateStoreDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.BranchCode))
        {
            return ApiResponse<StoreDetailDto>.Fail("Mã chi nhánh không được để trống.");
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return ApiResponse<StoreDetailDto>.Fail("Tên chi nhánh không được để trống.");
        }

        if (string.IsNullOrWhiteSpace(dto.Address))
        {
            return ApiResponse<StoreDetailDto>.Fail("Địa chỉ chi nhánh không được để trống.");
        }

        var normalizedCode = dto.BranchCode.Trim().ToUpper();
        var codeExists = await _context.Branches
            .AnyAsync(b => b.BranchCode.ToUpper() == normalizedCode);

        if (codeExists)
        {
            return ApiResponse<StoreDetailDto>.Fail($"Mã chi nhánh '{normalizedCode}' đã tồn tại trong hệ thống.");
        }

        var now = DateTime.UtcNow;
        var branch = new Branch
        {
            BranchCode = normalizedCode,
            Name = dto.Name.Trim(),
            Address = dto.Address.Trim(),
            KioskAllowedIp = string.IsNullOrWhiteSpace(dto.KioskAllowedIp) ? null : dto.KioskAllowedIp.Trim(),
            KioskAllowedBrowser = string.IsNullOrWhiteSpace(dto.KioskAllowedBrowser) ? null : dto.KioskAllowedBrowser.Trim(),
            Status = "ACTIVE",
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.Branches.Add(branch);
        await _context.SaveChangesAsync();

        return ApiResponse<StoreDetailDto>.Ok(MapToDetailDto(branch), "Thêm mới chi nhánh cửa hàng thành công.");
    }

    /// <summary>
    /// Cập nhật thông tin chi nhánh cửa hàng (Operations Admin).
    /// </summary>
    public async Task<ApiResponse<StoreDetailDto>> UpdateStoreAsync(int id, UpdateStoreDto dto)
    {
        var store = await _context.Branches
            .Include(b => b.Kiosks)
            .FirstOrDefaultAsync(b => b.Id == (ulong)id);

        if (store == null)
        {
            return ApiResponse<StoreDetailDto>.Fail("Không tìm thấy chi nhánh cửa hàng.");
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return ApiResponse<StoreDetailDto>.Fail("Tên chi nhánh không được để trống.");
        }

        if (string.IsNullOrWhiteSpace(dto.Address))
        {
            return ApiResponse<StoreDetailDto>.Fail("Địa chỉ chi nhánh không được để trống.");
        }

        store.Name = dto.Name.Trim();
        store.Address = dto.Address.Trim();
        store.KioskAllowedIp = string.IsNullOrWhiteSpace(dto.KioskAllowedIp) ? null : dto.KioskAllowedIp.Trim();
        store.KioskAllowedBrowser = string.IsNullOrWhiteSpace(dto.KioskAllowedBrowser) ? null : dto.KioskAllowedBrowser.Trim();

        if (!string.IsNullOrWhiteSpace(dto.Status))
        {
            var validStatus = dto.Status.Trim().ToUpper();
            if (validStatus != "ACTIVE" && validStatus != "LOCKED")
            {
                return ApiResponse<StoreDetailDto>.Fail("Trạng thái chi nhánh không hợp lệ. Chỉ chấp nhận ACTIVE hoặc LOCKED.");
            }
            store.Status = validStatus;
        }

        store.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return ApiResponse<StoreDetailDto>.Ok(MapToDetailDto(store), "Cập nhật chi nhánh cửa hàng thành công.");
    }

    /// <summary>
    /// Khóa hoặc Mở khóa chi nhánh cửa hàng (Operations Admin).
    /// </summary>
    public async Task<ApiResponse<StoreDetailDto>> UpdateStoreStatusAsync(int id, UpdateStoreStatusDto dto)
    {
        var store = await _context.Branches
            .Include(b => b.Kiosks)
            .FirstOrDefaultAsync(b => b.Id == (ulong)id);

        if (store == null)
        {
            return ApiResponse<StoreDetailDto>.Fail("Không tìm thấy chi nhánh cửa hàng.");
        }

        if (string.IsNullOrWhiteSpace(dto.Status))
        {
            return ApiResponse<StoreDetailDto>.Fail("Vui lòng cung cấp trạng thái mới cho chi nhánh.");
        }

        var normalizedStatus = dto.Status.Trim().ToUpper();
        if (normalizedStatus != "ACTIVE" && normalizedStatus != "LOCKED")
        {
            return ApiResponse<StoreDetailDto>.Fail("Trạng thái không hợp lệ. Chỉ hỗ trợ 'ACTIVE' hoặc 'LOCKED'.");
        }

        store.Status = normalizedStatus;
        store.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var statusMessage = normalizedStatus == "LOCKED" 
            ? "Đã khóa chi nhánh thành công. Tất cả các trạm Kiosk tại chi nhánh này đã bị tạm dừng hoạt động."
            : "Đã mở khóa chi nhánh thành công. Các trạm Kiosk có thể hoạt động trở lại.";

        return ApiResponse<StoreDetailDto>.Ok(MapToDetailDto(store), statusMessage);
    }

    /// <summary>
    /// Chuyển đổi Entity Branch sang StoreDetailDto.
    /// </summary>
    private static StoreDetailDto MapToDetailDto(Branch b)
    {
        var kiosks = b.Kiosks ?? new List<KioskDevice>();
        var fiveMinutesAgo = DateTime.UtcNow.AddMinutes(-5);

        return new StoreDetailDto
        {
            StoreId = (int)b.Id,
            StoreCode = b.BranchCode,
            StoreName = b.Name,
            Address = b.Address,
            Status = b.Status,
            KioskAllowedIp = b.KioskAllowedIp,
            KioskAllowedBrowser = b.KioskAllowedBrowser,
            TotalKiosks = kiosks.Count,
            ActiveKiosks = kiosks.Count(k => k.Status == "ACTIVE"),
            CreatedAt = b.CreatedAt,
            UpdatedAt = b.UpdatedAt,
            Kiosks = kiosks.Select(k => new KioskDetailDto
            {
                KioskId = (int)k.Id,
                StoreId = (int)k.BranchId,
                StoreCode = b.BranchCode,
                StoreName = b.Name,
                KioskCode = k.KioskCode,
                KioskName = k.Name,
                DeviceToken = k.DeviceToken,
                Status = k.Status,
                AllowedIp = k.AllowedIp,
                AllowedBrowser = k.AllowedBrowser,
                IpAddress = k.IpAddress,
                LastBrowserUserAgent = k.LastBrowserUserAgent,
                LastPingAt = k.LastPingAt,
                IsOnline = k.LastPingAt.HasValue && k.LastPingAt.Value >= fiveMinutesAgo,
                CreatedAt = k.CreatedAt,
                UpdatedAt = k.UpdatedAt
            }).ToList()
        };
    }
}
