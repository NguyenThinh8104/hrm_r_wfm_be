namespace Shared.Security;

/// <summary>
/// Dịch vụ xử lý mã hóa và xác thực mật khẩu / mã PIN nhân viên bằng thuật toán BCrypt.
/// Hỗ trợ gọi trực tiếp qua static method hoặc inject qua interface IPasswordHasher.
/// </summary>
public class PasswordHasher : IPasswordHasher
{
    /// <summary>
    /// Phương thức tĩnh tạo mã băm (hash) BCrypt từ chuỗi văn bản thô.
    /// </summary>
    /// <param name="plainText">Văn bản thô (Mật khẩu hoặc mã PIN)</param>
    /// <returns>Chuỗi mã băm BCrypt</returns>
    public static string Hash(string plainText)
    {
        return BCrypt.Net.BCrypt.HashPassword(plainText, workFactor: 11);
    }

    /// <summary>
    /// Phương thức tĩnh kiểm tra chuỗi văn bản thô với mã băm hash trong DB.
    /// </summary>
    /// <param name="plainText">Văn bản thô (Mật khẩu hoặc mã PIN)</param>
    /// <param name="hash">Mã băm lưu trong DB</param>
    /// <returns>True nếu trùng khớp, ngược lại False</returns>
    public static bool Verify(string plainText, string hash)
    {
        if (string.IsNullOrEmpty(plainText) || string.IsNullOrEmpty(hash))
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(plainText, hash);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Thực thi phương thức Hash từ IPasswordHasher.
    /// </summary>
    string IPasswordHasher.Hash(string plainText) => Hash(plainText);

    /// <summary>
    /// Thực thi phương thức Verify từ IPasswordHasher.
    /// </summary>
    bool IPasswordHasher.Verify(string plainText, string hash) => Verify(plainText, hash);
}
