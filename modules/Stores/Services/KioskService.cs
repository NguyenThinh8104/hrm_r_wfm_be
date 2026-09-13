using Microsoft.EntityFrameworkCore;
using Domain.Entities;
using Modules.Stores.DTOs;
using Modules.Stores.Interfaces;
using Shared.Common;
using Shared.Common.Constants;
using Shared.Data;

namespace Modules.Stores.Services;

/// <summary>
/// Dịch vụ xử lý logic nghiệp vụ kích hoạt, xác thực và cấu hình trạm Kiosk tại quầy cửa hàng (UC 1.2).
/// </summary>
public class KioskService : IKioskService
{
    private readonly AppDbContext _context;

    public KioskService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Tạo mã kích hoạt Kiosk OTP ngẫu nhiên (15 phút) cho cửa hàng bởi StoreManager hoặc Admin.
    /// </summary>
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
    public async Task<ApiResponse<KioskActivationResponseDto>> ActivateKioskAsync(ActivateKioskRequestDto request, string? clientIp, string? userAgent = null)
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

        if (activationCode.Branch == null || activationCode.Branch.Status != "ACTIVE")
        {
            return ApiResponse<KioskActivationResponseDto>.Fail("Chi nhánh hiện đang bị khóa hoặc ngừng hoạt động. Không thể kích hoạt Kiosk mới.");
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
            AllowedIp = activationCode.Branch.KioskAllowedIp,
            AllowedBrowser = activationCode.Branch.KioskAllowedBrowser,
            IpAddress = clientIp,
            LastBrowserUserAgent = userAgent,
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
    /// Kiểm tra tính hợp lệ về trạng thái, mạng (IP) và trình duyệt.
    /// </summary>
    public async Task<ApiResponse<KioskActivationResponseDto>> VerifyKioskTokenAsync(string deviceToken, string? clientIp, string? userAgent = null)
    {
        if (string.IsNullOrWhiteSpace(deviceToken))
        {
            return ApiResponse<KioskActivationResponseDto>.Fail(KioskMessages.TokenInvalid);
        }

        var kiosk = await _context.KioskDevices
            .Include(k => k.Branch)
            .FirstOrDefaultAsync(k => k.DeviceToken == deviceToken.Trim());

        if (kiosk == null)
        {
            return ApiResponse<KioskActivationResponseDto>.Fail(KioskMessages.DeviceNotFound);
        }

        // 1. Kiểm tra trạng thái Kiosk
        if (kiosk.Status != "ACTIVE")
        {
            return ApiResponse<KioskActivationResponseDto>.Fail($"Trạm Kiosk '{kiosk.KioskCode}' đang ở trạng thái {kiosk.Status} (Tạm khóa). Vui lòng liên hệ Quản trị vận hành.");
        }

        // 2. Kiểm tra trạng thái Chi nhánh
        if (kiosk.Branch == null || kiosk.Branch.Status != "ACTIVE")
        {
            return ApiResponse<KioskActivationResponseDto>.Fail($"Chi nhánh '{kiosk.Branch?.Name}' đang ở trạng thái {kiosk.Branch?.Status} (Tạm khóa). Toàn bộ trạm Kiosk tại chi nhánh tạm ngừng hoạt động.");
        }

        // 3. Kiểm tra thông tin mạng (IP Whitelist)
        var effectiveAllowedIp = !string.IsNullOrWhiteSpace(kiosk.AllowedIp) ? kiosk.AllowedIp : kiosk.Branch.KioskAllowedIp;
        if (!string.IsNullOrWhiteSpace(effectiveAllowedIp) && !string.IsNullOrWhiteSpace(clientIp))
        {
            if (!IsIpAllowed(clientIp, effectiveAllowedIp))
            {
                return ApiResponse<KioskActivationResponseDto>.Fail($"Truy cập bị từ chối: Địa chỉ IP ({clientIp}) không khớp với cấu hình mạng Kiosk quầy được cấp phép ({effectiveAllowedIp}).");
            }
        }

        // 4. Kiểm tra thông tin trình duyệt (Browser / User-Agent Whitelist)
        var effectiveAllowedBrowser = !string.IsNullOrWhiteSpace(kiosk.AllowedBrowser) ? kiosk.AllowedBrowser : kiosk.Branch.KioskAllowedBrowser;
        if (!string.IsNullOrWhiteSpace(effectiveAllowedBrowser) && !string.IsNullOrWhiteSpace(userAgent))
        {
            if (!IsBrowserAllowed(userAgent, effectiveAllowedBrowser))
            {
                return ApiResponse<KioskActivationResponseDto>.Fail($"Truy cập bị từ chối: Trình duyệt client không nằm trong danh sách trình duyệt quầy được cấp phép ({effectiveAllowedBrowser}).");
            }
        }

        // Cập nhật trạng thái Ping và thông tin kết nối thực tế
        kiosk.LastPingAt = DateTime.UtcNow;
        if (!string.IsNullOrEmpty(clientIp)) kiosk.IpAddress = clientIp;
        if (!string.IsNullOrEmpty(userAgent)) kiosk.LastBrowserUserAgent = userAgent;
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
    /// Lấy danh sách toàn bộ các trạm Kiosk trong hệ thống chuỗi (Operations Admin).
    /// </summary>
    public async Task<ApiResponse<List<KioskDetailDto>>> GetAllKiosksAsync()
    {
        var fiveMinutesAgo = DateTime.UtcNow.AddMinutes(-5);

        var kiosks = await _context.KioskDevices
            .Include(k => k.Branch)
            .OrderBy(k => k.Branch.BranchCode)
            .ThenBy(k => k.KioskCode)
            .Select(k => new KioskDetailDto
            {
                KioskId = (int)k.Id,
                StoreId = (int)k.BranchId,
                StoreCode = k.Branch.BranchCode,
                StoreName = k.Branch.Name,
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
            })
            .ToListAsync();

        return ApiResponse<List<KioskDetailDto>>.Ok(kiosks, "Lấy danh sách tất cả trạm Kiosk thành công.");
    }

    /// <summary>
    /// Lấy chi tiết thông tin trạm Kiosk theo ID.
    /// </summary>
    public async Task<ApiResponse<KioskDetailDto>> GetKioskByIdAsync(int kioskId)
    {
        var fiveMinutesAgo = DateTime.UtcNow.AddMinutes(-5);

        var kiosk = await _context.KioskDevices
            .Include(k => k.Branch)
            .FirstOrDefaultAsync(k => k.Id == (ulong)kioskId);

        if (kiosk == null)
        {
            return ApiResponse<KioskDetailDto>.Fail("Không tìm thấy thông tin trạm Kiosk.");
        }

        return ApiResponse<KioskDetailDto>.Ok(new KioskDetailDto
        {
            KioskId = (int)kiosk.Id,
            StoreId = (int)kiosk.BranchId,
            StoreCode = kiosk.Branch.BranchCode,
            StoreName = kiosk.Branch.Name,
            KioskCode = kiosk.KioskCode,
            KioskName = kiosk.Name,
            DeviceToken = kiosk.DeviceToken,
            Status = kiosk.Status,
            AllowedIp = kiosk.AllowedIp,
            AllowedBrowser = kiosk.AllowedBrowser,
            IpAddress = kiosk.IpAddress,
            LastBrowserUserAgent = kiosk.LastBrowserUserAgent,
            LastPingAt = kiosk.LastPingAt,
            IsOnline = kiosk.LastPingAt.HasValue && kiosk.LastPingAt.Value >= fiveMinutesAgo,
            CreatedAt = kiosk.CreatedAt,
            UpdatedAt = kiosk.UpdatedAt
        }, "Lấy thông tin trạm Kiosk thành công.");
    }

    /// <summary>
    /// Lấy danh sách các trạm Kiosk đã được kích hoạt thuộc một chi nhánh cửa hàng.
    /// </summary>
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

    /// <summary>
    /// Cập nhật cấu hình mạng và trình duyệt cho trạm Kiosk quầy.
    /// </summary>
    public async Task<ApiResponse<KioskDetailDto>> UpdateKioskConfigAsync(int kioskId, UpdateKioskConfigDto dto)
    {
        var kiosk = await _context.KioskDevices
            .Include(k => k.Branch)
            .FirstOrDefaultAsync(k => k.Id == (ulong)kioskId);

        if (kiosk == null)
        {
            return ApiResponse<KioskDetailDto>.Fail("Không tìm thấy thông tin trạm Kiosk.");
        }

        if (!string.IsNullOrWhiteSpace(dto.Name))
        {
            kiosk.Name = dto.Name.Trim();
        }

        kiosk.AllowedIp = string.IsNullOrWhiteSpace(dto.AllowedIp) ? null : dto.AllowedIp.Trim();
        kiosk.AllowedBrowser = string.IsNullOrWhiteSpace(dto.AllowedBrowser) ? null : dto.AllowedBrowser.Trim();

        if (!string.IsNullOrWhiteSpace(dto.Status))
        {
            var validStatus = dto.Status.Trim().ToUpper();
            if (validStatus != "ACTIVE" && validStatus != "LOCKED")
            {
                return ApiResponse<KioskDetailDto>.Fail("Trạng thái Kiosk không hợp lệ. Chỉ chấp nhận ACTIVE hoặc LOCKED.");
            }
            kiosk.Status = validStatus;
        }

        kiosk.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return await GetKioskByIdAsync(kioskId);
    }

    /// <summary>
    /// Khóa khẩn cấp hoặc Mở khóa trạm Kiosk quầy.
    /// </summary>
    public async Task<ApiResponse<KioskDetailDto>> UpdateKioskStatusAsync(int kioskId, UpdateKioskStatusDto dto)
    {
        var kiosk = await _context.KioskDevices
            .Include(k => k.Branch)
            .FirstOrDefaultAsync(k => k.Id == (ulong)kioskId);

        if (kiosk == null)
        {
            return ApiResponse<KioskDetailDto>.Fail("Không tìm thấy thông tin trạm Kiosk.");
        }

        if (string.IsNullOrWhiteSpace(dto.Status))
        {
            return ApiResponse<KioskDetailDto>.Fail("Vui lòng cung cấp trạng thái mới cho Kiosk.");
        }

        var normalizedStatus = dto.Status.Trim().ToUpper();
        if (normalizedStatus != "ACTIVE" && normalizedStatus != "LOCKED")
        {
            return ApiResponse<KioskDetailDto>.Fail("Trạng thái không hợp lệ. Chỉ hỗ trợ 'ACTIVE' hoặc 'LOCKED'.");
        }

        kiosk.Status = normalizedStatus;
        kiosk.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return await GetKioskByIdAsync(kioskId);
    }

    /// <summary>
    /// Kiểm tra IP client có nằm trong danh sách IP / dải IP cho phép.
    /// Hỗ trợ bỏ qua với localhost (127.0.0.1, ::1) trong môi trường thử nghiệm.
    /// </summary>
    private static bool IsIpAllowed(string clientIp, string allowedIpConfig)
    {
        if (clientIp == "127.0.0.1" || clientIp == "::1" || clientIp == "localhost")
        {
            return true;
        }

        var allowedList = allowedIpConfig.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var allowed in allowedList)
        {
            if (allowed == "*" || allowed.Equals(clientIp, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Hỗ trợ dạng wildcard 192.168.1.*
            if (allowed.EndsWith(".*"))
            {
                var prefix = allowed.Substring(0, allowed.Length - 1);
                if (clientIp.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Kiểm tra trình duyệt Client (User-Agent) có khớp với danh sách trình duyệt được phép.
    /// </summary>
    private static bool IsBrowserAllowed(string userAgent, string allowedBrowserConfig)
    {
        var allowedList = allowedBrowserConfig.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var allowed in allowedList)
        {
            if (allowed == "*" || userAgent.Contains(allowed, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
