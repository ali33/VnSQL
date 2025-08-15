namespace VnSQL.Core.Interfaces;

/// <summary>
/// Interface cho cluster manager
/// </summary>
public interface IClusterManager
{
    /// <summary>
    /// Node ID của instance hiện tại
    /// </summary>
    string NodeId { get; }
    
    /// <summary>
    /// Trạng thái cluster
    /// </summary>
    ClusterStatus Status { get; }
    
    /// <summary>
    /// Danh sách các node trong cluster
    /// </summary>
    IEnumerable<ClusterNode> Nodes { get; }
    
    /// <summary>
    /// Khởi tạo cluster
    /// </summary>
    Task InitializeAsync();
    
    /// <summary>
    /// Tham gia cluster
    /// </summary>
    Task JoinClusterAsync(string clusterAddress);
    
    /// <summary>
    /// Rời khỏi cluster
    /// </summary>
    Task LeaveClusterAsync();
    
    /// <summary>
    /// Đồng bộ dữ liệu với node khác
    /// </summary>
    Task SyncDataAsync(string targetNodeId);
    
    /// <summary>
    /// Gửi message đến node khác
    /// </summary>
    Task SendMessageAsync(string targetNodeId, ClusterMessage message);
    
    /// <summary>
    /// Broadcast message đến tất cả nodes
    /// </summary>
    Task BroadcastMessageAsync(ClusterMessage message);
    
    /// <summary>
    /// Kiểm tra node có online không
    /// </summary>
    Task<bool> IsNodeOnlineAsync(string nodeId);
    
    /// <summary>
    /// Lấy thông tin node
    /// </summary>
    ClusterNode? GetNode(string nodeId);
    
    /// <summary>
    /// Đóng cluster manager
    /// </summary>
    Task CloseAsync();
}

/// <summary>
/// Trạng thái cluster
/// </summary>
public enum ClusterStatus
{
    Disconnected,
    Connecting,
    Connected,
    Syncing,
    Error
}

/// <summary>
/// Thông tin node trong cluster
/// </summary>
public class ClusterNode
{
    public string Id { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int Port { get; set; }
    public bool IsOnline { get; set; }
    public DateTime LastSeen { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Message trong cluster
/// </summary>
public class ClusterMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Type { get; set; } = string.Empty;
    public string SourceNodeId { get; set; } = string.Empty;
    public string? TargetNodeId { get; set; }
    public object? Data { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
