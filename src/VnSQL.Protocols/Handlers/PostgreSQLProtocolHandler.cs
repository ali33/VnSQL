using System.Net.Sockets;
using System.Text;
using VnSQL.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VnSQL.Protocols.Configuration;
using VnSQL.Core.Sql;

namespace VnSQL.Protocols.Handlers;

/// <summary>
/// PostgreSQL Protocol Handler
/// </summary>
public class PostgreSQLProtocolHandler : IProtocolHandler
{
    private readonly ILogger<PostgreSQLProtocolHandler> _logger;
    private readonly PostgreSQLConfiguration _configuration;
    private readonly QueryExecutor _queryExecutor;
    private TcpClient? _client;
    private NetworkStream? _stream;
    
    public string ProtocolName => "PostgreSQL";
    public int DefaultPort => _configuration.Port;
    
    public PostgreSQLProtocolHandler(ILogger<PostgreSQLProtocolHandler> logger, IOptions<PostgreSQLConfiguration> configuration, QueryExecutor queryExecutor)
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
            
            // Send PostgreSQL startup message
            await SendStartupMessageAsync();
            
            // Handle authentication
            var authResult = await HandleAuthenticationAsync();
            if (!authResult)
            {
                await SendErrorResponseAsync("FATAL", "28P01", "password authentication failed");
                return;
            }
            
            await SendAuthenticationOkAsync();
            await SendReadyForQueryAsync();
            
            // Main connection loop
            await HandleQueriesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling PostgreSQL connection");
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
            _logger.LogInformation("PostgreSQL authentication attempt: username={Username}", username);
            
            // Get expected credentials from configuration
            var expectedUsername = _configuration.Authentication.Username;
            var expectedPassword = _configuration.Authentication.Password;
            
            // Simple string comparison for now
            if (username == expectedUsername && password == expectedPassword)
            {
                _logger.LogInformation("PostgreSQL authentication successful!");
                return true;
            }
            else
            {
                _logger.LogWarning("PostgreSQL authentication failed - username or password mismatch");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during PostgreSQL authentication");
            return false;
        }
    }
    
    public async Task<QueryResult> ExecuteQueryAsync(string query, string database)
    {
        try
        {
            _logger.LogInformation("Executing PostgreSQL query: {Query}", query);
            
            // Use QueryExecutor to handle SQL commands
            return await _queryExecutor.ExecuteAsync(query, "PostgreSQL");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing PostgreSQL query: {Query}", query);
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
                await SendErrorResponseAsync("ERROR", "42601", result.ErrorMessage ?? "Unknown error");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending PostgreSQL response");
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
            
            _logger.LogInformation("PostgreSQL connection closed");
        }
        catch (ObjectDisposedException)
        {
            _logger.LogInformation("PostgreSQL connection was already closed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing PostgreSQL connection");
        }
    }
    
    private async Task SendStartupMessageAsync()
    {
        // PostgreSQL startup message
        var startupMessage = new List<byte>();
        
        // Protocol version (3.0)
        startupMessage.AddRange(BitConverter.GetBytes(0x00030000).Reverse());
        
        // Parameter list
        var parameters = new Dictionary<string, string>
        {
            ["user"] = _configuration.Authentication.Username,
            ["database"] = "postgres",
            ["application_name"] = "VnSQL",
            ["client_encoding"] = "UTF8"
        };
        
        foreach (var param in parameters)
        {
            startupMessage.AddRange(Encoding.UTF8.GetBytes(param.Key));
            startupMessage.Add(0); // null terminator
            startupMessage.AddRange(Encoding.UTF8.GetBytes(param.Value));
            startupMessage.Add(0); // null terminator
        }
        
        startupMessage.Add(0); // final null terminator
        
        // Send message length (excluding length field itself)
        var length = startupMessage.Count + 4;
        var lengthBytes = BitConverter.GetBytes(length).Reverse().ToArray();
        
        await _stream!.WriteAsync(lengthBytes);
        await _stream.WriteAsync(startupMessage.ToArray());
        await _stream.FlushAsync();
        
        _logger.LogInformation("Sent PostgreSQL startup message");
    }
    
    private async Task<bool> HandleAuthenticationAsync()
    {
        try
        {
            // Read authentication request
            var message = await ReadMessageAsync();
            if (message == null || message.Length < 1)
            {
                _logger.LogWarning("Invalid authentication message received");
                return false;
            }
            
            var messageType = (char)message[0];
            if (messageType == 'R') // Authentication request
            {
                var authType = BitConverter.ToInt32(message.Skip(1).Take(4).Reverse().ToArray(), 0);
                
                if (authType == 5) // MD5 password
                {
                    // For simplicity, we'll use a basic authentication approach
                    // In a real implementation, you'd need to handle MD5 hashing
                    return await AuthenticateAsync(_configuration.Authentication.Username, "password");
                }
                else if (authType == 3) // Cleartext password
                {
                    // Read password from client
                    var passwordMessage = await ReadMessageAsync();
                    if (passwordMessage != null)
                    {
                        var password = Encoding.UTF8.GetString(passwordMessage.Skip(1).ToArray()).TrimEnd('\0');
                        return await AuthenticateAsync(_configuration.Authentication.Username, password);
                    }
                }
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during PostgreSQL authentication");
            return false;
        }
    }
    
    private async Task SendAuthenticationOkAsync()
    {
        var message = new List<byte> { (byte)'R' };
        message.AddRange(BitConverter.GetBytes(0).Reverse()); // AuthenticationOk
        
        await SendMessageAsync(message.ToArray());
        _logger.LogInformation("Sent PostgreSQL authentication OK");
    }
    
    private async Task SendReadyForQueryAsync()
    {
        var message = new List<byte> { (byte)'Z' };
        message.Add(0x49); // 'I' = idle
        
        await SendMessageAsync(message.ToArray());
        _logger.LogInformation("Sent PostgreSQL ready for query");
    }
    
    private async Task HandleQueriesAsync()
    {
        try
        {
            _logger.LogInformation("Starting PostgreSQL query handling loop");
            
            while (_client?.Connected == true && _stream != null)
            {
                var message = await ReadMessageAsync();
                if (message == null)
                {
                    _logger.LogInformation("No message received, client may have disconnected");
                    break;
                }
                
                if (message.Length == 0)
                {
                    continue;
                }
                
                var messageType = (char)message[0];
                
                switch (messageType)
                {
                    case 'Q': // Query
                        var query = Encoding.UTF8.GetString(message.Skip(1).ToArray()).TrimEnd('\0');
                        _logger.LogInformation("Executing PostgreSQL query: {Query}", query);
                        var result = await ExecuteQueryAsync(query, "postgres");
                        await SendResponseAsync(result);
                        break;
                        
                    case 'X': // Terminate
                        _logger.LogInformation("Client requested termination");
                        return;
                        
                    default:
                        _logger.LogWarning("Unknown PostgreSQL message type: {MessageType}", messageType);
                        break;
                }
            }
        }
        catch (ObjectDisposedException)
        {
            _logger.LogInformation("NetworkStream was disposed during PostgreSQL query handling");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in PostgreSQL query handling loop");
        }
        
        _logger.LogInformation("PostgreSQL query handling loop ended");
    }
    
    private async Task SendQueryResponseAsync(QueryResult result)
    {
        if (result.Data == null || result.ColumnNames == null)
        {
            await SendCommandCompleteAsync("SELECT 0");
            await SendReadyForQueryAsync();
            return;
        }
        
        // Row description
        await SendRowDescriptionAsync(result.ColumnNames, result.ColumnTypes);
        
        // Data rows
        foreach (var row in result.Data)
        {
            await SendDataRowAsync(row, result.ColumnNames);
        }
        
        // Command complete
        await SendCommandCompleteAsync($"SELECT {result.Data.Count}");
        
        // Ready for query
        await SendReadyForQueryAsync();
    }
    
    private async Task SendRowDescriptionAsync(List<string> columnNames, List<string>? columnTypes)
    {
        var message = new List<byte> { (byte)'T' };
        
        // Number of fields
        message.AddRange(BitConverter.GetBytes((short)columnNames.Count).Reverse());
        
        foreach (var columnName in columnNames)
        {
            // Field name
            var nameBytes = Encoding.UTF8.GetBytes(columnName);
            message.AddRange(BitConverter.GetBytes((short)nameBytes.Length).Reverse());
            message.AddRange(nameBytes);
            
            // Table OID (0 for now)
            message.AddRange(BitConverter.GetBytes(0).Reverse());
            
            // Column attribute number (0 for now)
            message.AddRange(BitConverter.GetBytes((short)0).Reverse());
            
            // Data type OID (25 = TEXT for now)
            message.AddRange(BitConverter.GetBytes(25).Reverse());
            
            // Data type size (-1 for variable length)
            message.AddRange(BitConverter.GetBytes((short)-1).Reverse());
            
            // Type modifier (-1 for no modifier)
            message.AddRange(BitConverter.GetBytes(-1).Reverse());
            
            // Format code (0 = text)
            message.AddRange(BitConverter.GetBytes((short)0).Reverse());
        }
        
        await SendMessageAsync(message.ToArray());
    }
    
    private async Task SendDataRowAsync(Dictionary<string, object?> row, List<string> columnNames)
    {
        var message = new List<byte> { (byte)'D' };
        
        // Number of columns
        message.AddRange(BitConverter.GetBytes((short)columnNames.Count).Reverse());
        
        foreach (var columnName in columnNames)
        {
            var value = row.GetValueOrDefault(columnName);
            if (value == null)
            {
                // NULL value
                message.AddRange(BitConverter.GetBytes(-1).Reverse());
            }
            else
            {
                var valueStr = value.ToString() ?? "";
                var valueBytes = Encoding.UTF8.GetBytes(valueStr);
                message.AddRange(BitConverter.GetBytes(valueBytes.Length).Reverse());
                message.AddRange(valueBytes);
            }
        }
        
        await SendMessageAsync(message.ToArray());
    }
    
    private async Task SendCommandCompleteAsync(string tag)
    {
        var message = new List<byte> { (byte)'C' };
        var tagBytes = Encoding.UTF8.GetBytes(tag);
        message.AddRange(tagBytes);
        message.Add(0); // null terminator
        
        await SendMessageAsync(message.ToArray());
    }
    
    private async Task SendErrorResponseAsync(string severity, string code, string message)
    {
        var response = new List<byte> { (byte)'E' };
        
        // Severity
        response.Add((byte)'S');
        var severityBytes = Encoding.UTF8.GetBytes(severity);
        response.AddRange(severityBytes);
        response.Add(0);
        
        // Code
        response.Add((byte)'C');
        var codeBytes = Encoding.UTF8.GetBytes(code);
        response.AddRange(codeBytes);
        response.Add(0);
        
        // Message
        response.Add((byte)'M');
        var messageBytes = Encoding.UTF8.GetBytes(message);
        response.AddRange(messageBytes);
        response.Add(0);
        
        response.Add(0); // final null terminator
        
        await SendMessageAsync(response.ToArray());
    }
    
    private async Task SendMessageAsync(byte[] data)
    {
        if (_stream == null) return;
        
        try
        {
            var length = data.Length + 4;
            var lengthBytes = BitConverter.GetBytes(length).Reverse().ToArray();
            
            await _stream.WriteAsync(lengthBytes);
            await _stream.WriteAsync(data);
            await _stream.FlushAsync();
        }
        catch (ObjectDisposedException)
        {
            _logger.LogInformation("NetworkStream was disposed, client likely disconnected");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending PostgreSQL message");
        }
    }
    
    private async Task<byte[]?> ReadMessageAsync()
    {
        if (_stream == null) return null;
        
        try
        {
            var lengthBytes = new byte[4];
            var bytesRead = await _stream.ReadAsync(lengthBytes);
            if (bytesRead == 0)
            {
                _logger.LogInformation("Client closed PostgreSQL connection");
                return null;
            }
            
            if (bytesRead != 4)
            {
                _logger.LogWarning("Incomplete length read: {BytesRead}/4", bytesRead);
                return null;
            }
            
            var length = BitConverter.ToInt32(lengthBytes.Reverse().ToArray(), 0);
            if (length <= 4)
            {
                return new byte[0];
            }
            
            var message = new byte[length - 4];
            bytesRead = await _stream.ReadAsync(message);
            
            if (bytesRead != message.Length)
            {
                _logger.LogWarning("Incomplete message read: {BytesRead}/{Length}", bytesRead, message.Length);
                return null;
            }
            
            return message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading PostgreSQL message");
            return null;
        }
    }
}
