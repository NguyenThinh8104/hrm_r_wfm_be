namespace Shared.Common;

/// <summary>
/// Kết quả thẩm định định biên nhân sự chi nhánh (Headcount Validation Result theo Effective Quota).
/// </summary>
public class HeadcountValidationResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public int CurrentCount { get; set; }
    public int EffectiveQuota { get; set; }
    public int Quota => EffectiveQuota;
    public int RemainingSlots => Math.Max(0, EffectiveQuota - CurrentCount);

    public static HeadcountValidationResult Success(int currentCount = 0, int effectiveQuota = 0)
    {
        return new HeadcountValidationResult
        {
            IsSuccess = true,
            CurrentCount = currentCount,
            EffectiveQuota = effectiveQuota
        };
    }

    public static HeadcountValidationResult Fail(string message, int currentCount = 0, int effectiveQuota = 0)
    {
        return new HeadcountValidationResult
        {
            IsSuccess = false,
            ErrorMessage = message,
            CurrentCount = currentCount,
            EffectiveQuota = effectiveQuota
        };
    }
}
