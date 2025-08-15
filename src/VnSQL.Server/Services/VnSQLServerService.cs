using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using VnSQL.Core.Interfaces;

namespace VnSQL.Server.Services;

/// <summary>
/// Main VnSQL Server Service
/// </summary>
public class VnSQLServerService : BackgroundService
{
    private readonly ILogger<VnSQLServerService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IStorageEngine _storageEngine;
    private readonly IProtocolHandler _protocolHandler;
    private readonly IClusterManager _clusterManager;
    private readonly List<TcpListener> _listeners = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    
    public VnSQLServerService(
        ILogger<VnSQLServerService> logger,
        IConfiguration configuration,
        IStorageEngine storageEngine,
        IProtocolHandler protocolHandler,
        IClusterManager clusterManager)
    {
        _logger = logger;
        _configuration = configuration;
        _storageEngine = storageEngine;
        _protocolHandler = protocolHandler;
        _clusterManager = clusterManager;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Starting VnSQL Server...");
            
            // Initialize storage engine
            await _storageEngine.InitializeAsync();
            _logger.LogInformation("Storage engine initialized: {StorageType}", _storageEngine.Name);
            
            // Initialize cluster manager if enabled
            var clusterEnabled = _configuration.GetValue<bool>("VnSQL:Cluster:Enabled");
            if (clusterEnabled)
            {
                await _clusterManager.InitializeAsync();
                _logger.LogInformation("Cluster manager initialized");
                
                // Join cluster if specified
                var clusterNodes = _configuration.GetSection("VnSQL:Cluster:Nodes").Get<string[]>();
                if (clusterNodes?.Length > 0)
                {
                    await _clusterManager.JoinClusterAsync(clusterNodes[0]);
                    _logger.LogInformation("Joined cluster at {ClusterAddress}", clusterNodes[0]);
                }
            }
            
            // Start protocol listeners
            await StartProtocolListenersAsync();
            
            _logger.LogInformation("VnSQL Server started successfully");
            _logger.LogInformation("Supported protocols: {ProtocolName} on port {Port}", 
                _protocolHandler.ProtocolName, _protocolHandler.DefaultPort);
            
            // Keep the service running
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting VnSQL Server");
            throw;
        }
        finally
        {
            await StopAsync(stoppingToken);
        }
    }
    
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Stopping VnSQL Server...");
            
            // Stop all listeners
            foreach (var listener in _listeners)
            {
                listener.Stop();
            }
            _listeners.Clear();
            
            // Close cluster manager
            await _clusterManager.CloseAsync();
            
            // Close storage engine
            await _storageEngine.CloseAsync();
            
            _logger.LogInformation("VnSQL Server stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping VnSQL Server");
        }
        finally
        {
            await base.StopAsync(cancellationToken);
        }
    }
    
    private async Task StartProtocolListenersAsync()
    {
        try
        {
            // Get protocol configuration
            var mysqlEnabled = _configuration.GetValue<bool>("VnSQL:Protocols:MySQL:Enabled");
            var mysqlPort = _configuration.GetValue<int>("VnSQL:Protocols:MySQL:Port", 3306);
            
            var postgresEnabled = _configuration.GetValue<bool>("VnSQL:Protocols:PostgreSQL:Enabled");
            var postgresPort = _configuration.GetValue<int>("VnSQL:Protocols:PostgreSQL:Port", 5432);
            
            var sqliteEnabled = _configuration.GetValue<bool>("VnSQL:Protocols:SQLite:Enabled");
            var sqlitePort = _configuration.GetValue<int>("VnSQL:Protocols:SQLite:Port", 5433);
            
            var host = _configuration.GetValue<string>("VnSQL:Server:Host", "0.0.0.0");
            var maxConnections = _configuration.GetValue<int>("VnSQL:Server:MaxConnections", 1000);
            
            // Start MySQL listener
            if (mysqlEnabled)
            {
                await StartListenerAsync(host, mysqlPort, "MySQL", maxConnections);
            }
            
            // Start PostgreSQL listener (placeholder)
            if (postgresEnabled)
            {
                _logger.LogInformation("PostgreSQL protocol not yet implemented");
            }
            
            // Start SQLite listener (placeholder)
            if (sqliteEnabled)
            {
                _logger.LogInformation("SQLite protocol not yet implemented");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting protocol listeners");
            throw;
        }
    }
    
    private async Task StartListenerAsync(string host, int port, string protocolName, int maxConnections)
    {
        try
        {
            var listener = new TcpListener(IPAddress.Parse(host), port);
            listener.Start(maxConnections);
            _listeners.Add(listener);
            
            _logger.LogInformation("Started {ProtocolName} listener on {Host}:{Port}", protocolName, host, port);
            
            // Start accepting connections
            _ = Task.Run(async () =>
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        var client = await listener.AcceptTcpClientAsync(_cancellationTokenSource.Token);
                        _logger.LogDebug("Accepted {ProtocolName} connection from {RemoteEndPoint}", 
                            protocolName, client.Client.RemoteEndPoint);
                        
                        // Handle connection in background
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await _protocolHandler.HandleConnectionAsync(client);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error handling {ProtocolName} connection", protocolName);
                            }
                        }, _cancellationTokenSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error accepting {ProtocolName} connection", protocolName);
                    }
                }
            }, _cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start {ProtocolName} listener on port {Port}", protocolName, port);
            throw;
        }
    }
}
