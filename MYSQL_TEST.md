# MySQL Connection Testing Guide

## VnSQL Server Status
- ✅ Server is running on port 3306
- ✅ TCP connection successful
- ✅ MySQL protocol handler configured with configurable authentication
- ✅ SimpleMySQLHandler available for basic connection testing

## Test Connection Commands

### 1. Using MySQL Command Line Client

```bash
# Connect to VnSQL server
mysql -h localhost -P 3306 -u root -p

# When prompted for password, enter: password (or any password for SimpleMySQLHandler)
```

### 2. Using MySQL Workbench

1. Open MySQL Workbench
2. Create new connection with:
   - **Hostname**: `localhost`
   - **Port**: `3306`
   - **Username**: `root`
   - **Password**: `password`

### 3. Test Queries

After connecting, try these basic queries:

```sql
-- Show databases (placeholder response)
SHOW DATABASES;

-- Show tables (placeholder response)
SHOW TABLES;

-- Simple select (placeholder response)
SELECT 'Hello from VnSQL!' as message;
```

## MySQL Handlers

### SimpleMySQLHandler (Current)
- **Purpose**: Basic connection testing
- **Authentication**: Accepts any username/password
- **Features**: Simple handshake, basic OK responses, password hashing support
- **Use Case**: Testing basic connectivity

### MySQLProtocolHandler (Full Implementation)
- **Purpose**: Full MySQL protocol implementation
- **Authentication**: Configurable via `appsettings.json`, supports password hashing
- **Features**: Complete MySQL protocol support, proper password verification
- **Use Case**: Production use

## Password Hashing

Both handlers now properly handle MySQL password hashing:

- **MySQL 4.1+ Protocol**: Uses SHA1-based password hashing
- **Salt Generation**: Random salt generated for each handshake
- **Password Verification**: Supports proper password verification against config
- **Security**: Password is never transmitted as plain text

### How MySQL Password Hashing Works

1. **Server sends handshake packet** with a random 8-byte salt
2. **Client receives salt** and uses it to hash the password
3. **Client sends hashed password** in authentication packet
4. **Server verifies** by hashing the expected password with the same salt

### MySQL Password Hashing Algorithm

The algorithm follows these steps:

1. **Step 1**: `SHA1(password)` - Hash the plain text password
2. **Step 2**: `SHA1(salt + SHA1(password))` - Hash salt + first hash
3. **Step 3**: `XOR(Step1, Step2)` - XOR the two hashes together

### Implementation Details

The `MySQLPasswordHasher` utility class provides:
- `HashPassword(password, salt)`: Generates MySQL-compatible password hash
- `VerifyPassword(password, salt, hashedPassword)`: Verifies password against hash
- `GenerateSalt(length)`: Generates random salt for password hashing

### Authentication Process

1. **Handshake**: Server generates random salt and sends to client
2. **Client Processing**: Client hashes password using received salt
3. **Authentication**: Client sends username + hashed password
4. **Verification**: Server hashes expected password with same salt and compares
5. **Result**: Authentication succeeds if hashes match and username is correct

To switch between handlers, modify `Program.cs`:
```csharp
// For simple testing
services.AddSingleton<IProtocolHandler, SimpleMySQLHandler>();

// For full implementation
services.AddSingleton<IProtocolHandler, MySQLProtocolHandler>();
```

## Configuration

The MySQL authentication is now configurable via `appsettings.json`:

```json
{
  "VnSQL": {
    "Protocols": {
      "MySQL": {
        "Enabled": true,
        "Port": 3306,
        "Authentication": {
          "RootUsername": "root",
          "RootPassword": "password"
        }
      }
    }
  }
}
```

## Troubleshooting

### If connection fails:
1. Check if VnSQL server is running: `Get-Process | Where-Object {$_.ProcessName -like "*VnSQL*"}`
2. Check if port 3306 is listening: `netstat -an | findstr :3306`
3. Verify firewall settings
4. Check server logs for errors

### If authentication fails:
1. Verify username/password in `appsettings.json`
2. Restart the server after changing configuration
3. Check server logs for authentication errors

### If server hangs after authentication:
1. **Fixed**: Server now properly handles all MySQL commands including COM_QUIT, COM_PING, etc.
2. **Fixed**: Added proper logging for all received commands
3. **Fixed**: Server responds with OK packet for non-query commands
4. **Fixed**: Improved error handling in query loop

### Recent Fixes:
- **Interface Simplification**: Now uses the original `AuthenticateAsync(string username, string password)` interface instead of complex hashed password methods
- **Password Hashing**: Properly handles MySQL password hashing with salt (for future implementation)
- **Command Parsing**: Added support for all MySQL commands (COM_QUERY, COM_QUIT, COM_PING, etc.)
- **Query Loop**: Fixed hanging issue in HandleQueriesAsync
- **Packet Creation**: Fixed IndexOutOfRangeException in CreateColumnDefinitionPacket by using List<byte> instead of fixed-size array
- **Connection Handling**: Fixed ObjectDisposedException by adding proper error handling for disposed NetworkStream
- **Resource Management**: Improved connection cleanup with proper disposal of NetworkStream and TcpClient
- **Logging**: Enhanced logging for better debugging

## PowerShell Test Scripts

Run the included test scripts:

```powershell
# Basic connection test
.\test-connection.ps1

# Simple MySQL handler test (starts server automatically)
.\test-simple-connection.ps1

# Test authentication with proper password hashing
.\test-authentication.ps1

# Test password hashing algorithm
.\test-password-hash.ps1
```

These will test the TCP connection and show the MySQL client command.
