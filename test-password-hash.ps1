Write-Host "Testing MySQL Password Hashing Algorithm..." -ForegroundColor Green

# Test the hashing algorithm
$password = "password"
$salt = [byte[]]@(0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08)

Write-Host "Password: $password" -ForegroundColor Yellow
Write-Host "Salt: $([Convert]::ToBase64String($salt))" -ForegroundColor Yellow

# Build and run a simple test
$testCode = @"
using System;
using System.Security.Cryptography;
using System.Text;

public class MySQLHashTest
{
    public static byte[] HashPassword(string password, byte[] salt)
    {
        if (string.IsNullOrEmpty(password))
            return new byte[0];
            
        using var sha1 = SHA1.Create();
        
        // Step 1: SHA1(password)
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var firstHash = sha1.ComputeHash(passwordBytes);
        
        Console.WriteLine("Step 1 - SHA1(password): " + Convert.ToBase64String(firstHash));
        
        // Step 2: SHA1(salt + SHA1(password))
        var combined = new byte[salt.Length + firstHash.Length];
        Buffer.BlockCopy(salt, 0, combined, 0, salt.Length);
        Buffer.BlockCopy(firstHash, 0, combined, salt.Length, firstHash.Length);
        
        var secondHash = sha1.ComputeHash(combined);
        Console.WriteLine("Step 2 - SHA1(salt + SHA1(password)): " + Convert.ToBase64String(secondHash));
        
        // Step 3: XOR of first hash and second hash
        var result = new byte[firstHash.Length];
        for (int i = 0; i < firstHash.Length; i++)
        {
            result[i] = (byte)(firstHash[i] ^ secondHash[i]);
        }
        
        return result;
    }
    
    public static void Main()
    {
        var password = "$password";
        var salt = new byte[] { $($salt -join ', ') };
        
        Console.WriteLine("Testing MySQL Password Hashing");
        Console.WriteLine("Password: " + password);
        Console.WriteLine("Salt: " + Convert.ToBase64String(salt));
        Console.WriteLine();
        
        var result = HashPassword(password, salt);
        Console.WriteLine("Final Hash: " + Convert.ToBase64String(result));
    }
}
"@

# Save test code to file
$testCode | Out-File -FilePath "MySQLHashTest.cs" -Encoding UTF8

# Compile and run
try {
    Write-Host "Compiling test..." -ForegroundColor Yellow
    dotnet new console --name MySQLHashTest --force
    Copy-Item "MySQLHashTest.cs" "MySQLHashTest/Program.cs" -Force
    
    Set-Location "MySQLHashTest"
    dotnet run
    Set-Location ".."
}
catch {
    Write-Host "Error running test: $($_.Exception.Message)" -ForegroundColor Red
}
finally {
    # Cleanup
    if (Test-Path "MySQLHashTest.cs") { Remove-Item "MySQLHashTest.cs" }
    if (Test-Path "MySQLHashTest") { Remove-Item "MySQLHashTest" -Recurse -Force }
}

Write-Host "`nTest completed!" -ForegroundColor Green
