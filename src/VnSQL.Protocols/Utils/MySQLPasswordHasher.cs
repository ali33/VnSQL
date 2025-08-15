using System.Security.Cryptography;
using System.Text;

namespace VnSQL.Protocols.Utils;

/// <summary>
/// MySQL Password Hashing Utility
/// </summary>
public static class MySQLPasswordHasher
{
    /// <summary>
    /// MySQL 4.1+ password hashing (SHA1)
    /// This implements the exact algorithm used by MySQL client
    /// </summary>
    public static byte[] HashPassword(string password, byte[] salt)
    {
        if (string.IsNullOrEmpty(password))
            return new byte[0];
            
        // MySQL 4.1+ uses SHA1 for password hashing
        using var sha1 = SHA1.Create();
        
        // Step 1: SHA1(password)
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var firstHash = sha1.ComputeHash(passwordBytes);
        
        // Step 2: SHA1(salt + SHA1(password))
        var combined = new byte[salt.Length + firstHash.Length];
        Buffer.BlockCopy(salt, 0, combined, 0, salt.Length);
        Buffer.BlockCopy(firstHash, 0, combined, salt.Length, firstHash.Length);
        
        var secondHash = sha1.ComputeHash(combined);
        
        // Step 3: XOR of first hash and second hash
        var result = new byte[firstHash.Length];
        for (int i = 0; i < firstHash.Length; i++)
        {
            result[i] = (byte)(firstHash[i] ^ secondHash[i]);
        }
        
        return result;
    }
    
    /// <summary>
    /// Verify password against hashed password
    /// </summary>
    public static bool VerifyPassword(string password, byte[] salt, byte[] hashedPassword)
    {
        var computedHash = HashPassword(password, salt);
        
        if (computedHash.Length != hashedPassword.Length)
            return false;
            
        for (int i = 0; i < computedHash.Length; i++)
        {
            if (computedHash[i] != hashedPassword[i])
                return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Generate random salt for password hashing
    /// </summary>
    public static byte[] GenerateSalt(int length = 20)
    {
        var salt = new byte[length];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(salt);
        return salt;
    }
}
