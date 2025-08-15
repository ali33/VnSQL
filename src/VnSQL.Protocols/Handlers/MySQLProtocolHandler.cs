using System.Net.Sockets;
using VnSQL.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace VnSQL.Protocols.Handlers;

/// <summary>
/// MySQL Protocol Handler
/// </summary>
public class MySQLProtocolHandler : IProtocolHandler
{
    private readonly ILogger<MySQLProtocolHandler> _logger;
    private TcpClient? _client;
    private NetworkStream? _stream;
    
    public string ProtocolName => "MySQL";
    public int DefaultPort => 3306;
    
    public MySQLProtocolHandler(ILogger<MySQLProtocolHandler> logger)
    {
        _logger = logger;
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
        // Simple authentication for demo
        return username == "root" && password == "password";
    }
    
    public async Task<QueryResult> ExecuteQueryAsync(string query, string database)
    {
        try
        {
            _logger.LogInformation("Executing MySQL query: {Query}", query);
            
            // Simple query parsing and execution
            var result = new QueryResult
            {
                Success = true,
                AffectedRows = 1,
                LastInsertId = 1,
                Data = new List<Dictionary<string, object?>>
                {
                    new Dictionary<string, object?>
                    {
                        ["result"] = "Query executed successfully"
                    }
                },
                ColumnNames = new List<string> { "result" },
                ColumnTypes = new List<string> { "VARCHAR" }
            };
            
            return result;
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
            _stream?.Close();
            _client?.Close();
            _logger.LogInformation("MySQL connection closed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing MySQL connection");
        }
    }
    
    private async Task SendHandshakePacketAsync()
    {
        // MySQL Protocol 10 handshake packet
        var packet = new byte[]
        {
            0x0A, // Protocol version
            0x35, 0x2E, 0x37, 0x2E, 0x32, 0x38, 0x2D, 0x30, 0x75, 0x62, 0x75, 0x6E, 0x74, 0x75, 0x30, 0x2E, 0x31, 0x38, 0x2E, 0x30, 0x34, 0x2E, 0x31, 0x00, // Server version
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // Connection ID
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // Auth plugin data part 1
            0x00, // Filler
            0x00, // Capability flags
            0x00, 0x00, // Character set
            0x00, 0x00, // Status flags
            0x00, 0x00, // Capability flags part 2
            0x00, // Auth plugin data len
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // Reserved
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // Auth plugin data part 2
            0x00 // Auth plugin name
        };
        
        await SendPacketAsync(packet);
    }
    
    private async Task<bool> HandleAuthenticationAsync()
    {
        // Read client authentication packet
        var packet = await ReadPacketAsync();
        if (packet == null || packet.Length < 4)
        {
            return false;
        }
        
        // Parse username and password (simplified)
        // In real implementation, you'd need to parse the MySQL authentication packet properly
        return true;
    }
    
    private async Task HandleQueriesAsync()
    {
        while (_client?.Connected == true)
        {
            var packet = await ReadPacketAsync();
            if (packet == null)
            {
                break;
            }
            
            // Parse query packet
            var query = ParseQueryPacket(packet);
            if (!string.IsNullOrEmpty(query))
            {
                var result = await ExecuteQueryAsync(query, "default");
                await SendResponseAsync(result);
            }
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
        packet[4] = 0x34, packet[5] = 0x32, packet[6] = 0x53, packet[7] = 0x30, packet[8] = 0x32; // SQL state
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
        var typeBytes = System.Text.Encoding.UTF8.GetBytes(columnType);
        
        var packet = new byte[20 + nameBytes.Length + typeBytes.Length];
        var offset = 0;
        
        // Catalog
        packet[offset++] = 0x03; // Length
        packet[offset++] = 0x64, packet[offset++] = 0x65, packet[offset++] = 0x66; // "def"
        
        // Database
        packet[offset++] = 0x00;
        
        // Table
        packet[offset++] = 0x00;
        
        // Original table
        packet[offset++] = 0x00;
        
        // Name
        packet[offset++] = (byte)nameBytes.Length;
        Buffer.BlockCopy(nameBytes, 0, packet, offset, nameBytes.Length);
        offset += nameBytes.Length;
        
        // Original name
        packet[offset++] = (byte)nameBytes.Length;
        Buffer.BlockCopy(nameBytes, 0, packet, offset, nameBytes.Length);
        offset += nameBytes.Length;
        
        // Filler
        packet[offset++] = 0x0C;
        
        // Character set
        packet[offset++] = 0x3F; packet[offset++] = 0x00;
        
        // Column length
        packet[offset++] = 0xFF; packet[offset++] = 0xFF; packet[offset++] = 0xFF; packet[offset++] = 0xFF;
        
        // Column type
        packet[offset++] = 0x0F; // VARCHAR
        
        // Flags
        packet[offset++] = 0x00; packet[offset++] = 0x00;
        
        // Decimals
        packet[offset++] = 0x00;
        
        // Filler
        packet[offset++] = 0x00; packet[offset++] = 0x00;
        
        return packet;
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
        
        var header = new byte[4];
        var bytesRead = await _stream.ReadAsync(header);
        if (bytesRead != 4)
        {
            return null;
        }
        
        var length = header[0] | (header[1] << 8) | (header[2] << 16);
        var packet = new byte[length];
        bytesRead = await _stream.ReadAsync(packet);
        
        return bytesRead == length ? packet : null;
    }
    
    private string ParseQueryPacket(byte[] packet)
    {
        if (packet.Length < 1) return "";
        
        var command = packet[0];
        if (command == 0x03) // COM_QUERY
        {
            var queryBytes = new byte[packet.Length - 1];
            Buffer.BlockCopy(packet, 1, queryBytes, 0, queryBytes.Length);
            return System.Text.Encoding.UTF8.GetString(queryBytes);
        }
        
        return "";
    }
}
