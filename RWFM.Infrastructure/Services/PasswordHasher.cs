namespace RWFM.Infrastructure.Services;

public class PasswordHasher
{
    public static string Hash(string value)
    {
        return BCrypt.Net.BCrypt.HashPassword(value, workFactor: 11);
    }

    public static bool Verify(string value, string hash)
    {
        if (string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(value)) return false;
        try
        {
            return BCrypt.Net.BCrypt.Verify(value, hash);
        }
        catch
        {
            return false;
        }
    }
}
