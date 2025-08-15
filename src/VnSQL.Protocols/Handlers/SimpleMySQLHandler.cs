using System.Net.Sockets;
using VnSQL.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VnSQL.Protocols.Configuration;
using VnSQL.Protocols.Utils;

namespace VnSQL.Protocols.Handlers;

/// <summary>
/// Simplified MySQL Protocol Handler for basic connection testing
/// </summary>
public class SimpleMySQLHandler : IProtocolHandler
{
    private readonly ILogger<SimpleMySQLHandler> _logger;
    private readonly MySQLConfiguration _configuration;
    private TcpClient? _client;
    private NetworkStream? _stream;
    
    public string ProtocolName => "MySQL";
    public int DefaultPort => _configuration.Port;
    
    public SimpleMySQLHandler(ILogger<SimpleMySQLHandler> logger, IOptions<MySQLConfiguration> configuration)
    {
        _logger = logger;
        _configuration = configuration.Value;
    }
    
    public async Task HandleConnectionAsync(TcpClient client)
    {
        try
        {
            _client = client;
            _stream = client.GetStream();
            
            _logger.LogInformation("New MySQL connection from {RemoteEndPoint}", client.Client.RemoteEndPoint);
            
            // Send simple handshake
            await SendSimpleHandshakeAsync();
            
            // Accept any authentication
            await AcceptAnyAuthenticationAsync();
            
            // Send OK packet
            await SendOkPacketAsync();
            
            _logger.LogInformation("MySQL connection authenticated successfully");
            
            // Keep connection alive for a while
            await Task.Delay(5000);
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
        _logger.LogInformation("Authentication attempt: {Username}", username);
        // Accept any authentication for testing
        return true;
    }
    
    public async Task<QueryResult> ExecuteQueryAsync(string query, string database)
    {
        _logger.LogInformation("Executing query: {Query}", query);
        
        return new QueryResult
        {
            Success = true,
            AffectedRows = 0,
            LastInsertId = 0,
            Data = new List<Dictionary<string, object?>>(),
            ColumnNames = new List<string>(),
            ColumnTypes = new List<string>()
        };
    }
    
    public async Task SendResponseAsync(QueryResult result)
    {
        if (result.Success)
        {
            await SendOkPacketAsync();
        }
        else
        {
            await SendErrorPacketAsync(1064, result.ErrorMessage ?? "Unknown error");
        }
    }
    
    public async Task CloseConnectionAsync()
    {
        try
        {
            _stream?.Close();
            _client?.Close();
            _logger.LogInformation("MySQL connection closed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing MySQL connection");
        }
    }
    
    private async Task SendSimpleHandshakeAsync()
    {
        // Generate salt for password hashing
        var salt = MySQLPasswordHasher.GenerateSalt(8);
        
        // Build handshake packet with proper salt
        var packet = new List<byte>();
        
        // Protocol version
        packet.Add(0x0A);
        
        // Server version (null-terminated)
        var version = "5.7.28-VnSQL";
        packet.AddRange(System.Text.Encoding.ASCII.GetBytes(version));
        packet.Add(0x00);
        
        // Connection ID (4 bytes, little endian)
        packet.AddRange(new byte[] { 0x01, 0x00, 0x00, 0x00 });
        
        // Auth plugin data part 1 (8 bytes) - this is the salt
        packet.AddRange(salt);
        
        // Filler
        packet.Add(0x00);
        
        // Capability flags part 1 (2 bytes, little endian)
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
        
        await SendPacketAsync(packet.ToArray());
    }
    
    private async Task AcceptAnyAuthenticationAsync()
    {
        try
        {
            // Read authentication packet
            var packet = await ReadPacketAsync();
            if (packet != null)
            {
                _logger.LogInformation("Received authentication packet, length: {Length}", packet.Length);
                
                // Parse authentication packet to extract username and password
                var (username, password) = ParseAuthenticationPacket(packet);
                _logger.LogInformation("Authentication attempt: username={Username}, password={Password}", username, password);
                
                // Use the interface method for authentication
                var authResult = await AuthenticateAsync(username, password);
                _logger.LogInformation("Authentication result: {Result}", authResult);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error reading authentication packet, but continuing");
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
            
            // For SimpleMySQLHandler, we'll accept any password
            // In a real implementation, you would need to handle the hashed password properly
            var password = "any_password";
            
            _logger.LogInformation("Parsed authentication: username={Username}, password={Password}", username, password);
            
            return (username, password);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing authentication packet");
            return ("", "");
        }
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
    
    private async Task SendPacketAsync(byte[] data)
    {
        if (_stream == null) return;
        
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
    
    private async Task<byte[]?> ReadPacketAsync()
    {
        if (_stream == null) return null;
        
        try
        {
            var header = new byte[4];
            var bytesRead = await _stream.ReadAsync(header);
            if (bytesRead == 0)
            {
                return null;
            }
            
            if (bytesRead != 4)
            {
                return null;
            }
            
            var length = header[0] | (header[1] << 8) | (header[2] << 16);
            if (length == 0)
            {
                return new byte[0];
            }
            
            var packet = new byte[length];
            bytesRead = await _stream.ReadAsync(packet);
            
            return bytesRead == length ? packet : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading packet");
            return null;
        }
    }
}
