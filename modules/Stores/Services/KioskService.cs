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
            CreatedAt = now
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
    /// Lấy danh sách các trạm Kiosk đã được kích hoạt thuộc một chi nhánh cửa hàng.
    /// </summary>
    /// <param name="storeId">Mã ID chi nhánh cửa hàng</param>
    /// <returns>ApiResponse chứa danh sách các thiết bị Kiosk của cửa hàng</returns>
    public async Task<ApiResponse<List<KioskActivationResponseDto>>> GetStoreKiosksAsync(int storeId)
    {
        var kiosks = await _context.KioskDevices
            .Include(k => k.Branch)
            .Where(k => k.BranchId == (ulong)storeId)
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
}
