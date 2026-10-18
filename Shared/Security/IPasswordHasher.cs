namespace Shared.Security;

/// <summary>
/// Interface dịch vụ mã hóa và xác thực mật khẩu người dùng.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Tạo mã băm (hash) từ chuỗi văn bản thô (mật khẩu).
    /// </summary>
    /// <param name="plainText">Chuỗi văn bản thô cần mã hóa</param>
    /// <returns>Chuỗi mã băm an toàn theo chuẩn BCrypt</returns>
    string Hash(string plainText);

    /// <summary>
    /// Kiểm tra chuỗi văn bản thô có khớp với chuỗi mã băm hash lưu trong DB hay không.
    /// </summary>
    /// <param name="plainText">Chuỗi văn bản thô cần xác thực</param>
    /// <param name="hash">Chuỗi mã băm trong cơ sở dữ liệu</param>
    /// <returns>True nếu mật khẩu hợp lệ, ngược lại trả về False</returns>
    bool Verify(string plainText, string hash);
}
