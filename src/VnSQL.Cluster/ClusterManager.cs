using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using VnSQL.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace VnSQL.Cluster;

/// <summary>
/// Cluster Manager implementation
/// </summary>
public class ClusterManager : IClusterManager, IDisposable
{
    private readonly ILogger<ClusterManager> _logger;
    private readonly string _nodeId;
    private readonly string _address;
    private readonly int _port;
    private readonly ConcurrentDictionary<string, ClusterNode> _nodes = new();
    private readonly TcpListener? _listener;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private ClusterStatus _status = ClusterStatus.Disconnected;
    
    public string NodeId => _nodeId;
    public ClusterStatus Status => _status;
    public IEnumerable<ClusterNode> Nodes => _nodes.Values;
    
    public ClusterManager(ILogger<ClusterManager> logger, string nodeId, string address = "localhost", int port = 8080)
    {
        _logger = logger;
        _nodeId = nodeId;
        _address = address;
        _port = port;
        
        try
        {
            _listener = new TcpListener(IPAddress.Parse(address), port);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create TCP listener");
        }
    }
    
    public async Task InitializeAsync()
    {
        try
        {
            if (_listener != null)
            {
                _listener.Start();
                _logger.LogInformation("Cluster manager listening on {Address}:{Port}", _address, _port);
                
                // Start listening for incoming connections
                _ = Task.Run(ListenForConnectionsAsync);
            }
            
            _status = ClusterStatus.Connected;
            _logger.LogInformation("Cluster manager initialized successfully");
        }
        catch (Exception ex)
        {
            _status = ClusterStatus.Error;
            _logger.LogError(ex, "Failed to initialize cluster manager");
            throw;
        }
    }
    
    public async Task JoinClusterAsync(string clusterAddress)
    {
        try
        {
            _status = ClusterStatus.Connecting;
            _logger.LogInformation("Joining cluster at {ClusterAddress}", clusterAddress);
            
            var parts = clusterAddress.Split(':');
            var address = parts[0];
            var port = int.Parse(parts[1]);
            
            using var client = new TcpClient();
            await client.ConnectAsync(address, port);
            
            // Send join message
            var joinMessage = new ClusterMessage
            {
                Type = "JOIN",
                SourceNodeId = _nodeId,
                Data = new
                {
                    NodeId = _nodeId,
                    Address = _address,
                    Port = _port
                }
            };
            
            await SendMessageToClientAsync(client, joinMessage);
            
            _status = ClusterStatus.Connected;
            _logger.LogInformation("Successfully joined cluster");
        }
        catch (Exception ex)
        {
            _status = ClusterStatus.Error;
            _logger.LogError(ex, "Failed to join cluster at {ClusterAddress}", clusterAddress);
            throw;
        }
    }
    
    public async Task LeaveClusterAsync()
    {
        try
        {
            _logger.LogInformation("Leaving cluster");
            
            var leaveMessage = new ClusterMessage
            {
                Type = "LEAVE",
                SourceNodeId = _nodeId
            };
            
            await BroadcastMessageAsync(leaveMessage);
            
            _status = ClusterStatus.Disconnected;
            _logger.LogInformation("Successfully left cluster");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to leave cluster");
            throw;
        }
    }
    
    public async Task SyncDataAsync(string targetNodeId)
    {
        try
        {
            _logger.LogInformation("Syncing data with node {TargetNodeId}", targetNodeId);
            
            var syncMessage = new ClusterMessage
            {
                Type = "SYNC_REQUEST",
                SourceNodeId = _nodeId,
                TargetNodeId = targetNodeId,
                Data = new { Timestamp = DateTime.UtcNow }
            };
            
            await SendMessageAsync(targetNodeId, syncMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync data with node {TargetNodeId}", targetNodeId);
            throw;
        }
    }
    
    public async Task SendMessageAsync(string targetNodeId, ClusterMessage message)
    {
        try
        {
            var targetNode = GetNode(targetNodeId);
            if (targetNode == null)
            {
                _logger.LogWarning("Target node {TargetNodeId} not found", targetNodeId);
                return;
            }
            
            using var client = new TcpClient();
            await client.ConnectAsync(targetNode.Address, targetNode.Port);
            await SendMessageToClientAsync(client, message);
            
            _logger.LogDebug("Sent message to node {TargetNodeId}", targetNodeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to node {TargetNodeId}", targetNodeId);
            throw;
        }
    }
    
    public async Task BroadcastMessageAsync(ClusterMessage message)
    {
        try
        {
            var tasks = _nodes.Values
                .Where(node => node.Id != _nodeId)
                .Select(node => SendMessageAsync(node.Id, message));
            
            await Task.WhenAll(tasks);
            _logger.LogDebug("Broadcasted message to {Count} nodes", _nodes.Count - 1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast message");
            throw;
        }
    }
    
    public async Task<bool> IsNodeOnlineAsync(string nodeId)
    {
        try
        {
            var node = GetNode(nodeId);
            if (node == null) return false;
            
            using var client = new TcpClient();
            await client.ConnectAsync(node.Address, node.Port);
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public ClusterNode? GetNode(string nodeId)
    {
        _nodes.TryGetValue(nodeId, out var node);
        return node;
    }
    
    public async Task CloseAsync()
    {
        try
        {
            _cancellationTokenSource.Cancel();
            _listener?.Stop();
            _status = ClusterStatus.Disconnected;
            _logger.LogInformation("Cluster manager closed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing cluster manager");
        }
    }
    
    private async Task ListenForConnectionsAsync()
    {
        while (!_cancellationTokenSource.Token.IsCancellationRequested && _listener != null)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync();
                _ = Task.Run(() => HandleClientConnectionAsync(client));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error accepting client connection");
            }
        }
    }
    
    private async Task HandleClientConnectionAsync(TcpClient client)
    {
        try
        {
            using var stream = client.GetStream();
            var buffer = new byte[4096];
            
            while (client.Connected)
            {
                var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;
                
                var messageJson = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
                var message = JsonSerializer.Deserialize<ClusterMessage>(messageJson);
                
                if (message != null)
                {
                    await HandleMessageAsync(message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling client connection");
        }
        finally
        {
            client.Close();
        }
    }
    
    private async Task HandleMessageAsync(ClusterMessage message)
    {
        try
        {
            _logger.LogDebug("Received message: {MessageType} from {SourceNodeId}", message.Type, message.SourceNodeId);
            
            switch (message.Type)
            {
                case "JOIN":
                    await HandleJoinMessageAsync(message);
                    break;
                case "LEAVE":
                    await HandleLeaveMessageAsync(message);
                    break;
                case "SYNC_REQUEST":
                    await HandleSyncRequestAsync(message);
                    break;
                case "SYNC_RESPONSE":
                    await HandleSyncResponseAsync(message);
                    break;
                case "HEARTBEAT":
                    await HandleHeartbeatAsync(message);
                    break;
                default:
                    _logger.LogWarning("Unknown message type: {MessageType}", message.Type);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling message: {MessageType}", message.Type);
        }
    }
    
    private async Task HandleJoinMessageAsync(ClusterMessage message)
    {
        if (message.Data is JsonElement element)
        {
            var nodeData = JsonSerializer.Deserialize<dynamic>(element.GetRawText());
            // Add node to cluster
            var node = new ClusterNode
            {
                Id = message.SourceNodeId,
                Address = "localhost", // In real implementation, extract from message
                Port = 8080,
                IsOnline = true,
                LastSeen = DateTime.UtcNow
            };
            
            _nodes.TryAdd(node.Id, node);
            _logger.LogInformation("Node {NodeId} joined the cluster", node.Id);
        }
    }
    
    private async Task HandleLeaveMessageAsync(ClusterMessage message)
    {
        _nodes.TryRemove(message.SourceNodeId, out _);
        _logger.LogInformation("Node {NodeId} left the cluster", message.SourceNodeId);
    }
    
    private async Task HandleSyncRequestAsync(ClusterMessage message)
    {
        // Send sync response
        var syncResponse = new ClusterMessage
        {
            Type = "SYNC_RESPONSE",
            SourceNodeId = _nodeId,
            TargetNodeId = message.SourceNodeId,
            Data = new { Timestamp = DateTime.UtcNow }
        };
        
        await SendMessageAsync(message.SourceNodeId, syncResponse);
    }
    
    private async Task HandleSyncResponseAsync(ClusterMessage message)
    {
        _logger.LogDebug("Received sync response from {SourceNodeId}", message.SourceNodeId);
    }
    
    private async Task HandleHeartbeatAsync(ClusterMessage message)
    {
        if (_nodes.TryGetValue(message.SourceNodeId, out var node))
        {
            node.LastSeen = DateTime.UtcNow;
            node.IsOnline = true;
        }
    }
    
    private async Task SendMessageToClientAsync(TcpClient client, ClusterMessage message)
    {
        var messageJson = JsonSerializer.Serialize(message);
        var messageBytes = System.Text.Encoding.UTF8.GetBytes(messageJson);
        
        using var stream = client.GetStream();
        await stream.WriteAsync(messageBytes, 0, messageBytes.Length);
        await stream.FlushAsync();
    }
    
    public void Dispose()
    {
        _cancellationTokenSource?.Dispose();
        _listener?.Stop();
    }
}
