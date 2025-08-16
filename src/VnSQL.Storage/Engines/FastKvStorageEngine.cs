using VnSQL.Core.Interfaces;
using VnSQL.Core.Models;
using Microsoft.Extensions.Logging;
using VnSQL.Storage.Kv.FastKv;
using System.Text.Json;

namespace VnSQL.Storage.Engines;

/// <summary>
/// FastKV-based persistent storage engine using ShardedPersistentDictionary
/// </summary>
public class FastKvStorageEngine : IStorageEngine, IDisposable
{
    private readonly ILogger<FastKvStorageEngine> _logger;
    private readonly string _dataPath;
    private readonly int _shardCount;
    
    // Sharded dictionaries for hierarchical structure
    private ShardedPersistentDictionary<string, Database>? _databases;
    private ShardedPersistentDictionary<string, Table>? _tables;
    private ShardedPersistentDictionary<string, Row>? _rows;
    
    // Key codecs and serializers
    private readonly StringKeyCodec _stringCodec = new();
    private readonly DatabaseSerializer _databaseSerializer = new();
    private readonly TableSerializer _tableSerializer = new();
    private readonly RowSerializer _rowSerializer = new();
    
    public string Name => "FastKvStorage";
    
    public FastKvStorageEngine(ILogger<FastKvStorageEngine> logger, string dataPath = "data", int shardCount = 4)
    {
        _logger = logger;
        _dataPath = dataPath;
        _shardCount = shardCount;
        
        // Ensure data directory exists
        Directory.CreateDirectory(_dataPath);
    }
    
    public async Task InitializeAsync()
    {
        try
        {
            // Initialize sharded dictionaries for hierarchical structure
            _databases = new ShardedPersistentDictionary<string, Database>(
                Path.Combine(_dataPath, "databases"),
                _shardCount,
                _stringCodec,
                _databaseSerializer,
                writeThrough: true);
                
            _tables = new ShardedPersistentDictionary<string, Table>(
                Path.Combine(_dataPath, "tables"),
                _shardCount,
                _stringCodec,
                _tableSerializer,
                writeThrough: true);
                
            _rows = new ShardedPersistentDictionary<string, Row>(
                Path.Combine(_dataPath, "rows"),
                _shardCount,
                _stringCodec,
                _rowSerializer,
                writeThrough: true);
                
            _logger.LogInformation("FastKvStorageEngine initialized successfully with {ShardCount} shards at {DataPath}", _shardCount, _dataPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize FastKvStorageEngine");
            throw;
        }
    }
    
    public async Task SaveDatabaseAsync(Database database)
    {
        try
        {
            if (_databases == null) throw new InvalidOperationException("Storage engine not initialized");
            
            await _databases.PutAsync(database.Name, database);
            _logger.LogDebug("Saved database to FastKV: {DatabaseName}", database.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save database to FastKV: {DatabaseName}", database.Name);
            throw;
        }
    }
    
    public async Task<Database?> LoadDatabaseAsync(string databaseName)
    {
        try
        {
            if (_databases == null) throw new InvalidOperationException("Storage engine not initialized");
            
            if (_databases.TryGetValue(databaseName, out var database))
            {
                _logger.LogDebug("Loaded database from FastKV: {DatabaseName}", databaseName);
                return database;
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load database from FastKV: {DatabaseName}", databaseName);
            throw;
        }
    }
    
    public async Task DeleteDatabaseAsync(string databaseName)
    {
        try
        {
            if (_databases == null) throw new InvalidOperationException("Storage engine not initialized");
            
            // Delete database
            await _databases.DeleteAsync(databaseName);
            
            // Delete all tables for this database
            var tableKeys = _tables!.ScanAllLiveItems()
                .Where(kv => kv.Key.StartsWith($"{databaseName}."))
                .Select(kv => kv.Key)
                .ToList();
                
            if (tableKeys.Any())
            {
                await _tables.DeleteBatchAsync(tableKeys);
            }
            
            // Delete all rows for this database
            var rowKeys = _rows!.ScanAllLiveItems()
                .Where(kv => kv.Key.StartsWith($"{databaseName}."))
                .Select(kv => kv.Key)
                .ToList();
                
            if (rowKeys.Any())
            {
                await _rows.DeleteBatchAsync(rowKeys);
            }
            
            _logger.LogInformation("Deleted database from FastKV: {DatabaseName}", databaseName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete database from FastKV: {DatabaseName}", databaseName);
            throw;
        }
    }
    
    public async Task<IEnumerable<string>> ListDatabasesAsync()
    {
        try
        {
            if (_databases == null) throw new InvalidOperationException("Storage engine not initialized");
            
            var databases = _databases.ScanAllLiveItems()
                .Select(kv => kv.Key)
                .ToList();
                
            _logger.LogDebug("Found {Count} databases in FastKV", databases.Count);
            return databases;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list databases from FastKV");
            throw;
        }
    }
    
    public async Task<bool> DatabaseExistsAsync(string databaseName)
    {
        try
        {
            if (_databases == null) throw new InvalidOperationException("Storage engine not initialized");
            return _databases.ContainsKey(databaseName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check database existence: {DatabaseName}", databaseName);
            throw;
        }
    }
    
    public async Task SaveTableAsync(string databaseName, Table table)
    {
        try
        {
            if (_tables == null) throw new InvalidOperationException("Storage engine not initialized");
            
            // Check if database exists
            if (!await DatabaseExistsAsync(databaseName))
            {
                throw new InvalidOperationException($"Database '{databaseName}' does not exist");
            }
            
            // Use hierarchical key: database.table
            var key = $"{databaseName}.{table.Name}";
            await _tables.PutAsync(key, table);
            _logger.LogDebug("Saved table to FastKV: {DatabaseName}.{TableName}", databaseName, table.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save table to FastKV: {DatabaseName}.{TableName}", databaseName, table.Name);
            throw;
        }
    }
    
    public async Task<Table?> LoadTableAsync(string databaseName, string tableName)
    {
        try
        {
            if (_tables == null) throw new InvalidOperationException("Storage engine not initialized");
            
            var key = $"{databaseName}.{tableName}";
            if (_tables.TryGetValue(key, out var table))
            {
                _logger.LogDebug("Loaded table from FastKV: {DatabaseName}.{TableName}", databaseName, tableName);
                return table;
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load table from FastKV: {DatabaseName}.{TableName}", databaseName, tableName);
            throw;
        }
    }
    
    public async Task DeleteTableAsync(string databaseName, string tableName)
    {
        try
        {
            if (_tables == null || _rows == null) throw new InvalidOperationException("Storage engine not initialized");
            
            var tableKey = $"{databaseName}.{tableName}";
            await _tables.DeleteAsync(tableKey);
            
            // Delete all rows for this table
            var rowKeys = _rows.ScanAllLiveItems()
                .Where(kv => kv.Key.StartsWith($"{databaseName}.{tableName}."))
                .Select(kv => kv.Key)
                .ToList();
                
            if (rowKeys.Any())
            {
                await _rows.DeleteBatchAsync(rowKeys);
            }
            
            _logger.LogInformation("Deleted table from FastKV: {DatabaseName}.{TableName}", databaseName, tableName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete table from FastKV: {DatabaseName}.{TableName}", databaseName, tableName);
            throw;
        }
    }
    
    public async Task SaveRowAsync(string databaseName, string tableName, Row row)
    {
        try
        {
            if (_rows == null) throw new InvalidOperationException("Storage engine not initialized");
            
            // Check if table exists
            var table = await LoadTableAsync(databaseName, tableName);
            if (table == null)
            {
                throw new InvalidOperationException($"Table '{tableName}' does not exist in database '{databaseName}'");
            }
            
            // Use hierarchical key: database.table.rowId
            var key = $"{databaseName}.{tableName}.{row.Id}";
            await _rows.PutAsync(key, row);
            _logger.LogDebug("Saved row to FastKV: {DatabaseName}.{TableName}.{RowId}", databaseName, tableName, row.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save row to FastKV: {DatabaseName}.{TableName}.{RowId}", databaseName, tableName, row.Id);
            throw;
        }
    }
    
    public async Task<IEnumerable<Row>> LoadRowsAsync(string databaseName, string tableName)
    {
        try
        {
            if (_rows == null) throw new InvalidOperationException("Storage engine not initialized");
            
            var prefix = $"{databaseName}.{tableName}.";
            var rows = _rows.ScanAllLiveItems()
                .Where(kv => kv.Key.StartsWith(prefix))
                .Select(kv => kv.Value)
                .ToList();
                
            _logger.LogDebug("Loaded {Count} rows from FastKV: {DatabaseName}.{TableName}", rows.Count, databaseName, tableName);
            return rows;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load rows from FastKV: {DatabaseName}.{TableName}", databaseName, tableName);
            throw;
        }
    }
    
    public async Task DeleteRowAsync(string databaseName, string tableName, long rowId)
    {
        try
        {
            if (_rows == null) throw new InvalidOperationException("Storage engine not initialized");
            
            var key = $"{databaseName}.{tableName}.{rowId}";
            await _rows.DeleteAsync(key);
            _logger.LogDebug("Deleted row from FastKV: {DatabaseName}.{TableName}.{RowId}", databaseName, tableName, rowId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete row from FastKV: {DatabaseName}.{TableName}.{RowId}", databaseName, tableName, rowId);
            throw;
        }
    }
    
    public async Task BackupAsync(string backupPath)
    {
        try
        {
            if (_databases == null || _tables == null || _rows == null) 
                throw new InvalidOperationException("Storage engine not initialized");
            
            // Create backup directory
            Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
            
            // Serialize all data to JSON
            var backupData = new
            {
                Databases = _databases.SnapshotToDictionary(),
                Tables = _tables.SnapshotToDictionary(),
                Rows = _rows.SnapshotToDictionary()
            };
            
            var json = JsonSerializer.Serialize(backupData, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            
            await File.WriteAllTextAsync(backupPath, json);
            _logger.LogInformation("FastKV backup completed: {BackupPath}", backupPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create FastKV backup: {BackupPath}", backupPath);
            throw;
        }
    }
    
    public async Task RestoreAsync(string backupPath)
    {
        try
        {
            if (_databases == null || _tables == null || _rows == null) 
                throw new InvalidOperationException("Storage engine not initialized");
            
            if (File.Exists(backupPath))
            {
                var json = await File.ReadAllTextAsync(backupPath);
                var backupData = JsonSerializer.Deserialize<BackupData>(json);
                
                if (backupData != null)
                {
                    // Clear current data by deleting all existing items
                    var existingDatabases = _databases.ScanAllLiveItems().Select(kv => kv.Key).ToList();
                    var existingTables = _tables.ScanAllLiveItems().Select(kv => kv.Key).ToList();
                    var existingRows = _rows.ScanAllLiveItems().Select(kv => kv.Key).ToList();
                    
                    if (existingDatabases.Any())
                        await _databases.DeleteBatchAsync(existingDatabases);
                    if (existingTables.Any())
                        await _tables.DeleteBatchAsync(existingTables);
                    if (existingRows.Any())
                        await _rows.DeleteBatchAsync(existingRows);
                    
                    // Restore data
                    if (backupData.Databases.Any())
                    {
                        await _databases.PutBatchAsync(backupData.Databases);
                    }
                    
                    if (backupData.Tables.Any())
                    {
                        await _tables.PutBatchAsync(backupData.Tables);
                    }
                    
                    if (backupData.Rows.Any())
                    {
                        await _rows.PutBatchAsync(backupData.Rows);
                    }
                    
                    _logger.LogInformation("FastKV restore completed from: {BackupPath}", backupPath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore from FastKV backup: {BackupPath}", backupPath);
            throw;
        }
    }
    
    public async Task CloseAsync()
    {
        try
        {
            // Compact all shards before closing
            if (_databases != null) await _databases.CompactAllAsync();
            if (_tables != null) await _tables.CompactAllAsync();
            if (_rows != null) await _rows.CompactAllAsync();
            
            _logger.LogInformation("FastKvStorageEngine closed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during FastKvStorageEngine close");
            throw;
        }
    }
    
    public void Dispose()
    {
        _databases?.Dispose();
        _tables?.Dispose();
        _rows?.Dispose();
    }
    
    // Backup data structure
    private class BackupData
    {
        public Dictionary<string, Database> Databases { get; set; } = new();
        public Dictionary<string, Table> Tables { get; set; } = new();
        public Dictionary<string, Row> Rows { get; set; } = new();
    }
}

// Custom serializers for FastKV
public class DatabaseSerializer : IValueSerializer<Database>
{
    public ReadOnlyMemory<byte> Serialize(Database value)
    {
        return JsonSerializer.SerializeToUtf8Bytes(value);
    }
    
    public Database Deserialize(ReadOnlySpan<byte> data)
    {
        return JsonSerializer.Deserialize<Database>(data) ?? new Database();
    }
}

public class TableSerializer : IValueSerializer<Table>
{
    public ReadOnlyMemory<byte> Serialize(Table value)
    {
        return JsonSerializer.SerializeToUtf8Bytes(value);
    }
    
    public Table Deserialize(ReadOnlySpan<byte> data)
    {
        return JsonSerializer.Deserialize<Table>(data) ?? new Table();
    }
}

public class RowSerializer : IValueSerializer<Row>
{
    public ReadOnlyMemory<byte> Serialize(Row value)
    {
        return JsonSerializer.SerializeToUtf8Bytes(value);
    }
    
    public Row Deserialize(ReadOnlySpan<byte> data)
    {
        return JsonSerializer.Deserialize<Row>(data) ?? new Row();
    }
}
