using System.Net.Sockets;
using System.Text;
using VnSQL.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VnSQL.Protocols.Configuration;
using VnSQL.Core.Sql;

namespace VnSQL.Protocols.Handlers;

/// <summary>
/// SQLite Protocol Handler (simplified for VnSQL)
/// Note: SQLite doesn't have a network protocol, this is a custom implementation
/// </summary>
public class SQLiteProtocolHandler : IProtocolHandler
{
    private readonly ILogger<SQLiteProtocolHandler> _logger;
    private readonly SQLiteConfiguration _configuration;
    private readonly QueryExecutor _queryExecutor;
    private TcpClient? _client;
    private NetworkStream? _stream;

    public string ProtocolName => "SQLite";
    public int Port => _configuration.Port;
    public bool Enabled => _configuration.Enabled;
    public string Host => _configuration.Host;
    public SQLiteProtocolHandler(ILogger<SQLiteProtocolHandler> logger, IOptions<SQLiteConfiguration> configuration, QueryExecutor queryExecutor)
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

            // Send SQLite welcome message
            await SendWelcomeMessageAsync();

            // Handle authentication (simplified for SQLite)
            var authResult = await HandleAuthenticationAsync();
            if (!authResult)
            {
                await SendErrorResponseAsync("Authentication failed");
                return;
            }

            await SendAuthenticationOkAsync();

            // Main connection loop
            await HandleQueriesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling SQLite connection");
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
            _logger.LogInformation("SQLite authentication attempt: username={Username}", username);

            // SQLite doesn't have built-in authentication, but we can implement a simple one
            var expectedUsername = _configuration.Authentication.Username;
            var expectedPassword = _configuration.Authentication.Password;

            if (username == expectedUsername && password == expectedPassword)
            {
                _logger.LogInformation("SQLite authentication successful!");
                return true;
            }
            else
            {
                _logger.LogWarning("SQLite authentication failed - username or password mismatch");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during SQLite authentication");
            return false;
        }
    }

    public async Task<QueryResult> ExecuteQueryAsync(string query, string database)
    {
        try
        {
            _logger.LogInformation("Executing SQLite query: {Query}", query);

            // Use QueryExecutor to handle SQL commands
            return await _queryExecutor.ExecuteAsync(query, "SQLite");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing SQLite query: {Query}", query);
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
            _logger.LogError(ex, "Error sending SQLite response");
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

            _logger.LogInformation("SQLite connection closed");
        }
        catch (ObjectDisposedException)
        {
            _logger.LogInformation("SQLite connection was already closed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing SQLite connection");
        }
    }

    private async Task SendWelcomeMessageAsync()
    {
        var welcomeMessage = "VnSQL SQLite Server v1.0.0\r\n" +
                           "Connected to SQLite database\r\n" +
                           "Type 'help' for commands\r\n\r\n";

        var messageBytes = Encoding.UTF8.GetBytes(welcomeMessage);
        await _stream!.WriteAsync(messageBytes);
        await _stream.FlushAsync();

        _logger.LogInformation("Sent SQLite welcome message");
    }

    private async Task<bool> HandleAuthenticationAsync()
    {
        try
        {
            // Send authentication prompt
            var authPrompt = "Username: ";
            var promptBytes = Encoding.UTF8.GetBytes(authPrompt);
            await _stream!.WriteAsync(promptBytes);
            await _stream.FlushAsync();

            // Read username
            var username = await ReadLineAsync();
            if (string.IsNullOrEmpty(username))
            {
                return false;
            }

            // Send password prompt
            var passwordPrompt = "Password: ";
            var passwordPromptBytes = Encoding.UTF8.GetBytes(passwordPrompt);
            await _stream.WriteAsync(passwordPromptBytes);
            await _stream.FlushAsync();

            // Read password
            var password = await ReadLineAsync();
            if (string.IsNullOrEmpty(password))
            {
                return false;
            }

            return await AuthenticateAsync(username, password);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during SQLite authentication");
            return false;
        }
    }

    private async Task SendAuthenticationOkAsync()
    {
        var authOkMessage = "Authentication successful!\r\n";
        var messageBytes = Encoding.UTF8.GetBytes(authOkMessage);
        await _stream!.WriteAsync(messageBytes);
        await _stream.FlushAsync();

        _logger.LogInformation("Sent SQLite authentication OK");
    }

    private async Task HandleQueriesAsync()
    {
        try
        {
            _logger.LogInformation("Starting SQLite query handling loop");

            var prompt = "sqlite> ";
            var promptBytes = Encoding.UTF8.GetBytes(prompt);

            while (_client?.Connected == true && _stream != null)
            {
                // Send prompt
                await _stream.WriteAsync(promptBytes);
                await _stream.FlushAsync();

                // Read query
                var query = await ReadLineAsync();
                if (string.IsNullOrEmpty(query))
                {
                    continue;
                }

                // Handle special commands
                if (query.ToLower() == "quit" || query.ToLower() == "exit")
                {
                    _logger.LogInformation("Client requested quit");
                    break;
                }

                if (query.ToLower() == "help")
                {
                    await SendHelpMessageAsync();
                    continue;
                }

                // Execute query
                _logger.LogInformation("Executing SQLite query: {Query}", query);
                var result = await ExecuteQueryAsync(query, "sqlite");
                await SendResponseAsync(result);
            }
        }
        catch (ObjectDisposedException)
        {
            _logger.LogInformation("NetworkStream was disposed during SQLite query handling");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SQLite query handling loop");
        }

        _logger.LogInformation("SQLite query handling loop ended");
    }

    private async Task SendQueryResponseAsync(QueryResult result)
    {
        if (result.Data == null || result.ColumnNames == null)
        {
            await SendMessageAsync("Query executed successfully.\r\n");
            return;
        }

        // Print column headers
        var header = string.Join(" | ", result.ColumnNames);
        await SendMessageAsync(header + "\r\n");

        // Print separator
        var separator = string.Join(" | ", result.ColumnNames.Select(c => new string('-', c.Length)));
        await SendMessageAsync(separator + "\r\n");

        // Print data rows
        foreach (var row in result.Data)
        {
            var rowData = result.ColumnNames.Select(col => row.GetValueOrDefault(col)?.ToString() ?? "NULL");
            var rowString = string.Join(" | ", rowData);
            await SendMessageAsync(rowString + "\r\n");
        }

        await SendMessageAsync($"\r\n{result.Data.Count} row(s) returned.\r\n");
    }

    private async Task SendErrorResponseAsync(string errorMessage)
    {
        var errorResponse = $"Error: {errorMessage}\r\n";
        await SendMessageAsync(errorResponse);
    }

    private async Task SendHelpMessageAsync()
    {
        var helpMessage = @"
SQLite Commands:
  .help                    - Show this help message
  .quit                    - Exit SQLite
  .tables                  - List all tables
  .schema <table>          - Show table schema
  .databases               - List all databases

SQL Commands:
  SELECT * FROM table;     - Query data
  INSERT INTO table VALUES (...); - Insert data
  UPDATE table SET ...;    - Update data
  DELETE FROM table;       - Delete data
  CREATE TABLE ...;        - Create table
  DROP TABLE ...;          - Drop table

Examples:
  SELECT name FROM sqlite_master WHERE type='table';
  PRAGMA table_info(users);
  CREATE TABLE users (id INTEGER PRIMARY KEY, name TEXT);

";
        await SendMessageAsync(helpMessage);
    }

    private async Task SendMessageAsync(string message)
    {
        if (_stream == null) return;

        try
        {
            var messageBytes = Encoding.UTF8.GetBytes(message);
            await _stream.WriteAsync(messageBytes);
            await _stream.FlushAsync();
        }
        catch (ObjectDisposedException)
        {
            _logger.LogInformation("NetworkStream was disposed, client likely disconnected");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending SQLite message");
        }
    }

    private async Task<string> ReadLineAsync()
    {
        if (_stream == null) return string.Empty;

        try
        {
            var buffer = new byte[1024];
            var line = new StringBuilder();

            while (true)
            {
                var bytesRead = await _stream.ReadAsync(buffer, 0, 1);
                if (bytesRead == 0)
                {
                    break; // Client disconnected
                }

                var character = (char)buffer[0];
                if (character == '\n')
                {
                    break;
                }
                else if (character == '\r')
                {
                    continue; // Skip carriage return
                }
                else
                {
                    line.Append(character);
                }
            }

            return line.ToString().Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading SQLite input");
            return string.Empty;
        }
    }
}
