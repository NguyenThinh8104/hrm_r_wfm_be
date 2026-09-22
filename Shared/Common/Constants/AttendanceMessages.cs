namespace Shared.Common.Constants;

public static class AttendanceMessages
{
    public const string PinValidationSuccess = "Xác thực mã PIN nhân viên thành công.";
    public const string InvalidPin = "Mã PIN không chính xác.";
    public const string InvalidPinOrOtp = "Mã xác thực OTP (hoặc PIN) không chính xác hoặc đã hết hạn.";
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
    public const string NoScheduleAtBranch = "Bạn không có lịch làm việc tại chi nhánh này trong ngày hôm nay. Vui lòng liên hệ Quản lý!";
    public const string AlreadyCheckedInShift = "Ca làm việc này đã được check-in trước đó.";
    public const string TooEarlyForCheckIn = "Chưa đến giờ điểm danh. Chỉ được check-in sớm tối đa 30 phút trước ca.";
    public const string CashierOpeningFloatRequired = "Thu ngân bắt buộc phải khai báo tiền lẻ đầu ca.";
    public const string NoOpenAttendanceLogForCheckOut = "Không tìm thấy phiên làm việc đang mở để check-out.";
    public const string InvalidKioskDeviceToken = "Mã định danh Kiosk DeviceToken không hợp lệ.";
    public const string KioskNotActive = "Thiết bị trạm Kiosk không hợp lệ hoặc chưa được kích hoạt.";
    public const string InvalidCheckInOtp = "Mã OTP Check-in không chính xác, đã được sử dụng hoặc hết hạn 60 giây.";
    public const string InvalidCheckOutOtp = "Mã OTP Check-out không chính xác, đã sử dụng hoặc hết hạn 60 giây.";
    public const string UploadPhotoSuccess = "Tải ảnh xác thực điểm danh lên S3 thành công.";
    public const string UploadPhotoFailed = "Vui lòng chọn file ảnh hợp lệ để tải lên.";
    public const string GetPresignedUrlSuccess = "Tạo liên kết xem ảnh tạm thời thành công.";
    public const string InvalidS3Key = "Mã định danh ảnh S3 (photoKey) không hợp lệ.";
    public const string LocationRequired = "Vui lòng bật định vị GPS và cho phép ứng dụng truy cập vị trí của bạn để yêu cầu mã OTP điểm danh.";
    public const string LocationOutOfGeofence = "Vị trí của bạn hiện cách cửa hàng {0} khoảng {1}m, vượt quá bán kính điểm danh cho phép ({2}m).";
    public const string NoShiftOrDispatchToday = "Bạn không có ca làm việc được xếp lịch hoặc lệnh điều động hôm nay tại bất kỳ chi nhánh nào. Vui lòng liên hệ Quản lý!";
    public const string DispatchedButNoShiftToday = "Bạn có lệnh điều động đến chi nhánh {0} nhưng chưa được xếp lịch ca trực hôm nay tại chi nhánh này. Vui lòng liên hệ Quản lý!";
    public const string NoShiftTodayForOtp = "Bạn không có ca làm việc được xếp lịch hôm nay tại bất kỳ chi nhánh nào. Vui lòng liên hệ Quản lý!";
    public const string BranchLocationNotConfigured = "Cửa hàng {0} chưa được cấu hình tọa độ vị trí GPS thực tế.";
    public const string OtpGeneratedSuccess = "Đã cấp mã OTP {0} 60 giây thành công.";
    public const string InvalidAttendanceOtp = "Mã OTP chấm công không chính xác, đã được sử dụng hoặc hết hạn.";
    public const string OtpTypeMismatch = "Loại mã OTP không khớp với thao tác yêu cầu ({0}).";
    public const string UploadAttendancePhotoV3Success = "Tải ảnh chấm công lên hệ thống thành công. Bản ghi đã hoàn tất.";
    public const string UploadAttendancePhotoV3Failed = "Không thể tải ảnh chấm công lên hệ thống. Vui lòng thử lại.";
    public const string AttendanceLogNotOwnedByKiosk = "Bản ghi chấm công không thuộc trạm Kiosk này.";
    public const string AttendanceLogAlreadyCompleted = "Bản ghi chấm công đã được hoàn tất trước đó.";
    public const string InvalidPhotoType = "Loại ảnh chấm công không hợp lệ. Chỉ chấp nhận CHECK_IN hoặc CHECK_OUT.";
    public const string ShiftAlreadyEnded = "Ca làm việc {0} đã kết thúc lúc {1}. Không thể điểm danh vào ca đã qua.";
    public const string TooEarlyForShift = "Chưa đến giờ điểm danh ca {0} ({1}). Chỉ được điểm danh sớm tối đa 30 phút (từ {2}).";
    public const string NoActiveShiftInTimeframe = "Hiện tại không có ca làm việc nào phù hợp trong khung giờ này. Vui lòng kiểm tra lại lịch phân công!";
    public const string CheckInLateSuccess = "Điểm danh VÀO CA MUỘN (LATE) lúc {0}! Giờ bắt đầu ca là {1}.";
}



