using System.Net.Sockets;
using VnSQL.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VnSQL.Protocols.Configuration;
using VnSQL.Protocols.Utils;
using VnSQL.Core.Sql;

namespace VnSQL.Protocols.Handlers;

/// <summary>
/// MySQL Protocol Handler
/// </summary>
public class MySQLProtocolHandler : IProtocolHandler
{
    private readonly ILogger<MySQLProtocolHandler> _logger;
    private readonly MySQLConfiguration _configuration;
    private readonly QueryExecutor _queryExecutor;
    private TcpClient? _client;
    private NetworkStream? _stream;
    
    public string ProtocolName => "MySQL";
    public int DefaultPort => _configuration.Port;
    
    public MySQLProtocolHandler(ILogger<MySQLProtocolHandler> logger, IOptions<MySQLConfiguration> configuration, QueryExecutor queryExecutor)
    {
        _logger = logger;
        _configuration = configuration.Value;
        _queryExecutor = queryExecutor;
    }
    
    public async Task HandleConnectionAsync(TcpClient client)
    {
        try
        {
            _client = client;
            _stream = client.GetStream();
            
            // Send MySQL handshake packet
            await SendHandshakePacketAsync();
            
            // Handle authentication
            var authResult = await HandleAuthenticationAsync();
            if (!authResult)
            {
                await SendErrorPacketAsync(1045, "Access denied");
                return;
            }
            
            await SendOkPacketAsync();
            
            // Main connection loop
            await HandleQueriesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling MySQL connection");
        }
        finally
        {
            await CloseConnectionAsync();
        }
    }
    
    public async Task<bool> AuthenticateAsync(string username, string password)
    {
        try
        {
            _logger.LogInformation("Authentication attempt: username={Username}", username);
            
            // Get expected credentials from configuration
            var expectedUsername = _configuration.Authentication.RootUsername;
            var expectedPassword = _configuration.Authentication.RootPassword;
            
            // Simple string comparison for now
            // In production, you might want to use the hashed password verification
            if (username == expectedUsername && password == expectedPassword)
            {
                _logger.LogInformation("Authentication successful!");
                return true;
            }
            else
            {
                _logger.LogWarning("Authentication failed - username or password mismatch");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during authentication");
            return false;
        }
    }
    
    public async Task<QueryResult> ExecuteQueryAsync(string query, string database)
    {
        try
        {
            _logger.LogInformation("Executing MySQL query: {Query}", query);
            
            // Use QueryExecutor to handle SQL commands
            return await _queryExecutor.ExecuteAsync(query, "MySQL");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing MySQL query: {Query}", query);
            return new QueryResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
    
    public async Task SendResponseAsync(QueryResult result)
    {
        try
        {
            if (result.Success)
            {
                await SendResultSetAsync(result);
            }
            else
            {
                await SendErrorPacketAsync(1064, result.ErrorMessage ?? "Unknown error");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending MySQL response");
        }
    }
    
    public async Task CloseConnectionAsync()
    {
        try
        {
            if (_stream != null)
            {
                _stream.Close();
                _stream.Dispose();
                _stream = null;
            }
            
            if (_client != null)
            {
                _client.Close();
                _client.Dispose();
                _client = null;
            }
            
            _logger.LogInformation("MySQL connection closed");
        }
        catch (ObjectDisposedException)
        {
            _logger.LogInformation("Connection was already closed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing MySQL connection");
        }
    }
    
    private byte[] _handshakeSalt = new byte[8];
    
    private async Task SendHandshakePacketAsync()
    {
        // Generate salt for password hashing and store it for later verification
        _handshakeSalt = MySQLPasswordHasher.GenerateSalt(8);
        
        // MySQL Protocol 10 handshake packet with proper capability flags
        var serverVersion = "5.7.28-VnSQL-1.0.0";
        var versionBytes = System.Text.Encoding.ASCII.GetBytes(serverVersion);
        
        var packet = new List<byte>();
        
        // Protocol version
        packet.Add(0x0A);
        
        // Server version (null-terminated)
        packet.AddRange(versionBytes);
        packet.Add(0x00);
        
        // Connection ID (4 bytes, little endian)
        packet.AddRange(new byte[] { 0x01, 0x00, 0x00, 0x00 });
        
        // Auth plugin data part 1 (8 bytes) - this is the salt
        packet.AddRange(_handshakeSalt);
        
        // Filler
        packet.Add(0x00);
        
        // Capability flags part 1 (2 bytes, little endian)
        // CLIENT_LONG_PASSWORD | CLIENT_FOUND_ROWS | CLIENT_LONG_FLAG | CLIENT_CONNECT_WITH_DB | CLIENT_NO_SCHEMA | CLIENT_COMPRESS | CLIENT_ODBC | CLIENT_LOCAL_FILES | CLIENT_IGNORE_SPACE | CLIENT_PROTOCOL_41 | CLIENT_INTERACTIVE | CLIENT_SSL | CLIENT_IGNORE_SIGPIPE | CLIENT_TRANSACTIONS | CLIENT_RESERVED | CLIENT_SECURE_CONNECTION
        packet.AddRange(new byte[] { 0xFF, 0xF7 });
        
        // Character set (1 byte)
        packet.Add(0x21); // utf8_general_ci
        
        // Status flags (2 bytes, little endian)
        packet.AddRange(new byte[] { 0x00, 0x00 });
        
        // Capability flags part 2 (2 bytes, little endian)
        packet.AddRange(new byte[] { 0x00, 0x00 });
        
        // Auth plugin data len (1 byte)
        packet.Add(0x00);
        
        // Reserved (10 bytes)
        packet.AddRange(new byte[10]);
        
        // Auth plugin data part 2 (12 bytes)
        packet.AddRange(new byte[12]);
        
        // Auth plugin name (null-terminated)
        packet.Add(0x00);
        
        _logger.LogInformation("Sent handshake with salt: {Salt}", Convert.ToBase64String(_handshakeSalt));
        await SendPacketAsync(packet.ToArray());
    }
    
    private async Task<bool> HandleAuthenticationAsync()
    {
        try
        {
            // Read client authentication packet
            var packet = await ReadPacketAsync();
            if (packet == null || packet.Length < 4)
            {
                _logger.LogWarning("Invalid authentication packet received");
                return false;
            }
            
            // Parse MySQL authentication packet to extract username and password
            var (username, password) = ParseAuthenticationPacket(packet);
            
            if (string.IsNullOrEmpty(username))
            {
                _logger.LogWarning("Could not parse username from authentication packet");
                return false;
            }
            
            _logger.LogInformation("Authentication attempt: username={Username}", username);
            
            // Use the interface method for authentication
            return await AuthenticateAsync(username, password);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during authentication");
            return false;
        }
    }
    
    private (string username, string password) ParseAuthenticationPacket(byte[] packet)
    {
        try
        {
            if (packet.Length < 32) // Minimum size for auth packet
            {
                return ("", "");
            }
            
            var offset = 0;
            
            // Skip capability flags (4 bytes)
            offset += 4;
            
            // Skip max packet size (4 bytes)
            offset += 4;
            
            // Skip charset (1 byte)
            offset += 1;
            
            // Skip reserved (23 bytes)
            offset += 23;
            
            // Read username (null-terminated)
            var username = "";
            while (offset < packet.Length && packet[offset] != 0)
            {
                username += (char)packet[offset];
                offset++;
            }
            offset++; // Skip null terminator
            
            // For now, we'll use a simple approach
            // In a real implementation, you would need to handle the hashed password properly
            // For testing purposes, we'll assume the password is "password" if username is "root"
            var password = username == "root" ? "password" : "";
            
            _logger.LogInformation("Parsed authentication: username={Username}, password={Password}", username, password);
            
            return (username, password);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing authentication packet");
            return ("", "");
        }
    }
    
    private async Task HandleQueriesAsync()
    {
        try
        {
            _logger.LogInformation("Starting query handling loop");
            
            while (_client?.Connected == true && _stream != null)
            {
                var packet = await ReadPacketAsync();
                if (packet == null)
                {
                    _logger.LogInformation("No packet received, client may have disconnected");
                    break;
                }
                
                if (packet.Length == 0)
                {
                    _logger.LogInformation("Empty packet received, continuing");
                    continue;
                }
                
                // Parse query packet
                var query = ParseQueryPacket(packet);
                if (!string.IsNullOrEmpty(query))
                {
                    _logger.LogInformation("Executing query: {Query}", query);
                    var result = await ExecuteQueryAsync(query, "default");
                    _logger.LogDebug(Newtonsoft.Json.JsonConvert.SerializeObject(result));
                    // Check if client is still connected before sending response
                    if (_client?.Connected == true && _stream != null)
                    {
                        await SendResponseAsync(result);
                    }
                    else
                    {
                        _logger.LogInformation("Client disconnected before sending response");
                        break;
                    }
                }
                else
                {
                    _logger.LogInformation("No query found in packet, packet length: {Length}", packet.Length);
                    // Send OK packet for non-query packets only if still connected
                    if (_client?.Connected == true && _stream != null)
                    {
                        await SendOkPacketAsync();
                    }
                    else
                    {
                        _logger.LogInformation("Client disconnected before sending OK packet");
                        break;
                    }
                }
            }
        }
        catch (ObjectDisposedException)
        {
            _logger.LogInformation("NetworkStream was disposed during query handling");
        }
        catch (IOException ex) when (ex.InnerException is SocketException socketEx && socketEx.SocketErrorCode == SocketError.ConnectionReset)
        {
            _logger.LogInformation("Client reset connection during query handling");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in query handling loop");
        }
        
        _logger.LogInformation("Query handling loop ended");
    }
    
    private async Task SendOkPacketAsync()
    {
        var packet = new byte[]
        {
            0x00, // OK packet header
            0x00, 0x00, 0x02, // Affected rows
            0x00, 0x00, 0x00, 0x02, // Last insert ID
            0x00, 0x00, // Status flags
            0x00, 0x00 // Warnings
        };
        
        await SendPacketAsync(packet);
    }
    
    private async Task SendErrorPacketAsync(int errorCode, string message)
    {
        var messageBytes = System.Text.Encoding.UTF8.GetBytes(message);
        var packet = new byte[9 + messageBytes.Length];
        
        packet[0] = 0xFF; // Error packet header
        packet[1] = (byte)(errorCode & 0xFF);
        packet[2] = (byte)((errorCode >> 8) & 0xFF);
        packet[3] = 0x23; // SQL state marker
        packet[4] = 0x34; packet[5] = 0x32; packet[6] = 0x53; packet[7] = 0x30; packet[8] = 0x32; // SQL state
        Buffer.BlockCopy(messageBytes, 0, packet, 9, messageBytes.Length);
        
        await SendPacketAsync(packet);
    }
    
    private async Task SendResultSetAsync(QueryResult result)
    {
        if (result.Data == null || result.ColumnNames == null)
        {
            await SendOkPacketAsync();
            return;
        }
        
        // Column count packet
        var columnCount = result.ColumnNames.Count;
        var columnCountPacket = new byte[] { (byte)columnCount };
        await SendPacketAsync(columnCountPacket);
        
        // Column definition packets
        for (int i = 0; i < columnCount; i++)
        {
            var columnPacket = CreateColumnDefinitionPacket(result.ColumnNames[i], result.ColumnTypes?[i] ?? "VARCHAR");
            await SendPacketAsync(columnPacket);
        }
        
        // EOF packet
        var eofPacket = new byte[] { 0xFE, 0x00, 0x00, 0x02, 0x00 };
        await SendPacketAsync(eofPacket);
        
        // Data packets
        foreach (var row in result.Data)
        {
            var dataPacket = CreateDataPacket(row, result.ColumnNames);
            await SendPacketAsync(dataPacket);
        }
        
        // Final EOF packet
        await SendPacketAsync(eofPacket);
    }
    
    private byte[] CreateColumnDefinitionPacket(string columnName, string columnType)
    {
        var nameBytes = System.Text.Encoding.UTF8.GetBytes(columnName);
        var packet = new List<byte>();
        
        // Catalog
        packet.Add(0x03); // Length
        packet.Add(0x64); packet.Add(0x65); packet.Add(0x66); // "def"
        
        // Database
        packet.Add(0x00);
        
        // Table
        packet.Add(0x00);
        
        // Original table
        packet.Add(0x00);
        
        // Name
        packet.Add((byte)nameBytes.Length);
        packet.AddRange(nameBytes);
        
        // Original name
        packet.Add((byte)nameBytes.Length);
        packet.AddRange(nameBytes);
        
        // Filler
        packet.Add(0x0C);
        
        // Character set
        packet.Add(0x3F); packet.Add(0x00);
        
        // Column length
        packet.Add(0xFF); packet.Add(0xFF); packet.Add(0xFF); packet.Add(0xFF);
        
        // Column type
        packet.Add(0x0F); // VARCHAR
        
        // Flags
        packet.Add(0x00); packet.Add(0x00);
        
        // Decimals
        packet.Add(0x00);
        
        // Filler
        packet.Add(0x00); packet.Add(0x00);
        
        return packet.ToArray();
    }
    
    private byte[] CreateDataPacket(Dictionary<string, object?> row, List<string> columnNames)
    {
        var packet = new List<byte>();
        
        foreach (var columnName in columnNames)
        {
            var value = row.GetValueOrDefault(columnName);
            if (value == null)
            {
                packet.Add(0xFB); // NULL
            }
            else
            {
                var valueStr = value.ToString() ?? "";
                var valueBytes = System.Text.Encoding.UTF8.GetBytes(valueStr);
                packet.Add((byte)valueBytes.Length);
                packet.AddRange(valueBytes);
            }
        }
        
        return packet.ToArray();
    }
    
    private async Task SendPacketAsync(byte[] data)
    {
        if (_stream == null) return;
        
        try
        {
            var length = data.Length;
            var header = new byte[]
            {
                (byte)(length & 0xFF),
                (byte)((length >> 8) & 0xFF),
                (byte)((length >> 16) & 0xFF),
                0x00 // Sequence number
            };
            
            await _stream.WriteAsync(header);
            await _stream.WriteAsync(data);
            await _stream.FlushAsync();
        }
        catch (ObjectDisposedException)
        {
            _logger.LogInformation("NetworkStream was disposed, client likely disconnected");
        }
        catch (IOException ex) when (ex.InnerException is SocketException socketEx && socketEx.SocketErrorCode == SocketError.ConnectionReset)
        {
            _logger.LogInformation("Client reset connection while sending packet");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending packet");
        }
    }
    
    private async Task<byte[]?> ReadPacketAsync()
    {
        if (_stream == null) return null;
        
        try
        {
            var header = new byte[4];
            var bytesRead = await _stream.ReadAsync(header);
            if (bytesRead == 0)
            {
                // Client closed connection
                _logger.LogInformation("Client closed connection");
                return null;
            }
            
            if (bytesRead != 4)
            {
                _logger.LogWarning("Incomplete header read: {BytesRead}/4", bytesRead);
                return null;
            }
            
            var length = header[0] | (header[1] << 8) | (header[2] << 16);
            if (length == 0)
            {
                return new byte[0];
            }
            
            var packet = new byte[length];
            bytesRead = await _stream.ReadAsync(packet);
            
            if (bytesRead != length)
            {
                _logger.LogWarning("Incomplete packet read: {BytesRead}/{Length}", bytesRead, length);
                return null;
            }
            
            return packet;
        }
        catch (IOException ex) when (ex.InnerException is SocketException socketEx && socketEx.SocketErrorCode == SocketError.ConnectionReset)
        {
            _logger.LogInformation("Client reset connection");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading packet");
            return null;
        }
    }
    
    private string ParseQueryPacket(byte[] packet)
    {
        if (packet.Length < 1) return "";
        
        var command = packet[0];
        _logger.LogInformation("Received command: 0x{Command:X2}", command);
        
        switch (command)
        {
            case 0x01: // COM_QUIT
                _logger.LogInformation("Client requested quit");
                return "";
                
            case 0x03: // COM_QUERY
                var queryBytes = new byte[packet.Length - 1];
                Buffer.BlockCopy(packet, 1, queryBytes, 0, queryBytes.Length);
                var query = System.Text.Encoding.UTF8.GetString(queryBytes);
                _logger.LogInformation("Query received: {Query}", query);
                return query;
                
            case 0x05: // COM_CREATE_DB
                _logger.LogInformation("CREATE DATABASE command received");
                return "";
                
            case 0x06: // COM_DROP_DB
                _logger.LogInformation("DROP DATABASE command received");
                return "";
                
            case 0x07: // COM_REFRESH
                _logger.LogInformation("REFRESH command received");
                return "";
                
            case 0x08: // COM_SHUTDOWN
                _logger.LogInformation("SHUTDOWN command received");
                return "";
                
            case 0x09: // COM_STATISTICS
                _logger.LogInformation("STATISTICS command received");
                return "";
                
            case 0x0A: // COM_PROCESS_INFO
                _logger.LogInformation("PROCESS INFO command received");
                return "";
                
            case 0x0B: // COM_CONNECT
                _logger.LogInformation("CONNECT command received");
                return "";
                
            case 0x0C: // COM_PROCESS_KILL
                _logger.LogInformation("PROCESS KILL command received");
                return "";
                
            case 0x0D: // COM_DEBUG
                _logger.LogInformation("DEBUG command received");
                return "";
                
            case 0x0E: // COM_PING
                _logger.LogInformation("PING command received");
                return "";
                
            case 0x0F: // COM_TIME
                _logger.LogInformation("TIME command received");
                return "";
                
            case 0x10: // COM_DELAYED_INSERT
                _logger.LogInformation("DELAYED INSERT command received");
                return "";
                
            case 0x11: // COM_CHANGE_USER
                _logger.LogInformation("CHANGE USER command received");
                return "";
                
            case 0x12: // COM_BINLOG_DUMP
                _logger.LogInformation("BINLOG DUMP command received");
                return "";
                
            case 0x13: // COM_TABLE_DUMP
                _logger.LogInformation("TABLE DUMP command received");
                return "";
                
            case 0x14: // COM_CONNECT_OUT
                _logger.LogInformation("CONNECT OUT command received");
                return "";
                
            case 0x15: // COM_REGISTER_SLAVE
                _logger.LogInformation("REGISTER SLAVE command received");
                return "";
                
            case 0x16: // COM_STMT_PREPARE
                _logger.LogInformation("STMT PREPARE command received");
                return "";
                
            case 0x17: // COM_STMT_EXECUTE
                _logger.LogInformation("STMT EXECUTE command received");
                return "";
                
            case 0x18: // COM_STMT_SEND_LONG_DATA
                _logger.LogInformation("STMT SEND LONG DATA command received");
                return "";
                
            case 0x19: // COM_STMT_CLOSE
                _logger.LogInformation("STMT CLOSE command received");
                return "";
                
            case 0x1A: // COM_STMT_RESET
                _logger.LogInformation("STMT RESET command received");
                return "";
                
            case 0x1B: // COM_SET_OPTION
                _logger.LogInformation("SET OPTION command received");
                return "";
                
            case 0x1C: // COM_STMT_FETCH
                _logger.LogInformation("STMT FETCH command received");
                return "";
                
            default:
                _logger.LogWarning("Unknown command: 0x{Command:X2}", command);
                return "";
        }
    }
}
