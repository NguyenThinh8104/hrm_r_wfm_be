namespace Shared.Security;

/// <summary>
/// Interface dịch vụ mã hóa và xác thực mật khẩu / mã PIN nhân viên.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Tạo mã băm (hash) từ chuỗi văn bản thô (mật khẩu hoặc mã PIN).
    /// </summary>
    /// <param name="plainText">Chuỗi văn bản thô cần mã hóa</param>
    /// <returns>Chuỗi mã băm an toàn theo chuẩn BCrypt</returns>
    string Hash(string plainText);

    /// <summary>
    /// Kiểm tra chuỗi văn bản thô (mã PIN) có khớp với mã băm hash lưu trong DB hay không.
    /// </summary>
    /// <param name="plainText">Chuỗi văn bản thô nhập từ màn hình Kiosk</param>
    /// <param name="hash">Chuỗi mã băm KioskPinHash trong cơ sở dữ liệu</param>
    /// <returns>True nếu mã PIN hợp lệ, ngược lại trả về False</returns>
    bool Verify(string plainText, string hash);
}
