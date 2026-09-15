namespace Shared.Common.Constants;

public static class AuthMessages
{
    // Login
    public const string USERNAME_PASSWORD_INVALID = "Vui lòng nhập tên đăng nhập và mật khẩu.";
    public const string INVALID_CREDENTIALS = "Tên đăng nhập hoặc mật khẩu không chính xác.";
    public const string ACCOUNT_LOCKED = "Tài khoản người dùng đang bị khóa.";
    public const string LOGIN_SUCCESS = "Đăng nhập thành công.";

    // Kiosk Login
    public const string KIOSK_LOGIN_REQUIRED = "Vui lòng nhập mã nhân viên và mã PIN.";
    public const string EMPLOYEE_NOT_FOUND = "Không tìm thấy nhân viên với mã này.";
    public const string PIN_INVALID = "Mã PIN không chính xác.";
    public const string NOT_ASSIGNED_TO_BRANCH = "Nhân viên {0} không thuộc chi nhánh này và không có lệnh điều động hợp lệ.";
    public const string KIOSK_LOGIN_SUCCESS = "Xác thực Kiosk thành công.";

    // User Info
    public const string USER_NOT_FOUND = "Không tìm thấy thông tin người dùng.";
    public const string USER_IDENTITY_NOT_FOUND = "Không xác định được danh tính người dùng.";

    // Forgot / Reset Password
    public const string EMAIL_NOT_IN_SYSTEM = "Email này không tồn tại trong hệ thống nhân viên.";
    public const string ACCOUNT_INACTIVE = "Tài khoản người dùng hiện đang bị khóa hoặc ngừng hoạt động.";
    public const string EMAIL_SEND_FAILED = "Hệ thống tạm thời không thể gửi email. Vui lòng kiểm tra lại kết nối mạng hoặc thử lại sau ít phút.";
    public const string OTP_SENT_SUCCESS = "Mã xác thực OTP đã được gửi đến hòm thư {0}. Mã có hiệu lực trong {1} phút.";

    // Google Login
    public const string ID_TOKEN_REQUIRED = "IdToken không được để trống.";
    public const string GOOGLE_TOKEN_INVALID = "Mã xác thực Google không hợp lệ hoặc đã hết hạn.";
    public const string GOOGLE_EMAIL_UNVERIFIED = "Tài khoản Google chưa được xác minh email.";
    public const string GOOGLE_ACCOUNT_NOT_FOUND = "Tài khoản Google ({0}) chưa được cấp quyền trong hệ thống. Vui lòng liên hệ Quản trị viên.";
    public const string GOOGLE_LOGIN_SUCCESS = "Đăng nhập bằng Google thành công.";
}
