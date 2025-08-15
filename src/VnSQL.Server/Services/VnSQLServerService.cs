using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VnSQL.Core.Interfaces;
using VnSQL.Protocols.Handlers;

namespace VnSQL.Server.Services;

/// <summary>
/// Main VnSQL Server Service
/// </summary>
public class VnSQLServerService : BackgroundService
{
    private readonly ILogger<VnSQLServerService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IStorageEngine _storageEngine;
    private readonly IServiceProvider _serviceProvider;
    private readonly IClusterManager _clusterManager;
    private readonly List<TcpListener> _listeners = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public VnSQLServerService(
        ILogger<VnSQLServerService> logger,
        IConfiguration configuration,
        IStorageEngine storageEngine,
        IServiceProvider serviceProvider,
        IClusterManager clusterManager)
    {
        _logger = logger;
        _configuration = configuration;
        _storageEngine = storageEngine;
        _serviceProvider = serviceProvider;
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

            _logger.LogInformation("VnSQL Server started successfully with multiple protocols");

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
            var host = _configuration.GetValue<string>("VnSQL:Server:Host", "127.0.0.1");
            var maxConnections = _configuration.GetValue<int>("VnSQL:Server:MaxConnections", 1000);

            // Get all protocol handlers from DI container
            var protocolHandlers = _serviceProvider.GetServices<IProtocolHandler>().ToList();
            _logger.LogInformation("Found {Count} protocol handlers", protocolHandlers.Count);

            foreach (var protocolHandler in protocolHandlers)
            {
                if (!protocolHandler.Enabled)
                    continue;
                string? currentHost = string.IsNullOrWhiteSpace(protocolHandler?.Host) ? host : protocolHandler?.Host;
                if (currentHost == null || string.IsNullOrWhiteSpace(currentHost))
                    currentHost = "127.0.0.1";
                await StartListenerAsync(currentHost, protocolHandler.Port, protocolHandler.ProtocolName, maxConnections, protocolHandler);
            }

            //// Start MySQL Protocol
            //var mysqlEnabled = _configuration.GetValue<bool>("VnSQL:Protocols:MySQL:Enabled");
            //if (mysqlEnabled)
            //{
            //    var mysqlHandler = protocolHandlers.FirstOrDefault(h => h.ProtocolName == "MySQL");
            //    if (mysqlHandler != null)
            //    {
            //        var mysqlPort = _configuration.GetValue<int>("VnSQL:Protocols:MySQL:Port", 3306);
            //        await StartListenerAsync(host, mysqlPort, "MySQL", maxConnections, mysqlHandler);
            //    }
            //    else
            //    {
            //        _logger.LogWarning("MySQL protocol enabled but handler not found");
            //    }
            //}

            //// Start PostgreSQL Protocol
            //var postgresEnabled = _configuration.GetValue<bool>("VnSQL:Protocols:PostgreSQL:Enabled");
            //if (postgresEnabled)
            //{
            //    var postgresHandler = protocolHandlers.FirstOrDefault(h => h.ProtocolName == "PostgreSQL");
            //    if (postgresHandler != null)
            //    {
            //        var postgresPort = _configuration.GetValue<int>("VnSQL:Protocols:PostgreSQL:Port", 5432);
            //        await StartListenerAsync(host, postgresPort, "PostgreSQL", maxConnections, postgresHandler);
            //    }
            //    else
            //    {
            //        _logger.LogWarning("PostgreSQL protocol enabled but handler not found");
            //    }
            //}

            //// Start SQLite Protocol
            //var sqliteEnabled = _configuration.GetValue<bool>("VnSQL:Protocols:SQLite:Enabled");
            //if (sqliteEnabled)
            //{
            //    var sqliteHandler = protocolHandlers.FirstOrDefault(h => h.ProtocolName == "SQLite");
            //    if (sqliteHandler != null)
            //    {
            //        var sqlitePort = _configuration.GetValue<int>("VnSQL:Protocols:SQLite:Port", 5433);
            //        await StartListenerAsync(host, sqlitePort, "SQLite", maxConnections, sqliteHandler);
            //    }
            //    else
            //    {
            //        _logger.LogWarning("SQLite protocol enabled but handler not found");
            //    }
            //}

            //// Start SQL Server Protocol
            //var sqlserverEnabled = _configuration.GetValue<bool>("VnSQL:Protocols:SQLServer:Enabled");
            //if (sqlserverEnabled)
            //{
            //    var sqlserverHandler = protocolHandlers.FirstOrDefault(h => h.ProtocolName == "SQLServer");
            //    if (sqlserverHandler != null)
            //    {
            //        var sqlserverPort = _configuration.GetValue<int>("VnSQL:Protocols:SQLServer:Port", 1433);
            //        await StartListenerAsync(host, sqlserverPort, "SQLServer", maxConnections, sqlserverHandler);
            //    }
            //    else
            //    {
            //        _logger.LogWarning("SQL Server protocol enabled but handler not found");
            //    }
            //}
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting protocol listeners");
            throw;
        }
    }

    private async Task StartListenerAsync(string host, int port, string protocolName, int maxConnections, IProtocolHandler handler)
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
                                await handler.HandleConnectionAsync(client);
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
