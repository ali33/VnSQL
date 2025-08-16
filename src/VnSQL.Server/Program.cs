using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VnSQL.Core.Interfaces;
using VnSQL.Storage.Engines;
using VnSQL.Protocols.Handlers;
using VnSQL.Protocols.Configuration;
using VnSQL.Cluster;
using VnSQL.Server.Services;
using VnSQL.Core.Sql;

namespace VnSQL.Server;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("🌐 VnSQL - SQL Server Phân tán cho người Việt Nam 🇻🇳");
        Console.WriteLine("==================================================");
        
        var host = CreateHostBuilder(args).Build();
        
        try
        {
            await host.RunAsync();
        }
        catch (Exception ex)
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "VnSQL Server failed to start");
            throw;
        }
    }
    
    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Configuration
                var configuration = context.Configuration;
                
                // Register storage engines
                services.AddSingleton<IStorageEngine, FileStorageEngine>(provider =>
                {
                    var logger = provider.GetRequiredService<ILogger<FileStorageEngine>>();
                    var dataPath = configuration["VnSQL:Storage:FileStorage:DataPath"] ?? "./data";
                    return new FileStorageEngine(logger, dataPath);
                });
                
                services.AddSingleton<IStorageEngine, MemoryStorageEngine>();
                
                services.AddSingleton<IStorageEngine, FastKvStorageEngine>(provider =>
                {
                    var logger = provider.GetRequiredService<ILogger<FastKvStorageEngine>>();
                    var dataPath = configuration["VnSQL:Storage:FastKv:DataPath"] ?? "./fastkv-data";
                    var shardCount = int.Parse(configuration["VnSQL:Storage:FastKv:ShardCount"] ?? "4");
                    return new FastKvStorageEngine(logger, dataPath, shardCount);
                });
                
                // Register MySQL configuration
                services.Configure<MySQLConfiguration>(
                    configuration.GetSection("VnSQL:Protocols:MySQL"));
                
                // Register PostgreSQL configuration
                services.Configure<PostgreSQLConfiguration>(
                    configuration.GetSection("VnSQL:Protocols:PostgreSQL"));
                
                // Register SQLite configuration
                services.Configure<SQLiteConfiguration>(
                    configuration.GetSection("VnSQL:Protocols:SQLite"));
                
                // Register SQL Server configuration
                services.Configure<SQLServerConfiguration>(
                    configuration.GetSection("VnSQL:Protocols:SQLServer"));
                
                // Register SQL components
                services.AddSingleton<IProtocolResponseFormatter, ProtocolResponseFormatter>();
                services.AddSingleton<QueryExecutor>();
                
                // Register protocol handlers
                services.AddSingleton<IProtocolHandler, MySQLProtocolHandler>();
                services.AddSingleton<IProtocolHandler, PostgreSQLProtocolHandler>();
                services.AddSingleton<IProtocolHandler, SQLiteProtocolHandler>();
                services.AddSingleton<IProtocolHandler, SQLServerProtocolHandler>();
                
                // Register cluster manager
                services.AddSingleton<IClusterManager>(provider =>
                {
                    var logger = provider.GetRequiredService<ILogger<ClusterManager>>();
                    var nodeId = configuration["VnSQL:Cluster:NodeId"] ?? "node-1";
                    var address = configuration["VnSQL:Cluster:Address"] ?? "localhost";
                    var port = int.Parse(configuration["VnSQL:Cluster:Port"] ?? "8080");
                    return new ClusterManager(logger, nodeId, address, port);
                });
                
                // Register main server service
                services.AddHostedService<VnSQLServerService>();
                
                // Register configuration
                services.AddSingleton(configuration);
            })
            .ConfigureLogging((context, logging) =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.AddDebug();
                
                var logLevel = context.Configuration["Logging:LogLevel:Default"] ?? "Information";
                logging.SetMinimumLevel(Enum.Parse<LogLevel>(logLevel));
            });
}
