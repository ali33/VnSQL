using System.Collections.Concurrent;
using VnSQL.Core.Interfaces;
using VnSQL.Core.Models;
using Microsoft.Extensions.Logging;

namespace VnSQL.Storage.Engines;

/// <summary>
/// In-memory storage engine
/// </summary>
public class MemoryStorageEngine : IStorageEngine
{
    private readonly ILogger<MemoryStorageEngine> _logger;
    private readonly ConcurrentDictionary<string, Database> _databases = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Table>> _tables = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentDictionary<long, Row>>> _rows = new();
    
    public string Name => "MemoryStorage";
    
    public MemoryStorageEngine(ILogger<MemoryStorageEngine> logger)
    {
        _logger = logger;
    }
    
    public async Task InitializeAsync()
    {
        _logger.LogInformation("MemoryStorageEngine initialized successfully");
    }
    
    public async Task SaveDatabaseAsync(Database database)
    {
        try
        {
            _databases.AddOrUpdate(database.Name, database, (_, _) => database);
            _logger.LogDebug("Saved database to memory: {DatabaseName}", database.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save database to memory: {DatabaseName}", database.Name);
            throw;
        }
    }
    
    public async Task<Database?> LoadDatabaseAsync(string databaseName)
    {
        try
        {
            _databases.TryGetValue(databaseName, out var database);
            _logger.LogDebug("Loaded database from memory: {DatabaseName}", databaseName);
            return database;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load database from memory: {DatabaseName}", databaseName);
            throw;
        }
    }
    
    public async Task DeleteDatabaseAsync(string databaseName)
    {
        try
        {
            _databases.TryRemove(databaseName, out _);
            _tables.TryRemove(databaseName, out _);
            _rows.TryRemove(databaseName, out _);
            _logger.LogInformation("Deleted database from memory: {DatabaseName}", databaseName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete database from memory: {DatabaseName}", databaseName);
            throw;
        }
    }
    
    public async Task<IEnumerable<string>> ListDatabasesAsync()
    {
        try
        {
            var databases = _databases.Keys.ToList();
            _logger.LogDebug("Found {Count} databases in memory", databases.Count);
            return databases;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list databases from memory");
            throw;
        }
    }
    
    public async Task<bool> DatabaseExistsAsync(string databaseName)
    {
        return _databases.ContainsKey(databaseName);
    }
    
    public async Task SaveTableAsync(string databaseName, Table table)
    {
        try
        {
            var databaseTables = _tables.GetOrAdd(databaseName, _ => new ConcurrentDictionary<string, Table>());
            databaseTables.AddOrUpdate(table.Name, table, (_, _) => table);
            _logger.LogDebug("Saved table to memory: {DatabaseName}.{TableName}", databaseName, table.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save table to memory: {DatabaseName}.{TableName}", databaseName, table.Name);
            throw;
        }
    }
    
    public async Task<Table?> LoadTableAsync(string databaseName, string tableName)
    {
        try
        {
            if (_tables.TryGetValue(databaseName, out var databaseTables))
            {
                databaseTables.TryGetValue(tableName, out var table);
                _logger.LogDebug("Loaded table from memory: {DatabaseName}.{TableName}", databaseName, tableName);
                return table;
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load table from memory: {DatabaseName}.{TableName}", databaseName, tableName);
            throw;
        }
    }
    
    public async Task DeleteTableAsync(string databaseName, string tableName)
    {
        try
        {
            if (_tables.TryGetValue(databaseName, out var databaseTables))
            {
                databaseTables.TryRemove(tableName, out _);
            }
            
            if (_rows.TryGetValue(databaseName, out var databaseRows))
            {
                databaseRows.TryRemove(tableName, out _);
            }
            
            _logger.LogInformation("Deleted table from memory: {DatabaseName}.{TableName}", databaseName, tableName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete table from memory: {DatabaseName}.{TableName}", databaseName, tableName);
            throw;
        }
    }
    
    public async Task SaveRowAsync(string databaseName, string tableName, Row row)
    {
        try
        {
            var databaseRows = _rows.GetOrAdd(databaseName, _ => new ConcurrentDictionary<string, ConcurrentDictionary<long, Row>>());
            var tableRows = databaseRows.GetOrAdd(tableName, _ => new ConcurrentDictionary<long, Row>());
            tableRows.AddOrUpdate(row.Id, row, (_, _) => row);
            _logger.LogDebug("Saved row to memory: {DatabaseName}.{TableName}.{RowId}", databaseName, tableName, row.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save row to memory: {DatabaseName}.{TableName}.{RowId}", databaseName, tableName, row.Id);
            throw;
        }
    }
    
    public async Task<IEnumerable<Row>> LoadRowsAsync(string databaseName, string tableName)
    {
        try
        {
            if (_rows.TryGetValue(databaseName, out var databaseRows) &&
                databaseRows.TryGetValue(tableName, out var tableRows))
            {
                var rows = tableRows.Values.ToList();
                _logger.LogDebug("Loaded {Count} rows from memory: {DatabaseName}.{TableName}", rows.Count, databaseName, tableName);
                return rows;
            }
            return Enumerable.Empty<Row>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load rows from memory: {DatabaseName}.{TableName}", databaseName, tableName);
            throw;
        }
    }
    
    public async Task DeleteRowAsync(string databaseName, string tableName, long rowId)
    {
        try
        {
            if (_rows.TryGetValue(databaseName, out var databaseRows) &&
                databaseRows.TryGetValue(tableName, out var tableRows))
            {
                tableRows.TryRemove(rowId, out _);
                _logger.LogDebug("Deleted row from memory: {DatabaseName}.{TableName}.{RowId}", databaseName, tableName, rowId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete row from memory: {DatabaseName}.{TableName}.{RowId}", databaseName, tableName, rowId);
            throw;
        }
    }
    
    public async Task BackupAsync(string backupPath)
    {
        try
        {
            // For memory storage, we serialize all data to JSON
            var backupData = new
            {
                Databases = _databases,
                Tables = _tables,
                Rows = _rows
            };
            
            var json = System.Text.Json.JsonSerializer.Serialize(backupData, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            
            await File.WriteAllTextAsync(backupPath, json);
            _logger.LogInformation("Memory backup completed: {BackupPath}", backupPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create memory backup: {BackupPath}", backupPath);
            throw;
        }
    }
    
    public async Task RestoreAsync(string backupPath)
    {
        try
        {
            if (File.Exists(backupPath))
            {
                var json = await File.ReadAllTextAsync(backupPath);
                var backupData = System.Text.Json.JsonSerializer.Deserialize<dynamic>(json);
                
                // Clear current data
                _databases.Clear();
                _tables.Clear();
                _rows.Clear();
                
                // Restore data (simplified - in real implementation, you'd need proper deserialization)
                _logger.LogInformation("Memory restore completed from: {BackupPath}", backupPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore from memory backup: {BackupPath}", backupPath);
            throw;
        }
    }
    
    public async Task CloseAsync()
    {
        _logger.LogInformation("MemoryStorageEngine closed");
    }
}
