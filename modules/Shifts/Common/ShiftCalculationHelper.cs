using System;

namespace Modules.Shifts.Common;

/// <summary>
/// Helper tính toán giờ công chuẩn (paidHours), giờ làm ca đêm (nightHours), nhận diện ca qua đêm và tự suy ra ShiftType.
/// </summary>
public static class ShiftCalculationHelper
{
    /// <summary>
    /// Tính toán giờ công được trả (PaidHours), giờ làm trong khung đêm 22:00-06:00 (NightHours), và cờ IsOvernight.
    /// </summary>
    public static (double PaidHours, double NightHours, bool IsOvernight) CalculateHours(
        TimeOnly startTime,
        TimeOnly endTime,
        uint breakDurationMinutes)
    {
        // 9. IsOvernight luôn do server tính: EndTime < StartTime
        bool isOvernight = endTime < startTime;

        int startMin = startTime.Hour * 60 + startTime.Minute;
        int endMin = endTime.Hour * 60 + endTime.Minute;

        int totalDurationMin = isOvernight ? (24 * 60 - startMin + endMin) : (endMin - startMin);

        // 13. paidHours = (EndTime - StartTime, cộng 24h nếu qua đêm) - BreakDuration (tính bằng giờ)
        double paidMinutes = Math.Max(0, totalDurationMin - (int)breakDurationMinutes);
        double paidHours = Math.Round(paidMinutes / 60.0, 2);

        // 13. nightHours = số giờ giao với khoảng 22:00–06:00
        // Khung đêm chuẩn BLLĐ VN: 22:00 (1320 phút) đến 06:00 (360 phút) sáng hôm sau.
        double nightMinutes = 0;
        if (!isOvernight)
        {
            // Ca trong ngày [startMin, endMin]
            // Giao với [00:00, 06:00] (0 đến 360 phút)
            int earlyOverlap = Math.Max(0, Math.Min(endMin, 360) - Math.Max(startMin, 0));
            // Giao với [22:00, 24:00] (1320 đến 1440 phút)
            int lateOverlap = Math.Max(0, Math.Min(endMin, 1440) - Math.Max(startMin, 1320));
            nightMinutes = earlyOverlap + lateOverlap;
        }
        else
        {
            // Ca qua đêm:
            // Đoạn 1 (Ngày 1): [startMin, 1440] giao với đêm ngày 1 [1320, 1440]
            int day1Night = Math.Max(0, 1440 - Math.Max(startMin, 1320));

            // Đoạn 2 (Ngày 2): [0, endMin] giao với đêm ngày 2 [0, 360]
            int day2Night = Math.Max(0, Math.Min(endMin, 360) - 0);

            nightMinutes = day1Night + day2Night;
        }

        double nightHours = Math.Round(nightMinutes / 60.0, 2);

        return (paidHours, nightHours, isOvernight);
    }

    public static bool IsOvernight(TimeOnly startTime, TimeOnly endTime) => endTime < startTime;

    public static double CalculatePaidHours(TimeOnly startTime, TimeOnly endTime, uint breakDurationMinutes)
        => CalculateHours(startTime, endTime, breakDurationMinutes).PaidHours;

    public static double CalculateNightHours(TimeOnly startTime, TimeOnly endTime)
        => CalculateHours(startTime, endTime, 0).NightHours;

    /// <summary>
    /// Suy luận ShiftType từ StartTime nếu không được cung cấp:
    /// 05:00 - 11:59 -> SANG
    /// 12:00 - 19:59 -> CHIEU
    /// 20:00 - 04:59 -> DEM
    /// Còn lại -> KHAC
    /// </summary>
    public static string InferShiftType(TimeOnly startTime)
    {
        if (startTime >= new TimeOnly(5, 0) && startTime < new TimeOnly(12, 0))
            return "SANG";
        if (startTime >= new TimeOnly(12, 0) && startTime < new TimeOnly(20, 0))
            return "CHIEU";
        if (startTime >= new TimeOnly(20, 0) || startTime < new TimeOnly(5, 0))
            return "DEM";
        return "KHAC";
    }

    /// <summary>
    /// Format TimeOnly sang định dạng "HH:mm".
    /// </summary>
    public static string FormatTime(TimeOnly time) => time.ToString("HH:mm");

    /// <summary>
    /// Parse chuỗi giờ ("HH:mm" hoặc "HH:mm:ss") sang TimeOnly an toàn.
    /// </summary>
    public static bool TryParseTime(string? timeStr, out TimeOnly time)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(timeStr)) return false;
        timeStr = timeStr.Trim();
        return TimeOnly.TryParse(timeStr, out time);
    }
}
