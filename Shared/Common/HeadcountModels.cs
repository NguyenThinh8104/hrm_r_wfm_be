namespace Shared.Common;

/// <summary>
/// Kết quả kiểm tra và trừ lùi định biên nhân sự chi nhánh (Headcount Validation Result).
/// </summary>
public class HeadcountValidationResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsOverride { get; set; }
    public ulong? ImportRequestId { get; set; }
    public int CurrentCount { get; set; }
    public int Quota { get; set; }
    public int RemainingAdditionalQuantity { get; set; }

    public static HeadcountValidationResult Success(bool isOverride = false, ulong? importRequestId = null, int currentCount = 0, int quota = 0, int remaining = 0)
    {
        return new HeadcountValidationResult
        {
            IsSuccess = true,
            IsOverride = isOverride,
            ImportRequestId = importRequestId,
            CurrentCount = currentCount,
            Quota = quota,
            RemainingAdditionalQuantity = remaining
        };
    }

    public static HeadcountValidationResult Fail(string message, int currentCount = 0, int quota = 0)
    {
        return new HeadcountValidationResult
        {
            IsSuccess = false,
            ErrorMessage = message,
            CurrentCount = currentCount,
            Quota = quota
        };
    }
}
