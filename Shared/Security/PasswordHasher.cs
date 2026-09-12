namespace Shared.Security;

public static class PasswordHasher
{
    public static string Hash(string plainText)
    {
        return BCrypt.Net.BCrypt.HashPassword(plainText, workFactor: 11);
    }

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
}

