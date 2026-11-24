using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Modules.Stores.DTOs;
using Modules.Stores.Interfaces;
using Shared.Common;
using Shared.Data;

namespace Modules.Stores.Services;

public class TierService : ITierService
{
    private readonly AppDbContext _context;

    public TierService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<List<TierDto>>> GetAllTiersAsync()
    {
        var tiers = await _context.BranchTiers
            .Include(t => t.Branches)
            .OrderBy(t => t.MinStaffCount)
            .ToListAsync();

        var dtos = tiers.Select(MapToDto).ToList();
        return ApiResponse<List<TierDto>>.Ok(dtos, "Lấy danh sách tier thành công");
    }

    public async Task<ApiResponse<TierDto>> GetTierByIdAsync(int id)
    {
        var tier = await _context.BranchTiers
            .Include(t => t.Branches)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tier == null)
            return ApiResponse<TierDto>.Fail($"Không tìm thấy tier với ID: {id}");

        return ApiResponse<TierDto>.Ok(MapToDto(tier), "Lấy thông tin tier thành công");
    }

    // =========================================================================================
    // NGHIỆP VỤ TẠO PHÂN CẤP CHI NHÁNH (CreateTierAsync)
    // Nơi xuất phát dữ liệu: Bảng `branch_tiers` trong MySQL (Entity: BranchTierEntity)
    // Các bước xử lý logic:
    //   Bước 1: Query EF Core kiểm tra trùng tên phân cấp (_context.BranchTiers.AnyAsync)
    //   Bước 2: Validate ràng buộc số lượng nhân sự: MaxStaffCount không được nhỏ hơn MinStaffCount
    //   Bước 3: Khởi tạo đối tượng BranchTierEntity với thời gian UTC chuẩn
    //   Bước 4: Gọi _context.BranchTiers.Add(tier) và _context.SaveChangesAsync() để thực thi INSERT vào DB
    //   Bước 5: Map Entity vừa tạo thành TierDto trả về cho Controller
    // =========================================================================================
    public async Task<ApiResponse<TierDto>> CreateTierAsync(CreateTierDto dto)
    {
        // 1. Kiểm tra nghiệp vụ: Tên phân cấp (TierName) phải là duy nhất trong toàn hệ thống
        var exists = await _context.BranchTiers.AnyAsync(t => t.TierName == dto.TierName);
        if (exists)
            return ApiResponse<TierDto>.Fail($"Tier với tên '{dto.TierName}' đã tồn tại");

        // 2. Kiểm tra nghiệp vụ: Hạn mức nhân sự tối đa (Max) phải lớn hơn hoặc bằng tối thiểu (Min)
        if (dto.MaxStaffCount.HasValue && dto.MaxStaffCount.Value < dto.MinStaffCount)
            return ApiResponse<TierDto>.Fail("Số nhân sự tối đa không được nhỏ hơn số nhân sự tối thiểu");

        // 3. Khởi tạo Entity lưu trữ vào Database (Table: branch_tiers)
        var tier = new BranchTierEntity
        {
            TierName = dto.TierName,
            Description = dto.Description,
            MinStaffCount = dto.MinStaffCount,
            MaxStaffCount = dto.MaxStaffCount,
            OtherConditions = dto.OtherConditions,
            Conditions = dto.Conditions,
            Benefits = dto.Benefits,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // 4. Thực thi câu lệnh SQL INSERT INTO `branch_tiers` thông qua EF Core
        _context.BranchTiers.Add(tier);
        await _context.SaveChangesAsync();

        // 5. Đóng gói kết quả DTO trả về cho client
        return ApiResponse<TierDto>.Ok(MapToDto(tier), "Tạo tier mới thành công");
    }

    public async Task<ApiResponse<TierDto>> UpdateTierAsync(int id, UpdateTierDto dto)
    {
        var tier = await _context.BranchTiers
            .Include(t => t.Branches)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tier == null)
            return ApiResponse<TierDto>.Fail($"Không tìm thấy tier với ID: {id}");

        if (!string.IsNullOrWhiteSpace(dto.TierName) && dto.TierName != tier.TierName)
        {
            var exists = await _context.BranchTiers.AnyAsync(t => t.TierName == dto.TierName && t.Id != id);
            if (exists)
                return ApiResponse<TierDto>.Fail($"Tier với tên '{dto.TierName}' đã tồn tại");
            tier.TierName = dto.TierName;
        }

        if (dto.Description != null) tier.Description = dto.Description;
        if (dto.MinStaffCount.HasValue) tier.MinStaffCount = dto.MinStaffCount.Value;
        if (dto.MaxStaffCount.HasValue) tier.MaxStaffCount = dto.MaxStaffCount.Value;
        if (dto.OtherConditions != null) tier.OtherConditions = dto.OtherConditions;
        if (dto.Conditions != null) tier.Conditions = dto.Conditions;
        if (dto.Benefits != null) tier.Benefits = dto.Benefits;

        if (tier.MaxStaffCount.HasValue && tier.MaxStaffCount.Value < tier.MinStaffCount)
            return ApiResponse<TierDto>.Fail("Số nhân sự tối đa không được nhỏ hơn số nhân sự tối thiểu");

        tier.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return ApiResponse<TierDto>.Ok(MapToDto(tier), "Cập nhật tier thành công");
    }

    public async Task<ApiResponse<bool>> DeleteTierAsync(int id)
    {
        var tier = await _context.BranchTiers
            .Include(t => t.Branches)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tier == null)
            return ApiResponse<bool>.Fail($"Không tìm thấy tier với ID: {id}");

        if (tier.Branches.Any())
            return ApiResponse<bool>.Fail($"Không thể xóa tier '{tier.TierName}' vì đang có {tier.Branches.Count} chi nhánh liên kết. Vui lòng chuyển các chi nhánh sang tier khác trước.");

        _context.BranchTiers.Remove(tier);
        await _context.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true, "Xóa tier thành công");
    }

    public async Task<BranchTierEntity?> MatchTierByStaffCountAsync(int staffCount)
    {
        return await _context.BranchTiers
            .Where(t => t.MinStaffCount <= staffCount && (!t.MaxStaffCount.HasValue || t.MaxStaffCount.Value >= staffCount))
            .OrderByDescending(t => t.MinStaffCount)
            .FirstOrDefaultAsync();
    }

    private static TierDto MapToDto(BranchTierEntity entity)
    {
        return new TierDto
        {
            Id = entity.Id,
            TierName = entity.TierName,
            Description = entity.Description,
            MinStaffCount = entity.MinStaffCount,
            MaxStaffCount = entity.MaxStaffCount,
            OtherConditions = entity.OtherConditions,
            Conditions = entity.Conditions,
            Benefits = entity.Benefits,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            BranchCount = entity.Branches?.Count ?? 0
        };
    }
}
