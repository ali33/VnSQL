# SQL Server Protocol Handler for VnSQL

## Overview

The SQL Server Protocol Handler implements the TDS (Tabular Data Stream) protocol to provide SQL Server compatibility for VnSQL. This allows SQL Server clients to connect to VnSQL as if it were a real SQL Server instance.

## Features

- **TDS Protocol Support**: Implements basic TDS protocol for SQL Server client compatibility
- **Authentication**: Supports SQL Server authentication with username/password
- **SQL Command Execution**: Executes SQL commands through the shared QueryExecutor
- **Response Formatting**: Formats responses according to SQL Server standards
- **Connection Management**: Handles connection lifecycle and cleanup

## Configuration

### appsettings.json

```json
{
  "VnSQL": {
    "Protocols": {
      "SQLServer": {
        "Enabled": true,
        "Port": 1433,
        "Host": "127.0.0.1",
        "MaxConnections": 100,
        "InstanceName": "SQLEXPRESS",
        "Authentication": {
          "Username": "sa",
          "Password": "password",
          "Database": "master",
          "WindowsAuthentication": false
        },
        "Ssl": {
          "Enabled": false,
          "CertificatePath": "",
          "KeyPath": ""
        }
      }
    }
  }
}
```

### Configuration Options

- **Port**: SQL Server port (default: 1433)
- **Host**: Server host address
- **MaxConnections**: Maximum concurrent connections
- **InstanceName**: SQL Server instance name
- **Authentication**: Username/password for SQL Server authentication
- **Ssl**: SSL/TLS configuration (currently not implemented)

## TDS Protocol Implementation

### Packet Types Supported

- **Pre-Login (0x01)**: Initial handshake packet
- **Login7 (0x10)**: Authentication packet
- **SQL Batch (0x01)**: SQL command execution
- **Column Metadata (0x81)**: Column information
- **Row Data (0xD1)**: Data rows
- **Done (0xFD)**: Completion packet
- **Error (0xAA)**: Error response
- **Attention (0x0E)**: Client attention signal

### Connection Flow

1. **Pre-Login Handshake**: Server sends pre-login packet with capabilities
2. **Login Authentication**: Client sends login packet, server validates credentials
3. **Login Response**: Server sends success/failure response
4. **Query Processing**: Client sends SQL batches, server executes and responds
5. **Connection Cleanup**: Proper connection termination

## Usage

### Starting the Server

```bash
dotnet run --project src/VnSQL.Server
```

### Testing with SQL Server Clients

#### Using sqlcmd

```bash
sqlcmd -S localhost,1433 -U sa -P password
```

#### Using SQL Server Management Studio (SSMS)

1. Connect to server: `localhost,1433`
2. Authentication: SQL Server Authentication
3. Login: `sa`
4. Password: `password`

#### Using Azure Data Studio

1. Create new connection
2. Server: `localhost,1433`
3. Authentication Type: SQL Login
4. User name: `sa`
5. Password: `password`

## Supported SQL Commands

The SQL Server Handler supports all standard SQL commands through the shared QueryExecutor:

- **DDL**: CREATE, DROP, ALTER
- **DML**: SELECT, INSERT, UPDATE, DELETE
- **DCL**: GRANT, REVOKE
- **System Commands**: SHOW DATABASES, SHOW TABLES, etc.

## Limitations

### Current Limitations

1. **Simplified TDS Implementation**: Only basic TDS protocol features are implemented
2. **Limited Data Types**: All data types are treated as VARCHAR for simplicity
3. **No SSL/TLS**: SSL encryption is not yet implemented
4. **No Windows Authentication**: Only SQL Server authentication is supported
5. **No Stored Procedures**: Stored procedure execution is not supported
6. **No Transactions**: Transaction management is not implemented

### Future Enhancements

1. **Full TDS Protocol**: Complete TDS protocol implementation
2. **Data Type Support**: Proper SQL Server data type mapping
3. **SSL/TLS Support**: Encrypted connections
4. **Windows Authentication**: Integrated security
5. **Stored Procedures**: T-SQL stored procedure support
6. **Transaction Management**: ACID transaction support
7. **Connection Pooling**: Efficient connection management

## Testing

### Automated Testing

Run the test script:

```powershell
.\test-sqlserver-connection.ps1
```

### Manual Testing

1. Start VnSQL server
2. Connect with SQL Server client
3. Execute test queries:

```sql
-- Test basic commands
SELECT 1 as test_column;
CREATE TABLE test_table (id INT, name VARCHAR(255));
INSERT INTO test_table VALUES (1, 'test');
SELECT * FROM test_table;
DROP TABLE test_table;
```

## Troubleshooting

### Common Issues

1. **Connection Refused**: Ensure server is running and port 1433 is available
2. **Authentication Failed**: Check username/password in configuration
3. **Protocol Errors**: Verify client is using TDS protocol
4. **Query Errors**: Check SQL syntax and table existence

### Debugging

Enable detailed logging in appsettings.json:

```json
{
  "Logging": {
    "LogLevel": {
      "VnSQL.Protocols.Handlers.SQLServerProtocolHandler": "Debug"
    }
  }
}
```

## Architecture

The SQL Server Handler follows the same architecture as other protocol handlers:

```
SQLServerProtocolHandler
├── TDS Protocol Implementation
├── Authentication Handler
├── Query Executor Integration
├── Response Formatter
└── Connection Manager
```

## Dependencies

- **VnSQL.Core**: Core interfaces and models
- **VnSQL.Protocols.Configuration**: Configuration classes
- **Microsoft.Extensions.Logging**: Logging infrastructure
- **Microsoft.Extensions.Options**: Configuration management

## Contributing

To extend the SQL Server Handler:

1. Implement additional TDS packet types
2. Add support for more SQL Server features
3. Improve error handling and logging
4. Add comprehensive unit tests
5. Update documentation

## License

This SQL Server Handler is part of the VnSQL project and follows the same licensing terms.
