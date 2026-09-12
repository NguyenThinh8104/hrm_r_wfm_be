namespace Shared.Common.Constants;

public static class AttendanceMessages
{
    public const string PinValidationSuccess = "Xác thực mã PIN nhân viên thành công.";
    public const string InvalidPin = "Mã PIN không chính xác.";
    public const string EmployeeNotFound = "Không tìm thấy thông tin nhân viên.";
    public const string EmployeeInactive = "Tài khoản nhân viên đã bị khóa hoặc ngừng hoạt động.";
    public const string NoShiftToday = "Nhân viên {0} không có lịch trực hôm nay ({1}) tại chi nhánh này.";
    public const string AlreadyCheckedIn = "Nhân viên đã check-in ca này rồi.";
    public const string CheckInSuccess = "Check-in thành công lúc {0}!";
    public const string CheckOutSuccess = "Check-out thành công lúc {0}. Hẹn gặp lại bạn!";
    public const string NotCheckedIn = "Chưa có lượt Check-in đầu ca hoặc đã Check-out trước đó.";
    public const string ShiftNotFound = "Không tìm thấy ca trực tương ứng.";
    public const string FraudReportSuccess = "Đã ghi nhận báo cáo gian lận ca và chuyển ngoại lệ lên Cửa hàng trưởng.";
    public const string AttendanceRecordNotFound = "Không tìm thấy bản ghi chấm công.";
    public const string LeaderIdentityNotFound = "Không xác định được danh tính Trưởng ca.";
}

