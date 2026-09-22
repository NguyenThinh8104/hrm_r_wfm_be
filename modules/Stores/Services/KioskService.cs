using Microsoft.EntityFrameworkCore;
using Domain.Entities;
using Modules.Stores.DTOs;
using Modules.Stores.Interfaces;
using Shared.Common;
using Shared.Common.Constants;
using Shared.Data;

namespace Modules.Stores.Services;

/// <summary>
/// Dịch vụ xử lý logic nghiệp vụ kích hoạt và xác thực trạm Kiosk tại cửa hàng.
/// </summary>
public class KioskService : IKioskService
{
    private readonly AppDbContext _context;

    public KioskService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Tạo mã kích hoạt Kiosk OTP ngẫu nhiên (15 phút) cho cửa hàng bởi StoreManager.
    /// </summary>
    /// <param name="managerUserId">Mã ID của người dùng Quản lý tạo mã</param>
    /// <param name="request">DTO chứa thông tin StoreId và tên Kiosk hiển thị</param>
    /// <returns>ApiResponse chứa thông tin mã kích hoạt OTP và thời gian hết hạn</returns>
    public async Task<ApiResponse<KioskCodeResponseDto>> CreateKioskCodeAsync(int managerUserId, CreateKioskCodeRequestDto request)
    {
        var branch = await _context.Branches.FindAsync((ulong)request.StoreId);
        if (branch == null || branch.Status != "ACTIVE")
        {
            return ApiResponse<KioskCodeResponseDto>.Fail(KioskMessages.StoreNotFound);
        }

        var random = new Random();
        var otpNumber = random.Next(1000, 9999);
        var code = $"POS-{otpNumber}";
        var now = DateTime.UtcNow;

        var activationCode = new KioskActivationCode
        {
            BranchId = (ulong)request.StoreId,
            KioskName = string.IsNullOrWhiteSpace(request.KioskName) ? "Trạm Kiosk Mới" : request.KioskName.Trim(),
            Code = code,
            GeneratedBy = (ulong)managerUserId,
            ExpiresAt = now.AddMinutes(15),
            IsUsed = false,
            CreatedAt = now
        };

        _context.KioskActivationCodes.Add(activationCode);
        await _context.SaveChangesAsync();

        return ApiResponse<KioskCodeResponseDto>.Ok(new KioskCodeResponseDto
        {
            ActivationCodeId = (int)activationCode.Id,
            StoreId = (int)activationCode.BranchId,
            KioskName = activationCode.KioskName,
            Code = activationCode.Code,
            ExpiresAt = activationCode.ExpiresAt,
            IsUsed = activationCode.IsUsed
        }, KioskMessages.CodeGenerationSuccess);
    }

    /// <summary>
    /// Kích hoạt thiết bị Kiosk mới sử dụng mã kích hoạt OTP do Cửa hàng trưởng cấp.
    /// </summary>
    /// <param name="request">DTO chứa mã kích hoạt Code (ví dụ: POS-1234)</param>
    /// <param name="clientIp">Địa chỉ IP của máy Kiosk gửi yêu cầu kích hoạt</param>
    /// <returns>ApiResponse chứa DeviceToken bí mật và thông tin thiết bị Kiosk sau khi kích hoạt</returns>
    public async Task<ApiResponse<KioskActivationResponseDto>> ActivateKioskAsync(ActivateKioskRequestDto request, string? clientIp)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return ApiResponse<KioskActivationResponseDto>.Fail(KioskMessages.CodeInvalid);
        }

        var inputCode = request.Code.Trim().ToUpper();

        var activationCode = await _context.KioskActivationCodes
            .Include(c => c.Branch)
            .FirstOrDefaultAsync(c => c.Code.ToUpper() == inputCode);

        if (activationCode == null)
        {
            return ApiResponse<KioskActivationResponseDto>.Fail(KioskMessages.CodeInvalid);
        }

        if (activationCode.IsUsed)
        {
            return ApiResponse<KioskActivationResponseDto>.Fail(KioskMessages.CodeAlreadyUsed);
        }

        if (activationCode.ExpiresAt < DateTime.UtcNow)
        {
            return ApiResponse<KioskActivationResponseDto>.Fail(KioskMessages.CodeExpired);
        }

        var now = DateTime.UtcNow;
        var existingKiosksCount = await _context.KioskDevices.CountAsync(k => k.BranchId == activationCode.BranchId);
        var kioskSeq = existingKiosksCount + 1;
        var kioskCode = $"{activationCode.Branch.BranchCode}-POS{kioskSeq:D2}";
        var deviceToken = $"ksk_tok_{Guid.NewGuid():N}";

        var kioskDevice = new KioskDevice
        {
            BranchId = activationCode.BranchId,
            KioskCode = kioskCode,
            Name = activationCode.KioskName,
            DeviceToken = deviceToken,
            Status = "ACTIVE",
            IpAddress = clientIp,
            LastPingAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.KioskDevices.Add(kioskDevice);
        await _context.SaveChangesAsync();

        activationCode.IsUsed = true;
        activationCode.CreatedKioskId = kioskDevice.Id;
        await _context.SaveChangesAsync();

        return ApiResponse<KioskActivationResponseDto>.Ok(new KioskActivationResponseDto
        {
            KioskId = (int)kioskDevice.Id,
            StoreId = (int)activationCode.BranchId,
            StoreCode = activationCode.Branch.BranchCode,
            StoreName = activationCode.Branch.Name,
            KioskCode = kioskDevice.KioskCode,
            KioskName = kioskDevice.Name,
            DeviceToken = kioskDevice.DeviceToken,
            Status = kioskDevice.Status,
            ActivatedAt = now
        }, KioskMessages.ActivationSuccess);
    }

    /// <summary>
    /// Xác minh DeviceToken của máy Kiosk khi khởi động hoặc Ping duy trì kết nối.
    /// </summary>
    /// <param name="deviceToken">Mã Token bí mật định danh thiết bị Kiosk</param>
    /// <param name="clientIp">Địa chỉ IP hiện tại của trạm Kiosk</param>
    /// <returns>ApiResponse xác nhận Token hợp lệ kèm thông tin cửa hàng gắn liền với Kiosk</returns>
    public async Task<ApiResponse<KioskActivationResponseDto>> VerifyKioskTokenAsync(string deviceToken, string? clientIp)
    {
        if (string.IsNullOrWhiteSpace(deviceToken))
        {
            return ApiResponse<KioskActivationResponseDto>.Fail(KioskMessages.TokenInvalid);
        }

        var kiosk = await _context.KioskDevices
            .Include(k => k.Branch)
            .FirstOrDefaultAsync(k => k.DeviceToken == deviceToken.Trim());

        if (kiosk == null || kiosk.Status != "ACTIVE")
        {
            return ApiResponse<KioskActivationResponseDto>.Fail(KioskMessages.DeviceNotFound);
        }

        kiosk.LastPingAt = DateTime.UtcNow;
        kiosk.UpdatedAt = DateTime.UtcNow;
        if (!string.IsNullOrEmpty(clientIp)) kiosk.IpAddress = clientIp;
        await _context.SaveChangesAsync();

        return ApiResponse<KioskActivationResponseDto>.Ok(new KioskActivationResponseDto
        {
            KioskId = (int)kiosk.Id,
            StoreId = (int)kiosk.BranchId,
            StoreCode = kiosk.Branch.BranchCode,
            StoreName = kiosk.Branch.Name,
            KioskCode = kiosk.KioskCode,
            KioskName = kiosk.Name,
            DeviceToken = kiosk.DeviceToken,
            Status = kiosk.Status,
            ActivatedAt = kiosk.CreatedAt
        }, KioskMessages.TokenValid);
    }

    /// <summary>
    /// Lấy danh sách các trạm Kiosk đã được kích hoạt thuộc một chi nhánh cửa hàng (Bỏ qua các trạm đã xóa mềm).
    /// </summary>
    /// <param name="storeId">Mã ID chi nhánh cửa hàng</param>
    /// <returns>ApiResponse chứa danh sách các thiết bị Kiosk của cửa hàng</returns>
    public async Task<ApiResponse<List<KioskActivationResponseDto>>> GetStoreKiosksAsync(int storeId)
    {
        var kiosks = await _context.KioskDevices
            .Include(k => k.Branch)
            .Where(k => k.BranchId == (ulong)storeId && k.Status != "DELETED")
            .OrderBy(k => k.KioskCode)
            .Select(k => new KioskActivationResponseDto
            {
                KioskId = (int)k.Id,
                StoreId = (int)k.BranchId,
                StoreCode = k.Branch.BranchCode,
                StoreName = k.Branch.Name,
                KioskCode = k.KioskCode,
                KioskName = k.Name,
                DeviceToken = k.DeviceToken,
                Status = k.Status,
                ActivatedAt = k.CreatedAt
            })
            .ToListAsync();

        return ApiResponse<List<KioskActivationResponseDto>>.Ok(kiosks);
    }

    /// <summary>
    /// Hủy ghép nối / Dừng hoạt động một trạm Kiosk theo KioskId bởi Store Manager.
    /// </summary>
    public async Task<ApiResponse<bool>> DeactivateKioskAsync(int kioskId)
    {
        var kiosk = await _context.KioskDevices.FindAsync((ulong)kioskId);
        if (kiosk == null || kiosk.Status == "DELETED")
        {
            return ApiResponse<bool>.Fail("Không tìm thấy thông tin trạm Kiosk.");
        }

        kiosk.Status = "INACTIVE";
        kiosk.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true, "Đã ngắt kết nối trạm Kiosk thành công.");
    }

    /// <summary>
    /// Hủy ghép nối trạm Kiosk bằng DeviceToken khi người dùng chọn Đăng xuất trên ứng dụng Kiosk.
    /// </summary>
    public async Task<ApiResponse<bool>> UnpairKioskTokenAsync(string deviceToken)
    {
        if (string.IsNullOrWhiteSpace(deviceToken))
        {
            return ApiResponse<bool>.Fail(KioskMessages.TokenInvalid);
        }

        var kiosk = await _context.KioskDevices.FirstOrDefaultAsync(k => k.DeviceToken == deviceToken.Trim() && k.Status != "DELETED");
        if (kiosk != null)
        {
            kiosk.Status = "INACTIVE";
            kiosk.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return ApiResponse<bool>.Ok(true, "Hủy ghép nối trạm Kiosk thành công.");
    }

    /// <summary>
    /// Xóa mềm thiết bị Kiosk (chuyển trạng thái sang DELETED).
    /// </summary>
    public async Task<ApiResponse<bool>> DeleteKioskAsync(int kioskId)
    {
        var kiosk = await _context.KioskDevices.FindAsync((ulong)kioskId);
        if (kiosk == null )
        {
            return ApiResponse<bool>.Fail("Không tìm thấy thông tin trạm Kiosk.");
        }

        // Xóa mềm: Chuyển trạng thái thiết bị sang DELETED
        kiosk.Status = "DELETED";
        kiosk.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true, "Đã xóa trạm Kiosk khỏi danh sách cửa hàng.");
    }

    // ==========================================
    // Kiosk Devices CRUD & Status Management
    // ==========================================

    public async Task<ApiResponse<List<KioskDto>>> GetBranchKiosksAsync(ulong branchId)
    {
        var branch = await _context.Branches
            .Include(b => b.Kiosks)
            .FirstOrDefaultAsync(b => b.Id == branchId);

        if (branch == null)
        {
            return ApiResponse<List<KioskDto>>.Fail("Không tìm thấy chi nhánh cửa hàng.");
        }

        var kiosks = branch.Kiosks
            .OrderBy(k => k.KioskCode)
            .Select(k => MapToKioskDto(k, branch))
            .ToList();

        return ApiResponse<List<KioskDto>>.Ok(kiosks, "Lấy danh sách Kiosk của chi nhánh thành công.");
    }

    public async Task<ApiResponse<KioskDto>> CreateBranchKioskAsync(ulong branchId, CreateKioskDto dto)
    {
        var branch = await _context.Branches
            .Include(b => b.Kiosks)
            .FirstOrDefaultAsync(b => b.Id == branchId);

        if (branch == null)
        {
            return ApiResponse<KioskDto>.Fail("Không tìm thấy chi nhánh cửa hàng để tạo Kiosk.");
        }

        var deviceName = string.IsNullOrWhiteSpace(dto.DeviceName) 
            ? $"Máy Kiosk {branch.Code} 0{branch.Kiosks.Count + 1}" 
            : dto.DeviceName.Trim();

        var kioskIndex = branch.Kiosks.Count + 1;
        var kioskCode = $"{branch.Code}-POS{kioskIndex:D2}";
        while (await _context.KioskDevices.AnyAsync(k => k.KioskCode == kioskCode))
        {
            kioskIndex++;
            kioskCode = $"{branch.Code}-POS{kioskIndex:D2}";
        }

        var kioskToken = string.IsNullOrWhiteSpace(dto.KioskToken)
            ? $"ksk_tok_{Guid.NewGuid():N}"
            : dto.KioskToken.Trim();

        var now = DateTime.UtcNow;
        var kiosk = new KioskDevice
        {
            BranchId = branchId,
            KioskCode = kioskCode,
            DeviceName = deviceName,
            KioskToken = kioskToken,
            IpWhitelist = string.IsNullOrWhiteSpace(dto.IpWhitelist) ? null : dto.IpWhitelist.Trim(),
            UserAgentPattern = string.IsNullOrWhiteSpace(dto.UserAgentPattern) ? null : dto.UserAgentPattern.Trim(),
            Status = "ACTIVE",
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.KioskDevices.Add(kiosk);
        await _context.SaveChangesAsync();

        return ApiResponse<KioskDto>.Ok(MapToKioskDto(kiosk, branch), "Tạo mới trạm Kiosk thành công.");
    }

    public async Task<ApiResponse<KioskDto>> UpdateKioskAsync(ulong kioskId, UpdateKioskDto dto)
    {
        var kiosk = await _context.KioskDevices
            .Include(k => k.Branch)
            .FirstOrDefaultAsync(k => k.Id == kioskId);

        if (kiosk == null)
        {
            return ApiResponse<KioskDto>.Fail("Không tìm thấy trạm Kiosk cần cập nhật.");
        }

        if (!string.IsNullOrWhiteSpace(dto.DeviceName))
        {
            kiosk.DeviceName = dto.DeviceName.Trim();
        }

        kiosk.IpWhitelist = string.IsNullOrWhiteSpace(dto.IpWhitelist) ? null : dto.IpWhitelist.Trim();
        kiosk.UserAgentPattern = string.IsNullOrWhiteSpace(dto.UserAgentPattern) ? null : dto.UserAgentPattern.Trim();
        kiosk.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return ApiResponse<KioskDto>.Ok(MapToKioskDto(kiosk, kiosk.Branch), "Cập nhật cấu hình Kiosk thành công.");
    }

    public async Task<ApiResponse<KioskDto>> UpdateKioskStatusAsync(ulong kioskId, UpdateKioskStatusDto dto)
    {
        var kiosk = await _context.KioskDevices
            .Include(k => k.Branch)
            .FirstOrDefaultAsync(k => k.Id == kioskId);

        if (kiosk == null)
        {
            return ApiResponse<KioskDto>.Fail("Không tìm thấy trạm Kiosk.");
        }

        var normalizedStatus = (dto.Status ?? string.Empty).Trim().ToUpper();
        if (normalizedStatus != "ACTIVE" && normalizedStatus != "BLOCKED" && normalizedStatus != "INACTIVE" && normalizedStatus != "LOCKED")
        {
            return ApiResponse<KioskDto>.Fail("Trạng thái không hợp lệ. Chỉ chấp nhận 'ACTIVE', 'BLOCKED' hoặc 'INACTIVE'.");
        }

        if (normalizedStatus == "LOCKED") normalizedStatus = "BLOCKED";

        kiosk.Status = normalizedStatus;
        kiosk.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var message = normalizedStatus == "BLOCKED" || normalizedStatus == "INACTIVE"
            ? "Đã khóa trạm Kiosk. Trạm sẽ bị từ chối chấm công ngay lập tức."
            : "Đã mở khóa hoạt động cho trạm Kiosk.";

        return ApiResponse<KioskDto>.Ok(MapToKioskDto(kiosk, kiosk.Branch), message);
    }

    public async Task<ApiResponse<List<KioskDto>>> GetAllKiosksAsync()
    {
        var kiosks = await _context.KioskDevices
            .Include(k => k.Branch)
            .OrderBy(k => k.Branch.BranchCode)
            .ThenBy(k => k.KioskCode)
            .Select(k => MapToKioskDto(k, k.Branch))
            .ToListAsync();

        return ApiResponse<List<KioskDto>>.Ok(kiosks, "Lấy danh sách Kiosk toàn chuỗi thành công.");
    }

    public async Task<ApiResponse<KioskDto>> GetKioskByIdAsync(ulong kioskId)
    {
        var kiosk = await _context.KioskDevices
            .Include(k => k.Branch)
            .FirstOrDefaultAsync(k => k.Id == kioskId);

        if (kiosk == null)
        {
            return ApiResponse<KioskDto>.Fail("Không tìm thấy trạm Kiosk.");
        }

        return ApiResponse<KioskDto>.Ok(MapToKioskDto(kiosk, kiosk.Branch), "Lấy thông tin Kiosk thành công.");
    }

    private static KioskDto MapToKioskDto(KioskDevice k, Branch b)
    {
        return new KioskDto
        {
            Id = k.Id,
            BranchId = k.BranchId,
            BranchCode = b?.Code ?? string.Empty,
            BranchName = b?.Name ?? string.Empty,
            DeviceName = k.DeviceName,
            KioskCode = k.KioskCode,
            IpWhitelist = k.IpWhitelist,
            KioskToken = k.KioskToken,
            UserAgentPattern = k.UserAgentPattern,
            Status = k.Status,
            LastPingAt = k.LastPingAt,
            CreatedAt = k.CreatedAt,
            UpdatedAt = k.UpdatedAt
        };
    }
}


