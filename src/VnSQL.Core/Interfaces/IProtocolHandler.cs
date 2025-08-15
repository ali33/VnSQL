using System.Net.Sockets;

namespace VnSQL.Core.Interfaces;

/// <summary>
/// Interface cho protocol handler
/// </summary>
public interface IProtocolHandler
{
    /// <summary>
    /// Tên của protocol
    /// </summary>
    string ProtocolName { get; }

    /// <summary>
    /// Port mặc định của protocol
    /// </summary>
    int Port { get; }

    bool Enabled { get; }

    string Host { get; } 

    /// <summary>
    /// Xử lý kết nối mới
    /// </summary>
    Task HandleConnectionAsync(TcpClient client);

    /// <summary>
    /// Xử lý authentication
    /// </summary>
    Task<bool> AuthenticateAsync(string username, string password);

    /// <summary>
    /// Xử lý query
    /// </summary>
    Task<QueryResult> ExecuteQueryAsync(string query, string database);

    /// <summary>
    /// Gửi response
    /// </summary>
    Task SendResponseAsync(QueryResult result);

    /// <summary>
    /// Đóng kết nối
    /// </summary>
    Task CloseConnectionAsync();
}

/// <summary>
/// Kết quả của query
/// </summary>
public class QueryResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int AffectedRows { get; set; }
    public long LastInsertId { get; set; }
    public List<Dictionary<string, object?>>? Data { get; set; }
    public List<string>? ColumnNames { get; set; }
    public List<string>? ColumnTypes { get; set; }
}
