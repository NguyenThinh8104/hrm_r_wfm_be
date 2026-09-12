using Microsoft.EntityFrameworkCore;
using Domain.Entities;
using Modules.Stores.DTOs;
using Modules.Stores.Interfaces;
using Shared.Common;
using Shared.Common.Constants;
using Shared.Data;

namespace Modules.Stores.Services;

public class KioskService : IKioskService
{
    private readonly AppDbContext _context;

    public KioskService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<KioskCodeResponseDto>> CreateKioskCodeAsync(int managerUserId, CreateKioskCodeRequestDto request)
    {
        var store = await _context.Stores.FindAsync(request.StoreId);
        if (store == null || !store.IsActive)
        {
            return ApiResponse<KioskCodeResponseDto>.Fail(KioskMessages.StoreNotFound);
        }

        var random = new Random();
        var otpNumber = random.Next(1000, 9999);
        var code = $"POS-{otpNumber}";
        var now = DateTime.UtcNow;

        var activationCode = new KioskActivationCode
        {
            StoreId = request.StoreId,
            KioskName = string.IsNullOrWhiteSpace(request.KioskName) ? "Trạm Kiosk Mới" : request.KioskName.Trim(),
            Code = code,
            GeneratedBy = managerUserId,
            ExpiresAt = now.AddMinutes(15),
            IsUsed = false,
            CreatedAt = now
        };

        _context.KioskActivationCodes.Add(activationCode);
        await _context.SaveChangesAsync();

        return ApiResponse<KioskCodeResponseDto>.Ok(new KioskCodeResponseDto
        {
            ActivationCodeId = activationCode.ActivationCodeId,
            StoreId = activationCode.StoreId,
            KioskName = activationCode.KioskName,
            Code = activationCode.Code,
            ExpiresAt = activationCode.ExpiresAt,
            IsUsed = activationCode.IsUsed
        }, KioskMessages.CodeGenerationSuccess);
    }

    public async Task<ApiResponse<KioskActivationResponseDto>> ActivateKioskAsync(ActivateKioskRequestDto request, string? clientIp)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return ApiResponse<KioskActivationResponseDto>.Fail(KioskMessages.CodeInvalid);
        }

        var inputCode = request.Code.Trim().ToUpper();

        var activationCode = await _context.KioskActivationCodes
            .Include(c => c.Store)
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
        var existingKiosksCount = await _context.KioskDevices.CountAsync(k => k.StoreId == activationCode.StoreId);
        var kioskSeq = existingKiosksCount + 1;
        var kioskCode = $"{activationCode.Store.StoreCode}-POS{kioskSeq:D2}";
        var deviceToken = $"ksk_tok_{Guid.NewGuid():N}";

        var kioskDevice = new KioskDevice
        {
            StoreId = activationCode.StoreId,
            KioskCode = kioskCode,
            Name = activationCode.KioskName,
            DeviceToken = deviceToken,
            Status = "Active",
            IpAddress = clientIp,
            LastPingAt = now,
            CreatedAt = now
        };

        _context.KioskDevices.Add(kioskDevice);
        await _context.SaveChangesAsync();

        activationCode.IsUsed = true;
        activationCode.CreatedKioskId = kioskDevice.KioskId;
        await _context.SaveChangesAsync();

        return ApiResponse<KioskActivationResponseDto>.Ok(new KioskActivationResponseDto
        {
            KioskId = kioskDevice.KioskId,
            StoreId = activationCode.StoreId,
            StoreCode = activationCode.Store.StoreCode,
            StoreName = activationCode.Store.StoreName,
            KioskCode = kioskDevice.KioskCode,
            KioskName = kioskDevice.Name,
            DeviceToken = kioskDevice.DeviceToken,
            Status = kioskDevice.Status,
            ActivatedAt = now
        }, KioskMessages.ActivationSuccess);
    }

    public async Task<ApiResponse<KioskActivationResponseDto>> VerifyKioskTokenAsync(string deviceToken, string? clientIp)
    {
        if (string.IsNullOrWhiteSpace(deviceToken))
        {
            return ApiResponse<KioskActivationResponseDto>.Fail(KioskMessages.TokenInvalid);
        }

        var kiosk = await _context.KioskDevices
            .Include(k => k.Store)
            .FirstOrDefaultAsync(k => k.DeviceToken == deviceToken.Trim());

        if (kiosk == null || kiosk.Status != "Active")
        {
            return ApiResponse<KioskActivationResponseDto>.Fail(KioskMessages.DeviceNotFound);
        }

        kiosk.LastPingAt = DateTime.UtcNow;
        if (!string.IsNullOrEmpty(clientIp)) kiosk.IpAddress = clientIp;
        await _context.SaveChangesAsync();

        return ApiResponse<KioskActivationResponseDto>.Ok(new KioskActivationResponseDto
        {
            KioskId = kiosk.KioskId,
            StoreId = kiosk.StoreId,
            StoreCode = kiosk.Store.StoreCode,
            StoreName = kiosk.Store.StoreName,
            KioskCode = kiosk.KioskCode,
            KioskName = kiosk.Name,
            DeviceToken = kiosk.DeviceToken,
            Status = kiosk.Status,
            ActivatedAt = kiosk.CreatedAt
        }, KioskMessages.TokenValid);
    }

    public async Task<ApiResponse<List<KioskActivationResponseDto>>> GetStoreKiosksAsync(int storeId)
    {
        var kiosks = await _context.KioskDevices
            .Include(k => k.Store)
            .Where(k => k.StoreId == storeId)
            .OrderBy(k => k.KioskCode)
            .Select(k => new KioskActivationResponseDto
            {
                KioskId = k.KioskId,
                StoreId = k.StoreId,
                StoreCode = k.Store.StoreCode,
                StoreName = k.Store.StoreName,
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

