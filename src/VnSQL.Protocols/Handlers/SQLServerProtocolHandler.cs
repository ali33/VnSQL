using System.Net.Sockets;
using System.Text;
using VnSQL.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VnSQL.Protocols.Configuration;
using VnSQL.Core.Sql;

namespace VnSQL.Protocols.Handlers;

/// <summary>
/// SQL Server Protocol Handler using TDS (Tabular Data Stream)
/// </summary>
public class SQLServerProtocolHandler : IProtocolHandler
{
    private readonly ILogger<SQLServerProtocolHandler> _logger;
    private readonly SQLServerConfiguration _configuration;
    private readonly QueryExecutor _queryExecutor;
    private TcpClient? _client;
    private NetworkStream? _stream;
    
    public string ProtocolName => "SQLServer";
    public int DefaultPort => _configuration.Port;
    
    public SQLServerProtocolHandler(ILogger<SQLServerProtocolHandler> logger, IOptions<SQLServerConfiguration> configuration, QueryExecutor queryExecutor)
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
            
            // Send TDS pre-login packet
            await SendPreLoginPacketAsync();
            
            // Handle TDS login
            var loginResult = await HandleTdsLoginAsync();
            if (!loginResult)
            {
                await SendLoginFailureAsync();
                return;
            }
            
            await SendLoginSuccessAsync();
            
            // Main connection loop
            await HandleQueriesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling SQL Server connection");
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
            _logger.LogInformation("SQL Server authentication attempt: username={Username}", username);
            
            // Get expected credentials from configuration
            var expectedUsername = _configuration.Authentication.Username;
            var expectedPassword = _configuration.Authentication.Password;
            
            // Simple string comparison for now
            if (username == expectedUsername && password == expectedPassword)
            {
                _logger.LogInformation("SQL Server authentication successful!");
                return true;
            }
            else
            {
                _logger.LogWarning("SQL Server authentication failed - username or password mismatch");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during SQL Server authentication");
            return false;
        }
    }
    
    public async Task<QueryResult> ExecuteQueryAsync(string query, string database)
    {
        try
        {
            _logger.LogInformation("Executing SQL Server query: {Query}", query);
            
            // Use QueryExecutor to handle SQL commands
            return await _queryExecutor.ExecuteAsync(query, "SQLServer");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing SQL Server query: {Query}", query);
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
                await SendQueryResponseAsync(result);
            }
            else
            {
                await SendErrorResponseAsync(result.ErrorMessage ?? "Unknown error");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending SQL Server response");
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
            
            _logger.LogInformation("SQL Server connection closed");
        }
        catch (ObjectDisposedException)
        {
            _logger.LogInformation("SQL Server connection was already closed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing SQL Server connection");
        }
    }
    
    private async Task SendPreLoginPacketAsync()
    {
        // TDS Pre-Login packet
        var packet = new List<byte>();
        
        // TDS Header
        packet.Add(0x12); // TDS version (7.2)
        packet.Add(0x01); // Packet type (Pre-Login)
        packet.Add(0x00); // Status
        packet.Add(0x00); // Length (placeholder)
        
        // Pre-Login options
        var options = new Dictionary<byte, byte[]>
        {
            [0x00] = new byte[] { 0x00, 0x00, 0x00, 0x00 }, // Version
            [0x01] = new byte[] { 0x00, 0x00, 0x00, 0x00 }, // Encryption
            [0x02] = new byte[] { 0x00, 0x00, 0x00, 0x00 }, // Instance
            [0x03] = new byte[] { 0x00, 0x00, 0x00, 0x00 }, // ThreadID
            [0x04] = new byte[] { 0x00, 0x00, 0x00, 0x00 }, // MARS
            [0xFF] = new byte[0] // Terminator
        };
        
        foreach (var option in options)
        {
            packet.Add(option.Key);
            packet.AddRange(option.Value);
        }
        
        // Update packet length
        var length = packet.Count;
        packet[2] = (byte)(length >> 8);
        packet[3] = (byte)(length & 0xFF);
        
        await _stream!.WriteAsync(packet.ToArray());
        await _stream.FlushAsync();
        
        _logger.LogInformation("Sent SQL Server Pre-Login packet");
    }
    
    private async Task<bool> HandleTdsLoginAsync()
    {
        try
        {
            // Read TDS login packet
            var packet = await ReadTdsPacketAsync();
            if (packet == null || packet.Length < 4)
            {
                _logger.LogWarning("Invalid TDS login packet received");
                return false;
            }
            
            var packetType = packet[1];
            if (packetType == 0x10) // TDS_LOGIN7
            {
                // Parse login packet (simplified)
                var loginData = ParseLoginPacket(packet);
                if (loginData != null)
                {
                    return await AuthenticateAsync(loginData.Username, loginData.Password);
                }
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during TDS login");
            return false;
        }
    }
    
    private LoginData? ParseLoginPacket(byte[] packet)
    {
        try
        {
            // Simplified TDS_LOGIN7 packet parsing
            // In a real implementation, you'd need to parse the full TDS specification
            
            var loginData = new LoginData();
            
            // Extract username and password from packet
            // This is a simplified implementation
            var data = Encoding.UTF8.GetString(packet);
            
            // Look for username and password patterns
            var usernameMatch = System.Text.RegularExpressions.Regex.Match(data, @"user[^=]*=([^;]+)");
            var passwordMatch = System.Text.RegularExpressions.Regex.Match(data, @"password[^=]*=([^;]+)");
            
            if (usernameMatch.Success)
            {
                loginData.Username = usernameMatch.Groups[1].Value.Trim();
            }
            
            if (passwordMatch.Success)
            {
                loginData.Password = passwordMatch.Groups[1].Value.Trim();
            }
            
            return loginData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing TDS login packet");
            return null;
        }
    }
    
    private async Task SendLoginSuccessAsync()
    {
        // TDS Login Response packet
        var packet = new List<byte>();
        
        // TDS Header
        packet.Add(0x12); // TDS version
        packet.Add(0x04); // Packet type (Login Response)
        packet.Add(0x00); // Status
        packet.Add(0x00); // Length (placeholder)
        
        // Login response data
        packet.Add(0x00); // Login response type (ACCEPT)
        packet.Add(0x00); // TDS version
        packet.Add(0x00); // TDS version
        packet.Add(0x00); // TDS version
        packet.Add(0x00); // TDS version
        
        // Update packet length
        var length = packet.Count;
        packet[2] = (byte)(length >> 8);
        packet[3] = (byte)(length & 0xFF);
        
        await _stream!.WriteAsync(packet.ToArray());
        await _stream.FlushAsync();
        
        _logger.LogInformation("Sent SQL Server Login Success");
    }
    
    private async Task SendLoginFailureAsync()
    {
        // TDS Login Response packet with failure
        var packet = new List<byte>();
        
        // TDS Header
        packet.Add(0x12); // TDS version
        packet.Add(0x04); // Packet type (Login Response)
        packet.Add(0x00); // Status
        packet.Add(0x00); // Length (placeholder)
        
        // Login response data
        packet.Add(0x01); // Login response type (REJECT)
        
        // Update packet length
        var length = packet.Count;
        packet[2] = (byte)(length >> 8);
        packet[3] = (byte)(length & 0xFF);
        
        await _stream!.WriteAsync(packet.ToArray());
        await _stream.FlushAsync();
        
        _logger.LogInformation("Sent SQL Server Login Failure");
    }
    
    private async Task HandleQueriesAsync()
    {
        try
        {
            _logger.LogInformation("Starting SQL Server query handling loop");
            
            while (_client?.Connected == true && _stream != null)
            {
                var packet = await ReadTdsPacketAsync();
                if (packet == null)
                {
                    _logger.LogInformation("No packet received, client may have disconnected");
                    break;
                }
                
                if (packet.Length < 4)
                {
                    continue;
                }
                
                var packetType = packet[1];
                
                switch (packetType)
                {
                    case 0x01: // TDS_SQL_BATCH
                        var query = ParseSqlBatch(packet);
                        if (!string.IsNullOrEmpty(query))
                        {
                            _logger.LogInformation("Executing SQL Server query: {Query}", query);
                            var result = await ExecuteQueryAsync(query, "master");
                            await SendResponseAsync(result);
                        }
                        break;
                        
                    case 0x0E: // TDS_ATTENTION
                        _logger.LogInformation("Client sent attention signal");
                        break;
                        
                    default:
                        _logger.LogWarning("Unknown TDS packet type: {PacketType}", packetType);
                        break;
                }
            }
        }
        catch (ObjectDisposedException)
        {
            _logger.LogInformation("NetworkStream was disposed during SQL Server query handling");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SQL Server query handling loop");
        }
        
        _logger.LogInformation("SQL Server query handling loop ended");
    }
    
    private string ParseSqlBatch(byte[] packet)
    {
        try
        {
            // Extract SQL query from TDS_SQL_BATCH packet
            // Skip TDS header (8 bytes) and get the SQL text
            if (packet.Length > 8)
            {
                var sqlData = new byte[packet.Length - 8];
                Array.Copy(packet, 8, sqlData, 0, sqlData.Length);
                return Encoding.UTF8.GetString(sqlData).TrimEnd('\0');
            }
            
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing SQL batch");
            return string.Empty;
        }
    }
    
    private async Task SendQueryResponseAsync(QueryResult result)
    {
        if (result.Data == null || result.ColumnNames == null)
        {
            await SendDonePacketAsync();
            return;
        }
        
        // Send column metadata
        await SendColumnMetadataAsync(result.ColumnNames, result.ColumnTypes);
        
        // Send data rows
        foreach (var row in result.Data)
        {
            await SendDataRowAsync(row, result.ColumnNames);
        }
        
        // Send completion packet
        await SendDonePacketAsync();
    }
    
    private async Task SendColumnMetadataAsync(List<string> columnNames, List<string>? columnTypes)
    {
        var packet = new List<byte>();
        
        // TDS Header
        packet.Add(0x12); // TDS version
        packet.Add(0x81); // Packet type (TDS_COLMETADATA)
        packet.Add(0x00); // Status
        packet.Add(0x00); // Length (placeholder)
        
        // Column count
        packet.Add((byte)(columnNames.Count >> 8));
        packet.Add((byte)(columnNames.Count & 0xFF));
        
        foreach (var columnName in columnNames)
        {
            // Column name
            var nameBytes = Encoding.UTF8.GetBytes(columnName);
            packet.Add((byte)(nameBytes.Length >> 8));
            packet.Add((byte)(nameBytes.Length & 0xFF));
            packet.AddRange(nameBytes);
            
            // Column type (simplified - all as VARCHAR)
            packet.Add(0x27); // VARCHAR
            packet.Add(0x00); // Length
            packet.Add(0xFF); // Length
            packet.Add(0xFF); // Length
        }
        
        // Update packet length
        var length = packet.Count;
        packet[2] = (byte)(length >> 8);
        packet[3] = (byte)(length & 0xFF);
        
        await _stream!.WriteAsync(packet.ToArray());
        await _stream.FlushAsync();
    }
    
    private async Task SendDataRowAsync(Dictionary<string, object?> row, List<string> columnNames)
    {
        var packet = new List<byte>();
        
        // TDS Header
        packet.Add(0x12); // TDS version
        packet.Add(0xD1); // Packet type (TDS_ROW)
        packet.Add(0x00); // Status
        packet.Add(0x00); // Length (placeholder)
        
        foreach (var columnName in columnNames)
        {
            var value = row.GetValueOrDefault(columnName);
            if (value == null)
            {
                // NULL value
                packet.Add(0x00);
                packet.Add(0x00);
            }
            else
            {
                var valueStr = value.ToString() ?? "";
                var valueBytes = Encoding.UTF8.GetBytes(valueStr);
                packet.Add((byte)(valueBytes.Length >> 8));
                packet.Add((byte)(valueBytes.Length & 0xFF));
                packet.AddRange(valueBytes);
            }
        }
        
        // Update packet length
        var length = packet.Count;
        packet[2] = (byte)(length >> 8);
        packet[3] = (byte)(length & 0xFF);
        
        await _stream!.WriteAsync(packet.ToArray());
        await _stream.FlushAsync();
    }
    
    private async Task SendDonePacketAsync()
    {
        var packet = new List<byte>();
        
        // TDS Header
        packet.Add(0x12); // TDS version
        packet.Add(0xFD); // Packet type (TDS_DONE)
        packet.Add(0x00); // Status
        packet.Add(0x00); // Length (placeholder)
        
        // Done flags
        packet.Add(0x00);
        packet.Add(0x00);
        
        // Row count
        packet.Add(0x00);
        packet.Add(0x00);
        packet.Add(0x00);
        packet.Add(0x00);
        
        // Update packet length
        var length = packet.Count;
        packet[2] = (byte)(length >> 8);
        packet[3] = (byte)(length & 0xFF);
        
        await _stream!.WriteAsync(packet.ToArray());
        await _stream.FlushAsync();
    }
    
    private async Task SendErrorResponseAsync(string errorMessage)
    {
        var packet = new List<byte>();
        
        // TDS Header
        packet.Add(0x12); // TDS version
        packet.Add(0xAA); // Packet type (TDS_ERROR)
        packet.Add(0x00); // Status
        packet.Add(0x00); // Length (placeholder)
        
        // Error message
        var messageBytes = Encoding.UTF8.GetBytes(errorMessage);
        packet.AddRange(messageBytes);
        
        // Update packet length
        var length = packet.Count;
        packet[2] = (byte)(length >> 8);
        packet[3] = (byte)(length & 0xFF);
        
        await _stream!.WriteAsync(packet.ToArray());
        await _stream.FlushAsync();
    }
    
    private async Task<byte[]?> ReadTdsPacketAsync()
    {
        if (_stream == null) return null;
        
        try
        {
            // Read TDS packet header (8 bytes)
            var header = new byte[8];
            var bytesRead = await _stream.ReadAsync(header);
            if (bytesRead == 0)
            {
                _logger.LogInformation("Client closed SQL Server connection");
                return null;
            }
            
            if (bytesRead != 8)
            {
                _logger.LogWarning("Incomplete TDS header read: {BytesRead}/8", bytesRead);
                return null;
            }
            
            // Extract packet length from header
            var length = (header[2] << 8) | header[3];
            if (length <= 8)
            {
                return header;
            }
            
            // Read packet body
            var body = new byte[length - 8];
            bytesRead = await _stream.ReadAsync(body);
            
            if (bytesRead != body.Length)
            {
                _logger.LogWarning("Incomplete TDS body read: {BytesRead}/{Length}", bytesRead, body.Length);
                return null;
            }
            
            // Combine header and body
            var packet = new byte[length];
            Array.Copy(header, 0, packet, 0, 8);
            Array.Copy(body, 0, packet, 8, body.Length);
            
            return packet;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading TDS packet");
            return null;
        }
    }
    
    private class LoginData
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
