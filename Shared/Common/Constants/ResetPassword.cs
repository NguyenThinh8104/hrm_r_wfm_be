using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Common.Constants
{
    public static class ResetPassword
    {
        public const string OTP_SENT = "Mã OTP đã được gửi đến email của bạn. Vui lòng kiểm tra hộp thư đến.";
        public const string OTP_INVALID = "Mã OTP không chính xác. Vui lòng kiểm tra lại.";
        public const string PASSWORD_RESET_SUCCESS = "Đặt lại mật khẩu thành công. Bạn có thể đăng nhập bằng mật khẩu mới.";
        public const string OTP_EXPIRED = "Mã OTP đã hết hạn (quá 5 phút) hoặc không tồn tại. Vui lòng thao tác lại.";
        public const string EMAIL_NOT_FOUND = "Không tìm thấy tài khoản với email này. Vui lòng kiểm tra lại.";
        public const string OTP_SUCCESS = "Xác thực mã OTP thành công. Bạn có thể đặt lại mật khẩu mới.";
        public const string USER_NOT_FOUND = "Tài khoản người dùng không tồn tại hoặc đã bị vô hiệu hóa.";
    }
}
